---
name: independent-review
description: "Advisory independent review of a pull request, written for its human reviewer: rates gameplay and design consistency, architectural integrity, correctness and regression risk, and proportionality, from a blind read of the code and docs before the author's explanation. Use when asked for an independent, advisory or second-opinion review of a PR."
---

Produces a **briefing for the PR's human reviewer** that rates the PR on four dimensions, names the few risks worth their attention and checks the author's claims. It is advisory: the output is a file the user reads, it approves nothing and it never runs the PR's code. Decisions and pilot status: [2026-11_independent-review](../../../Documents/architecture/2026-11_independent-review.md).

The leading word is **blind**: pass 1 forms its own view from the code and the docs before anything sees the author's reasoning. The second is **lean**: this runs on every PR, so each step reads only what its dimension needs, and small PRs use no sub-agents at all.

Arguments: a PR number on `RE-SS3D/SS3D` (required); `--out <dir>` (default `review-out/`, git-ignored); `--allow-self` to override the self-review guard.

## The four dimensions

1. **Gameplay and design consistency.** Does the player-visible behaviour match the domain's design doc? Does it add behaviour players will notice that no design doc covers ("needs an owner decision", not "wrong")? Which [vision](../../../Documents/design/vision.md) pillar (§2) does it serve, and does it make something harder to read than the 2D original would (§3)? Does it fit the current milestone focus or the related systems it touches? SS13 precedent from model knowledge is allowed, labelled "SS13 precedent, unverified", and never a blocker on its own.
2. **Architectural integrity.** Lifecycle, dependency boundaries, server authority and ownership, event patterns; duplicated functionality or needless coupling. Standards and smells: `.claude/skills/spec-review/SKILL.md` § 3. Conformance to the plan or effort it implements: that skill's Spec brief in § 4.
3. **Correctness and regression risk.** Multiplayer (client-reachable `ServerRpc`s and input handlers that change server state without validation; state that late joiners and observers miss, such as RPC-only state or SyncVar initial values), scene load, despawn and destroy order, old saves and clients (`ProtocolVersion.Current`, `PersistenceEnvelope.CurrentSchemaVersion`), each touched map's **Pitfalls**, hot paths. **Tests:** do the tests and CI actually prove the riskiest changed behaviour? Each gap becomes a test sketch in the cheapest tier that proves it ([testing philosophy](../../../Documents/architecture/2026-11_testing-philosophy.md) § Tiers).
4. **Proportionality and maintainability.** Needless complexity, new debt, a simpler approach that would do. Then the final call this dimension owns: are the imperfections found in 1–4 serious enough to block?

**Ratings:** `Sound` (nothing worth a reviewer's time) · `Concerns` (noted items only) · `Blocking` (at least one likely blocker) · `n/a` (the PR doesn't touch this dimension; say why in three words).

## 1. Pin the PR

1. Find the remote that points at `RE-SS3D/SS3D` (`git remote -v`), or use the URL directly. Fetch `develop` and `pull/<n>/head` into local refs (`refs/review/…`). Work with `git diff` and `git show <ref>:<path>`; leave the working tree and the current branch alone.
2. Read the PR's metadata **without its body**: title, head and base branch, files, CI check conclusions (`gh pr view <n> --repo RE-SS3D/SS3D --json title,headRefName,baseRefName,files,statusCheckRollup`, or the GitHub MCP `pull_request_read` tool). Without API access, carry on and mark the claims and CI sections "not available".
3. **Self-review guard:** stop and ask for a fresh session when this conversation wrote or edited any of the PR's commits, or the current branch is the PR's head branch. `--allow-self` overrides; say so in the header.
4. **Size and route.** Count changed lines in the merge-base diff, excluding Unity serialized files (`*.unity`, `*.prefab`, `*.asset`, `*.mat`, `*.meta`, `*.controller`, `*.anim`):
   - over **3,000** → write the one-line "too large for an automated review; split the PR or review by hand" briefing and stop;
   - up to **300** → **inline route**: you run pass 1 yourself, no sub-agents;
   - otherwise → **agent route**.
5. Write two diff files to `--out`, serialized files left out of both (list their paths instead):
   - `pr-<n>.diff`: everything else;
   - `pr-<n>.gameplay.diff`: player-facing changes only: runtime code under `Assets/Scripts/SS3D/` outside `Editor/` and `Systems/Testing/`, UXML/USS, and `Builds/Game/Data/`. Empty means dimension 1 is `n/a` and its agent doesn't run.

## 2. Collect the blind sources

Collect **paths and short facts**; pass 1 does the reading.

