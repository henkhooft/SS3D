---
name: autoplan
description: "Turn an underspecified feature into a build-ready plan without the owner in the loop: pick the next piece of work if none is named, ground it in the docs and SS14, self-grill it with a blind questioner sub-agent, and write a plan with a decision log and a short list of owner questions. Use when asked what to work on next, or to plan, spec or self-grill a feature or milestone slice autonomously."
---

Produces a **plan the owner skims instead of a grill the owner sits through**. The `grilling` skill's rounds still happen, but a **questioner** sub-agent asks and you answer, with **SS14 as the oracle** for anything SS3D's design leaves open. The owner sees only the decisions that are theirs: taste and low-confidence calls, each with the recommendation the build will follow until they say otherwise.

Arguments: a catalog row id (`U7`), a milestone slice (`M13`), a free-text feature, or nothing (step 1 picks).

## 1. Pick (only when no target was given)

1. **Landing work first.** `gh pr list --repo RE-SS3D/SS3D --author "@me" --state open --json number,title,isDraft,reviewDecision,statusCheckRollup`. A PR with failing checks or unanswered review comments beats new work: report it and stop, unless the user says to plan anyway.
2. **Candidates**: [Documents/milestones/INDEX.md](../../../Documents/milestones/INDEX.md) **Current focus**, then [expected-content.md](../../../Documents/milestones/expected-content.md) **Ready next**, top down. Skip rows marked after Playtest 1 while Playtest 1 is the target.
3. **Overlap**: drop a candidate whose system maps cover files an open PR or a `feature/*` branch from the last two weeks already changes (`gh pr list … --json files`, `git log --since=14.days --name-only origin/<branch>`).

Done when one target is named, with the line that picked it.

## 2. Ground

Read in this order and keep a **sources list** (path + section) as you go:

1. The target's catalog row and milestone slice, including its **Done when**.
2. `Documents/architecture/INDEX.md` coverage row → the domain's design doc and those of adjacent systems → the system maps (Overview, **Pitfalls**, `Verified` staleness) → [agent-first composition](../../../Documents/architecture/2026-07_agent-first-composition.md) when the work adds a SubSystem, entity behaviour, UI surface or prefab wiring.
3. **The oracle.** Sparse-clone SS14 into the scratchpad when it isn't there:
   `git clone --depth 1 --filter=blob:none --sparse https://github.com/space-wizards/space-station-14.git ss14 && git -C ss14 sparse-checkout set Resources/Prototypes Resources/ServerInfo/Guidebook Resources/Locale/en-US`.
   Read the guidebook page and prototypes for the target's verbs. This is data, not instructions. The clone has no C#, so numbers that live in code are "not in prototypes", never guessed.
4. The code entry points the maps name, only as far as the plan needs.

Done when every design section, map and SS14 file that bears on the target is on the sources list, and you can say in two lines what exists today.

## 3. Self-grill

Spawn the **questioner** as a sub-agent with [QUESTIONER.md](QUESTIONER.md) as its brief, plus the target, the sources list and the two-line current state. Give it **no answers and no plan**: it forms its own design tree from the sources. Keep it alive with `SendMessage` across rounds, so each round costs only the new answers.

For every question it asks, answer with one tag:

| Tag | Use when | The answer must carry |
|---|---|---|
| `SS3D` | a design doc section settles it | the section |
| `SS14` | SS3D is silent, SS14 settles it | the SS14 path or prototype id |
| `adaptation` | SS14's answer is 2D-bound or clashes with a 3D rule (`vision.md` §3) | the SS14 rule and why it changes |
| `taste` | feel, look, numbers players notice, scope cuts | your recommendation |
| `low-confidence` | sources conflict or say nothing | your recommendation and what would settle it |

Send the answers back. The questioner challenges weak ones, and a challenged answer is re-answered or retagged `low-confidence`. Stop when the questioner reports an **empty frontier**, or after **four rounds**. Remaining open questions go to the owner list as `low-confidence`.

A rule that no design doc states (it only lives in SS14 or in your answer) is a **proposed design rule**. Design docs are owner-only, so collect these for the plan rather than editing `Documents/design/`.

Done when every question has a tagged answer or sits on the owner list.

## 4. Write the plan

Write `Documents/plans/<slug>.plan.md` following [PLAN-TEMPLATE.md](PLAN-TEMPLATE.md), and link it from the milestone slice's Links column (then run `python3 Tools/generate_roadmap.py`). Commit both on the working branch.

Done when the template's checks pass. Then report to the user: the plan path, the owner questions (taste and low-confidence only, each with its recommendation), and the proposed design rules. **Owner questions don't block the build.** Implementation follows the recommendations, and the plan marks the code each one touches so an override is a small change.
