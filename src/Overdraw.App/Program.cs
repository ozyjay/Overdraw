using System.Drawing;
using System.Runtime.InteropServices;
namespace Overdraw.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);

        var options = CliOptions.Parse(args);
        if (!options.IsValid)
        {
            Console.Error.WriteLine(options.ErrorMessage);
            PrintUsage();
            return 1;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        var monitors = MonitorCatalog.Enumerate();
        if (options.ListMonitors)
        {
            foreach (var monitor in monitors)
            {
                Console.WriteLine(monitor.ToDisplayString());
            }

            return 0;
        }

        if (!options.RunOverlaySpike && !options.RunPenSpike && !options.RunInkSpike && !options.RunPointerInkSpike)
        {
            PrintUsage();
            return 0;
        }

        if (monitors.Count == 0)
        {
            Console.Error.WriteLine("No monitors detected.");
            return 1;
        }

        try
        {
            var monitor = MonitorCatalog.Select(monitors, options.MonitorSelector);
            Console.WriteLine($"Using monitor {monitor.ToDisplayString()}");
            if (options.RunPenSpike)
            {
                var penSpike = new PenDiagnosticsWindow(monitor, options.Verbose);
                return penSpike.Run();
            }

            if (options.RunInkSpike)
            {
                var inkSpike = new PenInkOverlayWindow(monitor, options.Verbose, InkCaptureMode.PenMouseHook);
                return inkSpike.Run();
            }

            if (options.RunPointerInkSpike)
            {
                var inkSpike = new PenInkOverlayWindow(monitor, options.Verbose, InkCaptureMode.PointerTarget);
                return inkSpike.Run();
            }

            var overlay = new NativeOverlayWindow(monitor, options.Verbose);
            return overlay.Run();
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: overdraw [--overlay-spike] [--pen-spike] [--ink-spike] [--pointer-ink-spike] [--list-monitors] [--monitor <selector>] [--verbose]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --overlay-spike    Run the Windows overlay feasibility prototype.");
        Console.WriteLine("  --pen-spike        Run the pen input diagnostics spike.");
        Console.WriteLine("  --ink-spike        Run the pen-only drawing overlay experiment.");
        Console.WriteLine("  --pointer-ink-spike Run the native pointer-target ink experiment.");
        Console.WriteLine("  --list-monitors    List detected monitors and exit.");
        Console.WriteLine("  --monitor          Use 'primary', a zero-based index, or a device name.");
        Console.WriteLine("  --verbose          Print extra placement or pointer diagnostics.");
        Console.WriteLine("  --help             Show this message.");
    }
}

internal sealed class NativeOverlayWindow
{
    private const int GwlExStyle = -20;
    private const string WindowClassName = "OverdrawNativeOverlayWindow";
    private const int HotKeyId = 0x0BD1;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF12 = 0x7B;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int WmDestroy = 0x0002;
    private const int WmPaint = 0x000F;
    private const int WmHotKey = 0x0312;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly byte WindowAlpha = 42;

    private readonly MonitorInfo _monitor;
    private readonly bool _verbose;
    private readonly NativeMethods.WndProc _wndProcDelegate;
    private IntPtr _hwnd;

    public NativeOverlayWindow(MonitorInfo monitor, bool verbose)
    {
        _monitor = monitor;
        _verbose = verbose;
        _wndProcDelegate = WindowProc;
    }

    public int Run()
    {
        var hInstance = NativeMethods.GetModuleHandle(IntPtr.Zero);
        RegisterWindowClass(hInstance);
        CreateWindow(hInstance);
        ShowAndPlaceWindow();
        if (_verbose)
        {
            LogPlacement("created");
        }
        RegisterHotKey();
        return MessageLoop();
    }

    private void RegisterWindowClass(IntPtr hInstance)
    {
        var windowClass = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            style = 0,
            lpfnWndProc = _wndProcDelegate,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = NativeMethods.LoadCursor(IntPtr.Zero, new IntPtr(32512)),
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero,
        };

