# Creative Mode & Map Authoring — design document

> Status: active

Resolves an item Area's own doc flagged and set aside: "actual editor tooling UI for the merge/split/rename workflow (separate from this data-architecture pass)" (`area.md` §8). Reuses round config's pool model directly rather than inventing a new selection mechanism (`round-config.md` §2), and reuses construction's structural vocabulary and Area's local recompute wholesale (`construction.md` §2–§5).

## 1. Design philosophy

**Creative mode is a normal gamemode-pool entry, not a bolted-on special case.** Round config already treats gamemode selection as "a set of enabled entries, each with a relative weight and a precondition... drawn from with a deterministic fallback" (`round-config.md` §2) — creative mode fits that shape exactly, so it needs no selection mechanism of its own.

**It builds real data, not a parallel sandbox.** Every wall, door, and Area placed in creative mode is the same object every other doc already reads and writes — there's no separate "creative buffer" to reconcile later. That's what makes exporting a genuinely usable map possible at all, and it's why this doc can answer Area §8 instead of just adding a new deferred item next to it.

**The construction menu is a deliberate exception to "no abstract menu."** Everywhere else in this project, a floating menu independent of the world is exactly what gets redesigned away. Creative mode is different in kind, not degree — it's explicitly a meta/authoring context, not in-fiction gameplay, the same carve-out round config's own admin config screen already makes for itself: "a real settings surface, not a checkbox-laden webpage" (`round-config.md` §1), still a deliberately-designed UI, just not a diegetic one. Confining that exception to this one mode keeps it from becoming precedent anywhere else.

This pass promotes four items straight out of the original out-of-scope list — object placement, utility routing, bulk tools, undo/redo — plus spawn-point authoring, the gap flagged there but never designed. None of them earn a new abstraction: object placement reuses each system's own vocabulary the same way structural placement already does; utility routing is the connected-component technique Area, lighting, electricity, disposal, and atmospherics all already share, now exposed as an authoring tool instead of staying implementation-only; bulk tools and undo are UX conveniences layered on the same instant-placement primitive, not new placement semantics.

## 2. Construction menu

A real panel/palette, not a radial or Tier 3 hand-tool grammar — the exception above, made concrete.

- **Palette entries reuse each system's own vocabulary directly** — wall, door, window, deck plating (`construction.md` §2, §3, §5, §7) as before, now joined by every placeable device and the utility segments described in §3–§4. No new object types are invented anywhere in this expansion; the palette is a navigation layer over vocabulary every other doc already owns.
- **The palette is grouped by department**, mirroring the grouping lobby's job browser already uses (`lobby.md` §2) — Structural, Power, Atmospherics, Disposal, Medical, Security, Cargo, Command/Comms, Fabrication — so a builder navigates the same mental map a crew member already would, not a flat alphabetical device list.
- **Placement is instant.** Selecting an entry and clicking a valid tile places it directly at its finished state — Sealed wall, functional door, laid deck plating, a device in its default placed state (§3) — skipping the staged ladder, material cost, and timers that earn their keep in normal play but add nothing here. Whichever adjacency mesh variant a normal build would resolve to, creative placement resolves to the same way, automatically — same underlying object and rules, just no intermediate stages to click through.
- **The eraser applies uniformly** — a structural tile, a placed device, or a utility segment all clear the same way: straight back to their absent state, no material recovery, no tool-gating, regardless of what's targeted. One mechanism, not per-category erase logic.
- Single-tile-or-segment click-placement is still the baseline interaction — §5 layers bulk operations on top of it without changing what one click does.

## 3. Object placement

Every placeable object from every system doc — APCs, SMES units, air alarms, scrubbers, vents, pumps, fabricators, cargo pads, disposal chutes and outlets, lockers, consoles, cameras, medical/surgical devices, the reactor — enters the palette as-is, in that system's own default or unpowered state. This pass pulls in the full vocabulary deliberately, per the original "every other device from every other doc" scope note — nothing held back to a later pass.

**Placement drops the object, not its configuration.** An APC needs no binding step — it inherits whatever Area owns the tile it's placed on automatically, the same rule Area already establishes for normal play (`area.md` §5). An air alarm's frequency, a vending machine's stock list, a locker's access requirement — every object's own configuration happens through that object's existing diegetic device interface after placement, exactly the way a player would configure it in a normal round. Creative mode adds no placement-time config screen of its own; that would just be a second, parallel interface for something every device already has one of.

**Devices spawn unpowered/idle by default**, not pre-charged or pre-warmed — a placed SMES starts empty, a placed reactor starts cold. A freshly-built area stays honest about what it actually needs (a live grid, a working loop) rather than presenting a station that was never actually brought online.

## 4. Utility routing — cables & pipes

