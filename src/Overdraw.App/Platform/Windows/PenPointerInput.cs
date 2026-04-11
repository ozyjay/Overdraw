using System.Drawing;
using System.Runtime.InteropServices;

namespace Overdraw.App;

internal static class PenPointerInput
{
    public const int WmPointerUpdate = 0x0245;
    public const int WmPointerDown = 0x0246;
    public const int WmPointerUp = 0x0247;

    private const uint PtPen = 3;

    public static bool RegisterTarget(IntPtr hwnd, out int error)
    {
        if (NativeMethods.RegisterPointerInputTarget(hwnd, PtPen))
        {
            error = 0;
            return true;
        }

        error = Marshal.GetLastWin32Error();
        return false;
    }

    public static bool TryCreatePenEvent(
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        Rectangle monitorBounds,
        out PenInputEvent input)
    {
        input = default;
        var pointerId = NativeMethods.GetPointerId(wParam);
        if (!NativeMethods.TryGetPointerType(pointerId, out var pointerType) || pointerType != PtPen)
        {
            return false;
        }

        var screenPoint = NativeMethods.GetPointFromLParam(lParam);
        if (!monitorBounds.Contains(screenPoint))
        {
            return false;
        }

        var kind = message switch
        {
            WmPointerDown => PenInputEventKind.Down,
            WmPointerUpdate => PenInputEventKind.Move,
            WmPointerUp => PenInputEventKind.Up,
            _ => (PenInputEventKind?)null
        };

        if (kind is null)
        {
            return false;
        }

        var localPoint = new Point(screenPoint.X - monitorBounds.Left, screenPoint.Y - monitorBounds.Top);
        input = new PenInputEvent(kind.Value, localPoint, pointerId);
        return true;
    }
}
