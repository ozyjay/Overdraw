using System.Drawing;
using System.Drawing.Drawing2D;

namespace Overdraw.App;

internal sealed class InkRenderer : IDisposable
{
    public static readonly Color TransparentKeyColor = Color.FromArgb(0, 1, 0);
    public static readonly Color InkColor = Color.FromArgb(255, 255, 64, 96);

    private static readonly Color[] Palette =
    [
        Color.FromArgb(255, 64, 96),
        Color.FromArgb(255, 220, 64),
        Color.FromArgb(64, 220, 255),
        Color.FromArgb(72, 220, 120),
        Color.FromArgb(255, 255, 255),
        Color.FromArgb(24, 24, 24)
    ];

    private static readonly int[] OpacityLevels = [89, 128, 179, 230, 255];
    private const float PenWidth = 10f;
    private const float EraserWidth = 30f;

    private readonly Size _size;
    private readonly Bitmap _bitmap;
    private readonly Graphics _graphics;
    private readonly List<InkStroke> _strokes = [];
    private readonly Stack<InkStroke> _redoStack = new();
    private int _paletteIndex;
    private int _opacityIndex = OpacityLevels.Length - 1;
    private InkStroke? _activeStroke;

    public InkRenderer(Size size)
    {
        _size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
        _bitmap = new Bitmap(_size.Width, _size.Height);
        _graphics = Graphics.FromImage(_bitmap);
        ConfigureGraphics(_graphics);
        ClearBitmap();
    }

    public void Paint(Graphics target)
    {
        target.DrawImageUnscaled(_bitmap, 0, 0);
    }

    public Rectangle? HandlePenEvent(PenInputEvent input)
    {
        return input.Kind switch
        {
            PenInputEventKind.Down => HandleDown(input),
            PenInputEventKind.Move => HandleMove(input),
            PenInputEventKind.Up => HandleUp(input),
            _ => null
        };
    }

    public Rectangle? Undo()
    {
        if (_activeStroke is not null || _strokes.Count == 0)
        {
            return null;
        }

        var lastIndex = _strokes.Count - 1;
        var stroke = _strokes[lastIndex];
        _strokes.RemoveAt(lastIndex);
        _redoStack.Push(stroke);
        RebuildBitmap();
        return FullCanvas;
    }

    public Rectangle? Redo()
    {
        if (_activeStroke is not null || _redoStack.Count == 0)
        {
            return null;
        }

        var stroke = _redoStack.Pop();
        _strokes.Add(stroke);
        DrawStroke(stroke, _graphics);
        return GetStrokeRectangle(stroke);
    }

    public Rectangle? Clear()
    {
        if (_activeStroke is null && _strokes.Count == 0 && _redoStack.Count == 0)
        {
            return null;
        }

        _activeStroke = null;
        _strokes.Clear();
        _redoStack.Clear();
        ClearBitmap();
        return FullCanvas;
    }

    public Rectangle? CycleColor()
    {
        _paletteIndex = (_paletteIndex + 1) % Palette.Length;
        return null;
    }

    public Rectangle? IncreaseOpacity()
    {
        if (_opacityIndex >= OpacityLevels.Length - 1)
        {
            return null;
        }

        _opacityIndex++;
        return null;
    }

    public Rectangle? DecreaseOpacity()
    {
        if (_opacityIndex <= 0)
        {
            return null;
        }

        _opacityIndex--;
        return null;
    }

    public string CurrentBrushDescription
    {
        get
        {
            var color = Palette[_paletteIndex];
            var opacity = (int)Math.Round(OpacityLevels[_opacityIndex] / 255d * 100d);
            return $"color=#{color.R:X2}{color.G:X2}{color.B:X2} opacity={opacity}%";
        }
    }

    private Rectangle HandleDown(PenInputEvent input)
    {
        var stroke = new InkStroke(CreateStyle(input.Tool));
        stroke.Points.Add(input.Point);
        _activeStroke = stroke;
        _redoStack.Clear();
        return DrawDot(input.Point, stroke.Style);
    }

    private Rectangle? HandleMove(PenInputEvent input)
    {
        if (_activeStroke is null)
        {
            return null;
        }

        var lastPoint = _activeStroke.Points[^1];
        _activeStroke.Points.Add(input.Point);
        return DrawSegment(lastPoint, input.Point, _activeStroke.Style);
    }

    private Rectangle? HandleUp(PenInputEvent input)
    {
        if (_activeStroke is null)
        {
            return null;
        }

        Rectangle? dirtyRectangle = null;
        var lastPoint = _activeStroke.Points[^1];
        if (lastPoint != input.Point)
        {
            _activeStroke.Points.Add(input.Point);
            dirtyRectangle = DrawSegment(lastPoint, input.Point, _activeStroke.Style);
        }

        var committedStroke = _activeStroke;
        _strokes.Add(committedStroke);
        _activeStroke = null;

        // Redraw the committed stroke as one connected polyline so semi-transparent
        // vertices do not accumulate darker "waypoint" dots from per-segment caps.
        RebuildBitmap();
        return dirtyRectangle is Rectangle dirty
            ? Rectangle.Union(dirty, GetStrokeRectangle(committedStroke))
            : GetStrokeRectangle(committedStroke);
    }

