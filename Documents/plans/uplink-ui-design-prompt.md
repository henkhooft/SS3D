# Operative uplink — Claude Design brief

> Generated 2026-11-18 from `Documents/design/antagonist-content.md` §4 and §6, the owner's decision in [expected-content](../milestones/expected-content.md) (Decision 4), SS14's uplink (`Resources/Prototypes/Catalog/UplinkCatalog/`, `Store/categories.yml`, `Locale/en-US/store/store.ftl` at `466d113`), and the [machine-interface](../architecture/systems/machine-interface.md) map. Regenerate it, don't edit it, once those change. It serves catalog rows N12 and N14 (after Playtest 1).

Paste everything under **Prompt** into Claude Design. This brief covers **content, structure and states only**. The look comes from the existing machine-interface shell and its tokens, so the result should be wireframes, not a visual design. The build maps it onto UXML against `DiegeticDeviceShell` and the vending panel's components.

## Prompt

You are laying out the screen of an **operative uplink**: a handheld shop that nuclear operatives in a multiplayer space-station game (3D, three-quarter view, after Space Station 13/14) use to spend telecrystals (TC) on gear. A player holds the uplink item and uses it, and a panel opens over the game world. The game's input is blocked while the panel is open; Esc or a close button dismisses it.

**Do not design the visual style.** The panel sits inside an existing device chassis that already has a look: a framed screen, a title block, a footer and a status row. Use plain grey wireframe boxes and system text. Your job is what goes on the screen, where, and how it behaves in each state. Fit everything inside a screen area of about 900 × 600 px at 1920 × 1080.

### Building blocks that already exist (reuse them by name)

The vending machine panel already uses these, and the uplink should be built from the same parts:

- **DeviceIdentityBlock**: the title and subtitle at the top of the screen.
- **ProductGrid / ProductCard**: a scrolling grid of cards, each with an icon, a name and a price.
- **DispenseTray**: a row that confirms where a purchased item went.
- **ActionLog**: a short scrolling log of recent actions.
- **PanelSection**: a titled group box.
- **StatusBadge / StatusBanner**: a small state label, and a full-width message bar.
- **SteelButton**: the standard button.
- **DeviceFooter**: the bottom strip.

If a state needs something these can't do, name the new component and say what it shows.

### Who sees it

Only operatives can open it. Anyone else who uses the item gets nothing, and the panel doesn't open, so there is no "access denied" screen to design. Every operative has their own uplink and their own balance; the squad doesn't share a pool.

### What the screen contains

1. **Header**: the title "Uplink", the operative's name, and the **TC balance**, large and always visible. The balance must stay readable while scrolling the catalog.
2. **Category tabs**, in this order: Weapons, Ammo, Explosives, Wearables, Medical. More categories come later (Deception, Disruption, Implants, Allies), so the tab row must allow more tabs without redesign.
3. **The listing grid** for the selected category. Each card shows: the item icon (a small render of the 3D item), its name, its price in TC, and, when relevant, its **stock** ("2 left" or "Out of stock") and a **restriction** tag ("One per squad"). A card the operative can't afford stays visible but is clearly unavailable, and shows how many TC are missing.
4. **A detail pane** for the selected card: name, a two-to-three-line description of what it does in play (for example "Compact SMG, 20-round magazine. Fits a belt."), price, stock, what's included for bundles ("SMG + 2 magazines"), and the **Buy** button.
5. **A purchase history**: every purchase in order, with time, item, cost and the balance afterwards. The design requires a real, inspectable ledger, so this can't be a transient toast. It can be a tab, a collapsible section, or the ActionLog.
6. **A delivery line** (the DispenseTray): after a purchase it says where the item went: "In your hand", or "At your feet" when both hands are full.

### Behaviour and states (draw each one)

- **Default**: Weapons tab selected, nothing selected, detail pane empty with a short hint.
- **Card selected, affordable**: detail pane filled, Buy enabled.
- **Card selected, unaffordable**: Buy disabled, reading "Need 4 more TC".
- **Out of stock**: card greyed out, Buy disabled, "Out of stock" reason.
- **Just bought**: balance updates at once, a new ledger line appears, the delivery line reports where the item is, and the panel stays open for another purchase. **One click buys**, with no confirm dialog; the ledger is the safety net.
- **Balance zero**: everything unaffordable, with one line saying so. Not a separate screen.
- **Purchase refused by the server** (stock ran out a moment ago, or the operative is downed): a StatusBanner with the reason, and the balance unchanged.

### Example data (use it in the wireframes)

Starting balance: **40 TC**. Prices come from SS14.

| Category | Item | TC | Notes |
|---|---|---|---|
| Weapons | C-20R SMG | 14 | |
| Weapons | Bulldog shotgun | 14 | |
| Weapons | L6 SAW bundle | 30 | includes 2 magazines |
| Weapons | Energy sword | 8 | melee |
| Ammo | C-20R magazine | 2 | |
| Ammo | Pistol magazine | 1 | |
| Ammo | Bulldog slug drum | 2 | |
| Explosives | C4 charge | 2 | |
| Explosives | Syndicate minibomb | 6 | throwable |
| Explosives | Explosive grenade | 4 | |
| Explosives | Syndicate bomb | 11 | example restriction and stock: One per squad, 1 left |
| Wearables | Syndicate hardsuit | 7 | |
| Wearables | Magboots | 2 | |
| Medical | Combat medkit | 3 | |
| Medical | Stimpack | 2 | |

### Out of scope

The visual style, colours and fonts (the shell owns them); war declaration and its +40 TC bonus; discounts; refunds; the PDA version of the uplink; how the item is delivered physically; sound.

### Deliverables

1. Wireframes of the seven states above.
2. A component tree for the screen, naming the reused building blocks and any new ones.
3. The list of data fields the screen needs from the server, per listing and per ledger line, so the build can define its snapshot.
