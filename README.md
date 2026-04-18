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

## Quick Setup

From an elevated PowerShell terminal outside VS Code:

```powershell
cd E:\Data\GithubProjects\Overdraw
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Overdraw.ps1 -Monitor 1 -VerboseLaunch
```

This builds the solution, creates and trusts the local UIAccess test certificate if needed, publishes, signs, installs to `C:\Program Files\Overdraw`, runs preflight checks, and creates shortcuts.

To launch Overdraw automatically after setup:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Overdraw.ps1 -Monitor 1 -VerboseLaunch -Launch
```

## Workflow

Use PowerShell from the repository root:

```powershell
cd E:\Data\GithubProjects\Overdraw
```

Install the .NET 8 SDK before building or running from source. For UIAccess install, signing, and `C:\Program Files\Overdraw` updates, use an elevated PowerShell session or run VS Code as Administrator.

### 1. Build

Build the solution first:

```powershell
dotnet build Overdraw.sln
```

### 2. Pick A Monitor

For multi-monitor setups, list the detected displays:

```powershell
dotnet run --project src/Overdraw.App -- --list-monitors
```

`--monitor` accepts `primary`, a zero-based index from `--list-monitors`, or a device name such as `\\.\DISPLAY2`.

The current XP-Pen setup usually uses:

```powershell
--monitor 1
```

### 3. Run From Source

Run the basic click-through overlay spike:

```powershell
dotnet run --project src/Overdraw.App -- --overlay-spike
```

This validates overlay window placement, always-on-top behavior, focus avoidance, and mouse click-through behavior. It closes with `Ctrl+Shift+F12`.

Run pen diagnostics:

```powershell
dotnet run --project src/Overdraw.App -- --pen-spike --monitor 1
```

This opens an interactive full-screen window on the selected monitor and reports whether Windows sees incoming pointer input as `pen`, `mouse`, or another pointer type.

Run the hook-based ink fallback:

```powershell
dotnet run --project src/Overdraw.App -- --ink-spike --monitor 1
```

This keeps the overlay click-through for normal mouse usage while using a low-level mouse hook to treat pen-originated mouse messages as drawing input. It remains available as a fallback diagnostic, but it is not the preferred interaction model because it follows the system cursor.

Run the preferred pointer-target ink path:

```powershell
dotnet run --project src/Overdraw.App -- --pointer-ink-spike --monitor 1
```

This tries to receive pen pointer input directly instead of using pen-originated mouse messages. If registration fails, the app prints the Win32 error and stays open for observation.

Use `--verbose` with any run mode to print extra placement or pointer diagnostics:

```powershell
dotnet run --project src/Overdraw.App -- --pointer-ink-spike --monitor 1 --verbose
```

### 4. Install The UIAccess Build

On normal local builds, `--pointer-ink-spike` may fail with `ERROR_ACCESS_DENIED`. The preferred validation path is a signed `uiAccess=true` build installed from a secure Windows location. See `docs/UIACCESS.md` for details.

The easiest full setup path is one command from an elevated PowerShell session outside VS Code:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Overdraw.ps1 -Monitor 1 -VerboseLaunch
```

This builds the solution, creates and trusts the local UIAccess test certificate if needed, publishes, signs, installs to `C:\Program Files\Overdraw`, runs preflight checks, and creates shortcuts.

To launch Overdraw automatically after setup, add `-Launch`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Overdraw.ps1 -Monitor 1 -VerboseLaunch -Launch
```

If you only want to run the install step without the initial solution build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-Overdraw.ps1 -Monitor 1
```

Use `-Monitor 1` for the current XP-Pen setup, or another selector accepted by `--monitor`. Add `-VerboseLaunch` if you want generated shortcuts to include `--verbose`.

To do the UIAccess setup manually the first time:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-OverdrawTestCertificate.ps1 -TrustInLocalMachineRoot
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-UiAccessTestBuild.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Test-UiAccessBuild.ps1
& 'C:\Program Files\Overdraw\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose
```

After the certificate has been created once, normal UIAccess development iterations usually only need:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-UiAccessTestBuild.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Test-UiAccessBuild.ps1
& 'C:\Program Files\Overdraw\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose
```

`uiAccess=true` launch checks require the executable to be signed by a trusted certificate chain and installed in a secure location such as `C:\Program Files\Overdraw`. If launch fails with `A referral was returned from the server`, confirm the preflight check reports `Trusted in LocalMachine Root: True`; current-user root trust is not the intended test path.

The publish script cleans the installed test directory before copying the new build, which avoids stale files from older publish shapes.

### 5. Test The Installed Build

Run the installed pointer-target ink path:

```powershell
& 'C:\Program Files\Overdraw\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose
```

Expected startup output includes:

```text
Pointer-target pen ink active
```

Core manual checks:

- Draw with the XP-Pen pen and confirm red strokes appear.
- Confirm mouse clicks, right-clicks, scrolling, and typing still reach the underlying app.
- Confirm the overlay does not steal focus.
- Press `Ctrl+Shift+K`, hold `Ctrl+Shift+S`, and confirm the overlay shows `Ctrl+Shift+S`.
- Type normal words into an underlying app and confirm Overdraw does not keep a typed history.
- Press `Ctrl+Shift+F12` to close Overdraw.

See `docs/TESTING.md` for the full regression checklist.

### 6. Controls

Current ink controls while an ink overlay is running:

- `Ctrl+Shift+Z`: undo the last stroke.
- `Ctrl+Shift+Y`: redo the last undone stroke.
- `Ctrl+Shift+Backspace`: clear all ink.
- `Ctrl+Shift+C`: cycle the colour for future strokes.
- `Ctrl+Shift+Up`: increase opacity for future strokes.
- `Ctrl+Shift+Down`: decrease opacity for future strokes.
- `Ctrl+Shift+K`: toggle the shortcut-style key display.
- `Ctrl+Shift+F12`: close Overdraw.

Colour and opacity changes apply only to new strokes. Existing strokes keep the colour and opacity they had when drawn. Hardware eraser support is planned after the stroke-history model is validated.
The key display starts disabled, shows only the currently held shortcut combination while enabled, and does not keep a typed history.

### 7. Uninstall Test Builds

To remove the installed test build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Remove-UiAccessTestBuild.ps1
```

To also remove the local test certificate trust/signing entries, run from an elevated PowerShell session:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Remove-UiAccessTestBuild.ps1 -RemoveCertificates
```

The signed UIAccess build path has been validated far enough to launch from `C:\Program Files\Overdraw`, preserve normal mouse/keyboard interaction, receive XP-Pen pointer input, and draw without moving the normal pointer to the pen location. A scoped cursor suppression experiment is in place for the remaining issue where Windows can show a busy/spinning cursor near the pen while drawing.

## Status
This repository now contains a .NET 8 Windows prototype scaffold plus project documentation. Monitor selection, click-through overlay behavior, XP-Pen pointer detection, signed UIAccess launch, and native pointer-target ink drawing have been validated on the target setup.

The current production direction is `--pointer-ink-spike` from the signed `uiAccess=true` install. The hook-based `--ink-spike` remains available as a fallback diagnostic, but it is not the preferred interaction model because it follows the system cursor.