        var atom = NativeMethods.RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410)
            {
                throw new InvalidOperationException($"RegisterClassEx failed with {error}.");
            }
        }
    }

    private void CreateWindow(IntPtr hInstance)
    {
        _hwnd = NativeMethods.CreateWindowEx(
            WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate,
            WindowClassName,
            "Overdraw Overlay Spike",
            WsPopup | WsVisible,
            _monitor.Bounds.Left,
            _monitor.Bounds.Top,
            _monitor.Bounds.Width,
            _monitor.Bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed with {Marshal.GetLastWin32Error()}.");
        }

        EnsureExtendedStyles();

        if (!NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, WindowAlpha, NativeMethods.LwaAlpha))
        {
            throw new InvalidOperationException($"SetLayeredWindowAttributes failed with {Marshal.GetLastWin32Error()}.");
        }
    }

    private void ShowAndPlaceWindow()
    {
        if (!NativeMethods.SetWindowPos(
                _hwnd,
                HwndTopmost,
                _monitor.Bounds.Left,
                _monitor.Bounds.Top,
                _monitor.Bounds.Width,
                _monitor.Bounds.Height,
                SwpNoActivate | SwpShowWindow))
        {
            throw new InvalidOperationException($"SetWindowPos failed with {Marshal.GetLastWin32Error()}.");
        }

        NativeMethods.ShowWindow(_hwnd, 4);
        NativeMethods.UpdateWindow(_hwnd);
    }

    private void RegisterHotKey()
    {
        if (!NativeMethods.RegisterHotKey(_hwnd, HotKeyId, ModControl | ModShift | ModNoRepeat, VkF12))
        {
            throw new InvalidOperationException("Failed to register Ctrl+Shift+F12 hotkey.");
        }
    }

    private int MessageLoop()
    {
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        return 0;
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmNcHitTest:
                return new IntPtr(HtTransparent);
            case WmPaint:
                Paint(hwnd);
                return IntPtr.Zero;
            case WmHotKey when wParam == new IntPtr(HotKeyId):
                NativeMethods.DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WmDestroy:
                NativeMethods.UnregisterHotKey(hwnd, HotKeyId);
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private void Paint(IntPtr hwnd)
    {
        NativeMethods.BeginPaint(hwnd, out var paintStruct);
        try
        {
            if (!NativeMethods.GetClientRect(hwnd, out var clientRect))
            {
                return;
            }

            using var graphics = Graphics.FromHdc(paintStruct.hdc);
            using var overlayBrush = new SolidBrush(Color.FromArgb(16, 16, 16));
            using var borderPen = new Pen(Color.FromArgb(190, 220, 220, 220), 1f);
            using var statusBrush = new SolidBrush(Color.FromArgb(180, 22, 22, 22));
            using var textBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
            using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

            var clientBounds = Rectangle.FromLTRB(clientRect.Left, clientRect.Top, clientRect.Right, clientRect.Bottom);
            graphics.FillRectangle(overlayBrush, clientBounds);

            var borderRect = Rectangle.Inflate(clientBounds, -18, -18);
            graphics.DrawRectangle(borderPen, borderRect);

            var statusRect = new Rectangle(borderRect.Left, borderRect.Bottom - 28, borderRect.Width, 28);
            graphics.FillRectangle(statusBrush, statusRect);
            graphics.DrawRectangle(borderPen, statusRect);

            var text = $"Overdraw spike | monitor [{_monitor.Index}] {_monitor.FriendlyName} | Ctrl+Shift+F12 closes";
            var textRect = Rectangle.Inflate(statusRect, -8, 0);
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                textRect,
                Color.FromArgb(240, 240, 240),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        finally
        {
            NativeMethods.EndPaint(hwnd, ref paintStruct);
        }
    }

    private void LogPlacement(string stage)
    {
        var actualMonitor = NativeMethods.GetMonitorInfoForWindow(_hwnd);
        NativeMethods.GetWindowRect(_hwnd, out var windowRect);
        var exStyle = NativeMethods.GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        Console.WriteLine(
            $"Overlay {stage}: target={_monitor.Bounds.Left},{_monitor.Bounds.Top} {_monitor.Bounds.Width}x{_monitor.Bounds.Height} " +
            $"actual={windowRect.Left},{windowRect.Top} {windowRect.Right - windowRect.Left}x{windowRect.Bottom - windowRect.Top} " +
            $"on {actualMonitor.DeviceName} exstyle=0x{exStyle:X}");
    }

    private void EnsureExtendedStyles()
    {
        var style = NativeMethods.GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        var desired = style | WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
        if (desired != style)
        {
            NativeMethods.SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(desired));
        }
    }
}

internal sealed class PenDiagnosticsWindow
{
    private const string WindowClassName = "OverdrawPenDiagnosticsWindow";
    private const int HotKeyId = 0x0BD2;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF12 = 0x7B;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const uint SwpShowWindow = 0x0040;
    private const int WmDestroy = 0x0002;
    private const int WmPaint = 0x000F;
    private const int WmHotKey = 0x0312;
    private const int WmPointerUpdate = 0x0245;
    private const int WmPointerDown = 0x0246;
    private const int WmPointerUp = 0x0247;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const byte WindowAlpha = 84;

