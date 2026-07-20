# Admin Tools — design document

> Status: active

Resolves a gap six other docs have all been carving out space for without ever designing: `pda.md` §3/§10 guarantees an ahelp tab-shaped slot but defers "the ticketing system itself" here; `lobby.md` §5/§11 defers "an admin/host override" of resolution; `objectives.md` §12 defers "admin override of an objective's state"; `observer.md` §9 defers "admin/staff observer tooling (godmode, jump-to-player, stealth observation)"; `round-end.md` §2 already specifies "an admin ends it" as always available but not how; `persistence-save.md` §5/§6/§11 defers the moderation workflow and the recovery-override surface. `round-config.md` §10 draws the boundary explicitly: "the full admin toolkit — ahelp, player management, audit logging, stealth observation... a separate pass; this doc only needs a config screen, not the whole toolkit." This doc is that toolkit.

## 1. Design philosophy

**This is explicitly out-of-fiction, like two other surfaces already are.** Lobby's job browser and creative mode's construction menu each earned a carve-out from "everything maps to something physical" because they're meta/administrative contexts, not in-fiction gameplay (`lobby.md` §1, `creative-mode.md` §1). Admin tools are the third. An admin toggling invulnerability or teleporting to a player doesn't need a diegetic justification any more than a lobby dropdown does — confining this exemption to these three surfaces is what keeps it from spreading into ordinary play.

**Detectable, not prevented — turned toward the admins themselves.** Every other doc in this project gives crew real, inspectable evidence instead of a hidden mechanism: id/access's per-device auth log, the AI's law audit trail, round config's draw history, persistence's round-history log. This doc runs the identical discipline on a different population. Every admin action that would otherwise be invisible — a kick, a teleport, a spawned item, a forced round start — writes a real, timestamped line to this doc's own log. Not because admins are suspects, but because "power is real and its use is logged" is the same bargain this project has made everywhere else, just extended above the simulation instead of inside it.

**Reuse the existing frameworks; this doc only fills the specific gaps.** Stealth observation is observer's ghost framework (`observer.md` §2–§3) plus one added privilege, not a second camera system. Ahelp is comms' message backend (`comms.md` §11) addressed to a queue instead of a person, the identical "address mode on the existing system" move `pda.md` §4 already made for private messages. A permission tier is the same named, checkable, cumulative credential shape id/access already uses for crew (`id-access.md` §4–§5) — just a separate rail, because admin authority has nothing to do with a character's in-fiction job.

**World-intervention actions bypass the simulation on purpose — that's not a contradiction of "physical, not RNG."** That rule governs how the simulated world resolves outcomes for players. An admin teleport or heal is a deliberate, logged escape hatch sitting above the simulation, not a simulated event pretending to be one. The two rules govern different layers and don't conflict.

## 2. Permission tiers

A named, tiered credential, entirely separate from the in-fiction ID/access rail (`id-access.md` §4) — a Captain player isn't automatically a Moderator, and a Moderator player's character carries no special in-fiction access because of it. Two different rails, same shape.

**Representative slice, not exhaustive** (§11) — the same "representative, not exhaustive" treatment id/access gave its own job→access table (`id-access.md` §5):

| Tier | Can do |
|---|---|
| **Moderator** | Claim and answer ahelp tickets (§3), mute, kick, stealth observation (§4) |
| **Admin** | Everything Moderator can, plus ban, world intervention (§5), round/objective override (§6) |
| **Owner/Host** | Everything Admin can, plus granting/revoking other admins' tiers, and round config's own config screen (`round-config.md` §5) |

**Strictly cumulative** — the same pattern the Captain's access-level set already establishes ("even the Captain's set is enumerated, not a hardcoded bypass flag," `id-access.md` §5), not a separate bitmask per tier. **Every check is logged, pass or fail** — the identical discipline the access check itself already runs (`id-access.md` §6), just pointed at a different credential.

## 3. Ahelp — reusing comms, not reinventing chat

