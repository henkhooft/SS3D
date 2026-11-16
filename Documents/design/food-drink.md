# Food & drink — design document

> Status: active

Defines eating, drinking, hunger, the kitchen and the bar. Several docs already assume a hunger alert that nothing defines (`health.md` §3, `main-hud.md` §9) and leave food to someone else (`chemistry.md` §10, §13). This doc fills that gap. It serves pillar 1 (the Chef and the Bartender have a job the crew needs) and pillar 3 (food and drink are what people accept from strangers), and pillar 2 only lightly (`vision.md` §2).

## 1. Design philosophy

In SS13 nobody remembers starving. They remember the poisoned donut, the bar during a quiet shift, the Chef who fed the crew through a siege. Hunger itself is low stakes; what it buys is a reason to visit the kitchen and a reason to trust what you're handed. That's the native part worth keeping. A survival-sim meter that nags every few minutes is the leftover, and this doc drops it.

Two rules follow:

- **Food is chemistry, not a parallel stat.** A burger is a substance container holding nutriment plus whatever else got into it. Eating is ingestion (`chemistry.md` §9). Poisoning, spiking, grinding and analysis then all work without a line of food-specific design, the same "one shared mechanism" move chemistry already makes for overdose (§8 there).
- **Hunger is pressure, not a threat.** Skipping food all shift should leave you weaker when the late-round crisis comes. It should never be what kills you.

## 2. Food is a substance container

- **Every food and drink is a container** (`chemistry.md` §3). Its contents are ordinary reagents. **Nutriment** is the reagent that feeds you; it fills hunger as it absorbs.
- **What goes in, stays in.** A poisoned ingredient makes a poisoned meal. Cooking doesn't sterilize anything, and mixing a cocktail doesn't dilute a dose away.
- **Any open liquid container can be drunk from.** A glass, a beaker, a bucket. Whatever is in it does what it does; chugging the acid beaker is a choice with consequences, not something the game forbids (pillar 5).
- **Sealed containers open first.** A can or a bottle is opened as its own small action before it can be drunk.
- **Examine follows chemistry's rules.** A dish shows what it is ("a burger"), but its reagent contents stay unanalyzed (`chemistry.md` §7) unless someone runs it through an analyzer.

## 3. The stomach

What you eat or drink sits in the stomach first and absorbs into the bloodstream over time. This is ingestion's slow onset (`chemistry.md` §9) made physical, and the stomach is the organ that does it (`health.md` §3).

The gap between swallowing and absorbing is the counterplay window. Anything not yet absorbed can be purged:

- **Vomiting** — from an emetic reagent, or caused by a poison itself. What comes up leaves a mess on the floor.
- **Pumping the stomach** — a Medical treatment on a patient who can't or won't vomit.

A poisoning becomes a race Medical can win if they act fast.

## 4. Hunger

### The ladder

| State | Reached by | Effect |
|---|---|---|
| **Full** | eating past your fill | the next bite is refused until it passes |
| **Well fed** | a meal (§6, §7) | stamina regenerates slightly faster; fades back to Fed |
| **Fed** | eating | nothing |
| **Hungry** | about half a shift without food | stamina regenerates a little slower; alert chip |
| **Starving** | most of a shift without food | stamina cap and regen both drop noticeably; others can see it |

- **Starving never deals damage.** Stamina is the existing knob for "a weaker body" (`stamina.md` §2); hunger is one more thing turning it, alongside organs, blood volume, shock and weight.
- **Full is the only overeating rule.** No fatness, no body-shape state. You just can't chain-eat a vending machine.
- **The timing is relative to a shift**, not fixed minutes, since round length is a server setting (`round-config.md` §2).

### Snacks and meals

- **Snacks** are packaged food and drink: vended, carried in loadouts, found lying around. They lift you out of Starving but never past Hungry.
- **Meals** are cooked in the kitchen (§6) or mixed at the bar (§7). Only a meal reaches Fed, and it leaves you Well fed for a while.

The vending machine is the fallback; the Chef is the upgrade. That difference is the job.

### When the clock runs

- Everyone starts Fed, latejoiners included.
- The clock keeps running while Downed or Critical (`health.md` §4) and stops at death.
- Cryo pauses it, as it pauses every systemic clock (`cryogenics.md` §3).
- It runs in every gamemode. A round short enough never reaches Hungry, so nothing needs switching off per mode. A server can turn hunger off entirely; a building server (`sandbox-server.md`) would.

## 5. Eating and drinking

- **Bites and sips.** Each is a short action that takes a portion of the contents. A burger is a few bites, a can a few sips.
- **Interruptible.** Getting hit or moving stops the next bite.
- **Partial items stay real.** A half-eaten burger or a half-drunk can is an object in the world, with what's left still inside it.
- **A covered mouth can't eat.** A sealed helmet or an internals mask (`armor.md` §3) has to come off first. An engineer on EVA finds air before lunch.
- **Taste.** On the first bite or sip, any reagent strong enough in the mix gives a taste line ("you taste something bitter"), shown to the eater only. Most poisons can be tasted; a few are deliberately tasteless. A careful eater can stop after one bite, and a traitor who wants to be sure pays for the tasteless one.

