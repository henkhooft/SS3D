# Networking — design document

> Status: active

Formalizes a gap `cryogenics.md` gestures at without ever owning it: its own worked example B says
a disconnected character "remains in the world, vulnerable, briefly," and §4 there says "a brief
drop and reconnect within the window does nothing at all" — without specifying what either of those
actually looks like. This doc is that specification: the connection lifecycle itself, from the
moment a client drops to the moment it's either back or `cryogenics.md` §4's grace period has
closed and that doc's own mechanism takes over.

## 1. Design philosophy

**This isn't fixing a BYOND artifact — it's formalizing a decision that's only ever lived in code.**
Unlike most of what this project resolves, SS13's own client-server model isn't the thing to test
against; this project already runs on a real client-server architecture. What's never been written
down is what a disconnected character *is* while nobody's driving it, and this doc is that write-up.

**A disconnected character is a real, visible fact, not a silent vanishing or a suspended
non-entity.** The same "let the world carry the information" discipline that puts wounds on a body
and lighting state on a room applies here: nobody needs a UI element to learn a nearby character
stopped responding, because the character itself already shows it.

**Reconnecting is resuming, not rejoining — the short-window sibling of `cryogenics.md` §5's own
framing.** That doc already commits to this for the *longer* window, after a character's been moved
into cryo storage. This doc is the identical principle for the window before that: the body never
paused or moved anywhere, it just sat there unpiloted, and getting control back is instant, not a
lobby screen.

**One shared trigger hands off to `cryogenics.md`, not two competing timers.** This doc owns the
window from disconnect to the moment that doc's grace period elapses. The instant it elapses,
`cryogenics.md` §4's mechanism takes over completely — relocation, pod entry, job slot freed. There's
no overlap to reconcile between the two.

## 2. Disconnection — what the body does

**The character stays exactly where it was, fully physically simulated.** It can still be pushed,
damaged, dragged, or examined — no special ragdoll state, no despawn, no invulnerability. Nothing
about the world's physics or interaction rules cares that nobody's controlling it, the same way
nothing changes about an unconscious character's physical presence either.

**A real, diegetic tell renders on the model itself.** The character's idle animation changes —
posture slackens, head-tracking and idle fidgets stop — the same "body is the doll" principle
already governing every other visible character state (`main-hud.md` §5). Anyone looking at a
disconnected character can tell something's wrong without examining them or consulting a device;
this is a physical fact, not a status lookup.

**No control input reaches the character from anyone.** This is the one hard rule: whatever caused
the disconnect, nobody else can drive that body in the interim. Physical interaction *with* it
(searching, dragging, restraining) works exactly as it would on anyone else — the character being
unpiloted doesn't grant or remove any access, item, or state it didn't already have.

## 3. The reconnection window — resuming, not rejoining

**Reconnecting before `cryogenics.md` §4's grace period elapses hands control straight back.** No
prompt, no lobby screen, no latejoin interface — the same player, the same character, exactly where
and how they left it. The tell from §2 clears the instant control resumes.

**This is a different case from `cryogenics.md` §5's own "resume."** That section covers reconnecting
*after* the character has already been moved into cryo storage — a real, physical relocation has
happened by then. This section is the simpler, earlier case: nothing has moved or paused yet, so
there's nothing to walk out of. Two windows, one shared philosophy, no mechanism duplicated between
them.

**Once the grace period closes, this doc's window is over.** Control cannot resume here anymore;
from that instant, `cryogenics.md` §4 owns what happens to the character entirely.

## 4. Joining fresh vs. reconnecting — two different doors

**Lobby's job-list interface (`lobby.md` §7) is for players who don't already have a character in
this round** — a fresh join, or latejoin into a currently-open slot. Reconnecting to an existing,
still-present character (§3) never touches that interface at all — the same distinction
`cryogenics.md` §5 already draws for its own resume case, just one step earlier in the timeline.

## 5. Server capacity

**A server has a real, fixed maximum connected-player count.** A connection attempt beyond that is
rejected outright, with a clear reason given to the player attempting to join — no queueing system,
no soft admission that quietly degrades performance for everyone already connected. This is a
deliberate simplicity choice, not an oversight: a queue is a real feature with its own UX (position,
estimated wait, cancel) that this pass doesn't need to justify building.

## 6. HUD & touchpoints

No new permanent chrome.

- The disconnected-character tell (§2) renders on the model itself — no icon, no HUD overlay, no
  admin-only indicator required to see it.
- A rejected connection attempt at capacity (§5) is a real, legible message at the point of
  connecting, not a silent failure to join.
- Existing admin tooling already covers anything deeper an admin might need — stealth observation
  (`admin-tools.md` §4) can already confirm a suspicious idle character is genuinely disconnected
  rather than just standing still on purpose; no new admin surface is designed here.

## 7. Worked examples

**A — A brief connection hiccup:**

| Step | What happens | State |
|---|---|---|
| 1 | A player's connection drops for a few seconds | Character's idle tell appears (§2); no one else can control the body |
| 2 | Connection re-establishes well within `cryogenics.md` §4's grace period | Control resumes instantly, no prompt |
| 3 | The tell clears | Nothing about the character's state, inventory, or position changed |

**B — A longer disconnect, handed off to cryogenics:**

| Step | What happens | State |
|---|---|---|
| 1 | A player disconnects and doesn't return | Idle tell persists; character remains a real, vulnerable physical presence |
| 2 | `cryogenics.md` §4's grace period elapses | This doc's window closes; that doc's automatic relocation-and-storage mechanism takes over completely |
| 3 | Player reconnects much later | Resolves through `cryogenics.md` §5's own resume flow, not this doc's §3 |

**C — Server at capacity:**

| Step | What happens | State |
|---|---|---|
| 1 | A server is already at its configured maximum | No slots available |
| 2 | A new player attempts to connect | Connection is rejected with a clear reason, no queue offered |

## 8. Integration notes

| Networking element | Touches existing / needed system |
|---|---|
| Disconnected-character tell | "Body is the doll" precedent (`main-hud.md` §5) |
| Reconnection window, handoff | `cryogenics.md` §4 (grace period), §5 (the longer-window sibling case) |
| Fresh join / latejoin boundary | `lobby.md` §7 |
| Connection-status field this doc's states feed | `id-access.md` §2, `pda.md` §3 (already generic; this doc doesn't add a new value) |
| Deeper inspection if needed | `admin-tools.md` §4 (stealth observation), not a new surface |

**Companion edit needed:** `cryogenics.md`'s own worked example B ("Character remains in the world,
vulnerable, briefly") and §4's "a brief drop and reconnect within the window does nothing at all"
should both cite this doc as the specification of what "briefly" and "nothing at all" actually mean.

## 9. Out of scope for this pass

- **Exact grace-period duration** — already `cryogenics.md` §10's own out-of-scope item; this doc
  doesn't duplicate that number.
- **Netcode-level concerns** — prediction, rollback, lag compensation, interpolation. These are
  implementation, not a gameplay decision this doc is positioned to make.
- **Voice chat networking** — `comms.md` §10's own territory, unextended here.
- **Server browser, matchmaking, or cross-server features** — this project targets direct-connect to
  a single dedicated server; nothing here assumes more than that.
- **A connection queue for a full server** — deliberately not designed (§5); reject is the whole
  mechanic this pass commits to.
- **Anti-cheat specifics** — every other doc in this project already assumes server authority over
  outcomes; this doc doesn't add a second, parallel claim about it.
- **Exact maximum-player-count value** — a hosting/balancing decision, not fixed here.
