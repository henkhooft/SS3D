# Round End & Transition — design document

> Status: active

Closes the gap flagged in `observer.md` §7 and §9: that doc supplies the camera and
spectator-state framework a finished round drops players into, but explicitly leaves the summary
screen and the transition back to a new round to "the next doc." This is that doc. It also spends
the secrecy `round-config.md` §4 spent the whole round protecting — the resolved gamemode identity
that stayed server-side is finally disclosed here, at the one moment revealing it costs nothing.
This pass also closes a second, more specific gap: `shuttles.md` §9 built the evacuation shuttle as
a real physical object but explicitly left its call, countdown, and point-of-no-return to this doc;
`objectives.md` §7's escape objective has been waiting on that same moment to check against. §3 is
that mechanism.

## 1. Design philosophy

**A round needs a legible ending, and the ending should read off real played facts.** The same
thread every other doc in this project pulls on — failure and outcome trace to something concrete
and visible, not an invisible roll — applies to the round as a whole. A round doesn't end in an
abstract team score; it ends in a ledger of things that actually happened: which objectives were
met, who died and how, whether the antagonist got away. The summary screen reports that ledger; it
does not compute a hidden rating.

**Reuse, don't reinvent, three times over.** Like `death-cloning-respawn.md` §1, most of this doc
is other systems already doing their jobs:

- **The spectator framework is observer's.** A player at round end is in exactly the detached
  camera/state observer already defines (`observer.md` §7) — round end doesn't build a second
  spectator mode, it drops everyone into the existing one and paints a summary over it.
- **The gamemode identity is round-config's.** The secret that stayed server-side all round
  (`round-config.md` §4) is the exact thing the summary reveals. Round end is where that secrecy is
  deliberately spent, not leaked — the reveal is a designed moment, the payoff for keeping it hidden.
- **The objective outcomes are the gamemode's.** Whether an antagonist's objectives were met is a
  fact the gamemode already tracks during play; round end reads and displays it, it does not
  re-adjudicate anything.

**Evacuation is a real, playable sequence, not an instant proxy for "round over."** The other three
triggers (§2) fire once and resolve instantly. Calling the shuttle is different in kind: it has its
own duration, its own physical risk (`shuttles.md` §6), and its own drama — the same reason a
shuttle got a real transform and real collision instead of an instant teleport in the first place
(`shuttles.md` §1). Round end doesn't shortcut that sequence just because it's the thing that
eventually ends the round (§3).

**The transition is a return to a known state, not a new subsystem.** Ending a round hands control
back to the pre-round flow `lobby.md` §7 and `round-config.md` §3 already define — draw the next
config, reset readiness, start again. Round end owns the seam, not a parallel lifecycle.

## 2. What ends a round

A round ends when one canonical thing becomes true, mirroring the single-trigger discipline
`health.md` §4 uses for death and `death-cloning-respawn.md` §2 uses for permadeath:

- **The active gamemode's end condition is met** — its win/lose/objective-resolution rule fires.
  This is the primary, designed ending; the specific condition belongs to each gamemode, not here.
- **A hard round timer elapses**, where a gamemode defines one — a bounded fallback so a stalled
  round still concludes.
- **An admin ends it** — an out-of-band manual end, always available, invoked through `admin-tools.md` §6.
- **The evacuation shuttle completes its journey** — arrives at its rendezvous, or is lost en route
  — the common path for any gamemode that resolves through evacuating rather than a bespoke win
  check. §3 defines the full call/countdown/arrival sequence this trigger actually runs on.

The first three fire once, from an instantaneous check. The fourth is deliberately the odd one out
— a real sequence with its own duration, not a snapshot. Whichever ultimately fires, the outcome is
fixed at that instant from the facts already on record (objectives, deaths, antagonist status).
Nothing is rolled after the trigger.

## 3. The evacuation shuttle — call, countdown, point of no return

The direct answer to the dependency both `shuttles.md` §9 and `objectives.md` §7 flag against this
doc: shuttles supplies the physical vehicle with real dock/undock/transit/collision behavior
(`shuttles.md` §2–§6); this doc is what actually calls it, counts down to its departure, and defines
the instant nothing more can be done about who made it aboard.

