# Tasks

## Milestone 0: Repository Bootstrap
- Create Python project metadata and initial package layout.
- Add baseline tooling for formatting, linting, and test execution.
- Add a minimal application entrypoint and placeholder module structure.
- Capture architecture decisions as they become concrete.

## Milestone 1: Windows Overlay Feasibility Spike
- Evaluate Python-compatible windowing options for a transparent always-on-top overlay.
- Prove click-through behavior for ordinary mouse interaction.
- Prove that keyboard input continues to reach the active underlying window.
- Record any limitations around focus, z-order, or display configuration.

## Milestone 2: Pen Input Feasibility Spike
- Identify the best Windows input path for XP-Pen pen events.
- Determine whether pen proximity is directly available or whether a practical equivalent signal is needed.
- Normalize pen-down, pen-move, and pen-up events behind a platform adapter.
- Document hardware and driver assumptions discovered during testing.

## Milestone 3: First End-To-End Prototype
- Combine the overlay and pen input spikes into a runnable prototype.
- Render visible strokes across the active screen area.
- Ensure mouse movement alone does not create strokes.
- Preserve underlying application usability during annotation.

## Milestone 4: Drawing Engine Stabilization
- Introduce stroke data structures and lifecycle management.
- Improve stroke smoothness and redraw performance.
- Add clear, minimal controls for clearing or exiting the overlay session.
- Validate behavior across longer annotation sessions.

## Milestone 5: Configuration And Packaging
- Add basic configuration for pen behavior, colors, and stroke width if justified.
- Define packaging and local installation workflow for Windows.
- Prepare repeatable setup instructions for XP-Pen users.

## Milestone 6: Quality And Release Readiness
- Add automated tests for hardware-independent logic.
- Add manual regression checklists for overlay and input behavior.
- Document known limitations and supported hardware assumptions.
- Produce a demo-ready v1 checklist.

## Priority Notes
- The overlay interaction contract is the highest-priority risk.
- Pen detection reliability is the second highest-priority risk.
- UI polish and extra drawing features come after the interaction model is proven.
