namespace Overdraw.App;

internal static class MonitorCatalog
{
    public static IReadOnlyList<MonitorInfo> Enumerate()
    {
        return NativeMethods.EnumerateMonitors()
            .Select((monitor, index) => monitor with { Index = index })
            .ToArray();
    }

    public static MonitorInfo Select(IReadOnlyList<MonitorInfo> monitors, string selector)
    {
        var normalized = selector.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("primary", StringComparison.OrdinalIgnoreCase))
        {
            return monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors[0];
        }

        if (int.TryParse(normalized, out var index))
        {
            var byIndex = monitors.FirstOrDefault(monitor => monitor.Index == index);
            return byIndex ?? throw new ArgumentException($"No monitor found at index {index}.");
        }

        var byName = monitors.FirstOrDefault(
            monitor =>
                monitor.DeviceName.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                monitor.FriendlyName.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                monitor.FriendlyName.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        return byName ?? throw new ArgumentException($"No monitor found for selector '{selector}'.");
    }
}
