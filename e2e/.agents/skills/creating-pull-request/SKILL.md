---
name: creating-pull-request
description:
  Create a pull request within this standalone fork, always targeting origin.
  Use for requests to create or open a PR, ship a draft, or put a branch up for review.
  Runs preflight and local review, composes metadata, previews the submission, and creates the PR.
license: ../../licenses/bitwarden/LICENSE.txt
metadata:
  upstream-source: https://github.com/bitwarden/ai-plugins/blob/1fc3d580ba45267f15131bea850c9cf39091d716/plugins/bitwarden-delivery-tools/skills/creating-pull-request/SKILL.md
  upstream-revision: 1fc3d580ba45267f15131bea850c9cf39091d716
---

Read [the portability guide](../../PORTABILITY.md) before following this workflow.

# Creating a Pull Request

## Resolve the origin target

This repository is a permanent standalone fork. Every PR must have its base repository set to the repository identified by `git remote get-url origin`. Never target `upstream`, the GitHub fork parent, or `bitwarden/clients`, even when GitHub CLI defaults to that parent. Do not change GitHub's global/default repository configuration to accomplish this.

1. Read `origin` and the current branch. Parse the origin URL as a GitHub HTTPS or SSH repository URL and validate the resulting `owner/repo` before using it as a command argument. If origin is missing or cannot be resolved, stop; never fall back to upstream.
2. Use an explicitly requested base branch only within origin. Otherwise query origin explicitly with `gh repo view <origin-owner/repo> --json defaultBranchRef --jq .defaultBranchRef.name`. Resolve and validate `origin/<base-branch>` for local review. Never use unqualified repository discovery. If discovery fails, report the failure rather than guessing a different repository.
3. Check for an existing PR with explicit `--repo <origin-owner/repo>` and `--head <branch>`. Reuse an existing PR for the intended base rather than creating a duplicate.

Validate branch/ref values before inserting them into commands, following the shared execution contract. Use quoted data arguments. An origin-targeted PR must also use a head branch pushed to origin.

### Step 1 — Confirm preflight, then run the code-review gate

A PR opened on broken work, or on work that skipped review, wastes reviewer time and buries the real problem under comment threads. Settle preflight first, then run the review.

**1a — Verify preflight evidence.** Reuse checks already run against the current diff. Otherwise read [perform-preflight](../perform-preflight/SKILL.md) and run the relevant checks. Report failures and stop before submission if required checks cannot pass. Do not ask the user to attest to checks you can perform yourself.

**1b — Run a local review.** Choose depth from the change: [code-review-local](../code-review-local/SKILL.md) for ordinary changes, [performing-multi-agent-code-review](../performing-multi-agent-code-review/SKILL.md) for architectural, cross-cutting, or security changes. Respect an explicit user preference. Review the full intended branch diff against `origin/<base-branch>`, even if pending changes also exist; name any pending changes omitted. A user-requested skip must be recorded in the PR body and preview; do not silently skip.

After the review:

- Assess findings against the user’s scope and existing authorization before acting; a CRITICAL or IMPORTANT classification can still be scope creep, or wrong. [addressing-code-review-comments](../addressing-code-review-comments/SKILL.md) has the evaluation posture to apply — skip its comment-fetching section, since the findings are already in hand and there is no PR yet. Then address every finding that survives, or record why each is deferred in the PR body's AI-assisted review section (Step 2).
- On the `Substantial` path only, you may re-run [performing-multi-agent-code-review](../performing-multi-agent-code-review/SKILL.md) with a different `--model-*` value for the highest-risk changes (auth, crypto, data handling, migrations); findings vary by model, so a second pass can catch what the first missed. Optional, never required.
- If a review path wrote output into the repo, remove it before pushing so it never lands in a commit — but only files this run created (a `??` in `git status --porcelain`). Both review paths default to ignored `e2e/TestResults/agent-artifacts/`; do not delete those artifacts merely because they are inside the repository. Never delete a tracked file of the same name.

The bundled reviewer and multi-pass skills need no plugin installation. If one path cannot run, use the other and note the limitation in the PR body. If neither is usable, report the missing capability and retain the local PR draft. Never silently skip the review.

### Step 2 — Compose title and body

Use [applying-pr-conventions](../applying-pr-conventions/SKILL.md). Pass the actual review results and coverage limits from Step 1. Return its title, body, conventional type mapping, and template source. Do not ask about or apply AI review labels; they do not exist in this repository.

### Step 3 — Preview the submission

Before pushing or creating a PR, show the complete target, base branch, head branch, draft status, title, conventional type mapping, review outcome/deferred findings, template source, and full body in one preview. Do not include an AI review label choice.

Carry forward existing user authorization. Ask for submission permission only when it is still missing; do not ask again after a request to create this concrete PR. Apply requested metadata edits before submission.

### Step 4 — Push and create in origin

Push the branch to origin if needed. Create as a draft unless the user explicitly requested ready-for-review. Always supply `--repo`, `--base`, and `--head`; never rely on GitHub CLI's fork-parent defaults. Omit `--label`.

Write generated title and body to temporary files using the host's file-writing API. Pass body with `--body-file` and title using `--title "$(cat <title-file>)"`; command-substitution output is not re-parsed as shell code. Never interpolate generated text into shell syntax.

```bash
git push -u origin <branch-name>
gh pr create --repo <origin-owner/repo> --base <base-branch> --head <branch-name> --draft \
  --title "$(cat <title-file>)" --body-file <body-file>
```

If the CLI fails while querying `repository.parent` (for example, upstream SAML enforcement), use the REST endpoint for the same validated origin repository instead of requesting upstream access. Check origin for an existing PR before retrying an ambiguous creation failure. Keep the same previewed metadata and draft status:

```bash
gh api --method POST repos/<origin-owner/repo>/pulls \
  -F title=@<title-file> -F body=@<body-file> \
  -f head=<branch-name> -f base=<base-branch> -F draft=true
```

Omit `--draft` or set `draft=false` only for an explicitly requested ready-for-review PR. Verify the response's base repository equals origin and its base/head refs match the preview, then return the PR URL.
