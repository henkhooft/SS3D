# Playable milestones

Hub for **what to focus on next** and the **dependency trees** that unlock a playable gate.
Conventions: [Documents/SKILL.md](../SKILL.md) (Milestones section). Domain coverage (designed
vs built) stays in [architecture/INDEX.md](../architecture/INDEX.md); structural risk in
[TECH_DEBT.md](../architecture/TECH_DEBT.md).

## Current focus

Two tracks run in parallel — an **operational** one that makes multiplayer playtests trustworthy, and the **gameplay** gate:

**Near-term operational push** — [test-server.md](test-server.md):

- **T1** — Loadable Windows build (Addressables/content/permissions)
- **T2** — Zero-setup remote join (stop hardcoding `127.0.0.1`)
- **T3** — Session stability: round/connect lifecycle, doors under MP, admin wedge tools, Nuke Ops happy-path smoke, test-station perf floor
- **T4** — End-game screen + restart + latejoin without softlock (**shared with M5**)

**Active gameplay gate** — [mvp1-nuke-ops.md](mvp1-nuke-ops.md) (M1 combat + M2 structural/blast API + client atmos VFX Phase 1 shipped):

- **M0** — Test station; vault/disk/nuke; loadouts (ammo, ID/access, defuse tools, internals); assignment; ops/crew identity
- **M1p** — Combat feel: VFX/decals/blood/sounds, aim IK, two-hand, reload cues, knockdown clarity, sprint, no debug chrome
- **M3** — Nuke loop + disk pinpoint + breaching + arm announce + examine state
- **M4** — Objectives + win/lose
- **M5** — End-game screen + restart; spectators still see the outcome
- **M7** — Health feel (crit ragdoll, screen-effect compositing) + thin field med + drag
- **M8** — Env→health + armor seal + internals + air alarms that alarm + thin fire response
- **M9** — Test-map APC lights matter + wall damage readable at range

Not focus: Area-id merge on breach, re-wiring client atmos VFX (already shipped), shuttle-as-ops-gate, full PDA/uplink/Traitor, cloning/respawn, evac shuttle, full radio, server browser / accounts.

The **lobby UITK redesign** ([lobby.md](../design/lobby.md)) remains a parallel capacity track; test server only needs a minimal join/spawn path.

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
