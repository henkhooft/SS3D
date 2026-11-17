> Implements: infrastructure — an advisory, independent AI review of pull requests for their human reviewers, decided by the owner in a grilling session on 2026-11-17
> Touches systems: docs governance (`.claude/skills/`, `CONTRIBUTING.md`, `.github/PULL_REQUEST_TEMPLATE.md`), CI (`.github/workflows/`, Phase 3 only)
> Status: in-progress

**Progress:** Phase 1 done (the `independent-review` skill). Phase 2 pilot run twice (v1 and the four-dimension v2) on RE-SS3D/SS3D#1529, #1530, #1532 and #1534, and marked by the owner; go / no-go open. EditMode for trusted authors' fork PRs is a separate CI change (decision 22).

# Independent review (Nov 2026)

Since the merge, every change goes through upstream review, and human reviewers open large,
agent-written PRs cold. The agent that wrote a PR also wrote its explanation, so reading the
explanation first means inheriting that agent's view of why the change is right. This effort adds
a second reader that forms its own view from the code and the project's docs first, then checks
the author's claims, and hands the human reviewer a short briefing: what the change does, the few
risks that matter for the current milestone, and what the tests do and don't show. The design specs,
system maps and `AGENTS.md` rules become the criteria it reviews against.

It is advisory. It never approves, never requests changes and never runs a PR's code. The steps
live in the skill, [`.claude/skills/independent-review/SKILL.md`](../../.claude/skills/independent-review/SKILL.md);
this doc holds the decisions, the pilot and the CI phase.

## Decisions

Settled with the owner in a grilling session, 2026-11-17.

1. **The reader is the PR's human reviewer.** The output is a briefing that helps them decide, not a
   to-do list for the author. Authors may run the same skill before opening a PR, but the bot is not
   a gate they iterate against: that turns into an automated nitpick loop. The briefing **rates the PR
   on four dimensions** (owner, 2026-11-17, after the first draft folded gameplay into one bullet):
   1. **Gameplay and design consistency**: the domain design doc, behaviour no design covers, the
      [vision](../design/vision.md) pillars (§2) and information density (§3), the milestone focus.
      SS13 precedent from model knowledge is labelled unverified and never blocks on its own.
   2. **Architectural integrity**: lifecycle, boundaries, authority, ownership, events, duplication, coupling.
   3. **Correctness and regression risk**: multiplayer, late join, scene load, destruction, and whether
      the tests prove the important behaviour.
   4. **Proportionality and maintainability**: needless complexity, debt, a simpler way, and the
      final call on whether anything found is serious enough to block.

   Each gets `Sound`, `Concerns`, `Blocking` or `n/a`, with a one-line reason.
2. **A local skill first, CI after Playtest 1.** The skill costs about a day and shows whether the
   idea works. CI wiring waits until Playtest 1 ([milestones](../milestones/INDEX.md)) has shipped
   and the pilot passed.
3. **A new skill built on `spec-review`.** `spec-review` stays the author's lightweight self-check.
   `independent-review` reuses its standards sources, smell baseline and spec brief by pointing at
   them, so they live in one place.
4. **v1 doesn't execute anything.** It reads the PR's CI results, maps the riskiest changed behaviour
   to existing tests, and writes the missing tests out as sketches. Running PR code would collide with
   [upstream-merge](2026-11_upstream-merge.md) decision 8 (fork PR code never reaches the self-hosted
   runner) and with [testing philosophy](2026-11_testing-philosophy.md) D7 (runner budgets) and D12
   (one CI run per change). A later maintainer-dispatched run (`-f pr=<n>`) is possible, not planned.
5. **The blocker bar.** A likely blocker carries evidence and breaks an `AGENTS.md` hard rule (a silent
   deviation from a design doc is one), breaks server authority or ownership, or breaks something the
   current milestone's **Done when** scenario depends on. Each dimension keeps at most one noted item,
   so at most four in all, each marked "not a merge blocker". That way gameplay notes can't be
   crowded out by code notes. A clean PR gets a table of `Sound`s and one line.
   Every finding says whether it is **confirmed** (traced end to end) or **suspected**.
6. **Blind first, then reconcile.** Pass 1 reads the diff, the touched maps (Pitfalls, `Verified`
   staleness), the cited spec and design sections, the milestone focus and CI, and never the PR body
   or commit messages. Agent-written commits often carry the full rationale. Pass 2 reads them and
   marks each claim confirmed, not shown or contradicted. Pass 2 adds roughly 5–10% to the cost:
   it checks claims against pass-1 findings and doesn't re-read the diff.