### Feeding someone else

- **Offering** — hand food or a drink to someone; they choose to eat it.
- **Force-feeding** — feed or pour into someone Downed or restrained. It takes the same bites and sips, so it takes visible time, and it can be interrupted.
- Both are actions on another person, so they're always narrated to those nearby (`action-messages.md` §2). Pouring something into a downed crew member is not a quiet act; that visibility is the counterweight to griefing.

## 6. The kitchen

- **Supply is plentiful.** The kitchen fridge is stocked at the start of the shift, cargo can reorder ingredients (`cargo.md` §2, §5), and vendors stock snacks. The scarce thing is the Chef's time, not food.
- **Cooking is combining plus heat.** The Chef puts ingredients into the microwave or oven and applies heat; a known combination produces a meal. A handful of recipes, not SS13's hundreds.
- **What goes in, stays in** (§2). The ingredients' contents carry into the meal.
- **The kitchen is Service access** (`id-access.md` §5), like the bar.

## 7. The bar

With hunger the only need, drinks carry little nutriment. The Bartender's product is effects.

- **Alcohol** is a reagent. While it's in the bloodstream, drunkenness rises; as it metabolizes, drunkenness falls.
- **Drunkenness shows.** The screen sways, speech slurs in local chat and on the radio (`comms.md` §3, §6), and aim gets slightly worse (`combat.md` §3). The bar is readable and funny at a glance.
- **Too much is overdose**, routed into toxin like any other overdose (`chemistry.md` §8).
- **Cocktails are reactions.** Mixed in a shaker through ordinary reactions (`chemistry.md` §4). A drink the Bartender mixes counts as a meal (§4).

## 8. HUD and examine

- **One Hunger chip** in the alert stack (`main-hud.md` §9): a warning at Hungry, critical at Starving.
- **Full has no chip.** You learn it from the refusal message when you try to take a bite.
- **Well fed has no chip.** The stack shows only what's wrong; Well fed shows on self-examine (`examine.md` §7).
- **Others see** "looks gaunt" on a Starving character, and drunkenness through the sway and slurred speech themselves. Nothing else about your hunger is visible to anyone but you.

## 9. Worked examples

**A — The spiked drink:**

| Step | What happens | State |
|---|---|---|
| 1 | A traitor pours a poison into the Captain's drink at the bar | The glass now holds the drink plus the poison; it looks the same |
| 2 | The Captain takes a sip | Taste line: "you taste something bitter" |
| 3 | The Captain stops and calls Medical | Most of the dose is still in the glass; one sip's worth is in the stomach |
| 4 | The doctor pumps the Captain's stomach | The unabsorbed sip is purged; the little that absorbed raises toxin briefly |
| 5 | Security takes the glass to an analyzer | The poison is identified (`chemistry.md` §7); now there's a question of who poured |

Had the traitor bought a tasteless poison, step 2 gives no warning, the Captain drinks it all, and step 4 becomes a race against absorption.

**B — The crisis on an empty stomach:**

| Step | What happens | State |
|---|---|---|
| 1 | An engineer works through the first half of the shift without eating | Fed → Hungry; chip lights; stamina regen slightly slower |
| 2 | They eat a vended snack | Back above Starving, still Hungry |
| 3 | The station is breached late in the shift | Starving; stamina cap and regen down |
| 4 | They sprint between hull holes carrying plating | They tire faster than the well-fed Security officer beside them |
| 5 | After the crisis, they eat at the kitchen | Fed, then Well fed |

## 10. Integration notes

| Element | Touches existing system |
|---|---|
| Food and drink contents, transfer | `chemistry.md` §3 |
| Ingestion, stomach stage, purging | `chemistry.md` §9 |
| Stomach organ | `health.md` §3 |
| Poison, overdose, alcohol toxin | `chemistry.md` §8; toxin pool `health.md` §2 |
| Analysis of food contents | `chemistry.md` §7 |
| Hunger and well fed as stamina modifiers | `stamina.md` §2 |
| Covered mouth | Helmets and internals, `armor.md` §3 |
| Force-feeding narration | `action-messages.md` §2 |
| Drunk speech | `comms.md` §3, §6 |
| Drunk aim | Accuracy cone, `combat.md` §3 |
| Hunger chip | Alert stack, `main-hud.md` §9 |
| Well fed, starving on examine | `examine.md` §7 |
| Kitchen and bar access | Service level, `id-access.md` §5 |
| Ingredient resupply | `cargo.md` §2, §5 |
| Tasteless poison | The uplink's restricted reagent, `antagonist-content.md` §4 |
| Clock paused in cryo | `cryogenics.md` §3 |

## 11. Out of scope for this pass

- Exact nutriment amounts, hunger rates, thresholds and the well-fed bonus (a balancing pass, not a design decision)
- The recipe list and the taste threshold per reagent
- Thirst as a separate need — revisit only if the Bartender feels pointless in play
- Botany and growing ingredients — its own design later
- Spoilage, rotting and food hygiene
- Species or diet differences
- Fatness and body shape
- Vomit as a slip hazard, and cleaning it up
- Plates, trays, utensils and serving as objects
