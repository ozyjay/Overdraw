# overdraw

`overdraw` is a Windows-first Python tool for drawing over the screen without blocking normal desktop interaction. The goal is to let a pen display user annotate freely across the entire desktop while continuing to click, type, edit, and navigate the underlying applications as normal.

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

## Planned Project Shape
- `AGENTS.md`: Codex operating guide for the repo
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

## Status
This repository currently contains project scaffolding and implementation guidance only. Runtime code has not been started yet.
