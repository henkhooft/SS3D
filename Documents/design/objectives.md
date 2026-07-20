# Objectives — design document

> Status: active

Closes a loop three docs have already assumed open: id/access names the exact shape this doc fills — "a traitor being ordinary crew with a hidden objective" — while deferring "traitor objectives" as that gamemode's own business, the same precedent round config's antag-category handoff sets for per-gamemode antag logic generally (`id-access.md` §5, §12; `round-config.md` §6); AI/cyborgs defers malf's own ability roster on the same precedent (`ai-cyborgs.md` §5). This doc is that business, for the individual-antagonist case.

## 1. Design philosophy

SS13's objective system is, at its core, sound game design under an artifact-heavy delivery mechanism: a paper printout from an uplink, resolved by an admin or an honor-system checkbox at round-end. The idea — a private, personal win condition layered on top of the round — doesn't need replacing. The resolution mechanism does.

Two rules fall out, both direct extensions of principles already load-bearing elsewhere in this project:

1. **Verify against real state, not a hidden roll or an honor system.** Disposal's sabotage detection is physical, never a hidden roll (`disposal.md` §7); the hacking interface's antag-tier tampering is detectable through real, gapped logs, not a clean erasure (`ai-cyborgs.md` §5). Objectives get the same treatment: wherever an objective's success condition already corresponds to state some other system tracks — possession, death, presence — that system is the verifier. Self-report is the fallback for the residual case where no such state exists, not the default mechanism.
2. **One shared check, many archetypes.** The same "one resolution function, many sources" shape explosives uses for its blast event (`explosives-destruction.md` §1) and the access check uses for its dozen consumers (`id-access.md` §6) applies here: every objective, regardless of archetype, resolves through the identical pending/complete/failed record (§2), just fed by a different system's state.

## 2. The objective record

One shared shape, instantiated per archetype:

- **Archetype** — steal, assassinate, or escape this pass (§5–7); the record shape is built to take more later (§12).
- **Target** — a reference into an existing system: a specific item instance (steal), a crew record (assassinate), or nothing (escape has no target beyond the holder themselves).
- **State** — pending, complete, or failed. Starts pending; each archetype's own check function (§5–7) is what flips it.
- **Verification mode** — auto (default; a real system supplies the state) or self-report (fallback; §3). Every archetype shipped this pass is auto.

An antagonist typically holds a small set of these — the composition and count is round config's antag-category handoff's business (§4), not this doc's.

## 3. Verification — auto by default, self-report as fallback

**Auto-verified objectives read state a system already owns.** Steal reads the inventory/Container system's possession state (`inventory-storage.md` §2). Assassinate reads the health/death system's death event log (`death-cloning-respawn.md`). Escape reads shuttle Area-occupancy at round-end (`shuttles.md` §2). None of these invent new tracked state — they're read paths, the same relationship examine has with every object property it renders (`examine.md` §5).

**Self-report is a real fallback, not a placeholder for laziness.** The record's verification-mode field exists so a future archetype that genuinely has no system hook — something purely social, with no world-state signature — can still resolve at round-end without forcing every objective through one mechanism that doesn't fit. Nothing in this pass's three archetypes needs it (§12).

**A completed state, once recorded, doesn't un-complete.** Assassinate records the death event at the moment it happens (§6); a later cloning doesn't retract it, the same way completing a task doesn't get undone by cleanup afterward. Steal and escape, by contrast, are inherently round-end snapshots — there's no earlier moment where "possession" or "presence" meaningfully locks in.

## 4. Assignment

Round config's antag-category handoff already establishes the shape lobby reads from for who becomes an antag (`round-config.md` §4). This doc is the analogous supplier one layer down: given an antagonist category, a pool of objective templates resolves into concrete records (§2) the moment that antagonist is assigned — the same "lobby doesn't invent it, it reads it" relationship id/access's access-level table has with lobby (`id-access.md` §5).

**Target selection runs one exclusion filter, not a conflict-solver.** An antagonist never gets themselves as an assassination target. Two antagonists in the same round never get the same steal-item instance. Beyond that single-instance exclusion, this doc doesn't attempt to balance objective difficulty or narrative coherence across the crew — that's the same per-gamemode curation territory round config already scopes out as that mode's own business (`round-config.md` §6), not duplicated here.

**Steal targets are drawn from a flagged subset of items, not any item.** A handful of map-placed or job-linked items — a head's ID, a specific piece of valuable science hardware — carry an objective-eligible tag, mirroring the size-class tag pattern inventory already uses (`inventory-storage.md` §4). The exact roster is a content pass (§12), not decided here.

## 5. Steal

**Condition:** the target item instance is somewhere on the antagonist's person at round-end — held, worn, or nested in a carried container, checked recursively the same way Container weight already computes recursively (`inventory-storage.md` §2).

**Check:** a single possession query against the Container system at round-end. No mid-round polling needed — the item can change hands any number of times during the round; only where it ends up matters, the same "outcome, not process" resolution escape uses (§7).

**What this doesn't need:** a new "stolen" flag on the item. It's an ordinary item the whole round; theft is just where it's found at the end, same as disposal's sabotage payoff never needing a special "smuggled" item state either (`disposal.md` §7).

## 6. Assassinate

