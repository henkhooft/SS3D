> Implements: Documents/design/round-end.md §2, §4–§6; Documents/design/antagonist-content.md §2, §6 (first pass, How the round ends); Documents/design/objectives.md §2, §4, §8 (team objective, assignment, spawn briefing); Documents/design/lobby.md §4, §7 (opt-in list, self-starting round)
> Touches systems: rounds-lobby, gamemodes-roles-traits, entities, persistence, chat-audio-screens, screen-effects, ui-shell
> Status: active

**Progress:** P1 implemented (round outcome, summary overlay and banner, 30 s beat with admin hold, Last Round tab, auto-start defaults, round history fields); P2–P3 not started. Advances [mvp1-nuke-ops.md](../milestones/mvp1-nuke-ops.md) M4 and M5 and [test-server.md](../milestones/test-server.md) T4.

# Round-end loop and thin Nuke Ops outcome (Nov 2026)

A round ends with a legible outcome, everyone sees a summary, and the server returns to the lobby and starts the next round on its own. A Nuke Ops round has a real squad and a real winner, crew win included. Scope was settled in an implementation grill on 2026-11-02; the gameplay rules are the cited design sections and the REVIEW_2026-10 decisions Q14, Q16, Q31 and Q32.

## Starting state (2026-11-02, `06c7e4045`)

- **Ending the round:** `RoundSubSystem.ProcessEndRound` sets `Ending`, posts "The round has ended." to OOC, calls `AppendRoundHistory`, waits 3 s, then `StopRound` despawns everyone and unloads the station. There is no outcome, no summary and no return to a fresh round unless `AutoStart` is on.
- **Self-starting rounds** exist as the `AutoStart` server setting (`RoundAutoStart`, 20 s countdown once at least one player is ready, admin hold/cancel), built for the sandbox ([2026-11_sandbox-server](2026-11_sandbox-server.md) Decision 2). Only `-sandbox` turns it on.
- **Round length** is enforced in `RoundSubSystem`'s tick with 5- and 1-minute warnings for every timed round.
- **Nuke:** `Nuke.Detonate()` (via `NukeDetonateInteraction`) calls `GamemodeSubSystem.EndRound()` instantly. No arming, countdown or disk check (that is M3).
- **Gamemode:** `NukeGamemode` is an empty `Gamemode` subclass. Legacy objectives (`DetonateNukeObjective`, `GetItemObjective`) go to every spawned player, complete on pickup, and `ObjectiveStatus` has four states.
- **Antagonist opt-ins** are stored per ckey (`RoleSubSystem.IsAntagonistOptedIn`) from a fixed list in `AntagonistCategories` that includes Traitor; nothing consumes them.
- **Roles:** `StartingRoles.asset` holds Assistant and Security. `RoleLoadout` spawns equipment and hand items. `EntitySubSystem.ResolveSpawnPosition` ignores job/operative markers (gamemodes map pitfall). Map validation already requires an operative marker (`AntagonistSpawnCategory.NuclearOperatives`), the nuke and the disk for Secret and Nuclear Emergency.
- **Round history:** `RoundHistoryEntry` stores timestamp, gamemode, map, player count, duration and a hardcoded `fallbackMap = true`.
- **Restraints** do not exist (only a "restrained" alert icon).

## Decisions