The tab-shaped slot `pda.md` §3 already reserved becomes real here. A ticket is comms' existing message/log backend (`comms.md` §11), addressed to an admin queue instead of a person or a channel — the same "address mode on the existing system, not a new one" move private messaging already made (`pda.md` §4), just a different address target.

- Opening the PDA's ahelp tab starts or continues a ticket thread — identical compose/read interaction to a private message, no new UI grammar.
- Any on-duty Moderator-tier-or-above admin sees the open queue; **claiming a ticket assigns it**, so two admins don't answer past each other.
- The full thread — player's message, replies, who claimed it, when it closed — is retained as the ticket's own permanent record, same "real, inspectable data, never summarized away" instinct as every other log in this project.

## 4. Stealth observation — observer's framework, one privilege added

An admin entering stealth observation *is* a ghost (`observer.md` §2–§3) — the identical free-flying, invisible, undetectable camera every dead or never-embodied player already gets, not a second spectator system. What's different:

- **Jump-to-player** — an admin-only camera snap, using the exact follow-lock every ghost already has (`observer.md` §3), just selectable from a full roster instead of only nearby points of interest.
- **Omniscient examine** — the variant `examine.md` §11 flagged and left unclaimed ("any admin/observer-only omniscient examine variant... not this doc's"). An admin in stealth observation examines any character or object for its full underlying state — the same content categories `examine.md` §7's table already defines per target type, just without that doc's §5 "always the public tier" restriction, because an admin isn't a crew member subject to the fiction.
- **Godmode**, for the rarer case an admin possesses a body directly (testing, demonstration) rather than staying a ghost — invulnerability and unlimited resources on that body, logged the same as every other action here.

## 5. World intervention

Teleport (to a player or a coordinate), spawn an item or creature, heal or damage a character, freeze/unfreeze — gated at Admin tier (§2), a deliberate, logged escape hatch above the normal simulation.

**Every intervention writes a real log line** — who, what action, target, timestamp — the same discipline as everything else in this doc. Nothing here is invisible fiat; it's a real action with a real record, same as any legitimate crew action already leaves one elsewhere in this project.

## 6. Round & objective override

This doc supplies the invocation point for three overrides other docs already defined the effect of but explicitly left open:

- **Lobby resolution override** — force the resolution pass early, or place a specific player into a specific job (`lobby.md` §5, §11).
- **Round-end override** — `round-end.md` §2's "an admin ends it," already specified as always available; this is what an admin actually invokes.
- **Objective-state override** — `objectives.md` §12's flagged manual correction of a pending/complete/failed record, for the rare bug or edge case that needs it.

None of these invent new round or objective logic — they're the identical state transitions those docs already defined, just given a real trigger and a log line here.

## 7. Moderation — kick, mute, ban, notes

`persistence-save.md` §5 already commits moderation notes and bans to disk as player meta; this doc is the workflow that writes them.

- **Mute** — silences a player's comms output (`comms.md`) for a duration, logged.
- **Kick** — disconnects the current session; no persistent record beyond the log line.
- **Ban** — kicks and blocks reconnection, keyed to the same stable identity `persistence-save.md` §5 already flags. On a server requiring `player-accounts.md` §3's verification, this is a real, reliable ban; on one that doesn't, a ban is only as good as the connection claiming that identity — the identical caveat that doc already states, not a new one invented here.
- **Notes** — a plain, timestamped remark attached to a player's record, visible to Moderator tier and above, never player-facing.

Appeal process, ban-duration conventions, and escalation policy are server policy, not designed here (§11).

## 8. HUD & touchpoints

No new permanent chrome for ordinary players — this entire doc is admin-facing.

- The admin toolkit itself is a real, deliberately-designed panel, the same out-of-fiction carve-out lobby's job browser and creative mode's construction menu already claim (`lobby.md` §1, `creative-mode.md` §1) — not diegetic, and not apologizing for it.
- The PDA's ahelp tab (`pda.md` §3) is the only player-facing footprint: a tab that exists, a compose box, nothing else.
- Stealth observation reuses observer's existing camera and UI wholesale (`observer.md` §3) — no separate admin-camera interface to build.