    private readonly MonitorInfo _monitor;
    private readonly bool _verbose;
    private readonly NativeMethods.WndProc _wndProcDelegate;
    private IntPtr _hwnd;
    private string _statusText;
    private uint _lastLoggedPointerId;
    private string _lastLoggedStage = string.Empty;
    private long _lastPointerUpdateTick;

    public PenDiagnosticsWindow(MonitorInfo monitor, bool verbose)
    {
        _monitor = monitor;
        _verbose = verbose;
        _wndProcDelegate = WindowProc;
        _statusText = "Bring the pen into range and tap or draw. Ctrl+Shift+F12 closes.";
    }

    public int Run()
    {
        var hInstance = NativeMethods.GetModuleHandle(IntPtr.Zero);
        RegisterWindowClass(hInstance);
        CreateWindow(hInstance);
        ShowAndPlaceWindow();
        if (_verbose)
        {
            LogPlacement("created");
        }
        RegisterHotKey();
        return MessageLoop();
    }

    private void RegisterWindowClass(IntPtr hInstance)
    {
        var windowClass = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            style = 0,
            lpfnWndProc = _wndProcDelegate,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = NativeMethods.LoadCursor(IntPtr.Zero, new IntPtr(32512)),
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero,
        };

        var atom = NativeMethods.RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410)
            {
                throw new InvalidOperationException($"RegisterClassEx failed with {error}.");
            }
        }
    }

    private void CreateWindow(IntPtr hInstance)
    {
        _hwnd = NativeMethods.CreateWindowEx(
            WsExLayered | WsExToolWindow,
            WindowClassName,
            "Overdraw Pen Diagnostics",
            WsPopup | WsVisible,
            _monitor.Bounds.Left,
            _monitor.Bounds.Top,
            _monitor.Bounds.Width,
            _monitor.Bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed with {Marshal.GetLastWin32Error()}.");
        }

        if (!NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, WindowAlpha, NativeMethods.LwaAlpha))
        {
            throw new InvalidOperationException($"SetLayeredWindowAttributes failed with {Marshal.GetLastWin32Error()}.");
        }
    }

    private void ShowAndPlaceWindow()
    {
        if (!NativeMethods.SetWindowPos(
                _hwnd,
                HwndTopmost,
                _monitor.Bounds.Left,
                _monitor.Bounds.Top,
                _monitor.Bounds.Width,
                _monitor.Bounds.Height,
                SwpShowWindow))
        {
            throw new InvalidOperationException($"SetWindowPos failed with {Marshal.GetLastWin32Error()}.");
        }

        NativeMethods.ShowWindow(_hwnd, 5);
        NativeMethods.UpdateWindow(_hwnd);
    }

    private void RegisterHotKey()
    {
        if (!NativeMethods.RegisterHotKey(_hwnd, HotKeyId, ModControl | ModShift | ModNoRepeat, VkF12))
        {
            throw new InvalidOperationException("Failed to register Ctrl+Shift+F12 hotkey.");
        }
    }

    private int MessageLoop()
    {
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        return 0;
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmPaint:
                Paint(hwnd);
                return IntPtr.Zero;
            case WmHotKey when wParam == new IntPtr(HotKeyId):
                NativeMethods.DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WmPointerDown:
            case WmPointerUpdate:
            case WmPointerUp:
                UpdatePointerStatus(message, wParam);
                return IntPtr.Zero;
            case WmMouseMove:
                UpdateMouseStatus("mouse-move", lParam);
                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
            case WmLButtonDown:
                UpdateMouseStatus("mouse-down", lParam);
                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
            case WmDestroy:
                NativeMethods.UnregisterHotKey(hwnd, HotKeyId);
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private void UpdatePointerStatus(uint message, IntPtr wParam)
    {
        var pointerId = NativeMethods.GetPointerId(wParam);
        var pointerType = NativeMethods.GetPointerTypeName(pointerId);
        var stage = message switch
        {
            WmPointerDown => "pointer-down",
            WmPointerUpdate => "pointer-update",
            WmPointerUp => "pointer-up",
            _ => "pointer"
        };

        _statusText = $"{stage} | type={pointerType} | id={pointerId} | Ctrl+Shift+F12 closes";
        if (_verbose && ShouldLogPointerEvent(stage, pointerId))
        {
            Console.WriteLine(_statusText);
        }

        NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    private void UpdateMouseStatus(string stage, IntPtr lParam)
    {
        var point = NativeMethods.GetPointFromLParam(lParam);
        _statusText = $"{stage} | x={point.X} y={point.Y} | Ctrl+Shift+F12 closes";
        NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    private void Paint(IntPtr hwnd)
    {
        NativeMethods.BeginPaint(hwnd, out var paintStruct);
        try
        {
            if (!NativeMethods.GetClientRect(hwnd, out var clientRect))
            {
                return;
            }

            using var graphics = Graphics.FromHdc(paintStruct.hdc);
            using var overlayBrush = new SolidBrush(Color.FromArgb(20, 24, 32));
            using var borderPen = new Pen(Color.FromArgb(220, 240, 240, 240), 2f);
            using var statusBrush = new SolidBrush(Color.FromArgb(190, 18, 18, 18));
            using var font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            using var titleFont = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Point);

            var clientBounds = Rectangle.FromLTRB(clientRect.Left, clientRect.Top, clientRect.Right, clientRect.Bottom);
            graphics.FillRectangle(overlayBrush, clientBounds);

            var borderRect = Rectangle.Inflate(clientBounds, -24, -24);
            graphics.DrawRectangle(borderPen, borderRect);

            var titleRect = new Rectangle(borderRect.Left + 24, borderRect.Top + 36, borderRect.Width - 48, 44);
            TextRenderer.DrawText(
                graphics,
                $"Pen diagnostics on [{_monitor.Index}] {_monitor.FriendlyName}",
                titleFont,
                titleRect,
                Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var hintRect = new Rectangle(borderRect.Left + 24, borderRect.Top + 92, borderRect.Width - 48, 72);
            TextRenderer.DrawText(
                graphics,
                "Use the XP-Pen pen on this display. The window will report whether Windows sees the input as pen, mouse, or another pointer type.",
                font,
                hintRect,
                Color.FromArgb(235, 235, 235),
                TextFormatFlags.Left | TextFormatFlags.WordBreak);

            var statusRect = new Rectangle(borderRect.Left + 24, borderRect.Bottom - 68, borderRect.Width - 48, 44);
            graphics.FillRectangle(statusBrush, statusRect);
            graphics.DrawRectangle(borderPen, statusRect);
            TextRenderer.DrawText(
                graphics,
                _statusText,
                font,
                Rectangle.Inflate(statusRect, -10, -4),
                Color.FromArgb(240, 240, 240),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        finally
        {
            NativeMethods.EndPaint(hwnd, ref paintStruct);
        }
    }

    private void LogPlacement(string stage)
    {
        var actualMonitor = NativeMethods.GetMonitorInfoForWindow(_hwnd);
        NativeMethods.GetWindowRect(_hwnd, out var windowRect);
        Console.WriteLine(
            $"Pen diagnostics {stage}: target={_monitor.Bounds.Left},{_monitor.Bounds.Top} {_monitor.Bounds.Width}x{_monitor.Bounds.Height} " +
            $"actual={windowRect.Left},{windowRect.Top} {windowRect.Right - windowRect.Left}x{windowRect.Bottom - windowRect.Top} " +
            $"on {actualMonitor.DeviceName}");
    }

    private bool ShouldLogPointerEvent(string stage, uint pointerId)
    {
        var now = Environment.TickCount64;
        var shouldLog =
            stage is "pointer-down" or "pointer-up" ||
            pointerId != _lastLoggedPointerId ||
            stage != _lastLoggedStage ||
            now - _lastPointerUpdateTick >= 120;

        if (shouldLog)
        {
            _lastLoggedPointerId = pointerId;
            _lastLoggedStage = stage;
            _lastPointerUpdateTick = now;
        }

        return shouldLog;
    }
}

internal enum InkCaptureMode
{
    PenMouseHook,
    PointerTarget
}

internal sealed class PenInkOverlayWindow
{
    private const string WindowClassName = "OverdrawPenInkOverlayWindow";
    private const int HotKeyId = 0x0BD3;
    private const int GwlExStyle = -20;
    private const int WhMouseLl = 14;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF12 = 0x7B;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int WmDestroy = 0x0002;
    private const int WmPaint = 0x000F;
    private const int WmHotKey = 0x0312;
    private const int WmSetCursor = 0x0020;
    private const int WmAppProcessInk = 0x8001;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint TransparentColorKey = 0x000100;

    private readonly MonitorInfo _monitor;
    private readonly bool _verbose;
    private readonly InkCaptureMode _captureMode;
    private readonly NativeMethods.WndProc _wndProcDelegate;
    private readonly NativeMethods.LowLevelMouseProc _mouseHookProc;
    private readonly Queue<PenInputEvent> _pendingInkEvents = new();
    private InkRenderer? _inkRenderer;
    private IntPtr _hwnd;
    private IntPtr _mouseHook;
    private bool _penIsDown;
    private bool _inkProcessQueued;
    private bool _cursorHiddenForPen;
    private string _statusText = "Pen-only drawing overlay. Mouse should pass through. Ctrl+Shift+F12 closes.";

    public PenInkOverlayWindow(MonitorInfo monitor, bool verbose, InkCaptureMode captureMode)
    {
        _monitor = monitor;
        _verbose = verbose;
        _captureMode = captureMode;
        _wndProcDelegate = WindowProc;
        _mouseHookProc = MouseHookProc;
    }

    public int Run()
    {
        var hInstance = NativeMethods.GetModuleHandle(IntPtr.Zero);
        RegisterWindowClass(hInstance);
        CreateWindow(hInstance);
        ShowAndPlaceWindow();
        if (_verbose)
        {
            LogPlacement("created");
        }
        RegisterHotKey();
        if (_captureMode == InkCaptureMode.PointerTarget)
        {
            RegisterPointerInputTarget();
        }
        else
        {
            InstallMouseHook(hInstance);
        }
        return MessageLoop();
    }

    private void RegisterWindowClass(IntPtr hInstance)
    {
        var windowClass = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            style = 0,
            lpfnWndProc = _wndProcDelegate,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = _captureMode == InkCaptureMode.PointerTarget
                ? IntPtr.Zero
                : NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IdcArrow),
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero,
        };

        var atom = NativeMethods.RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410)
            {
                throw new InvalidOperationException($"RegisterClassEx failed with {error}.");
            }
        }
    }

    private void CreateWindow(IntPtr hInstance)
    {
        _hwnd = NativeMethods.CreateWindowEx(
            WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate,
            WindowClassName,
            "Overdraw Pen Ink Overlay",
            WsPopup | WsVisible,
            _monitor.Bounds.Left,
            _monitor.Bounds.Top,
            _monitor.Bounds.Width,
            _monitor.Bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed with {Marshal.GetLastWin32Error()}.");
        }

        EnsureExtendedStyles();
        if (!NativeMethods.SetLayeredWindowAttributes(_hwnd, TransparentColorKey, 0, NativeMethods.LwaColorKey))
        {
            throw new InvalidOperationException($"SetLayeredWindowAttributes failed with {Marshal.GetLastWin32Error()}.");
        }

        _inkRenderer = new InkRenderer(_monitor.Bounds.Size);
    }

    private void ShowAndPlaceWindow()
    {
        if (!NativeMethods.SetWindowPos(
                _hwnd,
                HwndTopmost,
                _monitor.Bounds.Left,
                _monitor.Bounds.Top,
                _monitor.Bounds.Width,
                _monitor.Bounds.Height,
                SwpNoActivate | SwpShowWindow))
        {
            throw new InvalidOperationException($"SetWindowPos failed with {Marshal.GetLastWin32Error()}.");
        }

        NativeMethods.ShowWindow(_hwnd, 4);
        NativeMethods.UpdateWindow(_hwnd);
    }

    private void RegisterHotKey()
    {
        if (!NativeMethods.RegisterHotKey(_hwnd, HotKeyId, ModControl | ModShift | ModNoRepeat, VkF12))
        {
            throw new InvalidOperationException("Failed to register Ctrl+Shift+F12 hotkey.");
        }
    }

    private void InstallMouseHook(IntPtr hInstance)
    {
        _mouseHook = NativeMethods.SetWindowsHookEx(WhMouseLl, _mouseHookProc, hInstance, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SetWindowsHookEx failed with {Marshal.GetLastWin32Error()}.");
        }
    }

    private void RegisterPointerInputTarget()
    {
        if (!PenPointerInput.RegisterTarget(_hwnd, out var error))
        {
            _statusText = $"RegisterPointerInputTarget failed with {error}. Ctrl+Shift+F12 closes.";
            Console.WriteLine(_statusText);
            Console.WriteLine("Pointer-target mode will stay open for observation, but direct pen ink is unlikely until this succeeds.");
            InvalidateRectangle(GetStatusRectangle());
            return;
        }

        _statusText = "Pointer-target pen ink active. Mouse should pass through. Ctrl+Shift+F12 closes.";
        Console.WriteLine(_statusText);
        InvalidateRectangle(GetStatusRectangle());
    }

    private int MessageLoop()
    {
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        return 0;
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmPaint:
                Paint(hwnd);
                return IntPtr.Zero;
            case WmHotKey when wParam == new IntPtr(HotKeyId):
                NativeMethods.DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WmAppProcessInk:
                _inkProcessQueued = false;
                ProcessPendingInk();
                return IntPtr.Zero;
            case PenPointerInput.WmPointerDown:
            case PenPointerInput.WmPointerUpdate:
            case PenPointerInput.WmPointerUp:
                if (_captureMode == InkCaptureMode.PointerTarget)
                {
                    HandlePointerInk(message, wParam, lParam);
                    return IntPtr.Zero;
                }

                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
            case WmSetCursor when _captureMode == InkCaptureMode.PointerTarget:
                NativeMethods.SetCursor(IntPtr.Zero);
                return new IntPtr(1);
            case WmDestroy:
                ShowCursorForPenIfNeeded();
                if (_mouseHook != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
                _inkRenderer?.Dispose();
                _inkRenderer = null;
                NativeMethods.UnregisterHotKey(hwnd, HotKeyId);
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private void Paint(IntPtr hwnd)
    {
        NativeMethods.BeginPaint(hwnd, out var paintStruct);
        try
        {
            if (!NativeMethods.GetClientRect(hwnd, out var clientRect))
            {
                return;
            }

            using var graphics = Graphics.FromHdc(paintStruct.hdc);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            using var transparentBrush = new SolidBrush(InkRenderer.TransparentKeyColor);
            using var statusBrush = new SolidBrush(Color.FromArgb(196, 22, 22, 22));
            using var statusBorderPen = new Pen(Color.FromArgb(180, 220, 220, 220), 1f);
            using var statusFont = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);

            var clientBounds = Rectangle.FromLTRB(clientRect.Left, clientRect.Top, clientRect.Right, clientRect.Bottom);
            graphics.FillRectangle(transparentBrush, clientBounds);

            _inkRenderer?.Paint(graphics);

            if (_captureMode != InkCaptureMode.PointerTarget)
            {
                var statusRect = GetStatusRectangle();
                graphics.FillRectangle(statusBrush, statusRect);
                graphics.DrawRectangle(statusBorderPen, statusRect);
                TextRenderer.DrawText(
                    graphics,
                    _statusText,
                    statusFont,
                    Rectangle.Inflate(statusRect, -8, -2),
                    Color.FromArgb(240, 240, 240),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        finally
        {
            NativeMethods.EndPaint(hwnd, ref paintStruct);
        }
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var hookStruct = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
        if (!NativeMethods.IsPenMouseMessage(hookStruct.dwExtraInfo))
        {
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        if (!_monitor.Bounds.Contains(hookStruct.pt))
        {
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var localPoint = new Point(hookStruct.pt.X - _monitor.Bounds.Left, hookStruct.pt.Y - _monitor.Bounds.Top);
        var message = unchecked((uint)wParam.ToInt64());
        switch (message)
        {
            case NativeMethods.WmMouseMove:
                EnqueuePenEvent(PenInputEventKind.Move, localPoint);
                return new IntPtr(1);
            case NativeMethods.WmLButtonDown:
                EnqueuePenEvent(PenInputEventKind.Down, localPoint);
                return new IntPtr(1);
            case NativeMethods.WmLButtonUp:
                EnqueuePenEvent(PenInputEventKind.Up, localPoint);
                return new IntPtr(1);
            default:
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
    }

    private void HandlePointerInk(uint message, IntPtr wParam, IntPtr lParam)
    {
        NativeMethods.SetCursor(IntPtr.Zero);
        if (!PenPointerInput.TryCreatePenEvent(message, wParam, lParam, _monitor.Bounds, out var input))
        {
            return;
        }

        switch (input.Kind)
        {
            case PenInputEventKind.Down:
                HideCursorForPenIfNeeded();
                EnqueuePenEvent(input);
                break;
            case PenInputEventKind.Move:
                if (_penIsDown)
                {
                    HideCursorForPenIfNeeded();
                }
                EnqueuePenEvent(input);
                break;
            case PenInputEventKind.Up:
                EnqueuePenEvent(input);
                ShowCursorForPenIfNeeded();
                break;
        }
    }

    private void HideCursorForPenIfNeeded()
    {
        if (_captureMode != InkCaptureMode.PointerTarget || _cursorHiddenForPen)
        {
            return;
        }

        NativeMethods.ShowCursor(false);
        NativeMethods.SetCursor(IntPtr.Zero);
        _cursorHiddenForPen = true;
    }

    private void ShowCursorForPenIfNeeded()
    {
        if (!_cursorHiddenForPen)
        {
            return;
        }

        NativeMethods.ShowCursor(true);
        _cursorHiddenForPen = false;
    }

    private void EnqueuePenEvent(PenInputEventKind kind, Point point, uint pointerId = 0)
    {
        EnqueuePenEvent(new PenInputEvent(kind, point, pointerId));
    }

    private void EnqueuePenEvent(PenInputEvent input)
    {
        _pendingInkEvents.Enqueue(input);
        if (_inkProcessQueued || _hwnd == IntPtr.Zero)
        {
            return;
        }

        _inkProcessQueued = true;
        NativeMethods.PostMessage(_hwnd, WmAppProcessInk, IntPtr.Zero, IntPtr.Zero);
    }

    private void ProcessPendingInk()
    {
        Rectangle? dirtyRectangle = null;
        while (_pendingInkEvents.Count > 0)
        {
            var inkEvent = _pendingInkEvents.Dequeue();
            var eventDirty = HandlePenEvent(inkEvent);

            if (eventDirty is Rectangle nextDirty)
            {
                dirtyRectangle = dirtyRectangle is Rectangle existing
                    ? Rectangle.Union(existing, nextDirty)
                    : nextDirty;
            }
        }

        if (dirtyRectangle is Rectangle dirty)
        {
            InvalidateRectangle(dirty);
        }
    }

    private Rectangle? HandlePenEvent(PenInputEvent input)
    {
        return input.Kind switch
        {
            PenInputEventKind.Down => HandlePenDown(input),
            PenInputEventKind.Move => HandlePenMove(input),
            PenInputEventKind.Up => HandlePenUp(input),
            _ => null
        };
    }

    private Rectangle? HandlePenDown(PenInputEvent input)
    {
        var point = input.Point;
        _penIsDown = true;
        var dirtyRectangle = _inkRenderer?.HandlePenEvent(input);
        var eventText = $"pen-down | x={point.X} y={point.Y} | mouse stays pass-through | Ctrl+Shift+F12 closes";
        if (_verbose)
        {
            Console.WriteLine(eventText);
        }

        if (_captureMode == InkCaptureMode.PointerTarget)
        {
            return dirtyRectangle;
        }

        _statusText = eventText;
        return CombineDirty(dirtyRectangle, GetStatusRectangle());
    }

    private Rectangle? HandlePenMove(PenInputEvent input)
    {
        if (_penIsDown)
        {
            return _inkRenderer?.HandlePenEvent(input);
        }

        return null;
    }

    private Rectangle? HandlePenUp(PenInputEvent input)
    {
        var point = input.Point;
        Rectangle? dirtyRectangle = null;
        if (_penIsDown)
        {
            dirtyRectangle = _inkRenderer?.HandlePenEvent(input);
        }

        _penIsDown = false;
        var eventText = $"pen-up | x={point.X} y={point.Y} | mouse stays pass-through | Ctrl+Shift+F12 closes";
        if (_verbose)
        {
            Console.WriteLine(eventText);
        }

        if (_captureMode == InkCaptureMode.PointerTarget)
        {
            return dirtyRectangle;
        }

        _statusText = eventText;
        return CombineDirty(dirtyRectangle, GetStatusRectangle());
    }

    private void LogPlacement(string stage)
    {
        var actualMonitor = NativeMethods.GetMonitorInfoForWindow(_hwnd);
        NativeMethods.GetWindowRect(_hwnd, out var windowRect);
        var exStyle = NativeMethods.GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        Console.WriteLine(
            $"Ink overlay {stage}: target={_monitor.Bounds.Left},{_monitor.Bounds.Top} {_monitor.Bounds.Width}x{_monitor.Bounds.Height} " +
            $"actual={windowRect.Left},{windowRect.Top} {windowRect.Right - windowRect.Left}x{windowRect.Bottom - windowRect.Top} " +
            $"on {actualMonitor.DeviceName} exstyle=0x{exStyle:X}");
    }

    private void EnsureExtendedStyles()
    {
        var style = NativeMethods.GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        var desired = style | WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
        if (desired != style)
        {
            NativeMethods.SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(desired));
        }
    }

    private Rectangle GetStatusRectangle()
    {
        return new Rectangle(24, Math.Max(0, _monitor.Bounds.Height - 48), Math.Max(0, _monitor.Bounds.Width - 48), 28);
    }

    private static Rectangle? CombineDirty(Rectangle? first, Rectangle? second)
    {
        if (first is null || first == Rectangle.Empty)
        {
            return second;
        }

        if (second is null || second == Rectangle.Empty)
        {
            return first;
        }

        return Rectangle.Union(first.Value, second.Value);
    }

    private void InvalidateRectangle(Rectangle rectangle)
    {
        var clamped = Rectangle.Intersect(new Rectangle(Point.Empty, _monitor.Bounds.Size), rectangle);
        if (clamped.Width <= 0 || clamped.Height <= 0)
        {
            return;
        }

        var nativeRect = new NativeMethods.Rect
        {
            Left = clamped.Left,
            Top = clamped.Top,
            Right = clamped.Right,
            Bottom = clamped.Bottom
        };
        NativeMethods.InvalidateRect(_hwnd, ref nativeRect, false);
    }

}

internal static partial class NativeMethods
{
    public const uint LwaColorKey = 0x00000001;
    public const uint LwaAlpha = 0x00000002;
    private const int DisplayDeviceActive = 0x00000001;
    private const uint MonitorDefaultToNearest = 2;
    private const long MiWpSignature = 0xFF515700;
    private const long MiWpSignatureMask = unchecked((long)0xFFFFFF00);
    private const long MiWpTouchMask = 0x80;
    public const uint WmMouseMove = 0x0200;
    public const uint WmLButtonDown = 0x0201;
    public const uint WmLButtonUp = 0x0202;
    public static readonly IntPtr IdcArrow = new(32512);
    public static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PaintStruct
    {
        public IntPtr hdc;
        public bool fErase;
        public Rect rcPaint;
        public bool fRestore;
        public bool fIncUpdate;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public Point pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MsLlHookStruct
    {
        public Point pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern sbyte GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterPointerInputTarget(IntPtr hwnd, uint pointerType);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr BeginPaint(IntPtr hWnd, out PaintStruct lpPaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EndPaint(IntPtr hWnd, ref PaintStruct lpPaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool erase);

    [DllImport("user32.dll", EntryPoint = "InvalidateRect", SetLastError = true)]
    public static extern bool InvalidateRect(IntPtr hWnd, ref Rect lpRect, bool erase);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(IntPtr lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int ShowCursor(bool bShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetPointerType(uint pointerId, out uint pointerType);

    public static IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (hMonitor, _, _, _) =>
            {
                var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
                if (!GetMonitorInfo(hMonitor, ref info))
                {
                    return true;
                }

                var bounds = Rectangle.FromLTRB(
                    info.rcMonitor.Left,
                    info.rcMonitor.Top,
                    info.rcMonitor.Right,
                    info.rcMonitor.Bottom);

                monitors.Add(new MonitorInfo(
                    monitors.Count,
                    info.szDevice,
                    GetFriendlyMonitorName(info.szDevice),
                    bounds,
                    (info.dwFlags & 1) != 0));
                return true;
            },
            IntPtr.Zero);

        return monitors;
    }

    public static MonitorInfo GetMonitorInfoForWindow(IntPtr hwnd)
    {
        var hMonitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return new MonitorInfo(-1, "unknown", "unknown", Rectangle.Empty, false);
        }

        var bounds = Rectangle.FromLTRB(
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Right,
            info.rcMonitor.Bottom);
        return new MonitorInfo(
            -1,
            info.szDevice,
            GetFriendlyMonitorName(info.szDevice),
            bounds,
            (info.dwFlags & 1) != 0);
    }

    public static string GetFriendlyMonitorName(string deviceName)
    {
        var adapter = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
        if (!EnumDisplayDevices(deviceName, 0, ref adapter, 0))
        {
            return deviceName;
        }

        if (!string.IsNullOrWhiteSpace(adapter.DeviceString))
        {
            return adapter.DeviceString;
        }

        for (uint index = 0; index < 8; index++)
        {
            var monitor = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(adapter.DeviceName, index, ref monitor, 0))
            {
                break;
            }

            if ((monitor.StateFlags & DisplayDeviceActive) != 0 && !string.IsNullOrWhiteSpace(monitor.DeviceString))
            {
                return monitor.DeviceString;
            }
        }

        return deviceName;
    }

    public static uint GetPointerId(IntPtr wParam)
    {
        return unchecked((uint)(wParam.ToInt64() & 0xFFFF));
    }

    public static string GetPointerTypeName(uint pointerId)
    {
        if (!TryGetPointerType(pointerId, out var pointerType))
        {
            return $"unknown({Marshal.GetLastWin32Error()})";
        }

        return pointerType switch
        {
            2 => "touch",
            3 => "pen",
            4 => "mouse",
            _ => $"pointer({pointerType})"
        };
    }

    public static bool TryGetPointerType(uint pointerId, out uint pointerType)
    {
        return GetPointerType(pointerId, out pointerType);
    }

    public static Point GetPointFromLParam(IntPtr lParam)
    {
        var raw = lParam.ToInt64();
        var x = unchecked((short)(raw & 0xFFFF));
        var y = unchecked((short)((raw >> 16) & 0xFFFF));
        return new Point(x, y);
    }

    public static bool IsPenMouseMessage(IntPtr extraInfo)
    {
        var value = extraInfo.ToInt64();
        return (value & MiWpSignatureMask) == MiWpSignature && (value & MiWpTouchMask) == 0;
    }
}
