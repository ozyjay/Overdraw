# AGENTS.md

## Mission
Build `overdraw`, a Windows-first Python desktop tool that draws a low-latency visual overlay across the entire screen while preserving normal interaction with underlying applications. The overlay must be effectively click-through for standard input, and drawing should activate only for pen-driven interaction from supported hardware such as an XP-Pen display.

## Product Intent
- The user can keep using an IDE, browser, document editor, terminal, or other desktop app normally.
- Mouse and keyboard input should continue reaching the underlying application as if Overdraw were not present.
- When the pen approaches the display and enters drawing range, Overdraw should treat that as intentional annotation input and render strokes across the screen.
- The initial target is a stable Windows proof of concept, not early cross-platform parity.

## Working Assumptions
- Primary runtime: modern CPython on Windows.
- Primary hardware scenario: XP-Pen pen display connected to a Windows workstation.
- Primary challenge: combining an always-on-top visual layer with input pass-through and pen-specific activation.
- Early implementation may require focused Windows API integration and small technical spikes before settling the final app stack.

## Engineering Principles
- Prefer correctness of interaction behavior over feature count.
- Preserve separation between Windows-specific integration and app-level drawing logic.
- Keep latency low and rendering predictable.
- Avoid introducing abstractions for non-Windows platforms until the Windows model is proven.
- Treat accidental focus capture, blocked clicks, or unintended mouse drawing as critical regressions.

## Expected Repository Shape
- `README.md`: project overview and quick start context.
- `docs/PRD.md`: product requirements and v1 scope.
- `docs/ARCHITECTURE.md`: system design and module boundaries.
- `docs/TASKS.md`: prioritized implementation backlog.
- `docs/TESTING.md`: validation strategy and manual hardware checks.
- `docs/CONTRIBUTING.md`: development workflow and expectations.
- `src/overdraw/`: future application package.
- `tests/`: future automated tests.

## Module Boundaries To Preserve
- Platform adapter:
  owns Windows-specific window creation, transparency, z-order, hit-testing behavior, focus management, and pen/input-device integration.
- Drawing engine:
  owns stroke state, smoothing decisions, compositing model, frame updates, and rendering-friendly data structures.
- Application orchestration:
  owns lifecycle, configuration, mode switching, telemetry hooks if later added, and coordination between platform and drawing layers.

## Planning Rules
- Validate feasibility with small Windows-focused spikes before locking major framework choices.
- Document architecture decisions when they affect input behavior, rendering model, or packaging strategy.
- Prefer explicit acceptance criteria for input-routing behavior over vague UX descriptions.
- Keep public interfaces small until hardware behavior is observed on the target XP-Pen setup.

## Coding Expectations
- Use Python type hints for new runtime code.
- Prefer standard library plus narrowly justified dependencies.
- Write code and docs in ASCII unless an existing file requires otherwise.
- Add comments only where behavior is subtle, hardware-driven, or Windows-specific.
- Keep platform-specific code isolated behind clear interfaces.

## Testing Expectations
- Add automated tests where logic is hardware-independent.
- Back Windows input and overlay behavior with manual validation scripts and checklists.
- Verify the core contract repeatedly:
  pen draws, overlay stays visible, mouse clicks pass through, keyboard input reaches the underlying app, and app focus is not stolen.
- Call out any behavior that cannot yet be tested automatically.

## Guardrails
- Do not ship a design that intercepts ordinary mouse activity as the default drawing path.
- Do not couple stroke logic directly to raw Windows event handling when a thinner adapter boundary will do.
- Do not optimize for cross-platform support before the Windows proof of concept is reliable.
- Do not assume all pen APIs expose proximity identically; isolate this behind a capability-driven interface.

## Definition Of Early Success
- A Windows prototype can render visible strokes over the full desktop.
- Underlying applications remain usable with mouse and keyboard while the overlay is present.
- Pen-driven drawing can be activated intentionally on supported hardware without degrading normal desktop interaction.
