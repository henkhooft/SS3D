---
name: independent-review
description: "Advisory independent review of a pull request, written for its human reviewer: a blind pass over the diff and docs before the author's explanation, then a check of the author's claims, filtered to milestone-level blockers. Use when asked for an independent, advisory or second-opinion review of a PR."
---

Produces a **briefing for the PR's human reviewer**: what the change actually does, the few risks worth their attention, and how the author's claims hold up. It is advisory. The output is a file the user reads; it approves nothing, requests nothing and never runs the PR's code. Decisions and pilot status: [2026-11_independent-review](../../../Documents/architecture/2026-11_independent-review.md).

The leading word is **blind**: the analysis forms its own view from the code and the docs before it sees the author's reasoning, so it can't simply adopt it.

Arguments: a PR number on `RE-SS3D/SS3D` (required); `--out <path>` (default `review-out/pr-<n>.md`, git-ignored); `--allow-self` to override the self-review guard.

## 1. Pin the PR

1. Find the remote that points at `RE-SS3D/SS3D` (`git remote -v`); call it `<remote>`. Fetch `<remote> develop` and the PR head: `git fetch <remote> pull/<n>/head:review/pr-<n>`. Work from refs with `git diff` and `git show <ref>:<path>`; leave the working tree and the current branch alone.
2. Read the PR's metadata **without its body**: number, title, head and base branch, changed files, CI check names and conclusions. Use `gh pr view <n> --repo RE-SS3D/SS3D --json number,title,headRefName,baseRefName,files,statusCheckRollup`, or the GitHub MCP `pull_request_read` tool, whichever this session has.
3. **Self-review guard.** Stop and ask for a fresh session when this conversation wrote or edited any of the PR's commits, or when the current branch is the PR's head branch. `--allow-self` overrides both; say so in the output's header.
4. Diff: `git diff <merge-base>...review/pr-<n>` with the merge-base of `<remote>/develop`. **Size gate:** count changed lines excluding Unity serialized files (`*.unity`, `*.prefab`, `*.asset`, `*.mat`, `*.meta`, `*.controller`, `*.anim`). Over 3,000 → write a one-line output ("too large for an automated review; split the PR or review by hand") and stop. Serialized files stay in the file list for the agents; they don't count toward the cap.

Done when: the PR ref exists locally, the diff is non-empty and under the gate, and you hold the metadata without having read the body.

## 2. Collect the blind sources

Collect **paths and short facts**; the sub-agents do the reading.

- **System maps:** every `Documents/architecture/systems/*.md` whose `> Code paths:` header covers a changed file. For each, read its `> Verified:` stamp and count commits since it that touch its code paths (`git log --oneline <sha>..<remote>/develop -- <code paths>`, fallback per [Documents/SKILL.md](../../../Documents/SKILL.md) § Verified stamp). A map with code commits since its stamp is **unverified**: agents treat it as possibly stale and check its claims against code before citing it.
- **Spec:** a plan or effort matching the head branch name, the title or the touched domain; plus any `Documents/…` path the PR body links, extracted mechanically so the prose stays unread: `gh pr view <n> --repo RE-SS3D/SS3D --json body --jq .body | grep -oE 'Documents/[A-Za-z0-9_./-]+\.md(#[A-Za-z0-9_-]+)?' | sort -u`. Then the design sections those docs' `Implements:` lines cite. None found → the Spec part reports "no spec" as a noted item.
- **Milestone focus:** the **Current focus** section of `Documents/milestones/INDEX.md`, plus the gate doc it names.
- **CI:** each check's conclusion. For a failing check, the last ~30 lines of its failed log. Also list open issues labelled `nightly-failure`, `weekly-failure` or `editmode-nightly-failure`: a failure develop already has isn't the PR's.

Write the CI facts to a scratch file the agents can read. Done when: every changed code file maps to at least one map or is listed as unmapped, and every source has a path.

## 3. Pass 1: two blind sub-agents, in parallel

Both briefs carry: the diff command, the PR ref, the changed-file list, the source paths from step 2 with each map's verified/unverified status, the CI scratch file, and the finding format below. Both say: *"Form your view from the code and the docs. The PR description and commit messages are withheld on purpose; read the change with `git diff` and `git show`, not `git log`."*