**Calling the shuttle is a physical interaction, not a menu command.** A call console — a diegetic
device screen in the same family as every other console in this project (`hacking-interface.md`
§2) — sited at a fixed station location. Any crew member can interact with it to call the shuttle;
per `shuttles.md` §5's own precedent for the evac shuttle's open, non-access-gated helm, gating the
call itself would just relocate the same problem one step earlier, so this doc keeps the call
symmetric with the helm: no credential required. **Flagged as a recommendation, not locked** — same
status as `shuttles.md` §5's own momentum-model call or `combat.md` §3's hitscan-vs-projectile call,
worth confirming before implementation-locked.

**A visible countdown starts on call** — a real timer, shown at the console and as an alert-stack
entry (the same hidden-until-relevant precedent `shuttles.md` §8 already sets), not a hidden clock.
**Recall is available for the length of the countdown** — a second interaction at the same console,
cancelling it and returning to normal round state, available to any crew member exactly as calling
was, plus an admin override at any time (§2).

**The countdown reaching zero is the point of no return.** Two things happen atomically at that
instant:

- The shuttle undocks and autopilot engages, per `shuttles.md` §3's clamp/seal model run in reverse
  and §4's automated piloting.
- **Whoever is physically within the shuttle's Area set at that exact tick is locked in as aboard,
  for good.** Recall is no longer possible; a player not aboard at this instant has missed it, full
  stop, no grace period. This is the precise moment `objectives.md` §7's escape check reads
  occupancy against — not because presence is re-verified again later, but because there's no
  ordinary way to leave a shuttle's Area mid-flight once it's undocked and moving through open
  space, so the roster locked in here is definitionally the same roster still aboard whenever the
  shuttle actually arrives.

**The shuttle then transits for real** — `shuttles.md` §4's automated piloting, §6's real collision,
and §9's own worked example of a hijacked, manually-flown, potentially crashed evac run all apply
exactly as that doc already specifies. Round end doesn't end at the point of no return; it ends
when the shuttle's journey actually concludes.

**Arrival (or loss) is what fires §2's "evacuation completes" trigger.** The shuttle reaching its
rendezvous point, or being destroyed en route, is that trigger. Whoever's still alive and still
aboard at that moment is who `objectives.md` §7's escape objective resolves true for; anyone who
died during transit — a hijacker's crash, sabotage along the way — simply isn't alive at that
check, the same as anywhere else in this project death is checked (`health.md` §4).

## 4. The reveal

The resolved gamemode identity and the antagonist roster — kept server-side and non-spoiling all
round per `round-config.md` §4 — are disclosed at round end. This is the deliberate spend of the
round's secrecy: the coarse, non-spoiling category players saw in the lobby (`lobby.md` §4) resolves
into the full truth of what the round actually was, who the antagonists were, and what they were
trying to do. Revealing it earlier would break the secrecy the whole config system exists to
protect; revealing it now is the reward for it having held.

## 5. The summary screen

Painted over the observer spectator state (`observer.md` §7), the summary reports the round's ledger:

- **Gamemode, revealed** — the true gamemode identity from §4.
- **Objective outcomes** — per antagonist (or team), each objective shown met or failed, as a real
  read of tracked state, not a computed grade. Same "real values, visible failure" discipline the
  fabricator and hacking screens follow. An antagonist's escape objective (`objectives.md` §7) reads
  from exactly the moment §3 defines.
- **Notable fates** — who died and the cause, drawn from the same wound/organ state a corpse already
  carries (`death-cloning-respawn.md` §2); who survived; who was cloned back (`death-cloning-respawn.md`
  §5); who missed the evac shuttle, or was lost aboard it (§3). The player's own fate is legible
  without spoilers about others beyond what the reveal grants.

The screen is diegetic-adjacent but sits in the same deferred-UI carve-out the observer experience
does (`main-hud.md` §13) — it is a summary surface, not a redesign of the HUD.

## 6. The transition

From the ended state, control returns to the pre-round flow:

- The next round's gamemode and map are drawn per `round-config.md` §3 (draw, gate, fallback), with
  the same secrecy re-established for the new round (`round-config.md` §4).
