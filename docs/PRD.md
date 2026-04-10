# Product Requirements Document

## Product Summary
Overdraw is a desktop annotation utility for Windows that lets a user draw over the entire screen using a pen display while continuing to interact with the underlying desktop applications. The core product promise is visual annotation without normal input interference.

## Target User
- Primary user: a Windows user with an XP-Pen display and pen who wants to annotate directly over active applications.
- Typical environments: IDEs, browsers, design tools, documents, presentations, and terminal windows.

## Problem
Existing drawing tools usually claim focus, capture clicks, require explicit mode switching, or block interaction with the content beneath them. This breaks workflows where the user wants to annotate while still using the underlying software normally.

## Goals
- Provide a desktop-wide drawing overlay that remains visually above other windows.
- Preserve underlying application clickability and keyboard usability during normal interaction.
- Use pen-specific interaction as the primary trigger for drawing.
- Keep the first version focused on a practical Windows workflow with XP-Pen hardware.

## Non-Goals
- Cross-platform support in v1.
- Rich illustration features such as layers, shapes, text tools, or asset export workflows.
- Collaboration, cloud sync, or multi-user features.
- Replacing a full drawing application.
- Default mouse-based drawing.

## User Stories
- As a developer, I want to circle and underline parts of my IDE without interrupting my ability to type or click in the editor.
- As a presenter, I want to annotate anything on screen while still advancing slides or interacting with the app underneath.
- As a reviewer, I want pen input to feel intentional and distinct from ordinary desktop mouse usage.

## Core Behavioral Requirements
- The overlay must be visually present across the desktop while active.
- Normal mouse interaction must continue reaching the underlying application.
- Normal keyboard interaction must continue reaching the underlying application.
- Drawing should only occur for pen-driven interaction or a similarly explicit pen-only path.
- The overlay should not unexpectedly steal focus from the active application.

## Activation Model
- Default assumption: pen proximity or equivalent pen capability places Overdraw into a draw-ready state.
- Drawing begins only when pen contact or pen-down input is detected.
- Moving the mouse alone must not create strokes.
- If reliable proximity detection is unavailable on some hardware path, the app may need a documented fallback mode, but that fallback should not weaken the default pen-only intent.

## Success Criteria
- A user can annotate across the full desktop with visible strokes.
- The underlying app remains usable with mouse and keyboard while Overdraw is running.
- Pen drawing feels responsive enough for live annotation.
- The app can be demonstrated successfully on the target XP-Pen hardware with a repeatable setup.

## Risks
- Pen proximity APIs may vary by driver or device.
- Some overlay or transparency techniques may still affect hit testing or focus behavior.
- Full-screen rendering performance may vary with chosen GUI or graphics stack.

## Acceptance Criteria For V1
- Demonstrate full-screen overlay rendering on Windows.
- Demonstrate pen-driven stroke rendering.
- Demonstrate click-through behavior for standard mouse interaction.
- Demonstrate that keyboard input continues to reach the active underlying application.
- Document any known hardware or driver constraints discovered during implementation.
