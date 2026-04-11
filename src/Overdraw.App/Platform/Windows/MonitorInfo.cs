using System.Drawing;

namespace Overdraw.App;

internal sealed record MonitorInfo(
    int Index,
    string DeviceName,
    string FriendlyName,
    Rectangle Bounds,
    bool IsPrimary)
{
    public string ToDisplayString()
    {
        var primarySuffix = IsPrimary ? " primary" : string.Empty;
        return $"[{Index}] {DeviceName} ({FriendlyName}) {Bounds.Width}x{Bounds.Height} at ({Bounds.Left}, {Bounds.Top}){primarySuffix}";
    }
}