- Player readiness and job preferences reset into the lobby's resolution pass (`lobby.md` §5, §7).
- Players leave the summary/observer state and re-enter the lobby for the next round.

The transition is a seam between two states both already designed elsewhere; this doc only defines
that the seam exists and in what order it runs.

## 7. Worked examples

**Antagonist wins on objectives.** The gamemode's end condition fires when its antagonist completes
their last objective. Outcome fixes at that instant. Everyone drops into observer spectator state;
the summary reveals the gamemode and the antagonist, shows all objectives met, lists who died. After
a beat, the transition draws the next round's config and returns players to the lobby.

**Timer elapses with objectives unmet.** No side met its condition before the round timer ran out.
The outcome fixes on the tracked state: objectives shown failed, survivors listed, the gamemode and
antagonists still revealed (secrecy is spent regardless of who "won"). Transition proceeds identically.

**Admin ends a broken round.** An admin ends the round manually. The same reveal and summary run off
whatever state exists at that moment — a real read, even of an incomplete round — and the transition
returns to the lobby.

**Evacuation shuttle called, hijacked, and crashed:**

| Step | What happens | Round state |
|---|---|---|
| 1 | A crew member interacts with the call console | Countdown starts, visible at the console and on the alert stack |
| 2 | Most of the crew boards before the countdown reaches zero; two stragglers don't make it | Countdown expires — point of no return: shuttle undocks, autopilot engages, the boarded roster locks in |
| 3 | An antagonist aboard takes manual control at the open helm and rams the shuttle into a station structure (`shuttles.md` §9) | Real collision resolves; the struck Area breaches; several aboard take impact damage |
| 4 | The antagonist is removed from the helm; autopilot re-engages and resumes toward the rendezvous | Shuttle, damaged but flying, continues its real transit |
| 5 | Shuttle reaches the rendezvous point | This doc's "evacuation completes" trigger (§2) fires; outcome fixes from whoever's alive and aboard at this instant |
| 6 | Round end resolves | Escape objectives (`objectives.md` §7) read true for survivors still aboard, false for the two who missed the countdown; the crash and both stragglers' fates appear in the summary's Notable fates (§5) |

## 8. Integration notes

| Round end leans on | Which does the work |
|---|---|
| Spectator camera/state at round end | `observer.md` §7 (the framework this builds its screen over) |
| Secret gamemode identity, now revealed | `round-config.md` §4 (secrecy) + §3 (next-round draw) |
| Objective tracking | The active gamemode's own objective state (read, not re-adjudicated) |
| Death/clone fates in the summary | `death-cloning-respawn.md` §2, §5 |
| Return to pre-round flow | `lobby.md` §5, §7 |
| Admin manual end | `admin-tools.md` §6 (invocation point; this doc defines the trigger's effect) |
| Evac call console | New — diegetic device-screen pattern (`hacking-interface.md` §2) |
| Evac shuttle physical behavior | `shuttles.md` §2–§6, §9 |
| Escape objective's resolution moment | `objectives.md` §7 (this doc supplies the instant its check reads) |

## 9. Out of scope for this pass

- **Per-gamemode end conditions.** This doc defines that a gamemode ends a round and gets revealed;
  each gamemode's specific win/lose rule and objective set belong to that gamemode, not here.
- **Scoring, MMR, or player-progression rewards.** The summary is a ledger of what happened, not a
  rating; any persistent scoring is a separate, later concern.
- **Full observer/ghost experience.** Unchanged from `observer.md` §9 and `main-hud.md` §13 — this
  doc supplies only the summary and transition, not the spectator UI itself.
- **Antagonist content and objective design.** What antagonists exist is designed in
  `antagonist-content.md`; what they pursue is `objectives.md`'s job. Round end only reports outcomes.
- **Exact countdown duration and the call console's access policy.** §3's no-access-gate call is a
  recommendation, not locked — a balancing/confirmation pass, not a design decision.
- **Multiple simultaneous evac vehicles, or a gamemode-specific evac variant** (a hostile boarding
  party contesting the shuttle, say) — a plausible future extension of §3's shape, not designed here.