7. **Agents split by what they read, not by dimension.** The skill runs on every PR, and cost scales
   with the number of agents, not passes. **Gameplay** (dimension 1) reads the design docs, vision and
   milestone gate plus only the player-facing part of the diff. **Engineering** (dimensions 2–4) reads
   the full diff, the touched maps and the spec. Those two reading sets barely overlap, and the other
   three dimensions share one. If the pilot shows Engineering short-changing a dimension, split it.
8. **Clean context comes from the briefs.** Sub-agent briefs carry no author rationale. The skill
   refuses to run in a session that wrote the PR, or from its head branch, without `--allow-self`.
9. **Pilot output is a local file.** After the pilot, the briefing becomes one PR comment headed
   "Independent AI review — advisory, not an approval", edited in place on re-runs. It is never a
   GitHub review state, so branch protection can't turn it into a gate.
10. **Pilot protocol.** The owner writes their own assessment of each pilot PR first ("what I'd want
    a reviewer to check"), so the bot's output doesn't anchor it. They then mark every finding valid,
    false positive or accepted debt and list what it missed. Go / no-go is below.
11. **CI trigger and sandbox.** The job needs no Unity, so it runs GitHub-hosted. Same-repo PRs run
    on open and ready-for-review; fork PRs run when a maintainer dispatches it with `-f pr=<n>`, never
    `pull_request_target`. The review agent reads PR text that outside contributors wrote while the job
    holds an API key, so it gets read-only file tools only: no shell, no network, no write tools. The
    workflow pre-fetches CI results into files, and posting the comment is a separate step.
12. **API key and budget.** The owner's key for now, recorded as a role in
    [project infrastructure](2026-11_project-infrastructure.md) like the Unity secrets, with a
    spending cap per run.
13. **Size gate.** Diffs over about 3,000 changed lines get a notice ("split the PR or review by hand")
    instead of a review. *Refined while writing the skill:* Unity serialized files (`.unity`, `.prefab`,
    `.asset`, `.meta` and similar) don't count toward the cap. Map PRs would otherwise always trip it,
    and their YAML isn't readable review material anyway.
14. **Feedback.** Each posted finding ends with `valid · false positive · accepted debt` checkboxes
    that maintainers tick. A script tallies them into this doc on demand. Accepted debt reaches
    [TECH_DEBT.md](TECH_DEBT.md) only when a human moves it.
15. **Re-runs.** Once on open or ready-for-review; after that only on a maintainer dispatch.
    Running it again edits the same comment.
16. **A regression set guards the reviewer.** The pilot PRs and their marked-up findings stay as a
    fixed set. Any change to the skill re-runs on them by hand, and the result is recorded below.
17. **Docs-only PRs** skip in CI, as `Run Tests` does under D12. The skill still runs on them when
    someone asks.
18. **Lean routes.** Diffs of up to 300 counted lines are reviewed inline by the orchestrator, with no
    sub-agents. The order (blind pass, then claims) stays the same; only the extra contexts go.
    The Gameplay agent is skipped when the PR changes nothing player-facing: dimension 1 is then `n/a`.
    The diff is written to a file once, with serialized Unity files left out. Sub-agents read only the
    docs in the sources file and the code the diff touches, and go further only to trace a finding.

19. **Decisions are listed, not guessed** (owner, 2026-11-17, after the pilot). In both pilot runs the
    gameplay questions were mostly choices the owner had made in an agent session that never reached
    a design doc: the Syndicate bomb's department size, how external airlocks open, open floor halves.
    The PR template gains a **Decisions** section, where the author lists each choice no doc settles and
    where it is now recorded. Pass 1 lists the decisions it reads from the code; pass 2 matches the two.
    Unrecorded ones go to the briefing's **Decisions for you** (at most three), on its first screen with
    the verdict and blockers. This turns most of dimension 1 from the reviewer's opinion into a check.
20. **Every brief carries the blocker bar.** In the v2 run, readers without it filed a confirmed
    server-authority hole (#1534) and an `AGENTS.md` hard-rule break (#1529) as notes, and the rating
    step had to promote them. The bar also names gate-doc **Done when** lines (test-server T3) as
    milestone criteria, since #1530's hitch only fails there.
21. **Staleness counts commits to the PR's own files.** Counting a map's whole code paths marked every
    touched map unverified in the pilot (up to 218 commits), so "drop findings that rest on an unverified
    map" covered everything. The gameplay diff also carries changed prefab and asset values: #1529's was
    seven files while its door behaviour lived in prefab fields.
22. **Tests have to run for the review to mean much.** All four pilot PRs came from the owner's fork, so
    every Unity job waited for a maintainer and every "tests pass" claim came out "not shown". EditMode
    should run on every upstream PR: fork PRs by people with write access get it dispatched
    automatically (`ci/trusted-pr-editmode`), which refines [upstream-merge](2026-11_upstream-merge.md)
    decision 8 on the same reasoning as same-repo PRs.

## Phases

### Phase 1: the skill — done

- [x] [`independent-review`](../../.claude/skills/independent-review/SKILL.md): pin and guard, size
  routes, blind sources, Gameplay + Engineering agents (or inline), reconcile, four-dimension rating,
  briefing template
- [x] Listed in [.claude/skills/README.md](../../.claude/skills/README.md); `review-out/` git-ignored;
  `CONTRIBUTING.md` step 5 mentions the advisory review

### Phase 2: pilot

- [ ] Owner writes a blind assessment of each pilot PR. *Skipped:* the owner marked the briefings
  directly, so "raised something the owner hadn't" and the Missed column are not measured.
- [x] Run the skill on each in a fresh session; save the briefings (2026-11-17, v1 and v2, dry run, local files)
- [x] Owner marks every finding: all valid except #1532's open-half look, accepted debt
- [ ] Decisions 19–21 folded into the skill and the PR template; re-run on the set (Regression set)
- [ ] Go / no-go, recorded here

| PR | What it tests | Owner's blockers | Bot blockers (valid / false) | Noted (valid / false) | Missed |
|---|---|---|---|---|---|
| RE-SS3D/SS3D#1529 department airlock skins and the external airlock | map/prefab work against `AGENTS.md` hard rules | not recorded | 1 / 0 (recipes off the Run All aggregator) | 4 / 0 | not measured |
| RE-SS3D/SS3D#1530 Syndicate bomb rework | perf and destruction regressions; a gameplay-size question | not recorded | 1 / 0 (server stall vs T3, suspected) | 5 / 0 | not measured |
| RE-SS3D/SS3D#1532 airlock floor halves | map/prefab work; spec match | not recorded | 0 / 0 | 3 / 0, 1 accepted debt (open halves look damaged) | not measured |
| RE-SS3D/SS3D#1534 worn storage through the server | a small networking fix: authority | not recorded | 1 / 0 (foreign storage opens on range alone) | 3 / 0 | not measured |

The table counts the v2 briefing. Cross-PR: #1532 and #1534 both took protocol 62. No near-empty PR is in
the set any more (the earlier #1534 description was wrong), so "can it say nothing to flag" is still untested.

**Go when**, across the set:

- at least half of the bot's likely blockers are valid;
- at most one false-positive blocker per PR;
- it raised at least one thing the owner hadn't.

All four pilot PRs are the owner's agent-assisted work. The first upstream PR by someone else
joins the set as a fifth case before Phase 3 goes live. The pilot doesn't wait for one.

### Phase 3: CI (after Playtest 1 and a "go")

- [ ] Fifth regression case: the first upstream PR by another contributor
- [ ] Workflow: GitHub-hosted; `pull_request` (opened, ready_for_review) for same-repo PRs,
  `workflow_dispatch` with `pr` for fork PRs and re-runs; skip docs-only PRs and the size gate (decisions 11, 13, 15, 17)
- [ ] Agent step with read-only file tools only; CI results pre-fetched to files; a separate
  step posts or edits the one comment (decisions 9, 11)
- [ ] API key secret and per-run spending cap; role row in [project infrastructure](2026-11_project-infrastructure.md) (decision 12)
- [ ] Tally script for the feedback checkboxes (decision 14)
- [ ] Sync [CONTRIBUTING.md](../../CONTRIBUTING.md) and the [CI pipeline](2026-07_ci-develop-release-pipeline.md) doc

## Regression set

Results of re-running the skill on the set after a change to it: the date, the change, whether every
valid blocker is still found, and any new false-positive blockers.

- **2026-11-17, v1** (Conformance + Risk, eight agents, ~880k tokens): 3 blockers, all later marked valid.
- **2026-11-17, v2** (four dimensions, Gameplay + Engineering, seven agents, ~940k tokens): the same 3
  blockers; added the gameplay questions (bomb size, external door access and opening, open halves) and a
  host drag re-lock in #1530. Single readers under-rated two blockers (decision 20). The inline route
  saved nothing, since the orchestrator had read #1534's body and handed it to a fresh agent.

## Risks

- **Stale maps produce confident wrong findings.** The skill marks maps with code commits since
  their `Verified` stamp as unverified and drops findings that rest only on such a map. Fixing the
  map through `update-system-docs` is still the cure.
- **A reviewer the team ignores is cost without value.** The go / no-go bar and the feedback tally
  exist to catch that early. Twenty comments with one useful one is a failure, even if the one is good.
- **Prompt injection in the CI phase.** Decision 11's tool limits are the guard. Giving the agent
  "just `gh`" or a shell undoes it.
