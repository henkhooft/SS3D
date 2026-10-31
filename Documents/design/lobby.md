# Lobby & Role Selection — design document

> Status: active

The first server-side system in this pass, and a genuinely different kind of design problem from everything before it. Every prior doc in this project tested itself against "does the 3D world carry this better than a panel" — but there's no world yet here. No character, no station, nothing to be diegetic about. This doc gets the same honest carve-out already used for OOC (`comms.md` §7) and the respawn timer (`death-cloning-respawn.md` §6): it's explicitly a session/meta screen, not an in-fiction one.

## 1. Design philosophy

**The test here isn't diegetic-vs-panel, it's native-vs-leftover.** SS13's job-select screen is literally rendered through BYOND's embedded HTML browser output — real tables, real hover tooltips, an actual webpage sitting inside the game window. That's the artifact this doc cuts. It's not a rendering-fidelity problem the way the targeting doll was; it's that the engine's only menu tool happened to be a browser, so every menu ended up looking like one. The fix is simple: same visual system as the rest of this project (flat surfaces, hairline borders, muted status colors, no neon) applied to a full-screen menu, so it reads as part of the same game instead of a tool embedded inside it.

**The mechanical decision: preferences, not a race.** SS13 has shipped both first-come-first-served job locking and preference resolution across different codebases. This doc adopts preferences deliberately, for the same reason chording replaced intent-cycling in the main HUD doc (§7) — a live scramble rewards reflexes and being logged in at exactly the right second, not an informed decision. Here, every player builds a preference list at their own pace, and the whole list resolves in one fair pass when the round actually starts. Nobody loses a role because someone else's click landed a frame earlier.

## 2. Job list

Grouped by department (Command, Security, Engineering, Medical, Science, Cargo, Service, Civilian) — pure information architecture, not engine-dependent, so it's kept as-is. Each entry shows the job name, department color (muted, matching the existing status-color palette), and a slot count (e.g. "Engineer — 2/4", "Captain — 0/1"). The access each job grants comes from id/access's job→access table (`id-access.md` §5) — lobby reads it, it doesn't author it.

**Locked jobs stay visible, not hidden.** A playtime-gated job (e.g. "Chief Engineer — requires 10 hours as Engineer") shows grayed out with the real requirement stated, rather than disappearing from the list entirely — the same "real, visible reason" rule armor's absorption values and the hacking interface's failure states already follow. A new player should be able to see the whole game's shape, including the parts they can't access yet, not discover job tiers exist by their absence.

**Short description on hover/expand** for any job whose name alone doesn't explain the role. This is the same trap this project has flagged twice already — comms' typed radio prefixes (§2) and crafting's freeform recipes (§1) — applied to job names: nothing here should be effectively wiki-only knowledge for a new player.

A **Server Info** tab carries the message of the day, the server's name, the build the server runs and its recent changes, and the player count — plain information, so a player knows where they are and what changed since last time.

## 3. Job preferences

**Each job gets a priority: High, Medium, Low or Never.** A player sets it by clicking the job's priority chip in the department browser (§2). Only one job can be High at a time — setting a new High moves the old one to Medium. This is SS13's familiar scheme, and it's quicker to set than ordering a long list. The cost is ties within Medium and Low, which resolution breaks fairly (§5).

**Live demand, not hidden math.** Each job in the list (and in the browser) shows how many players currently have it somewhere in their top preferences relative to its slot count — e.g. a small "12 interested / 4 slots" indicator. This is real, visible information a player can act on (deprioritize an oversubscribed role, or accept the odds) rather than a black box they're guessing against. Same "give real info instead of a hidden mechanism" instinct that's shown up everywhere else in this project, just applied to social/demand data instead of physical state.

**"No preference" is a real, explicit option** — a player can leave every job at Never and be assigned wherever needed, rather than being forced to rate jobs they don't care about just to have a valid submission.

Preferences are editable at any point up until resolution (§5) — there's no lock-in moment before round start, so changing your mind costs nothing. They're **kept for the whole server session**: a player who stays connected between rounds doesn't set them again.

## 4. Special/antagonist role opt-in

A separate section, visually distinct from the job list — this is an eligibility toggle, not a job, and conflating the two would misrepresent what it does.

**The list shows the antagonist roles of every gamemode the server has enabled** (`round-config.md` §3) — never a role from a mode that can't run, and never a hint of which mode will. A player opts in or out of each, with a short description of what that role means: enough context that a new player isn't clicking blind, not so much that it spoils the round.