**Condition:** the target crew record reached the dead state (`health.md` §4's death threshold) at any point during the round.

**Check:** the health/death system already has to know the exact moment a character crosses into death for its own purposes (cloning eligibility, `death-cloning-respawn.md` §2). This objective just subscribes to that same event and timestamps it against the record. **Cause is irrelevant.** A target that dies to the antagonist, to another antagonist, to a hull breach, or to their own bad decisions all satisfy the condition identically — attributing a death to a specific killer is a hidden-roll problem this project isn't interested in solving, and the state itself (someone is dead) is what actually matters.

**A later revival doesn't retract completion** (§3) — the death genuinely happened, and undoing it later is a separate, real event (cloning), not a rewrite of history.

## 7. Escape

**Condition:** the antagonist is alive and physically present within a departing extraction vehicle's set of Areas when round-end resolves evacuation.

**Check:** reuses shuttles' existing Area-attachment model — a shuttle's interior tiles are just Areas riding a moving root transform (`shuttles.md` §11) — so "is this character aboard" is the same occupancy query Area already answers for every other purpose (lighting, atmo, camera grouping). No new tracking needed, just a query run at the moment round-end's own evacuation logic resolves.

**This objective's check runs at the moment round-end's evacuation sequence locks in** — the point of no return `round-end.md` §3 defines for the evac shuttle, closing the dependency shuttles §9 flagged (`shuttles.md` §9). This doc supplies the check function; `round-end.md` §3 supplies the moment it fires.

## 8. The PDA objectives tab

**A hidden tab, present only for characters holding at least one objective record** — the same gating precedent id/access's summary tab already sets ("crew see only whether their own ID works," `id-access.md` §3, restated for a different kind of privacy). Non-antagonists never see this tab exist; there's no empty state to stumble into.

**Read-only, listing each held objective in plain language** with its current state (pending / complete / failed) styled the same way the alert stack and hacking interface tiers already use restrained status color rather than decoration. No progress bars, no hidden percentage — an objective is binary, and the tab says so plainly.

**No new item, no uplink dependency.** This deliberately doesn't require an uplink or antag-item-acquisition system to exist first — objectives are assigned and read independent of how (or whether) an antagonist gets tools, which is its own, separate, not-yet-designed question (§12).

## 9. Round-end resolution

At round-end, each held objective's final state (§2) feeds the round-end summary (`round-end.md` §5) — same dependency §7 already flagged. This doc guarantees every objective has resolved to a real, non-ambiguous state (never left pending) by the time that summary needs to read it; the summary's own presentation — how it's revealed, in what order, with what framing — is round-end.md's job, not this doc's.

## 10. Worked examples

**A — Steal, resolved quietly:**

| Step | What happens | Objective state |
|---|---|---|
| 1 | An antagonist is assigned "steal the Research Director's prototype scanner" at round start | Pending |
| 2 | Mid-round, they lift it from an unattended desk and stash it in a backpack | Still pending — possession isn't checked yet |
| 3 | The item changes hands twice more (dropped, picked up by someone else, stolen back) | Still pending — none of that matters |
| 4 | Round ends with the item in the antagonist's backpack | Complete — round-end possession query passes |

**B — Assassination with no clean attribution:**

| Step | What happens | Objective state |
|---|---|---|
| 1 | An antagonist is assigned to kill the Head of Security | Pending |
| 2 | The HoS dies mid-round in a firefight that also involved a second, unrelated antagonist | Death event recorded, timestamped |
| 3 | No system attempts to determine whose shot was fatal | Objective flips to complete regardless — the state (dead) is what's checked |
| 4 | The HoS is later cloned | Complete stays complete — the earlier death event already happened |

**C — Escape failing despite survival:**

| Step | What happens | Objective state |
|---|---|---|
| 1 | An antagonist holds an escape objective alongside a steal objective | Both pending |
| 2 | They complete the steal objective early, then get delayed crossing the station | Steal complete; escape still pending |
| 3 | The evacuation shuttle undocks and departs while they're still in a station Area | — |
| 4 | Round-end resolves: alive, but never inside the shuttle's Area set at departure | Escape fails — steal's completion is unaffected by escape's outcome |

## 11. Integration notes

| Element | Touches existing / needed system |
|---|---|
| Objective assignment | Antag-category handoff (`round-config.md` §4) |
| Steal check | Container/possession system (`inventory-storage.md` §2) |
| Steal-eligible item tag | Same pattern as size-class tagging (`inventory-storage.md` §4) |
| Assassinate check | Death event, health/death system (`health.md` §4, `death-cloning-respawn.md`) |
| Escape check | Shuttle Area-attachment/occupancy (`shuttles.md` §2, §11) |
| Escape resolution timing | `round-end.md` §3 (evac point-of-no-return) |
| PDA objectives tab | Diegetic tab-bar pattern, hidden-tab precedent (`pda.md` §3, `id-access.md` §3) |
| Round-end summary read | `round-end.md` §5 (summary screen) |

**Companion edit needed:** `pda.md` should gain a row in its tab bar for the objectives tab (§8) — a small addition to that doc's existing tab-bar list, not a functional change to anything already specified there.

## 12. Out of scope for this pass

- **Sabotage, protect, and other archetypes** — plausible future entries in §2's shape, not designed here; sabotage in particular needs a real "did this succeed" state that structural-damage systems don't cleanly expose yet.
- **Faction/shared objectives** (revolution, cult, or any team-goal gamemode) — this doc covers individual-antagonist objectives only, the same scoping line round config already draws around per-gamemode antag logic (`round-config.md` §6).
- **Uplink or any antag-item-acquisition system** — a real and eventually necessary system, deliberately not assumed or designed here (§8).
- **Exact steal-item roster and assassination-target pool composition per job/gamemode** — a content pass.
- **Round-end reveal presentation** — sequencing, framing, narrative text — belongs to round-end/transition, not this doc.
- **Admin override of an objective's state** — in-round admin tools' territory, a separate still-open pass.
