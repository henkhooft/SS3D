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

**Active gameplay gate** — deepest unfinished critical leaves on [mvp1-nuke-ops.md](mvp1-nuke-ops.md) (M1 combat + M2 structural/blast API shipped):

- **M0** — Vault + disk on map, runtime antag/job spawn pick, ops gear at spawn (`RoleLoadout`)
- **M3** — Nuke device loop (disk load → arm / countdown / defuse / detonate); optional breaching charges on the existing blast API
- **M4** — Thin Nuke Ops gamemode (assignment, on-station syndie spawn, win/lose)
- **M5** — Round-end summary/reveal over the existing death→ghost spectator. **Shared with test-server T4**

Not focus: re-opening structural damage as a foundation, Area-id merge on breach, shuttle-as-ops-gate, full PDA/uplink/Traitor, cloning/respawn, evac shuttle, server browser / accounts.

The **lobby UITK redesign** ([lobby.md](../design/lobby.md)) is implementation-ready per the owner — a parallel track when capacity allows; a minimal join/spawn path is the only hard dependency the test server has on it.

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
