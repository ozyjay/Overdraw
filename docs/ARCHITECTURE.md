# Architecture

## Overview
Overdraw should be structured as a Windows-first Python application with a narrow platform integration layer and a platform-agnostic drawing core. The dominant architectural requirement is input routing: the application must render above the desktop while preserving normal interaction with whatever window sits underneath.

## Proposed System Shape
- Application orchestration:
  starts the app, owns configuration, wires subsystems together, and manages runtime modes.
- Platform adapter:
  handles Windows window creation, transparency, always-on-top behavior, hit-testing configuration, focus avoidance, and pen/input integration.
- Drawing engine:
  stores stroke state, converts pen events into renderable paths, and exposes redraw-friendly data for the overlay.
- Overlay renderer:
  paints the current drawing state onto the transparent full-screen surface with minimal latency.

## Runtime Flow
1. Start the application and create the overlay window.
2. Configure the overlay to remain visible without acting like a normal interactive window for standard desktop input.
3. Listen for device events through the Windows adapter.
4. When pen-specific draw conditions are met, translate incoming pen data into stroke updates.
5. Render updated strokes on the overlay while leaving normal mouse and keyboard interaction untouched.

## Windows-Specific Concerns
- Transparent overlay behavior:
  the chosen windowing approach must support a visible surface combined with click-through semantics for ordinary interaction.
- Focus management:
  the overlay should avoid becoming the active window during normal operation.
- Pen integration:
  the implementation must determine whether Windows Ink, pointer events, tablet APIs, or vendor-exposed behavior provides the most reliable path for XP-Pen input and proximity detection.
- Multi-monitor behavior:
  early design should assume the app may eventually need to cover the full virtual desktop, even if the first proof of concept starts on the primary display.

## Module Boundaries
- `src/overdraw/app/`:
  lifecycle, configuration loading, and mode coordination.
- `src/overdraw/platform/windows/`:
  Windows adapter, overlay window management, and device-event handling.
- `src/overdraw/drawing/`:
  stroke model, pen-event normalization, and render state.
- `src/overdraw/rendering/`:
  drawing primitives and frame update integration with the selected GUI/rendering stack.

These paths are intentional placeholders, not locked implementation names.

## Interface Direction
- Platform adapter should expose normalized pen events and overlay state transitions, not raw Windows messages throughout the codebase.
- Drawing engine should accept normalized events and emit a renderable stroke model.
- Application orchestration should decide when drawing is enabled and how subsystems are composed.

## Early Technical Spikes
- Verify at least one Python-compatible windowing approach can provide:
  visible transparent overlay, always-on-top placement, and non-blocking mouse behavior.
- Verify a reliable path for XP-Pen pen detection and proximity or equivalent draw gating.
- Measure rendering latency and redraw behavior with continuous strokes.

## Architecture Constraints
- Do not bake XP-Pen driver assumptions directly into the stroke engine.
- Do not let raw Windows event handling leak into higher-level app logic.
- Do not over-design plugin systems, cross-platform shims, or persistence layers before the core interaction contract works.