| # | Decision |
|---|---|
| 1 | Scope is M5 + thin M4. M3's nuke loop is out; the existing instant `Detonate()` stands in for "the device detonates" until M3 replaces it. |
| 2 | One **round outcome record**, fixed on the server at the instant a trigger fires (round-end §2): trigger (`GamemodeCondition`, `Timer`, `Admin`), winner (`None`, `Operatives`, `OperativesMinor`, `Crew`), resolved gamemode, antagonist roster with objective results, and every player who took a body (name, job, alive/dead). Nothing is computed after the trigger. |
| 3 | **Summary content, pass 1:** gamemode, winner, what ended the round, antagonist roster with objectives met/failed, everyone's name, job and alive/dead. The viewer's own row is highlighted. Cause of death is out. Detonation adds "Station destroyed"; fates still show state at the trigger instant. |
| 4 | **Everyone connected** sees the summary: living, ghost and lobby players. It is an overlay; the world keeps running and players keep control (SS13 end-of-round precedent). It can collapse to a banner. Nothing during the summary changes the fixed outcome. |
| 5 | The summary beat is a **fixed 30 s**; an admin can **hold** it (same wedge pattern as the countdown hold). After it, `StopRound` runs as today and everyone is in the lobby. |
| 6 | The lobby keeps a **"Last round" card** with the same summary until the next round ends, so latecomers can read it. |
| 7 | **`AutoStart` defaults on** for every dedicated server (round-end §6, lobby §7, Q16). Add a **minimum-ready** server setting, default 1. Readiness resets each round, as today. |
| 8 | **Admin end and Extended timeout** have winner `None`. An admin end in a Nuke Ops round resolves the §6 table **as a timeout**, so a stopped round still gets a result (owner decision; §6 only names the timer). |
| 9 | `RoundHistoryEntry` gains trigger, winner and operative ckeys; `fallbackMap` gets its real value. |
| 10 | **Squad size** = ceil(ready players / 4), capped at 5. The mode minimum is a server setting, default 1. |
| 11 | **The draw** (antagonist §2, Q31) runs once, at round-start resolution, before jobs are assigned: a plain random draw among ready players who opted into Nuclear Operative. Those players skip job resolution. Too few opt-ins, under Secret **or** an explicit Nuclear Emergency, runs the round as Extended and tells admins in Admin chat and the Server Settings status. Latejoiners are never drawn. |
| 12 | **Operative role, thin:** an `Operative` `RoleData` that never appears in the job list; spawns at the operative marker; a `RoleLoadout` from existing items (gun, spare magazines, an ID with operative access, an outfit distinct from any crew job). Pinpointer and disk loading are M3; sealed hardsuit and internals are M8. Crew spawning on job markers stays out. |
| 13 | **Objectives** follow Q32: only the squad holds one, a single shared "detonate the station's device" team objective (objectives §2); three states; resolved when the round ends. Legacy every-player objectives and pickup completion are removed. |
| 14 | **Briefing:** a personal feed message at spawn naming the role, the objective and the other squad members (objectives §8). The Escape-menu Notes panel is deferred. |
| 15 | The **lobby opt-in list** shows only antagonist roles of enabled modes (lobby §4, Q31): Nuclear Operative alone for now. |
| 16 | **Operative out** = body dead (health §4) or destroyed. SSD and ghosted-out bodies count as alive (Q35). **Deviates from antagonist-content.md §6** ("dead or restrained"): restraint is ignored until a restraint mechanic exists. |
| 17 | **Crew win** is checked whenever an operative's state changes and ends the round immediately (§6). |
| 18 | **Timeout disk check:** held = the disk is anywhere in the container tree on a *living* operative's body. Anything else, including a missing disk, is a crew win. "Loaded in the device" waits for M3. |
| 19 | **Detonation presentation:** a station-wide announcement plus a short white-out and screen shake for every client, then the summary. No `ResolveBlast` on the station. |
| 20 | Nuke Ops default round length is **30 minutes** (server setting). Gamemode-specific lengths stay deferred. |
| 21 | The gamemode-identity leak to clients (round-config §4 delta) stays parked; the summary shows the resolved mode regardless. |

## Shape

- **Outcome.** A plain serializable `RoundOutcome` built on the server by one entry point that every trigger calls (gamemode condition, timer, admin stop). Nuke Ops rules live in a **pure resolver** (`NukeOpsOutcomeRules` or similar) that takes squad states and disk location and returns the winner. `RoundSubSystem` owns the outcome's lifetime and sends it to clients when `Ending` begins and to anyone who connects while it is the last round.
- **Ending beat.** `ProcessEndRound` replaces its 3 s delay with the 30 s beat plus admin hold, then `StopRound` continues unchanged (map-editor autosave, despawn, unload). Admin stop requests during Ongoing go through the same outcome entry point with trigger `Admin`.
- **Summary UI** follows the UiShell view/subsystem split ([ui-shell](systems/ui-shell.md)): a round-end view on a layer above the HUD, collapsible to a banner, and a "Last round" card in `LobbyShellView`. Visuals come from the separate design pass.
- **Squad and roles** extend `RoleSubSystem.ResolveRoundStartRoles` with a draw step before `JobResolver`, and teach `EntitySubSystem.ResolveSpawnPosition` the operative marker. The draw itself is a pure function for EditMode tests.
- **Objectives** reshape `GamemodeSubSystem`/`Gamemode` around antagonist-only assignment and a resolve-at-end pass. Keep `GamemodeObjectivesView` on the `Networked View` prefab untouched unless it breaks.
- **Detonation** goes through `CommsSubSystem.SendAnnouncement` and a screen-effects pulse sent to every client.

