> Purpose: the minimum set of player interactions a Nuclear Operatives round should have, from SS14 as the default spec, crossed against what SS3D has and what art exists
> Status: draft — tiers await owner review
> Scope: Nuke Ops round, operatives and the five MVP1 crew jobs, plus the universal verbs both sides use
> Sources: SS14 `space-wizards/space-station-14` at `466d113` (2026-11-18): `Resources/Prototypes`, `Resources/ServerInfo/Guidebook`, `Resources/Locale/en-US`. SS3D audit of `develop` as of `5e7bb867d`.

# Expected content: Nuclear Operatives

This is the backlog the milestones draw from. A gate doc says *when* something has to work. This catalog says *what players expect to be able to do*, so that agents can pick work that serves the round without the owner specifying each item. A row that is `must` or `expected` and not `done` is a candidate for the next piece of work. Anything outside this catalog is dilution unless the owner says otherwise.

**How to read a row**

- **Tier.** `must`: the round feels broken or not like SS13/SS14 without it. `expected`: players notice it's missing in their first round or two. `later`: nice to have. The tiers are proposed from SS14 and await the owner's review.
- **SS3D.** `done` means the code path exists. `partial` means the verb exists with a gap, named in the note. `missing` means there's no code for it.
- **Art.** `in game` means a prefab already uses it, `SS3D-Art` means the model is in [art-available-for-import.json](../art-available-for-import.json), `none` means no art was found, and `—` means none is needed.
- **SS14.** A path relative to the SS14 repo (`P/` = `Resources/Prototypes/`, `G/` = `Resources/ServerInfo/Guidebook/`), with a prototype id where there is one.
- **Default spec.** SS14 decides unless a row is marked **⚑**. A ⚑ row conflicts with an owner decision already recorded in a design doc or milestone, or needs a 3D adaptation. Those rows are settled under [Decisions for the owner](#decisions-for-the-owner), not by an agent.

## Operatives

| Id | Interaction | Tier | SS3D | Art | SS14 | Note |
|---|---|---|---|---|---|---|
| N1 | Spawn geared: hardsuit, internals, magboots, gun, tools | must | done | in game | `P/Roles/Antags/nukeops.yml` `SyndicateOperativeGearFullNoUplink` | SS3D: C20R + Stechkin, full toolbelt, C4, minibomb |
| N2 | Spare magazines in the kit | must | missing | none (magazine models only in reload animations) | `P/Catalog/UplinkCatalog/ammo.yml` | `antagonist-content.md` §6 first pass promises spare magazines; no loadout carries one. Blocked on M1c physical magazines |
| N3 | Pinpointer toward the disk | must | missing | SS3D-Art `locator` | `P/Entities/Objects/Devices/pinpointer.yml` `PinpointerSyndicateNuclear` | `antagonist-content.md` §6 **first pass** says every operative carries one; MVP1 calls it "thin". 3D form ⚑ (held arrow vs HUD compass) |
| N4 | Team radio channel | expected | missing | — | `P/Roles/Antags/nukeops.yml` `ClothingHeadsetAltSyndicate` | Operatives spawn with no headset. Needs radio gating (U14) to be meaningful |
| N5 | Briefing and objective on spawn | expected | done | — | `L/…/preset-nukeops.ftl` `nukeops-briefing` | M4 squad draw and briefing |
| N6 | Role split: Commander, Corpsman, Operative | expected | missing | — | `P/Roles/Antags/nukeops.yml` `NukeopsCommander`, `NukeopsMedic` | Corpsman kit (N7) and the war declarator (N13) hang off it |
| N7 | Corpsman medical kit | expected | missing | partial (hypospray in SS3D-Art) | `nukeops.yml` `SyndicateOperativeMedicFull` | Needs N6 |
| N8 | Take the disk, load it into the nuke | must | done | in game | `P/Entities/Structures/Machines/nuke.yml` `diskSlot` | |
| N9 | Arm the nuke, countdown, station-wide announcement | must | done | in game | `nuke.yml` `alertLevelOnActivate: DeltaNuke` | SS3D: 3 min; SS14: 300 s |
| N10 | Enter the nuke code; anchor the nuke | must (SS14) | missing | — | `L/nuke/nuke-component.ftl` | ⚑ `antagonist-content.md` §6 deliberately has no code and treats the device as a plain timed charge. Recommend keeping SS3D's rule |
| N11 | Detonation ends the round with a result screen | must | done | — | `preset-nukeops.ftl` | M4/M5 |
| N12 | Uplink with telecrystals: open, browse, buy | must | missing | SS3D-Art `syndicatetelecrystal` | `P/Entities/Objects/Specific/syndicate.yml` `BaseUplinkRadio40TC`; `P/Catalog/UplinkCatalog/` | ⚑ design: later pass; MVP1: "no uplink". The single biggest identity gap for a Nuke Ops player |
| N13 | Declare war (+40 TC, crew warned, departure delayed) | expected | missing | none | `P/Entities/Objects/Devices/Syndicate_Gadgets/war_declarator.yml` | Needs N12, and a travel phase (N15) to delay |
| N14 | Uplink stock: firearms, ammo, explosives, melee, wearables, medicine | expected | partial | SS3D-Art `bulldogshotgun`, `l6`, `energyswords`, `fraggrenades`, `riotshield`; in game: C20R, C4, minibomb, Syndicate bomb | `P/Catalog/UplinkCatalog/*.yml` | Most iconic SS14 items have SS3D-Art models. Comes with N12 |
| N15 | Start off-station and travel in (shuttle) | must (SS14) | missing | — | `computers.yml` `ComputerShuttleSyndie` | ⚑ design later pass; shuttles are MVP2. A spawn pad stands in |
| N16 | Jetpack through vacuum | expected | missing | in index: `jetpack` | `P/Catalog/UplinkCatalog/wearables.yml` `UplinkJetpack` | Pairs with N15 and with zero-g movement (U31) |
| N17 | Toggle hardsuit helmet | expected | missing | in game | `P/Actions/types.yml` `ActionToggleSuitPiece` | SS3D seals hardsuit + helmet as separate items |
| N18 | Syndicate bomb: place, set timer, defuse | later | done | in game | `P/Entities/Structures/Machines/bombs.yml` `SyndicateBomb` | Ahead of SS14's tier |

## Crew by job

| Id | Interaction | Tier | SS3D | Art | SS14 | Note |
|---|---|---|---|---|---|---|
| C1 | Captain: hold or hide the disk | must | done (vault) | in game | `P/Catalog/Fills/Lockers/heads.yml` `LockerFillCaptainNoLaser` | SS14 puts it in the Captain's locker; SS3D design puts it in the vault. Both work |
| C2 | Captain: own pinpointer to find the disk again | expected | missing | SS3D-Art `locator` | `heads.yml` `PinpointerNuclear` | Same item as N3 |
| C3 | Captain: broad access ID | expected | done | — | `P/Roles/Jobs/Command/captain.yml` | Command access; not re-verified in code |
| C4 | Captain: station announcement from a console | expected | missing | SS3D-Art `consolecmd`, `requestconsole` | `computers.yml` `ComputerComms` | Only server-side station cues exist |
| C5 | Security: fire and reload | must | done | in game | `P/Entities/Objects/Weapons/Guns/Pistols/pistols.yml` | |
| C6 | Security: spare magazine in kit | expected | missing | none | `P/Roles/Jobs/Security/security_officer.yml` | Same blocker as N2 |
| C7 | Security: handcuff someone | must (SS14) | missing | SS3D-Art `handcuffs`, `cablerestraints` | `P/Entities/Objects/Misc/handcuffs.yml` | ⚑ MVP1 defers handcuffs to MVP2, but `antagonist-content.md` §6 gives crew the win when every operative is "dead or restrained" |
| C8 | Security: stun baton knockdown | expected | partial | in game | `P/Entities/Objects/Weapons/security.yml` `Stunbaton` | Awaits the owner's hand test (M1c) |
| C9 | Security: flash | expected | missing | SS3D-Art `flash` | `security.yml` `Flash` | |
| C10 | Security: armory with lethal guns | expected | missing | in game (M4), SS3D-Art shotguns | `P/Catalog/Fills/Lockers/security.yml` | M1c open: the M4 in an armory |
| C11 | Engineer: seal a hull breach | must | done | in game | `G/Engineering/ExpandingRepairingStation.xml` | |
| C12 | Engineer: refill a spaced room | expected | done | in game | `G/Engineering/Spacing.xml` | Air alarm + supply network (#285) |
| C13 | Engineer: spawn with a tool belt | expected | done | in game | `P/Catalog/Fills/Items/belt.yml` `ClothingBeltUtilityEngineering` | |
| C14 | Engineer: build from a construction list | expected | partial | — | `G/Engineering/Construction.xml` | SS3D: radial "Construct X" per target, no recipe browser |
| C15 | Engineer: open a wire panel; cut, mend, pulse | later | missing | — | `G/Engineering/WirePanels.xml` | `hacking-interface.md` is deferred |
| C16 | Doctor: scan a patient with a health analyzer | must | partial | in game `healthscanner` | `P/Entities/Objects/Specific/Medical/healthanalyzer.yml` | Doctor spawns with a HealthScanner, but no scan interaction was found. Examine shows health |
| C17 | Doctor: treat brute and burn | must | done | in game | `healing.yml` `Brutepack` | Patches + dressings |
| C18 | Doctor: stop bleeding with gauze | expected | done | in game | `healing.yml` `Gauze` | |
| C19 | Doctor: defibrillate | expected | done | in game | `P/Entities/Objects/Specific/Medical/defib.yml` | |
| C20 | Anyone: emergency medipen on a critical body | expected | missing | SS3D-Art `hypospray`; no medipen model | `hypospray.yml` `EmergencyMedipen` | |
| C21 | Assistant: spawn with ID, headset, maintenance access | expected | done | in game | `P/Roles/Jobs/Civilian/passenger.yml` | |

## Universal

| Id | Interaction | Tier | SS3D | Art | SS14 | Note |
|---|---|---|---|---|---|---|
| U1 | Move, walk/run toggle, sprint | must | done | — | `G/NewPlayer/Controls/Controls.xml` | M1m |
| U2 | Pick up, drop, swap hands, use in hand | must | done | — | `Controls.xml` | |
| U3 | Use a held item on a target | must | done | — | `Controls.xml` | |
| U4 | Throw an item | must | done | — | `Controls.xml` | Throw mode (R) |
| U5 | Examine, including a body's wounds | must | done | — | `P/Entities/Mobs/base.yml` `Examiner` | |
| U6 | Combat mode, melee, shoot | must | done | — | `P/Body/species_base.yml` `CombatMode` | Help/Harm intent (F) |
| U7 | **Pull and drag a body or object** | must | missing | — | `species_base.yml` `Puller` | Only the unbolted girder and the Syndicate bomb are draggable. Blocks carrying the wounded to medbay and carrying off the disk holder. Owner decided 2026-11-18: `must` (Decision 1) |
| U8 | Strip: take from another body | must | done | — | `species_base.yml` `Stripping` | Search + Take |
| U9 | Strip: put an item on another body | expected | missing | — | `Controls.xml` (strip menu) | Dress, put on a mask, cuff (with C7) |
| U10 | Open doors with ID access | must | done | in game | `G/Engineering/Airlocks.xml` | |
| U11 | Pry an unpowered airlock with a crowbar | expected | missing | — | `P/Entities/Objects/Tools/crowbars.yml` `Prying` | MVP1 M12 open; needs a design rule. SS14's rule: crowbar pries an unpowered door, bolted doors resist |
| U12 | Open, close and lock lockers | expected | done | in game | `Controls.xml` | |
| U13 | Local speech, whisper, shout | must | done | — | `G/NewPlayer/Controls/Radio.xml` | |
| U14 | Radio over a headset, channels by key | must | partial | — | `Radio.xml` | Sends, but the headset requirement is data only ("until MVP2 gating") |
| U15 | Emote by text or menu | expected | partial | — | `P/Voice/speech_emotes.yml` | Emotes render, but no player trigger was found |
| U16 | Toggle internals | must | done | — | `P/Actions/internals.yml` | |
| U17 | Decompression vents air and pulls bodies | must | done | — | `G/Engineering/Spacing.xml` | M8 |
| U18 | Breathing and suffocation | expected | done | — | `species_base.yml` `Respirator` | |
| U19 | Equip and unequip clothing slots | expected | done | — | `Controls.xml` | |
| U20 | Open backpack and belt | expected | done | — | `Controls.xml` | |
| U21 | Crawl when downed | expected | done | — | `species_base.yml` `Crawler` | M1s |
| U22 | Lie down or rest by choice | expected | missing | — | not found in SS14 prototypes | ⚑ 3D pose decision. Playing dead is classic SS13 |
| U23 | Buckle to a chair or bed | expected | partial | in game: chairs, beds; SS3D-Art `shuttleseat`, `stools` | `species_base.yml` `Buckle` | `SitInteraction` exists, but no prefab has a `SeatAnchor` |
| U24 | Bleed, go critical, die | expected | done | — | `P/Entities/Mobs/base.yml` `Bloodstream`, `MobThresholds` | |
| U25 | Catch fire; stop, drop and roll | expected | unverified | — | `base.yml` `Flammable` | The flamethrower and napalm exist; burning bodies not checked |
| U26 | Fire extinguisher | expected | missing | in game `fire-extinguisher`; SS3D-Art cabinets | `P/Entities/Objects/Misc/fire_extinguisher.yml` | SS14 rates it `later`; with a flamethrower in the arsenal it's `expected` here. In the MVP1 feel pass |
| U27 | Station alert level on nuke arm | expected | partial | — | `nuke.yml` `DeltaNuke` | Announcement only; no alert state or lighting change |
| U28 | Observe as a ghost after death | expected | done | — | `P/Entities/Mobs/Player/observer.yml` | No follow-lock |
| U29 | Crates: open, close, drag | expected | unverified | SS3D-Art `crates` | `Controls.xml` | No crate prefab found; dragging needs U7 |
| U30 | Radial verb menu for extra actions | expected | done | — | `Controls.xml` (context menu) | SS3D's adaptation of right-click verbs |
| U31 | **Move in zero-g: push off structures, drift, throw to propel** | must | missing | — | `Controls.xml` (spacewalks); jetpack in N16 | Without magboots, a body off the floor ragdolls with no control and no way back. Owner decided 2026-11-18: `must` (Decision 1) |

## Later

Held back, so agents don't start them without the owner: door remote, Captain's antique laser, mindshield; criminal records, forensics; airlock access configurator, airlock plating; cryo, stasis, cloning, surgery, chemistry; emag, radio jammer, implants, reinforcements, lone operative; death acidifier; climbing tables, sleeping, pointing, slipping, hunger, temperature; encryption-key swaps; stack splitting; whistle. Sources: SS14 rows tiered `later` in the extraction.

## Decisions for the owner

Each is a conflict between SS14 and a recorded SS3D decision. Agents don't settle these.

1. ~~**Dragging bodies (U7).**~~ **Decided 2026-11-18: `must`, together with moving in zero-g (U31).** Pulling is core in SS14. Without it, the Doctor can't bring a downed crew member to safety and nobody can carry off a disk holder. The owner added zero-g movement: today, anyone who loses their footing without magboots ragdolls with no way to move. Still open: does it land before Playtest 1? That would pull it out of MVP1's feel-pass deferral.
2. **Pinpointer (N3, C2).** `antagonist-content.md` §6 already lists it in the first pass, so the code is behind the design. Recommend `must`. You only need to choose the 3D form: a held item with an arrow (SS14), or a HUD compass while it's held.
3. **Handcuffs (C7).** SS14 `must`; MVP1 defers it to MVP2; the §6 crew-win rule says "dead or restrained". Recommend `expected`, after Playtest 1, alongside U9 (put an item on another body).
4. **Uplink (N12–N14).** SS14 `must`; the design calls it a later pass and MVP1 says no uplink. Recommend `must` for the gate after Playtest 1. It's the biggest operative-identity gap, and most of the stock already has SS3D-Art models. It needs a decision on where the uplink lives before the PDA exists (SS14 uses a radio).
5. **Nuke code and anchoring (N10).** SS14 `must`; SS3D's design deliberately treats the device as a plain timed charge. Recommend keeping SS3D's rule and dropping the row.
6. **Travel phase (N15, N13, N16).** SS14 `must`; needs shuttles (MVP2). Recommend `later` until shuttles exist. War declaration and the jetpack wait with it.
7. **Rest by choice (U22).** Recommend `expected`, with the pose reusing the Downed crawl body.

## Ready next

`must` and `expected` rows that are missing or partial, whose owner decision (if any) is made, and have art or need none. Agents pick from the top:

1. **U7** pull and drag bodies and objects, and **U31** zero-g movement (owner-decided `must`)
2. **C16** health analyzer scan: the item already ships in the Doctor's belt.
3. **U14 + N4** radio gating plus an operative team channel.
4. **U15** player emote trigger.
5. **U23** seats: put `SeatAnchor` on the chairs that are already in game.
6. **C9** flash (SS3D-Art model).
7. **C20** emergency medipen.
8. **U27** alert state on arm.
9. **N17** hardsuit helmet toggle.
10. **N6 + N7** operative roles and the Corpsman kit.
11. **C4** Captain's announcement console.

Already in flight elsewhere, don't duplicate: N2/C6 and C10 (M1c), U11 (M12), C8 (hand test).

## Maintenance

Update a row's **SS3D** column when its work ships, as part of `update-system-docs`. Re-derive the tiers only when the owner changes the target round. Add a row only with an SS14 source or an owner decision.