**Conformance** (does it follow the rules and the plan?): read `.claude/skills/spec-review/SKILL.md` § 3 (standards sources and the smell baseline) and the Spec sub-agent brief in § 4, and apply both to this diff.

**Risk** (what breaks, and does it matter now?):

- **Authority and networking:** client-reachable paths (`ServerRpc`, input handlers) that change server state without validation; ownership checks; state late joiners and observers miss (SyncVar/SyncList initial state, RPC-only state); despawn and destroy ordering; scene load. Old clients or saves that break without a bump of `ProtocolVersion.Current` or `PersistenceEnvelope.CurrentSchemaVersion`.
- **Regressions:** each touched map's **Pitfalls**; hot paths the change makes slower; callers of changed signatures.
- **Tests:** for the riskiest changed behaviour, the tests that cover it (the map's **Tests** section, `Assets/Scripts/Tests/`), and whether CI ran them. For each gap, the missing test as a sketch, in the cheapest tier that proves it ([testing philosophy](../../../Documents/architecture/2026-11_testing-philosophy.md) § Tiers).
- **Gameplay and milestone:** behaviour that contradicts the cited design sections; whether the change touches or threatens the current milestone slices.

**Finding format** (each agent, 500 words max):

```
- <title> · <blocker-candidate | note> · <confirmed | suspected>
  Evidence: <path:line on the PR head> — <quoted line or traced call path>
  Impact: <the concrete failure: who sees what, when>
  Confirm by: <test, reproduction or file to check> (suspected only)
  Action: <smallest fix or next step>
```

**Confirmed** means the failure is traced through the code end to end; anything less is **suspected**. Plus, from Risk: a 2–4 line summary of what the change does, written from the code, and the tests mapping.

Done when: both reports are back.

## 4. Pass 2: reconcile the author's claims

Now read the PR body and the commit messages (`git log --format=%B <merge-base>..review/pr-<n>`). List each checkable claim: behaviour, "handles X", tests run, ticked networking-checklist boxes, "follows design §N", docs synced. Mark each **confirmed** (pass 1 or a targeted file read supports it), **not shown** (nothing in the diff or CI demonstrates it) or **contradicted** (cite the evidence). A contradicted claim becomes a finding. Open only the files a claim needs; this pass checks claims, it doesn't re-review the diff.

## 5. Apply the blocker bar

Merge duplicates across the two reports, then sort every finding:

- **Likely blocker:** has evidence **and** does at least one of: breaks a hard rule in `AGENTS.md`; breaks server authority or ownership; is a regression that threatens the current milestone focus. A suspected one stays a blocker only with its **Confirm by** step.
- **Noted:** at most **3** of the rest, the most consequential first, each ending "not a merge blocker". This is where duplication, debt, smells and "no spec" land.
- **Dropped:** everything else. Analyzer-enforced style, improvements with no concrete failure, and findings that rest only on an unverified map's claim, unless the code confirms them.

Done when: every finding is in exactly one bucket and Noted holds 3 or fewer.

## 6. Write the briefing

Write `--out` in this shape and tell the user its path. A PR with nothing to flag gets the header, the verdict line, the summary and the sources, nothing else.

```markdown
## Independent AI review — advisory, not an approval

PR #<n> · head `<sha>` · against develop `<sha>` · focus: <milestone focus, one line><· self-review override>

**Verdict:** <No likely blockers | N likely blocker(s)> — <one sentence>

### What this PR does (read from the code)
<2–4 lines>

### Likely blockers
1. **<title>** — <confirmed | suspected> · <authority | hard rule | milestone regression>
   Evidence: `<path:line>` <quote or call path>
   Why it blocks: <rule broken or gate threatened>
   Next step: <confirm-by and/or fix>
   - [ ] valid · [ ] false positive · [ ] accepted debt

### Noted (not merge blockers)
- **<title>** — <one line, evidence> · [ ] valid · [ ] false positive · [ ] accepted debt

### Author's claims
| Claim | Check |
|---|---|
| <claim> | confirmed / not shown / contradicted: <why> |

### Tests and CI
- CI: <green | failing: check — cause (already red on develop? say so)>
- Covered: <behaviour → test>
- Missing: <behaviour → tier, test sketch>

### Sources
<maps with verified/unverified status · spec docs · design §§ · what had no map>
```

Posting the briefing as a PR comment arrives with the CI phase in the effort doc. Until then the output stays a local file.
