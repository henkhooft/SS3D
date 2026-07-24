# Playable milestones

Hub for **what to focus on next** and the **dependency trees** that unlock a playable gate.
Conventions: [Documents/SKILL.md](../SKILL.md) (Milestones section). Domain coverage (designed
vs built) stays in [architecture/INDEX.md](../architecture/INDEX.md); structural risk in
[TECH_DEBT.md](../architecture/TECH_DEBT.md).

## Current focus

Two tracks run in parallel — an **operational** one that makes multiplayer playtests trustworthy, and the **gameplay** gate:

**Near-term operational push** — [test-server.md](test-server.md):

- **T1** — Loadable Windows build (Addressables/content/permissions — diagnose a real build)
- **T2** — Zero-setup remote join (stop hardcoding `127.0.0.1`)
- **T3** — Session stability: flawless round start/stop, connect/disconnect, graceful server + client handling, no gross host/client sync gaps (includes known remote drop/selection break)

**Active gameplay gate** — [mvp1-nuke-ops.md](mvp1-nuke-ops.md) (M1 combat foundation + M2 structural/blast API shipped):

- **M0** — Basic editor-built test station; vault/disk; per-role loadouts + assignment
- **M1p** — Combat feel: weapon VFX/decals/blood/sounds, aim IK, two-hand rules, no hit-debug UI in normal play
- **M3** — Nuke loop + disk pinpoint + breaching charges / realistic-ish explosions
- **M4** — Objectives + win/lose checking
- **M5** — End-game screen + round restart (**shared with T4**)
- **M7** — Health feel: ragdoll on crit; screen effects that composite cleanly
- **M8** — Atmos→health (spacing/fire/temp) + armor environmental seal + air alarms

Not focus: Area-id merge on breach, shuttle-as-ops-gate, full PDA/uplink/Traitor, cloning/respawn, evac shuttle, server browser / accounts.

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