    private InkStrokeStyle CreateStyle(PenTool tool)
    {
        if (tool == PenTool.Eraser)
        {
            return new InkStrokeStyle(PenTool.Eraser, TransparentKeyColor, EraserWidth);
        }

        var color = Palette[_paletteIndex];
        return new InkStrokeStyle(
            PenTool.Pen,
            Color.FromArgb(OpacityLevels[_opacityIndex], color.R, color.G, color.B),
            PenWidth);
    }

    private void RebuildBitmap()
    {
        ClearBitmap();
        foreach (var stroke in _strokes)
        {
            DrawStroke(stroke, _graphics);
        }
    }

    private void DrawStroke(InkStroke stroke, Graphics graphics)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        if (stroke.Points.Count == 1)
        {
            DrawDot(stroke.Points[0], stroke.Style, graphics);
            return;
        }

        using var pen = CreatePen(stroke.Style, LineCap.Round);
        using var _ = UseCompositingMode(graphics, stroke.Style);
        graphics.DrawLines(pen, stroke.Points.ToArray());
    }

    private Rectangle DrawSegment(Point start, Point end, InkStrokeStyle style)
    {
        using var pen = CreatePen(style, LineCap.Flat);
        using var _ = UseCompositingMode(_graphics, style);
        _graphics.DrawLine(pen, start, end);
        return GetSegmentRectangle(start, end, style);
    }

    private Rectangle DrawDot(Point point, InkStrokeStyle style)
    {
        return DrawDot(point, style, _graphics);
    }

    private Rectangle DrawDot(Point point, InkStrokeStyle style, Graphics graphics)
    {
        using var brush = new SolidBrush(style.Color);
        using var _ = UseCompositingMode(graphics, style);
        var diameter = (int)Math.Ceiling(style.Width);
        graphics.FillEllipse(brush, point.X - (diameter / 2), point.Y - (diameter / 2), diameter, diameter);
        return GetDotRectangle(point, diameter);
    }

    private static Pen CreatePen(InkStrokeStyle style, LineCap cap)
    {
        return new Pen(style.Color, style.Width)
        {
            StartCap = cap,
            EndCap = cap,
            LineJoin = LineJoin.Round
        };
    }

    private static IDisposable UseCompositingMode(Graphics graphics, InkStrokeStyle style)
    {
        if (style.Tool != PenTool.Eraser)
        {
            return NoopDisposable.Instance;
        }

        var originalMode = graphics.CompositingMode;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        return new CompositingModeScope(graphics, originalMode);
    }

    private void ClearBitmap()
    {
        _graphics.Clear(TransparentKeyColor);
    }

    private Rectangle GetStrokeRectangle(InkStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return Rectangle.Empty;
        }

        var bounds = GetDotRectangle(stroke.Points[0], (int)Math.Ceiling(stroke.Style.Width));
        for (var index = 1; index < stroke.Points.Count; index++)
        {
            bounds = Rectangle.Union(bounds, GetSegmentRectangle(stroke.Points[index - 1], stroke.Points[index], stroke.Style));
        }

        return bounds;
    }

    private static Rectangle GetDotRectangle(Point point, int diameter)
    {
        return Rectangle.FromLTRB(
            point.X - (diameter / 2) - 4,
            point.Y - (diameter / 2) - 4,
            point.X + (diameter / 2) + 4,
            point.Y + (diameter / 2) + 4);
    }

    private Rectangle GetSegmentRectangle(Point start, Point end, InkStrokeStyle style)
    {
        var padding = (int)Math.Ceiling(style.Width) + 8;
        var left = Math.Min(start.X, end.X) - padding;
        var top = Math.Min(start.Y, end.Y) - padding;
        var right = Math.Max(start.X, end.X) + padding;
        var bottom = Math.Max(start.Y, end.Y) + padding;
        return Rectangle.Intersect(
            FullCanvas,
            Rectangle.FromLTRB(left, top, right, bottom));
    }

    private Rectangle FullCanvas => new(Point.Empty, _size);

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
    }

    public void Dispose()
    {
        _graphics.Dispose();
        _bitmap.Dispose();
    }

    private sealed class InkStroke(InkStrokeStyle style)
    {
        public InkStrokeStyle Style { get; } = style;
        public List<Point> Points { get; } = [];
    }

    private readonly record struct InkStrokeStyle(PenTool Tool, Color Color, float Width);

    private sealed class CompositingModeScope(Graphics graphics, CompositingMode originalMode) : IDisposable
    {
        public void Dispose()
        {
            graphics.CompositingMode = originalMode;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