## Phases

### P1 — Round-end loop (generic)

- `RoundOutcome` with triggers and winner `None`; detonation counts as `Operatives` (Decisions 2, 8).
- Summary overlay + banner, everyone connected, viewer row highlighted (Decisions 3, 4).
- 30 s beat with admin hold (Decision 5); "Last round" lobby card (Decision 6).
- `AutoStart` on by default, minimum-ready setting (Decision 7).
- Round history fields (Decision 9).
- Tests: EditMode for outcome construction per trigger and the beat/hold rules. `LobbyLifecyclePilot`: host + ghost + remote client see the summary (screenshots), everyone returns to the lobby, the second round starts from the ready countdown without an admin.
- Ships on its own: closes most of T4 without any Nuke Ops code.

### P2 — Squad

- Opt-in list from enabled modes (Decision 15).
- Draw, size, minimum and Extended fallback with admin notice (Decisions 10, 11).
- Operative role, marker spawn, loadout (Decision 12).
- Team objective replaces legacy objectives (Decision 13); spawn briefing (Decision 14).
- Tests: EditMode for the draw (size, opt-in only, fallback, latejoin excluded) and objective assignment. Pilot: an opted-in host is drawn, spawns at the operative marker with the kit and briefing; a non-opted remote client gets a job.

### P3 — Nuke Ops rules

- Pure resolver for the §6 table with Decisions 8, 16 and 18; EditMode tests for every row plus SSD, ghosted, nested-bag and missing-disk cases.
- Crew-win check on operative state change (Decision 17); timeout and admin-end resolution.
- Detonation announcement, white-out and shake (Decision 19); 30 min default length (Decision 20).
- Pilot: an ops-win scenario (detonation) and a crew-win scenario (squad killed), each followed by the automatic next round.
- **Acceptance:** CI Multiplayer Smoke Test green on the branch (the TomNAS runner is back online as of 2026-11-02).

## Parked

- M3: disk loading, arming, countdown, defuse, pinpointer, arm announcement, indestructible disk.
- M8: sealed hardsuit and internals in the operative kit.
- Cause of death on the summary; objectives Notes panel; restraints (then lift Decision 16's deviation).
- Gamemode-identity leak; crew spawning on job markers; gamemode-specific round length.
- Evacuation trigger and escape roster (round-end §3, MVP2).

## Implementation notes

**P1 (2026-11-02).** Code: `Systems/Rounds/RoundEnd.cs`, `Systems/Rounds/Outcome/`, `UI/Lobby/RoundEnd/`; map: [rounds-lobby](systems/rounds-lobby.md). Divergences from the decisions above and the design pass:

- **Last round lives in a lobby tab, not a sidebar card** (Decision 6). The design's card sits in a wide left sidebar; the real lobby's sidebar is the 280 px character preview column. The Last Round tab (after Server Info) renders the same ledger as the overlay. The shell opens it once per ended round, but not while the player is on Server Settings or Chat.
- **Lobby players read the summary in that tab during the beat.** The overlay sits on the shell's Overlay layer, below the lobby window (Modal), so only players with a body or ghost see the overlay itself (Decision 4).
- **Dedicated means `NetworkType.DedicatedServer`.** A listen host (editor, main-menu Host) keeps `AutoStart` off. The admin form gained an Auto Start toggle and a Minimum Ready field (Decision 7).
- **Admin hold** is a Hold Summary / Resume Summary pair in Server Settings → Round Controls, posted to Admin chat (Decision 5).
- **The outcome reaches clients through a buffered `ObserversRpc`**, so a client connecting later still gets the last round. `fallbackMap` is written from a flag that P2's Extended fallback sets.
- **Tab** is a code-defined `ToggleRoundSummary` system action, live in the Global, Gameplay, TileMenu and MapEditor contexts ([inputs](systems/inputs.md)).
