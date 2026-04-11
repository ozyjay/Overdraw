using System.Drawing;

namespace Overdraw.App;

internal enum PenInputEventKind
{
    Down,
    Move,
    Up
}

internal readonly record struct PenInputEvent(PenInputEventKind Kind, Point Point, uint PointerId);