- **System maps:** each `Documents/architecture/systems/*.md` whose `> Code paths:` header covers a changed file (one grep over the headers). For each, count code commits since its `> Verified:` stamp (`git log --oneline <sha>..<develop> -- <code paths>`). Any → **unverified**: check its claims against code before relying on them.
- **Spec:** a plan or effort matching the head branch, title or domain; `Documents/…` paths the PR body links, extracted without reading the prose (`… --jq .body | grep -oE 'Documents/[A-Za-z0-9_./-]+\.md(#[A-Za-z0-9_-]+)?' | sort -u`); the design sections their `Implements:` lines cite; the domain's `Documents/design/<domain>.md`.
- **Milestone:** `Documents/milestones/INDEX.md` § Current focus and the gate doc it names.
- **CI:** conclusions, the last ~30 lines of any failed log, and open `nightly-failure` / `weekly-failure` / `editmode-nightly-failure` issues (a failure develop already has isn't the PR's).

Write these to `pr-<n>.sources.md`. Done when every changed code file is under a map or listed as unmapped.

## 3. Pass 1 (blind)

Every reader works from the sources file and the diff files. Read the docs listed there and the code the diff touches; go further only to trace a specific finding. The PR description and commit messages are withheld until pass 2, so form the view from code and docs (`git diff`, `git show`, not `git log`).

**Inline route:** apply all four dimensions yourself, then go to step 4.

**Agent route:** spawn in parallel, each brief carrying the sources file path, its diff file, its dimensions' text from **The four dimensions** above, the finding format and the line on withheld context:

- **Gameplay** (dimension 1): `pr-<n>.gameplay.diff`, `vision.md` §2–§5, the domain design doc, the milestone gate. Skipped when the gameplay diff is empty.
- **Engineering** (dimensions 2–4): `pr-<n>.diff`, the maps and their Pitfalls, the spec, `2026-07_agent-first-composition.md` when the diff adds a SubSystem, entity behaviour, UI surface or prefab wiring. Also returns a 2–4 line summary of what the change does, written from the code, and the tests mapping.

**Finding format** (400 words per dimension at most):

```
- [D<1-4>] <title> · <blocker-candidate | note> · <confirmed | suspected>
  Evidence: <path:line on the PR head> — <quote or traced call path>
  Impact: <the concrete failure: who sees what, when>
  Confirm by: <test, reproduction or file to check> (suspected only)
  Action: <smallest fix or next step>
```

**Confirmed** means traced through the code end to end; anything less is **suspected**.

## 4. Pass 2: reconcile the author's claims

Now read the PR body and the commit messages (`git log --format=%B <merge-base>..<head>`). List each checkable claim (behaviour, "handles X", tests run, ticked networking-checklist boxes, "follows design §N", docs synced) and mark it **confirmed**, **not shown** or **contradicted** (cite the evidence). A contradicted claim becomes a finding in its dimension. Open only the files a claim needs; this pass checks claims, it doesn't re-review.

## 5. Rate (dimension 4's call)

Merge duplicates, then sort each finding:

- **Likely blocker:** has evidence **and** breaks an `AGENTS.md` hard rule (a silent deviation from a design doc is one), breaks server authority or ownership, or breaks something the current milestone's **Done when** scenario depends on. A suspected one stays a blocker only with its **Confirm by** step.
- **Noted:** the single most consequential remaining finding per dimension, so at most four. Each ends "not a merge blocker".
- **Dropped:** the rest: analyzer-enforced style, improvements with no concrete failure, findings that rest only on an unverified map's claim.

Each dimension's rating follows from what it kept. Done when every finding sits in one bucket and each dimension has a rating with a one-line reason.

## 6. Write the briefing

Write `pr-<n>.md` in `--out` and tell the user its path. Leave out empty sections.

```markdown
## Independent AI review — advisory, not an approval

PR #<n> · head `<sha>` · against develop `<sha>` · focus: <milestone focus, one line> · route: <inline | agents><· self-review override>

**Verdict:** <No likely blockers | N likely blocker(s)> — <one sentence>

| Dimension | Rating | Why |
|---|---|---|
| 1. Gameplay and design | <rating> | <one line> |
| 2. Architecture | <rating> | <one line> |
| 3. Correctness and regressions | <rating> | <one line> |
| 4. Proportionality | <rating> | <one line> |

### What this PR does (read from the code)
<2–4 lines>

### Likely blockers
1. **[D<n>] <title>** — <confirmed | suspected>
   Evidence: `<path:line>` <quote or call path>
   Why it blocks: <rule broken or gate threatened>
   Next step: <confirm-by and/or fix>
   - [ ] valid · [ ] false positive · [ ] accepted debt

### Noted (not merge blockers)
- **[D<n>] <title>** — <one line, evidence> · [ ] valid · [ ] false positive · [ ] accepted debt

### Author's claims
| Claim | Check |
|---|---|

### Tests and CI
- CI: <green | failing: check — cause (already red on develop?) | not available>
- Covered: <behaviour → test>
- Missing: <behaviour → tier, test sketch>

### Sources
<maps with verified/unverified status · spec · design §§ · unmapped files>
```

Posting the briefing as a PR comment arrives with the CI phase in the effort doc. Until then it stays a local file.
