> Implements: infrastructure — an advisory, independent AI review of pull requests for their human reviewers, decided by the owner in a grilling session on 2026-11-17
> Touches systems: docs governance (`.claude/skills/`, `CONTRIBUTING.md`), CI (`.github/workflows/`, Phase 3 only)
> Status: in-progress

**Progress:** Phase 1 done (the `independent-review` skill). Next: the Phase 2 pilot on RE-SS3D/SS3D#1529, #1530, #1532 and #1534.

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
   a gate they iterate against: that turns into an automated nitpick loop.
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
5. **The blocker bar.** A likely blocker carries evidence and breaks an `AGENTS.md` hard rule, breaks
   server authority or ownership, or is a regression that threatens the current milestone focus.
   At most 3 other items are noted, each marked "not a merge blocker". A clean PR gets one line.
   Every finding says whether it is **confirmed** (traced end to end) or **suspected**.
6. **Blind first, then reconcile.** Pass 1 reads the diff, the touched maps (Pitfalls, `Verified`
   staleness), the cited spec and design sections, the milestone focus and CI, and never the PR body
   or commit messages. Agent-written commits often carry the full rationale. Pass 2 reads them and
   marks each claim confirmed, not shown or contradicted. Pass 2 adds roughly 5–10% to the cost:
   it checks claims against pass-1 findings and doesn't re-read the diff.
7. **Two pass-1 agents.** **Conformance** (Standards + Spec) and **Risk** (correctness, tests,
   gameplay and milestone), each given only the touched maps and cited sections. Cost scales with
   the agent count, not the pass count. If the pilot shows Conformance missing Spec findings, split it.
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

## Phases

### Phase 1: the skill — done

- [x] [`independent-review`](../../.claude/skills/independent-review/SKILL.md): pin and guard, blind
  sources, two pass-1 agents, reconcile, blocker bar, briefing template
- [x] Listed in [.claude/skills/README.md](../../.claude/skills/README.md); `review-out/` git-ignored;
  `CONTRIBUTING.md` step 5 mentions the advisory review

### Phase 2: pilot

- [ ] Owner writes a blind assessment of each pilot PR
- [ ] Run the skill on each in a fresh session; save the briefings
- [ ] Owner marks every finding and lists misses; fill in the results table below
- [ ] Go / no-go, recorded here

| PR | What it tests | Owner's blockers | Bot blockers (valid / false) | Noted (valid / false) | Missed |
|---|---|---|---|---|---|
| RE-SS3D/SS3D#1529 external airlocks on the ministation | map/prefab work against `AGENTS.md` hard rules | | | | |
| RE-SS3D/SS3D#1530 big-blast hitch | perf and destruction regressions; the Risk angle | | | | |
| RE-SS3D/SS3D#1532 airlock floor halves | map/prefab work; spec match | | | | |
| RE-SS3D/SS3D#1534 protocol bump to 62 | a near-empty PR: can it say "nothing to flag"? | | | | |

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
valid blocker is still found, and any new false-positive blockers. Empty until the pilot fills the set.

## Risks

- **Stale maps produce confident wrong findings.** The skill marks maps with code commits since
  their `Verified` stamp as unverified and drops findings that rest only on such a map. Fixing the
  map through `update-system-docs` is still the cure.
- **A reviewer the team ignores is cost without value.** The go / no-go bar and the feedback tally
  exist to catch that early. Twenty comments with one useful one is a failure, even if the one is good.
- **Prompt injection in the CI phase.** Decision 11's tool limits are the guard. Giving the agent
  "just `gh`" or a shell undoes it.
