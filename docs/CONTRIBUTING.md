# Contributing

## Purpose
This project starts as a Windows-first prototype for a difficult desktop interaction problem. Contributions should prioritize correctness, reproducibility, and clear documentation over feature breadth.

## Development Expectations
- Keep the interaction contract front and center:
  visible overlay, pen-driven drawing, mouse/keyboard pass-through, and no accidental focus capture.
- Isolate Windows-specific behavior behind clear interfaces.
- Prefer small, testable increments over large speculative rewrites.
- Update the docs when architecture decisions or scope assumptions change.

## Code Style
- Use Python type hints for new modules.
- Prefer clear module boundaries over deep inheritance or premature abstractions.
- Keep dependencies minimal and justify additions in the relevant task or architecture notes.
- Add comments only for non-obvious behavior, especially around Windows APIs and hardware quirks.

## Workflow
- Start risky work with a small feasibility spike when the behavior is uncertain.
- Record important findings in `docs/ARCHITECTURE.md` or `docs/TASKS.md`.
- Keep changes aligned with the structure and guardrails in `AGENTS.md`.
- When adding runtime code later, include at least basic verification notes for how it was tested.

## Testing
- Add automated tests for logic that does not require the live Windows overlay or attached pen hardware.
- Add or update manual test notes for any change that affects input routing, drawing behavior, rendering, or focus.
- Call out gaps where behavior still depends on manual XP-Pen verification.

## Documentation Maintenance
- `AGENTS.md` is the top-level Codex instruction file and should stay aligned with the actual repo direction.
- `README.md` should reflect the current user-facing project intent.
- `docs/PRD.md`, `docs/ARCHITECTURE.md`, `docs/TASKS.md`, and `docs/TESTING.md` should be updated when the team learns something that changes implementation direction or acceptance criteria.
