> Implements: infrastructure — a development loop in which agents pick, plan, build and validate work and the owner steers, decided in conversation with the owner on 2026-11-18
> Touches systems: docs governance (`Documents/milestones/`, `.claude/skills/`), CI (`.github/workflows/`), the test harness (`Testing/ui/`)
> Status: in-progress

**Progress:** stages 1–2 built (catalog, `autoplan`). Stage 3 is on its first trial (M13 on a second editor machine). The CI step for fork PRs is in RE-SS3D/SS3D#1542. Stage 5's gate (`independent-review`) is a go (owner, 2026-11-18).

# Autonomous development loop (Nov 2026)

The owner was the bottleneck at every step: deciding what to work on, specifying it in a live grilling session, steering the build and validating it by hand. The editor was a second bottleneck, because one Unity instance ran every test. This effort moves the owner to **steering**: ranking the backlog, answering the few decisions that are theirs, and hand-testing feel. Agents do the rest, in parallel where the work allows.

## The loop

| Stage | What happens | Where | Owner's part |
|---|---|---|---|
| 1. Pick | Landing work first (open PRs with failing checks or review comments), then the milestone **Current focus**, then the catalog's **Ready next**, skipping work that overlaps open branches | any session | ranks the catalog |
| 2. Plan | `autoplan`: ground the target in the docs and SS14, run a self-grill with a blind questioner sub-agent, write a plan with a tagged decision log | any session | answers taste and low-confidence questions asynchronously; none of them block |
| 3. Build, blind lane | Code, data, docs and EditMode tests, test-first | cloud session or any machine; no Unity needed | — |
| 4. Build, rig lane | Prefab wiring through recipes, PlayMode, MPPM, scenarios, the targeted smoke line | a machine with the Unity editor | — |
| 5. Review | `independent-review` from a fresh session, against design, architecture, correctness and proportionality | any session | reads the briefing |
| 6. Accept | Hand test where the plan says feel or visuals need one, then a PR to RE-SS3D | owner | hand test, merge |

## Decisions

1. **SS14 is the default spec.** When SS3D's design is silent, SS14's prototypes and guidebook settle the question, and the plan cites them. SS14 beats /tg/station because it's the modern version of the same game. Agents escalate only 3D adaptations, taste and scope cuts.
2. **The catalog is the backlog.** [expected-content.md](../milestones/expected-content.md) lists the interactions a Nuke Ops round should have, crossed against SS3D and the art. Work outside it, or outside the milestone focus, is dilution unless the owner asks for it. Conflicts between SS14 and a recorded SS3D decision are settled by the owner in the catalog's **Decisions for the owner**, not by an agent.
3. **The self-grill keeps the questioner blind.** The questioner sub-agent reads the sources but never the answerer's plan, and challenges uncited or contradicted answers. The value of a grill is the pushback, and an agent grilling itself agrees with itself. The grill stops after four rounds; what's left goes to the owner list.
4. **Owner questions don't block.** The build follows each recommendation, and the plan names the todos it touches, so an override is a small change.
5. **Design rules stay owner-only.** A rule that only lives in SS14 or in a plan is a **proposed design rule** in that plan. It becomes design when the owner moves it into `Documents/design/`.
6. **Two lanes, split by what needs a running game.** Every todo is tagged `blind` or `rig`. Blind work comes first, so a build needs only one rig visit. A second machine with an editor makes a second rig.
7. **CI is the blind lane's test run.** Compile and EditMode run in CI on push, so a cloud session doesn't need Unity. For PRs from a maintainer's fork this needs RE-SS3D/SS3D#1542, which dispatches EditMode for trusted authors. The rig re-runs EditMode itself after its own changes.
8. **Landing beats starting.** A session asked what to do next first checks open PRs for failing checks or review comments. Parallel sessions that only start work grow the owner's queue faster than it drains.
9. **`independent-review` is the stage-5 gate** (owner, 2026-11-18, after the pilot). Its branch is merged into this effort's; the CI phase still waits for Playtest 1.
10. **Tokens are measured per stage.** The owner's budget is real, so every trial reports tokens per stage. First measurements: catalog extraction about 266k (two agents); M13 self-grill questioner about 117k over four rounds; pinpointer (M14) questioner about 91k over three rounds; independent review about 250k per PR.

## Open

- **Merge on green.** Can work that passes its tests and doesn't touch feel or visuals merge without the owner's hand test? Until that's answered, the hand-test queue limits throughput.
- **The first end-to-end trial.** M13 ([plan](../plans/drag-and-zero-g.plan.md)) is being built on a second editor machine. Its report gives the real per-stage token costs and shows where the blind/rig split leaks.
- **Teleport or a local runner.** The rig visit is a manual hand-off today. A small runner that picks up branches marked ready for validation would remove the owner from it; build it only if the hand-off proves to be the bottleneck.

## Pieces

- Catalog: [Documents/milestones/expected-content.md](../milestones/expected-content.md)
- Planning skill: [.claude/skills/autoplan/](../../.claude/skills/autoplan/SKILL.md), with its questioner brief and plan template
- First plan: [drag-and-zero-g.plan.md](../plans/drag-and-zero-g.plan.md)
- Review gate: [.claude/skills/independent-review/](../../.claude/skills/independent-review/SKILL.md), [effort](2026-11_independent-review.md)
- CI for fork PRs: RE-SS3D/SS3D#1542