**Opt-ins are set before the round's mode is chosen.** The mode is drawn as the countdown ends, and only players who opted into that mode's roles are considered (`antagonist-content.md` §2). Nobody gets a second chance to opt in after the draw — that would reveal it.

## 5. Resolution — one pass, not a scramble

At round start — countdown expiry, or an admin forcing the start (`admin-tools.md` §6) — every player's preferences resolve server-side in a single allocation pass: try each player's High job, then their Medium jobs, then Low, respecting slot caps. Ties within a priority are broken fairly, not by who connected first.

**A guaranteed fallback role** (Assistant/Civilian-equivalent) exists specifically so nobody comes out of resolution unable to join at all — the one hard guarantee the algorithm has to honor.

The exact fairness/weighting rule inside that pass — random tiebreak, seniority weighting, anything more sophisticated — is a balancing decision, not designed here. What this doc commits to is the *shape*: preferences in, one fair resolution pass, guaranteed fallback out.

## 6. Identity

A **character creator** in the lobby: name and appearance, previewed on the character itself. The character is saved with the player's preferences, so a returning player starts as who they were last time. Loadout choice (what you spawn carrying) and non-human species are later passes.

## 7. Round start & latejoin

**The round starts itself.** A countdown begins once enough players have readied up, and starts the round when it reaches zero. Admins can **hold** the countdown (waiting for someone) or **force** an immediate start (`admin-tools.md` §6). No operator has to be present for a round to begin — after a round ends, players return here and the loop runs again (`round-end.md` §6).

**Latejoin reuses the same priority chips**, filtered live to whatever's currently open, and the player joins as soon as they confirm. This is now genuinely first-come-first-served, and that's fine — a latejoiner is joining a world already in motion, not competing at a shared starting line, so the race concern §1 raised doesn't apply here. An admin can turn latejoin off for a round.

**Observing is always available** as an entry point — a player can choose not to take a body at all and enter the round as a ghost (`observer.md` §2).

## 8. HUD/visual notes

This screen doesn't inherit main HUD's minimal-permanent-chrome rule, and that's a deliberate distinction, not an oversight — that rule exists specifically to keep panels from competing with a 3D world that's actually there to look at. Here there's no world yet, so a dense, everything-visible-at-once screen (job list, preference builder, antag opt-in, timer) is the right call rather than something to collapse or hide. What carries over is the visual *language* — flat surfaces, hairline borders, muted status colors — not the restraint.

## 9. Integration notes

| Element | Touches existing / needed system |
|---|---|
| Job unlock/playtime gating | Player meta layer (`persistence-save.md` §5) |
| Antagonist roles on offer | Enabled gamemodes — `round-config.md` §3 |
| Hold / force start, latejoin toggle | `admin-tools.md` §6 |
| Observe entry | The ghost — `observer.md` §2 |
| Return after a round | `round-end.md` §6 |
| Spawn resolution | Spawn points placed in the map editor (`creative-mode.md` §2) |

## 10. Worked examples

**A — Standard round start:**

| Step | What happens | Lobby state |
|---|---|---|
| 1 | Player sets Engineer to High, Atmospheric Technician and Cargo Technician to Medium | Engineer reads "9 interested / 4 slots" |
| 2 | Player reconsiders given the demand, makes Atmospheric Technician High | Engineer drops to Medium automatically — no cost to changing their mind |
| 3 | Player opts into Nuclear Operative | Antag section shows the opt-in as active, separate from the job list |
| 4 | Enough players ready up; countdown reaches zero | The mode is drawn, then resolution runs once, server-side |
| 5 | Atmospheric Technician had room | Player is assigned their High job |

**B — Latejoin:**

| Step | What happens | Lobby state |
|---|---|---|
| 1 | Round has been running for 20 minutes; a new player connects | Same job chips, filtered to currently-open slots only |
| 2 | Only two jobs show open | Player picks one and confirms — no waiting for a resolution pass |
| 3 | Selection resolves immediately | Player spawns in right away, consistent with joining a world already in motion |

## 11. Out of scope for this pass

- Round configuration & gamemode selection itself (`round-config.md`)
- The admin toolkit itself, beyond hold/force start — designed in `admin-tools.md` §6
- Loadout choice and non-human species in the character creator
- The exact fairness/weighting algorithm inside preference resolution (a balancing/backend decision, not a design one)
- Real authentication underlying a stable per-player key — designed in `player-accounts.md` (`persistence-save.md` §5 treats it as a black box the same way this doc's job-list data is read, not authored)
- What a ghost can do (`observer.md`) — this doc only adds the entry point

