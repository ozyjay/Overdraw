# Testing Strategy

## Goals
Validate that Overdraw preserves the core interaction contract on Windows while remaining usable with XP-Pen pen input.

## Test Layers
- Unit tests:
  cover hardware-independent logic such as stroke modeling, event normalization rules, and configuration parsing.
- Integration tests:
  cover subsystem interaction where practical without requiring the full hardware path.
- Manual hardware validation:
  cover overlay behavior, focus behavior, XP-Pen pen activation, latency, and click-through expectations.

## Critical Manual Scenarios
- Launch Overdraw while another application is focused and confirm the active app remains usable.
- Verify the overlay is visible and remains on top of normal windows.
- Click, drag, and type into the underlying application with mouse and keyboard and confirm input is not blocked.
- Bring the XP-Pen pen into range and verify Overdraw becomes draw-ready without disturbing normal app focus.
- Draw visible strokes with the pen and confirm stroke updates remain responsive.
- Move the mouse without the pen and confirm no accidental strokes are created.
- Clear or exit the overlay session and confirm the desktop returns to normal behavior.

## Failure Cases To Watch
- Overlay becomes the focused window unexpectedly.
- Mouse clicks stop reaching the underlying app.
- Keyboard input is swallowed or redirected.
- Pen hover or proximity causes unstable mode switching.
- Rendering lags badly during longer strokes.
- Multi-monitor or DPI behavior causes offset or clipped drawing.

## Automation Guidance
- Automate only the logic that is stable and hardware-independent first.
- Keep Windows API behavior behind interfaces so non-UI logic can be tested without the live overlay.
- Use small fixtures for stroke data and normalized pen events.
- Treat hardware validation scripts and checklists as first-class test assets.

## Exit Criteria For Early Prototype
- Full-screen overlay can be demonstrated on Windows.
- Standard mouse and keyboard interaction still works in the underlying app.
- Pen-driven drawing is functional on the target XP-Pen setup.
- Known limitations are written down with reproduction notes where possible.