**This is the direct answer to electricity's own flagged gap** — "editor tooling for authoring the backbone loop/spur topology," named there as "the same category as Area's own authoring-tool gap" (`electricity.md` §11) — the same way §7 (Area authoring) already answered Area's. It also gives atmospherics' and disposal's tile-grain pipe segments (`atmospherics.md` §6, `disposal.md` §3) a real placement path, where those docs each deliberately left the fixture set and placement UI to a later pass.

**Cable and pipe segments place the same way structural elements do** — tile-grain, instant, adjacency-resolved automatically. A placed segment auto-connects to any same-type segment on an adjacent tile, forming the connected-component graph each system already reads — electricity's thick/standard cable graph, atmospherics' separate gas- and liquid-typed pipe networks, disposal's chute/junction/outlet graph. No new graph model — this tool authors input into the same connected-component technique Area's flood fill, lighting, electricity, disposal, and atmospherics already all share (`atmospherics.md` §6 names the full list).

**Three physically distinct segment types, never one generic "pipe"**: electrical cable (thick/standard), atmos pipe (gas-rated and liquid-rated are separate placeable types, per atmospherics' "never one pipe carrying either"), and disposal pipe. Placing the wrong type where a builder meant another is a real, visible mistake — same as it would be in normal construction — not a picklist error this tool smooths over.

**Endpoints (SMES, APC, pump, vent, chute, junction, outlet) are ordinary objects from §3**, not part of this tool — a builder places the device, then routes segments to it, the same two-step reality any of these systems would have in a live round.

## 5. Bulk tools

Three tools, each matched to what actually benefits from batching — this isn't one rectangle-fill stretched to cover every case, the same "two real techniques, not one stretched to cover both" discipline atmospherics already applies to diffusion vs. bulk flow (`atmospherics.md` §1).

- **Rectangle-fill** — for structural elements and single-instance-repeatable objects (deck plating, floor, a row of lockers, a spaced-out camera run). Click-drag a rectangle; every tile inside gets the selected palette entry, exactly as if placed individually.
- **Line/path draw** — for utility segments. Click a start tile and an end tile; the tool auto-routes a straight or Manhattan path between them and fills it with the selected segment type, connecting into the network the same way a hand-placed run would. This is the tool that actually makes utility routing (§4) practical at station scale — nobody hand-clicks two hundred cable tiles.
- **Copy-paste (stamp)** — select an already-built region (structural, placed objects, and its Area tag together) as a clipboard, paste it elsewhere in the same session. Session-scoped only for this pass — not saved as a persistent, reusable template library across sessions. A plausible future extension once this baseline is proven, the same posture single-tile placement took toward bulk tools in the original pass.

**A bulk operation is one undo step**, not one per tile — undoing a rectangle-fill or a line-draw reverts the whole batch at once (§6).

## 6. Undo/redo

**Per-builder, private stacks** — each builder undoes only their own placements and erasures; nobody can undo another builder's work directly. This matches the project's existing default for concurrent edits ("last-write-wins is a reasonable default") without overriding it: undo doesn't introduce a second conflict-resolution system, it just needs one real rule for the case last-write-wins alone doesn't cover.

**A validity check gates every undo step.** Each entry in a builder's stack records the tile or segment it targeted and the state it left there. Before reverting, the system checks whether that target still matches — if nothing else has touched it since, the undo applies cleanly. If another builder has since placed, erased, or routed through that same tile, the step is skipped rather than silently reverting state someone else has already built on top of, and the builder sees a real, visible reason why ("can't undo — modified since by another builder"), not a silent no-op. Same "real, visible reason" standard armor's absorption values, the hacking interface's failure states, and lobby's locked-job display already run.

**Redo mirrors undo** — same private stack, same validity check, cleared the moment a new action is taken after an undo.

**No cross-session persistence.** A builder's undo stack is session-scoped, cleared on disconnect — consistent with saving being a snapshot of live state (§9) rather than an event-sourced log this tool would need to replay.

## 7. Area authoring

The construction menu's area tool is the concrete answer to Area §8's deferred item: select tiles and rename, merge, or manually split their Area — the exact manual-override behavior Area's own doc already specified as needed "for real cases the geometry alone can't decide" (`area.md` §3), now given a real interface instead of remaining unbuilt.

**Automatic recompute still runs underneath, unchanged.** Sealing a wall that encloses a subregion still splits it via the same local recompute this project already established (`construction.md` §4). The manual area tool exists for the cases recompute alone can't decide — a pillar-fragmented open bay that should stay one Area, a walled-off private office that shouldn't — not as a replacement for the automatic path.

## 8. Spawn-point authoring

**The gap the original doc flagged as real but undesigned** — "a real gap in whether a saved map is actually round-usable yet" — closed here rather than deferred again.

**A spawn point is a placed marker tagged with a specific job**, reusing the same job vocabulary and department grouping lobby's job browser already defines (`lobby.md` §2) — no new job taxonomy invented. A department can carry multiple spawn points per job (a Security spawn near the HOS office and a second near general security, say); at resolution, one is picked at random among that job's tagged points on the drawn map.

**Latejoin needs no separate authoring.** Lobby's latejoin flow already reuses the identical job-list interface, filtered to open slots (`lobby.md` §7) — it resolves against the same tagged points a round-start assignment would, not a second pool.

**Antagonist-tagged spawn points now have a real consumer.** A spawn point can also be tagged to an antagonist category instead of a job — the identical marker and per-map collection as job-tagged spawns (above), just read by `antagonist-content.md` §6 for its Nuclear Operative squad instead of by lobby's job resolution. Hazard-specific spawn points remain undesigned — no gamemode needs one yet.

**Coverage validation happens at the map pool, not silently at round start.** Before an admin enables a saved map in round config's pool, the map pool's config screen (`round-config.md` §5) checks the map's tagged spawn points against the currently active job list and flags any job with zero coverage — real, inspectable information an admin acts on before a round can fail at resolution, not a crew member discovering mid-round-start that their assigned job has nowhere to put them. Companion edit, §15.

## 9. Saving — this is the map pool

No new file format or export pipeline needed. Per §1, creative mode's tile and Area data already *is* the same data every round loads at start — "save" is a snapshot of that live state to a named file, nothing more.

**A save becomes a new entry in round config's existing map pool** (`round-config.md` §2), disabled by default. Getting it into rotation is the identical admin action that already enables any other map pool entry — the pool's own enabled toggle is the review gate; there's no separate approval system to design.

**Saving isn't a once-per-session action.** Nothing about the underlying data model requires waiting for round end — a builder can snapshot iterative versions under different names as a session progresses.

A save now captures placed objects, routed utility networks, and tagged spawn points alongside tile and Area state — no new snapshot mechanism, the same live-state capture already established, just a larger state surface to snapshot now that there's more of it.

## 10. Round integration

**One more gamemode pool entry**, same weight/precondition/enabled shape as everything else in the pool (`round-config.md` §2) — no parallel selection path. In practice a server keeps its weight at or near zero so it never randomly drafts into ordinary rotation, and an admin raises it deliberately when a build session is wanted.

**No antagonist categories resolve.** Round config already handles a gamemode with an empty antag-category set — its own safe-default fallback is exactly that (`round-config.md` §3) — creative mode is simply another mode that resolves to nothing there, chosen deliberately rather than reached by fallback.

**Job list collapses to a single Builder role** for the session — a job-list delta handed to lobby the same way any gamemode-specific delta already crosses over (`round-config.md` §4). Department and job distinctions don't mean anything when everyone present is there to build, not to staff a station.

**Hazards and antagonists simply don't spawn**, because nothing in the mode's own runtime logic spawns them — the same boundary round config already draws around "how a gamemode picks its antags... that mode's own business" (`round-config.md` §6); this mode's business is just building. Preventing griefing or PvP during a session is a server-policy question, not a mechanic this doc needs to design.

Creative mode's own session still collapses the job list to a single Builder role, unchanged — spawn points placed during a session serve the *map*, not that session's own players. They're read the first time the saved map is actually drawn into a real round with its real job list, per §8.

## 11. HUD touchpoints

The construction menu panel is the one deliberate departure from "no abstract menu" in this entire project, and it's confined to this mode's own session — nothing about a normal round's HUD changes because this mode exists.

## 12. Worked examples

**A — Designing a new outpost annex:**

| Step | What happens | System state |
|---|---|---|
| 1 | Admin enables and weights the creative-mode pool entry for one round | Round config draws it like any other mode (§10) |
| 2 | Builders lay deck plating out from the station's edge, wall a perimeter, place a door | Every object placed is the same one construction's doc defines — instant, no material or timer |
| 3 | Final wall seals the perimeter | Area's local recompute claims the new footprint as its own Area, exactly as it would in a normal round (`construction.md` §4) |
| 4 | Builders save the result as "Outpost — Annex Draft 1" | New disabled entry appears in round config's map pool |

**B — Area tool resolving a real edge case:**

| Step | What happens | System state |
|---|---|---|
| 1 | A private office sits inside a larger open department bay with no wall of its own | Auto-detection alone would fold it into the surrounding Area |
| 2 | A builder selects its tiles with the area tool and splits it out, naming it | Exactly the manual-override case Area §3 already anticipated, now actually doable in-game |

**C — A saved map reaching rotation:**

| Step | What happens | System state |
|---|---|---|
| 1 | "Outpost — Annex Draft 1" sits disabled in the map pool after the session | No different from any other disabled pool entry |
| 2 | An admin reviews it, sets a weight, enables it | Ordinary round config admin action (`round-config.md` §5) — no separate promotion step exists or is needed |
| 3 | A later round draws it | Same draw mechanism as any other map |

**D — Wiring a new annex:**

| Step | What happens | System state |
|---|---|---|
| 1 | Builders finish walling a new annex (§7, unchanged) | Area recompute claims the footprint |
| 2 | One builder places an APC, then line-draws standard cable back to the department SMES | Cable auto-connects into the existing backbone graph (§4) |
| 3 | A second builder places two air alarms and a scrubber, line-draws gas pipe to the nearest atmos trunk | Separate gas-typed network, same technique |
| 4 | Session save | Both networks and both new devices snapshot into the map pool entry (§9) |

**E — Undo blocked by a concurrent edit:**

| Step | What happens | System state |
|---|---|---|
| 1 | Builder A rectangle-fills a floor section | One bulk undo entry created |
| 2 | Builder B places a locker on one of those tiles before A undoes | That tile's state has changed since A's action |
| 3 | Builder A hits undo | Validity check fails for that one tile; the rest of the batch reverts, that tile is skipped, A sees why |

**F — Spawn coverage catching a gap before rotation:**

| Step | What happens | System state |
|---|---|---|
| 1 | "Outpost — Annex Draft 1" is saved with Engineer and Security spawns tagged, but no Cargo Technician spawn | Map pool entry exists, disabled |
| 2 | An admin tries to enable it against the standard job list | Config screen flags "Cargo Technician: no spawn coverage" |
| 3 | Admin either tags a Cargo spawn back in creative mode or narrows the job list for that map | No silent failure reaches an actual round |

## 13. Integration notes

| Creative mode element | Touches existing / needed system |
|---|---|
| Gamemode selection | Round config's pool/weight/precondition/fallback shape (`round-config.md` §2–§3) |
| Job list override | Job-list delta handoff (`round-config.md` §4, `lobby.md` §2) |
| Placeable objects | Construction's wall/door/window/deck-plating vocabulary (`construction.md` §2, §3, §5, §7) |
| Instant placement/erase | Skips construction's staged ladder and deconstruction sequence entirely (`construction.md` §2, §6) |
| Area boundary changes | Same local recompute as normal play (`construction.md` §4) |
| Manual area rename/merge/split | Area's deferred manual-override tooling (`area.md` §3, §8) |
| Saving | Round config's existing map pool (`round-config.md` §2) |
| Promotion to rotation | Ordinary admin config-screen action (`round-config.md` §5), no new system |
| Object placement | Every system's own object vocabulary, placed in default state; Area→APC auto-binding (`area.md` §5) |
| Utility routing | Electricity's cable graph (`electricity.md` §11 gap, now answered), atmospherics' gas/liquid pipe networks (`atmospherics.md` §6), disposal's pipe network (`disposal.md` §3) |
| Bulk tools | Same instant-placement/erase primitives (§2), batched |
| Undo/redo | New per-builder validity-checked stack; interacts with, doesn't replace, last-write-wins |
| Spawn-point authoring | Lobby's job vocabulary and latejoin flow (`lobby.md` §2, §7), round config's map pool config screen (`round-config.md` §5) |

## 14. Out of scope for this pass

- Persistent, cross-session stamp/template library — session-scoped copy-paste only for this pass (§5)
- Simultaneous multi-builder edit-conflict resolution beyond last-write-wins plus undo's validity check — no further arbitration designed
- Hazard-specific spawn-point tagging — no gamemode needs one yet; antagonist-tagged spawn points are now designed (§8), consumed by `antagonist-content.md` §6
- Map metadata beyond a name — author, tags, thumbnail generation
- Griefing/PvP prevention during a session — server policy, not a mechanic

## 15. Companion edits flagged

- `electricity.md` §11 — remove "editor tooling for authoring the backbone loop/spur topology" from out-of-scope; point to this doc's §4.
- `atmospherics.md` (out-of-scope list) — "vent/valve/pipe fixture physical object models and placement UI": the placement-UI half is now answered by §3–§4 here; the fixture-set-as-content half remains atmospherics' own job — repoint the citation, don't delete the line.
- `disposal.md` — pipe segments now have a real authoring path via §4; add an integration-notes row there pointing back here.
- `lobby.md` §9 (integration notes) — spawn resolution was previously unaddressed/assumed infra; add a row pointing to this doc's §8 as the system that now supplies it.
- `round-config.md` §5 — map pool config screen gains the spawn-coverage validation check from §8.
