# UIAccess Investigation

## Why This Matters
The `--pointer-ink-spike` mode attempts to use `RegisterPointerInputTarget` so Overdraw can receive pen pointer input directly while the visible overlay remains click-through. On the current local development build, this fails with Win32 error `5` (`ERROR_ACCESS_DENIED`).

That result is expected for a normal unpackaged desktop process. The cleaner native input path likely requires the app to run with Windows `uiAccess` privileges.

## What UIAccess Requires
`uiAccess` is not the same as administrator elevation. It is an accessibility-oriented trust mode that lets a desktop app bypass normal User Interface Privilege Isolation restrictions.

For Overdraw, the practical requirements are:

- The executable manifest must request `uiAccess="true"`.
- The process should run as `asInvoker`, not as a normal always-admin app.
- The executable must be Authenticode signed.
- The signing certificate must chain to a trusted root on the target machine.
- The installed executable must live in a secure location, typically under `C:\Program Files\`, `C:\Program Files (x86)\`, or `C:\Windows\System32\`.
- Local debug builds from the repo directory should keep `uiAccess="false"` unless they are installed and signed through a dedicated test flow.

## Why We Should Not Flip This On By Default
Setting `uiAccess="true"` in the normal debug app manifest will make local development brittle. A build run from the repository directory is not in a secure install location, and an unsigned executable will not satisfy Windows checks.

The repo should keep ordinary debug builds runnable, then add a separate signed/installable test path for `uiAccess` validation.

## Proposed Test Path
1. Add an application manifest template for a `uiAccess=true` build.
2. Create or import a test code-signing certificate on the development machine.
3. Publish the app to a test install directory under `C:\Program Files\Overdraw\`.
4. Sign the published executable.
5. Run `--pointer-ink-spike --monitor 1 --verbose` from the installed location.
6. Confirm whether `RegisterPointerInputTarget` succeeds.
7. If it succeeds, validate whether native pen input arrives without moving the system cursor.

## Repo Support
The repo now has separate manifests:

- `src/Overdraw.App/app.manifest`: normal local builds with `uiAccess="false"`.
- `src/Overdraw.App/app.uiaccess.manifest`: opt-in test builds with `uiAccess="true"`.

Build the UIAccess manifest variant without publishing:

```powershell
dotnet build src/Overdraw.App/Overdraw.App.csproj -p:UiAccess=true
```

Create a local test code-signing certificate:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-OverdrawTestCertificate.ps1 -TrustInLocalMachineRoot
```

Use an elevated PowerShell session for `-TrustInLocalMachineRoot`. Current-user trust may be enough for ordinary signature validation, but UIAccess launch checks are stricter and should be tested with local-machine root trust.

Do not use `-TrustInCurrentUserRoot` for the UIAccess launch test unless you are intentionally testing a weaker trust path. A `uiAccess=true` executable can still fail to start with `A referral was returned from the server` when the signer is not trusted at the machine level.

Publish, sign, and install the UIAccess test build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-UiAccessTestBuild.ps1
```

Run preflight checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-UiAccessBuild.ps1
```

The preflight output should show:

- `Signature status: Valid`
- `Secure location: True`
- `Matches repo thumbprint: True`
- `Trusted in LocalMachine Root: True`

The install step writes to `C:\Program Files\Overdraw` by default. Run PowerShell as Administrator for that step, or pass `-SkipInstall` to publish/sign only.

Run the installed build:

```powershell
& 'C:\Program Files\Overdraw\Overdraw.App.exe' --pointer-ink-spike --monitor 1 --verbose
```

## Current Decision
Keep `--ink-spike` as the working fallback because it proves pen drawing plus mouse pass-through. Keep `--pointer-ink-spike` as the preferred architecture experiment.

The signed `uiAccess=true` test build can now pass the local preflight checks and launch from `C:\Program Files\Overdraw`. Confirmed behavior so far:

- The installed executable is signed by the repo-generated test certificate.
- The signing certificate can be trusted in `Cert:\LocalMachine\Root`.
- The installed executable runs from a secure location.
- Mouse and keyboard interaction can still reach underlying desktop UI while the pointer-target overlay is running.
- `RegisterPointerInputTarget` succeeds in the installed UIAccess build.
- XP-Pen strokes draw in `--pointer-ink-spike`.
- Native pointer-target mode does not move the normal pointer to the pen location during drawing.

Remaining validation:

- Verify whether scoped pointer-target cursor suppression removes the Windows busy/spinning cursor while the pen pointer is active.
- Measure whether pointer-target mode has acceptable stroke latency compared with the hook-based fallback.

## References
- Microsoft application manifest documentation: `requestedExecutionLevel` supports the `uiAccess` attribute for UI accessibility applications.
- Microsoft UAC policy documentation: UIAccess applications are required to be signed and, by default, installed in secure filesystem locations such as `Program Files`.
