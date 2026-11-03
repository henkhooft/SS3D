> Implements: infrastructure — testing policy for AI-driven development; operational runbooks stay in [Testing/ui/README.md](../../Testing/ui/README.md) and [systems/networking-session-testing.md](systems/networking-session-testing.md)
> Touches systems: networking-session, rounds-lobby, ui-shell, all domains with tests
> Status: shipped

# Testing philosophy

The harness exists so an agent can change code and quickly learn whether it broke something, then
keep iterating. Regressions get caught because the checks run often, not because any single run is
exhaustive. Wall-clock time and tokens are the budget. A slow check that rarely fails costs something
on every run and slows development, which is the current priority.

**Rule:** test each behaviour in the cheapest tier that can prove it. Agents run targeted checks.
Expensive suites run nightly, and a regression caught there is acceptable for now.

## Tiers

| Tier | Proves | Doesn't do | Where it runs |
|------|--------|------------|---------------|
| **1. EditMode** | Given these inputs, expect these outputs: rules, state machines, calculations, data and asset validation. | Scenes, frames, networking. | Every change (whole suite, under ~1 min). CI on PRs. |
| **2. Scenarios** | Behaviour that spans systems, in one Play Mode host: e.g. spawn a medkit in hand, hurt the player, apply the bandage (console + inventory + health). | Separate client/server processes. Lobby and round flow, except where that is the subject. | Targeted: the scenarios for the systems a change touched. All of them nightly. **Not built yet.** |
| **3. Multiplayer** | What only breaks when client and server are separate processes: late join, reconnect and body reclaim, ownership, state syncing to observers. | Gameplay that a host scenario already proves. | Nightly (`multiplayer-smoke-test.yml`). Locally only when the change touches networking or session contracts. |
| **4. Visual** | Rendered UI and screens look right. Screenshots are evidence for a human or agent to review. | Act as the pass/fail gate for logic. | Only when the change is visual (UI Toolkit, USS, rendering, screen effects). |

## Rules

1. **Cheapest tier first.** Before adding a check to tier 2–4, ask whether a pure seam could make it
   an EditMode test. Extracting that seam is usually worth it.
2. **Expensive tiers cover several systems at once.** Tiers 2–3 pay a large setup cost (Play Mode, a
   station, a round, a build). A check that pays it should exercise interactions between several
   systems, not one isolated rule.
3. **Pay setup once.** A scenario run boots once into a test room and runs many checks. Going through
   lobby, ready and round start belongs only to the checks about the lobby and round flow.
4. **Expensive tests justify their cost.** Every permanent tier 2–4 check names, in its header
   comment, the contract or regression it guards and why a cheaper tier can't prove it. A check
   written to validate a feature during development is temporary by default. It stays only if the
   area breaks easily or it caught a real regression. A permanent check covering stable code that
   hasn't failed in a long time should move to a cheaper tier or be deleted.
5. **Assertions decide; screenshots inform.** Take screenshots only in tier 4. Agents read them only
   when the diff is visual.
6. **Keep output short.** A pass is one line. A failure says what failed, where, and which log to read.
   Long reports go to files, not into the agent's context.
7. **Fix the cause, never the gate.** Don't allowlist an error, loosen an assertion or add a
   known-issue entry to make a run green without saying so.

## Agent loop

After a change, with the editor open:

1. `Testing/ui/validate.sh --steps compile,editmode`
2. Add the focused steps or scenarios for the systems the change touched (the step list is in the
   [UI runbook](../../Testing/ui/README.md#validation-loop-agents-start-here)). Add `visual` only for
   visual changes, `lifecycle` only for lobby/round/session changes.
3. Run a multiplayer smoke scenario only when the change touches networking, spawning or session
   contracts: the one scenario that covers it, not the whole set.

Don't run `--full` unless the owner asks for it. It takes about 25 minutes warm.

## Current state

The rework that brings the harness in line with this policy is
[2026-11_test-harness-rework.md](2026-11_test-harness-rework.md).


- **EditMode** fits tier 1 and is the most effective suite.
- **PlayMode** (`Assets/Scripts/Tests/PlayMode`) is mostly round start/stop timing, which the
  lifecycle and sandbox pilots and two smoke scenarios also cover. It is under review against rule 4.
- **Pilots** (`LobbyUiPilot`, `LobbyLifecyclePilot` and its partial files) mix tier 2 and tier 4, and
  the lifecycle pilot has absorbed feature validation (speech, ragdoll, reclaim, overlay, respawn).
  They are under review against rules 2–4.
- **Tier 2 scenarios** don't exist yet. The building blocks do: console commands
  (`Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/`) and the `AutomationSubSystem` DSL.
- **Nightly CI** runs the smoke set only. PlayMode and the pilots need a graphical editor and run
  only through `validate.sh`.
