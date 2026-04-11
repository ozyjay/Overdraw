# overdraw

`overdraw` is a Windows-first C# tool for drawing over the screen without blocking normal desktop interaction. The goal is to let a pen display user annotate freely across the entire desktop while continuing to click, type, edit, and navigate the underlying applications as normal.

## Core Idea
Overdraw provides an always-on-top visual layer that behaves like an annotation surface rather than a traditional drawing app window.

- The overlay stays visible across the desktop.
- Standard mouse and keyboard interaction should pass through to the application underneath.
- Pen interaction is the primary drawing input.
- Pen proximity or equivalent pen-specific state should be used to distinguish intentional drawing from normal desktop usage.

## Primary Use Case
A user with an XP-Pen display wants to circle code, sketch UI ideas, mark documents, or highlight parts of the screen while still interacting with their IDE, browser, terminal, or document editor. Overdraw should feel like drawing on glass over the desktop, not like switching into a separate application mode.

## V1 Constraints
- Windows is the only planned target for the first implementation.
- The first milestone is a proof of concept for full-screen click-through overlay behavior plus pen-driven drawing.
- Mouse drawing is out of scope unless later added as an explicit mode.
- Cross-platform support is deferred until the Windows interaction model is proven.
- The runtime stack is now .NET 8 plus WinForms/Win32 interop rather than Python.

## Planned Project Shape
- `AGENTS.md`: Codex operating guide for the repo
- `Overdraw.sln`: solution entrypoint
- `src/Overdraw.App/`: current Windows application project
- `docs/PRD.md`: v1 product requirements
- `docs/ARCHITECTURE.md`: system design and module boundaries
- `docs/TASKS.md`: implementation backlog
- `docs/TESTING.md`: validation and hardware test plan
- `docs/CONTRIBUTING.md`: development workflow

## Initial Development Priorities
1. Prove that a Windows overlay can remain visually on top without blocking underlying mouse and keyboard interaction.
2. Detect pen-specific input or proximity reliably enough to gate drawing behavior.
3. Render low-latency strokes across the desktop.
4. Preserve stability and focus behavior while the overlay is active.

## Current Spike
With a local .NET 8 environment, the current feasibility prototype can be run with:

```powershell
dotnet run --project src/Overdraw.App -- --overlay-spike
```

This spike is only for validating overlay window behavior on Windows.

The current spike is a native Windows Forms window with Win32 extended styles and hit-testing overrides so we can validate monitor targeting and true mouse click-through behavior in a more suitable stack.
The overlay is designed to avoid taking focus, so it closes with `Ctrl+Shift+F12` rather than `Esc`.

There is also a pen diagnostics mode that opens an interactive full-screen window on the selected monitor and reports whether Windows sees the incoming pointer as `pen`, `mouse`, or another pointer type:

```powershell
dotnet run --project src/Overdraw.App -- --pen-spike --monitor 1
```

Use `--verbose` with either spike to print extra placement or pointer diagnostics to the terminal.

There is now an experimental combined mode that keeps the overlay click-through for normal mouse usage while using a low-level mouse hook to treat pen-originated mouse messages as drawing input:

```powershell
dotnet run --project src/Overdraw.App -- --ink-spike --monitor 1
```

This is an experiment, not a finished interaction model. Its purpose is to test whether pen-originated mouse messages can be intercepted for drawing while real mouse input still passes through to the underlying applications.

There is also a native pointer-target ink experiment that tries to receive pen pointer input directly instead of using pen-originated mouse messages:

```powershell
dotnet run --project src/Overdraw.App -- --pointer-ink-spike --monitor 1
```

This is the preferred direction if Windows allows the app to register as a pen pointer target while the overlay remains click-through. If registration fails, the app prints the Win32 error and stays open for observation.

On normal local builds, `--pointer-ink-spike` may fail with `ERROR_ACCESS_DENIED`. That is now tracked in `docs/UIACCESS.md`; the likely next validation path is a signed `uiAccess=true` build installed from a secure Windows location.

For the UIAccess test path:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-OverdrawTestCertificate.ps1 -TrustInLocalMachineRoot
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-UiAccessTestBuild.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Test-UiAccessBuild.ps1
& 'C:\Program Files\Overdraw\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose
```

Run these from an elevated PowerShell session. `uiAccess=true` launch checks require the executable to be signed by a trusted certificate chain and installed in a secure location such as `C:\Program Files\Overdraw`.
If the launch fails with `A referral was returned from the server`, confirm the preflight check reports `Trusted in LocalMachine Root: True`; current-user root trust is not the intended test path.

The signed UIAccess build path has been validated far enough to launch from `C:\Program Files\Overdraw`, preserve normal mouse/keyboard interaction, receive XP-Pen pointer input, and draw without moving the normal pointer to the pen location. A scoped cursor suppression experiment is in place for the remaining issue where Windows can show a busy/spinning cursor near the pen while drawing.

Build the project with:

```powershell
dotnet build Overdraw.sln
```

For multi-monitor setups, list the detected displays first:

```powershell
dotnet run --project src/Overdraw.App -- --list-monitors
```

Then target a specific display for the overlay spike:

```powershell
dotnet run --project src/Overdraw.App -- --overlay-spike --monitor 1
```

`--monitor` accepts `primary`, a zero-based index from `--list-monitors`, or a device name such as `\\.\DISPLAY2`.

## Status
This repository now contains a .NET 8 Windows prototype scaffold plus project documentation. Monitor selection, click-through overlay behavior, XP-Pen pointer detection, signed UIAccess launch, and native pointer-target ink drawing have been validated on the target setup.

The current production direction is `--pointer-ink-spike` from the signed `uiAccess=true` install. The hook-based `--ink-spike` remains available as a fallback diagnostic, but it is not the preferred interaction model because it follows the system cursor.
