# Playable milestones

Hub for **what to focus on next** and the **dependency trees** that unlock a playable gate.
Conventions: [Documents/SKILL.md](../SKILL.md) (Milestones section). Domain coverage (designed
vs built) stays in [architecture/INDEX.md](../architecture/INDEX.md); structural risk in
[TECH_DEBT.md](../architecture/TECH_DEBT.md).

## Current focus

Two tracks run in parallel — an **operational** one that makes the game testable by others, and the **gameplay** gate:

**Near-term operational push** — the hop-on/off test server ([test-server.md](test-server.md)), so a link + download is all a playtester needs:

- **T1** — Loadable Windows build: it runs but is missing critical files/permissions (likely unpacked Addressables content) — diagnose against a real build
- **T2** — Zero-setup remote join: launch bats hardcode `127.0.0.1`; a public build must default to the hosted server

**Active gameplay gate** — deepest unfinished critical leaves on [mvp1-nuke-ops.md](mvp1-nuke-ops.md):

- **M1** — Combat ranged + dedicated weapons (+ thin armor) — ranged hitscan slice shipped; thin armor still open
- **M2** — Station structural damage model — scoped to route-opening (no live area/atmos recompute)
- **M5** — Round-resolution spine: death→spectator + round-end summary/reveal (death detach already wired via the ghost body; the summary/reveal is net-new). **Shared with the test server's T4 round-end loop** — the same spine unlocks both.

The **lobby UITK redesign** ([lobby.md](../design/lobby.md)) is now implementation-ready per the owner (UI direction settled) — a parallel track to commission when capacity allows; a minimal join/spawn path is the only hard dependency the test server has on it.

Not focus: shuttle-as-ops-gate, full PDA/uplink/Traitor, cloning/respawn, evac shuttle, breach-driven atmos/area consequences, server browser / accounts.

## Gates

| Gate | Status | Depends on | Doc |
|------|--------|------------|-----|
| Test server — hop-on/off playtest | planned | — (parallel; reuses MVP1 M5 loop) | [test-server.md](test-server.md) |
| MVP1 — Nuclear Operatives round | planned | — | [mvp1-nuke-ops.md](mvp1-nuke-ops.md) |
| MVP2 — Bare-bones station round | planned | MVP1 | [mvp2-station-round.md](mvp2-station-round.md) |

## How to use

1. Open this hub for current focus.
2. Open the active gate doc for the dependency tree and slice status.
3. Follow leaf links into design §§, architecture efforts, or plans for HOW — do not restate them here.
4. On ship, run `update-system-docs` so slice status and this hub’s current focus stay accurate.
