# Playable milestones

Hub for **what to focus on next** and the **dependency trees** that unlock a playable gate.
Conventions: [Documents/SKILL.md](../SKILL.md) (Milestones section). Domain coverage (designed
vs built) stays in [architecture/INDEX.md](../architecture/INDEX.md); structural risk in
[TECH_DEBT.md](../architecture/TECH_DEBT.md).

## Current focus

Deepest unfinished critical leaves on the active gate ([mvp1-nuke-ops.md](mvp1-nuke-ops.md)):

- **M1** — Combat ranged + dedicated weapons (+ thin armor) — resolve hitscan-vs-projectile first
- **M2** — Station structural damage model — scoped to route-opening (no live area/atmos recompute)
- **M5** — Round-resolution spine: death→spectator + round-end summary/reveal (death detach already wired via the ghost body; the summary/reveal is net-new and was previously hidden inside the gamemode slice)

Not focus: lobby UITK redesign, shuttle-as-ops-gate, full PDA/uplink/Traitor, cloning/respawn, evac shuttle, breach-driven atmos/area consequences.

## Gates

| Gate | Status | Depends on | Doc |
|------|--------|------------|-----|
| MVP1 — Nuclear Operatives round | planned | — | [mvp1-nuke-ops.md](mvp1-nuke-ops.md) |
| MVP2 — Bare-bones station round | planned | MVP1 | [mvp2-station-round.md](mvp2-station-round.md) |

## How to use

1. Open this hub for current focus.
2. Open the active gate doc for the dependency tree and slice status.
3. Follow leaf links into design §§, architecture efforts, or plans for HOW — do not restate them here.
4. On ship, run `update-system-docs` so slice status and this hub’s current focus stay accurate.
