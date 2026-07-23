> Code paths: none (obsolete runtime purged)
> Entry points: none — redesign not started
> Status: stub
> Verified: 97254ee1d — 2026-07-23

# Crafting

## Overview

Obsolete recipe-crafting runtime, hand `Craft` extension, uGUI menu, recipe assets, and
`CraftingSubSystem` hub registration were **purged** (TECH_DEBT 1.6). Do not reintroduce those
types. Future freeform crafting follows the design spec only — no implementation yet.

## Start here

- Design (read-only): [Documents/design/crafting.md](../../design/crafting.md) — intended direction
- Debt close: [TECH_DEBT.md](../TECH_DEBT.md) §6 Resolved / former §1.6

## Extension points

None until a redesign effort lands. Prefer a new architecture effort + Phase 0 from design over
reviving deleted recipe graphs or uGUI menus.

## Pitfalls

*(none for live code — system has no runtime surface)*

## Depends on / Used by

- **Depends on:** none (code removed)
- **Used by:** design-only citations from other design docs; no runtime consumers

## Related docs

- Design (read-only): [Documents/design/crafting.md](../../design/crafting.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md) (crafting menu row: purged)
- [INDEX.md](../INDEX.md)