## 9. Worked examples

**A — Ahelp ticket claimed and resolved:**

| Step | What happens | Admin-tools state |
|---|---|---|
| 1 | A player opens their PDA's ahelp tab, describes a stuck interaction | Ticket enters the open queue |
| 2 | An on-duty Moderator claims it | Ticket assigned; no second admin can claim it out from under them |
| 3 | Admin replies, confirms the issue, resolves it in-round | Full thread retained as the ticket's permanent record |

**B — Stealth observation catches a rule violation:**

| Step | What happens | Admin-tools state |
|---|---|---|
| 1 | An admin enters stealth observation, flies to a reported location | Identical ghost camera every player already gets, per `observer.md` §3 |
| 2 | Omniscient examine confirms a player is exploiting an interaction | Full underlying state visible, bypassing examine's normal public-tier restriction |
| 3 | Admin escalates to a mute and a logged note | §7's moderation actions fire; both write real log lines |

**C — Objective-state override after a bug:**

| Step | What happens | Admin-tools state |
|---|---|---|
| 1 | A steal objective's target item is destroyed by an unrelated explosion mid-round, leaving it uncheckable | Objective would otherwise never resolve cleanly |
| 2 | An admin manually sets the objective to failed at Admin tier | §6's override fires; `objectives.md` §2's record now holds a real, non-ambiguous state again |
| 3 | Action logs the same as any other admin action | Real, inspectable correction, not a silent database edit |

**D — A ban later found to be ckey-spoofed:**

| Step | What happens | Admin-tools state |
|---|---|---|
| 1 | An admin bans a player for repeated griefing | Ban keyed to the player's claimed identity, per §7 |
| 2 | The banned player reconnects under a different claimed ckey | Ban doesn't catch them — the exact limitation `persistence-save.md` §5 already flagged |
| 3 | Admin notes the evasion; real authentication (`player-accounts.md`) is the actual fix, and even that only raises the cost of evasion rather than eliminating it (`player-accounts.md` §5) | Consistent with the rest of this project's honesty about what it hasn't solved yet |

## 10. Integration notes

| Admin-tools element | Touches existing / needed system |
|---|---|
| Permission tiers | Separate rail, same shape as ID/access (`id-access.md` §4–§6) |
| Ahelp ticket | Comms message/log backend (`comms.md` §11), same address-mode move as `pda.md` §4 |
| Ahelp tab entry point | `pda.md` §3 (already reserved) |
| Stealth observation | Observer's ghost framework (`observer.md` §2–§3) |
| Omniscient examine | Flagged and left unclaimed by `examine.md` §5, §7, §11 |
| World intervention logging | Same "real, inspectable data" discipline as `round-config.md` §5, `ai-cyborgs.md` §4 |
| Lobby resolution override | `lobby.md` §5, §11 |
| Round-end override | `round-end.md` §2 |
| Objective-state override | `objectives.md` §12 |
| Moderation notes/bans storage | `persistence-save.md` §5 (player meta layer) |
| Ban reliability caveat | `persistence-save.md` §5's stable-identity limitation, inherited not re-solved |
| Owner-tier config screen | `round-config.md` §5 (unchanged, cross-referenced not redesigned) |

## 11. Out of scope for this pass

- **The full admin action roster and exact tier-to-action mapping** beyond §2's representative slice — a content pass, not a design decision.
- **Ban duration conventions, appeal process, and escalation policy** — server policy, not a mechanic.
- **Exact permission-tier names and count** beyond the three-tier illustration in §2 — a server-configurable content decision.
- **Vote-kick or other player-driven moderation** — a different, player-facing mechanic, not this doc's territory.
- **Real player authentication** — designed in `player-accounts.md`; this doc inherits the same black-box treatment for it that `persistence-save.md` §5 already established.
- **Server-level operations** (restart, config-file edits, hosting/deployment) — outside gameplay design scope entirely.
