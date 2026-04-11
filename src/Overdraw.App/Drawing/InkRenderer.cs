using System.Drawing;

namespace Overdraw.App;

internal sealed class InkRenderer : IDisposable
{
    public static readonly Color TransparentKeyColor = Color.FromArgb(0, 1, 0);
    public static readonly Color InkColor = Color.FromArgb(255, 255, 64, 96);

    private readonly Size _size;
    private readonly Bitmap _bitmap;
    private readonly Graphics _graphics;
    private Point? _lastPoint;
    private bool _penIsDown;

    public InkRenderer(Size size)
    {
        _size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
        _bitmap = new Bitmap(_size.Width, _size.Height);
        _graphics = Graphics.FromImage(_bitmap);
        _graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        _graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        _graphics.Clear(TransparentKeyColor);
    }

    public void Paint(Graphics target)
    {
        target.DrawImageUnscaled(_bitmap, 0, 0);
    }

    public Rectangle? HandlePenEvent(PenInputEvent input)
    {
        return input.Kind switch
        {
            PenInputEventKind.Down => HandleDown(input.Point),
            PenInputEventKind.Move => HandleMove(input.Point),
            PenInputEventKind.Up => HandleUp(input.Point),
            _ => null
        };
    }

    private Rectangle HandleDown(Point point)
    {
        _penIsDown = true;
        _lastPoint = point;
        return DrawDot(point);
    }

    private Rectangle? HandleMove(Point point)
    {
        if (!_penIsDown)
        {
            return null;
        }

        return DrawSegment(point);
    }

    private Rectangle? HandleUp(Point point)
    {
        Rectangle? dirtyRectangle = null;
        if (_penIsDown)
        {
            dirtyRectangle = DrawSegment(point);
        }

        _penIsDown = false;
        _lastPoint = null;
        return dirtyRectangle;
    }

    private Rectangle DrawSegment(Point point)
    {
        using var inkPen = new Pen(InkColor, 10f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };

        if (_lastPoint is Point lastPoint)
        {
            _graphics.DrawLine(inkPen, lastPoint, point);
            _lastPoint = point;
            return GetSegmentRectangle(lastPoint, point, 18);
        }

        _lastPoint = point;
        return DrawDot(point);
    }

    private Rectangle DrawDot(Point point)
    {
        using var inkBrush = new SolidBrush(InkColor);
        const int diameter = 10;
        _graphics.FillEllipse(inkBrush, point.X - (diameter / 2), point.Y - (diameter / 2), diameter, diameter);
        return GetDotRectangle(point, diameter);
    }

    private static Rectangle GetDotRectangle(Point point, int diameter)
    {
        return Rectangle.FromLTRB(
            point.X - (diameter / 2) - 4,
            point.Y - (diameter / 2) - 4,
            point.X + (diameter / 2) + 4,
            point.Y + (diameter / 2) + 4);
    }

    private Rectangle GetSegmentRectangle(Point start, Point end, int padding)
    {
        var left = Math.Min(start.X, end.X) - padding;
        var top = Math.Min(start.Y, end.Y) - padding;
        var right = Math.Max(start.X, end.X) + padding;
        var bottom = Math.Max(start.Y, end.Y) + padding;
        return Rectangle.Intersect(
            new Rectangle(Point.Empty, _size),
            Rectangle.FromLTRB(left, top, right, bottom));
    }

    public void Dispose()
    {
        _graphics.Dispose();
        _bitmap.Dispose();
    }
}
