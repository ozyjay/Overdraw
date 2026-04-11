namespace Overdraw.App;

internal sealed record CliOptions(
    bool RunOverlaySpike,
    bool RunPenSpike,
    bool RunInkSpike,
    bool RunPointerInkSpike,
    bool ListMonitors,
    bool ShowHelp,
    bool Verbose,
    string MonitorSelector,
    bool IsValid,
    string? ErrorMessage)
{
    public static CliOptions Parse(IEnumerable<string> args)
    {
        var runOverlaySpike = false;
        var runPenSpike = false;
        var runInkSpike = false;
        var runPointerInkSpike = false;
        var listMonitors = false;
        var showHelp = false;
        var verbose = false;
        var monitorSelector = "primary";

        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            switch (arg)
            {
                case "--overlay-spike":
                    runOverlaySpike = true;
                    break;
                case "--pen-spike":
                    runPenSpike = true;
                    break;
                case "--ink-spike":
                    runInkSpike = true;
                    break;
                case "--pointer-ink-spike":
                    runPointerInkSpike = true;
                    break;
                case "--list-monitors":
                    listMonitors = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--monitor":
                    if (!enumerator.MoveNext())
                    {
                        return Invalid("Missing value for --monitor.");
                    }

                    monitorSelector = enumerator.Current;
                    break;
                default:
                    return Invalid($"Unknown argument: {arg}");
            }
        }

        var modeCount = Convert.ToInt32(runOverlaySpike) +
                        Convert.ToInt32(runPenSpike) +
                        Convert.ToInt32(runInkSpike) +
                        Convert.ToInt32(runPointerInkSpike);
        if (modeCount > 1)
        {
            return Invalid("Choose only one run mode.", monitorSelector);
        }

        return new CliOptions(runOverlaySpike, runPenSpike, runInkSpike, runPointerInkSpike, listMonitors, showHelp, verbose, monitorSelector, true, null);
    }

    private static CliOptions Invalid(string message, string monitorSelector = "primary")
    {
        return new CliOptions(false, false, false, false, false, false, false, monitorSelector, false, message);
    }
}
