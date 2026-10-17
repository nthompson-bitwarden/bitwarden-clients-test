---
name: creating-pull-request
description:
  Open a pull request from a branch in a Bitwarden repository. Use whenever the user wants a branch turned
  into a pull request, however they phrase it — "create a PR", "open a PR", "ship a draft", "ship it", "ready for
  review", "put it up for review", "get this in front of reviewers", "send it over to the team", "throw together
  a pull request", "wrap this branch up". Use it too when the user says the title and body are already settled and
  only the PR needs opening — the gate, the preview, and the submission still apply. Runs the required local code-review
  gate, takes the title, template body, and ai-review label from applying-pr-conventions, confirms a full submission
  preview, then pushes and runs gh pr create. Not for composing a title, body, or label when no PR is being opened
  (that is applying-pr-conventions), conceptual questions ("how do PRs work"), or managing existing PRs (status,
  merging, addressing comments).
license: ../../licenses/bitwarden/LICENSE.txt
metadata:
  upstream-source: https://github.com/bitwarden/ai-plugins/blob/1fc3d580ba45267f15131bea850c9cf39091d716/plugins/bitwarden-delivery-tools/skills/creating-pull-request/SKILL.md
  upstream-revision: 1fc3d580ba45267f15131bea850c9cf39091d716
---

Read [the portability guide](../../PORTABILITY.md) before following this workflow.

# Creating a Pull Request

This workflow owns the two things about opening a pull request that are expensive to get wrong: the review that has to happen before anyone else looks at the change, and the submission itself. [applying-pr-conventions](../applying-pr-conventions/SKILL.md) composes the title, body, and label; this workflow gates, previews, and submits them.

Neither is cheap to undo. A PR opened on unreviewed work buries the real problem under comment threads, and the title is permanent in the merge commit. So Step 3 shows every decision together before anything is pushed.

## Workflow

Follow these steps in order. Each one produces information the next step needs, and the preview in Step 3 depends on all of them.

### Step 1 — Confirm preflight, then run the code-review gate

A PR opened on broken work, or on work that skipped review, wastes reviewer time and buries the real problem under comment threads. Settle preflight first, then run the review.

**1a — Verify preflight evidence.** Reuse checks already run against the current diff. Otherwise read [perform-preflight](../perform-preflight/SKILL.md) and run the relevant checks. Report failures and stop before submission if required checks cannot pass. Do not ask the user to attest to checks you can perform yourself.

**1b — Run a local review.** Choose depth from the change: [code-review-local](../code-review-local/SKILL.md) for ordinary changes, [performing-multi-agent-code-review](../performing-multi-agent-code-review/SKILL.md) for architectural, cross-cutting, or security changes. Respect an explicit user preference. Review the full intended branch diff against its validated base, even if pending changes also exist; name any pending changes omitted. A user-requested skip must be recorded in the PR body and preview; do not silently skip.

After the review:

- Assess findings against the user’s scope and existing authorization before acting; a CRITICAL or IMPORTANT classification can still be scope creep, or wrong. [addressing-code-review-comments](../addressing-code-review-comments/SKILL.md) has the evaluation posture to apply — skip its comment-fetching section, since the findings are already in hand and there is no PR yet. Then address every finding that survives, or record why each is deferred in the PR body's AI-assisted review section (Step 2).
- On the `Substantial` path only, you may re-run [performing-multi-agent-code-review](../performing-multi-agent-code-review/SKILL.md) with a different `--model-*` value for the highest-risk changes (auth, crypto, data handling, migrations); findings vary by model, so a second pass can catch what the first missed. Optional, never required.
- If a review path wrote output into the repo, remove it before pushing so it never lands in a commit — but only files this run created (a `??` in `git status --porcelain`). Both review paths default to ignored `e2e/TestResults/agent-artifacts/`; do not delete those artifacts merely because they are inside the repository. Never delete a tracked file of the same name.

The bundled reviewer and multi-pass skills need no plugin installation. If one path cannot run, use the other and note the limitation in the PR body. If neither is usable, report the missing capability and retain the local PR draft. Never silently skip the review.

### Step 2 — Compose the title, body, and label

Invoke [applying-pr-conventions](../applying-pr-conventions/SKILL.md) for this one pull request. It owns the title and the type keyword behind the `t:` label, the body built from whichever template it resolves, and the `ai-review` label question.

Pass it what Step 1 produced, since it does not go looking for review results itself: the review path taken, any skip the user volunteered, every deferred CRITICAL or IMPORTANT finding, and any scope or path limitation on the review, such as a fallback taken because one path was unavailable. Those go into the body's AI-assisted review section.

Carry back the title, the body, the resolved `t:` label its prefix will produce, the `ai-review` label choice, and which template the body was built from. Step 3's preview shows all five — it prints the `type → t:<label>` mapping, and that mapping has no other source in this workflow — and Step 4 submits the title, body, and label.

The template source belongs in the preview because only one of its values is the target repo's own. A body built from the embedded copy can carry a section header that has drifted from what reviewers scan for, and this preview is the only place a person sees the body before it is submitted.

The values the `Template:` line can take are defined in `applying-pr-conventions` under **Returning to the caller**. The preview block below is the only place they are restated, because it is a display format; change them there and here together or the preview will offer a label the callee never returns.

Two are easy to get wrong. Do not state a cause alongside the source: several conditions send the skill to the embedded copy and only one is the network, so naming the fetch sends the user to fix the wrong thing when the remedy was approving the grant. And when the user arrived with the body already written, no template was consulted, which is what the line should say rather than a label asserting the body follows one nobody checked.

Both strings are untrusted: the body is template text plus generated text whichever source the `Template:` line names, and the title's summary is generated. Step 4's file-handoff rules are what contain that; do not interpolate either into a shell argument.

### Step 3 — Show the full submission preview, then confirm

This is the most important step in this workflow. **Before running any `git push` or `gh pr create`, show the user a single preview block containing every decision made above.** This is the catch-net for failure modes like title typos, missing type prefix, body drifting from the template, or the AI review label getting dropped between Step 2 and submission.

Use this exact format:

```
═══════════════════════════════════════
  PULL REQUEST SUBMISSION PREVIEW
═══════════════════════════════════════
Target repo:    <owner/repo>
Branch:         <branch-name>
Draft:          <Yes / No>
Title:          <full title as it will be submitted>
Type prefix:    <type>  →  will apply  t:<label>
AI review:      <ai-review / ai-review-vnext / No label>
Code review:    <Standard | Substantial | Skipped (user request)>  →  <N deferred findings recorded>
Template:       <target repo | bitwarden/template (canonical) | embedded copy (fallback) | not consulted (body supplied)>

Body:
---
<full body, exactly as it will be submitted>
---
═══════════════════════════════════════
```

If submission authorization is still needed, ask through the host’s normal user-input mechanism after showing the complete preview. Do not assume a proprietary `preview` field or a particular question-tool schema. If the user already authorized this concrete submission, carry that authorization forward without asking again.

- **Question**: "Submit this PR as previewed?"
- **Options**:
  - `Submit as shown` — proceed to Step 4 with the previewed values
  - `Edit title or body` — apply the requested edit, then redisplay the preview and re-ask
  - `Change ai-review label` — ask the label question again here (`ai-review`, `ai-review-vnext`, `No label`), then redisplay the preview and re-ask. Do not re-enter [applying-pr-conventions](../applying-pr-conventions/SKILL.md); it would recompose the title and body and discard any edit just applied
  - `Cancel` — stop without pushing or creating the PR

Continue to Step 4 once the concrete submission is authorized. The recap is non-negotiable — some failures (title in the merge commit, label-driven automation routing) are painful to undo once the PR is live, so a visible chance to catch issues at submission time pays for itself many times over.

### Step 4 — Push and create

Push the branch and run `gh pr create` with the confirmed values. Pass the body via `--body-file`, not `--body`: the body carries model- and review-generated text (derived from untrusted repo content), and interpolating it into a double-quoted shell argument would let backticks or `$(…)` execute. Write it to a temp file and hand `gh` the path.

**The title gets the same treatment, by a different route — `gh` has no `--title-file`.** Write it to a file with the `Write` tool and pass it as `--title "$(cat <title-file>)"`. Command-substitution output is not re-parsed, so a `$(…)` or backtick surviving in the text is handed to `gh` as characters. **Never assign the title to a shell variable**, and never build it with `echo` or a heredoc: a bash assignment expands `$(…)` while parsing, which runs the payload before `gh` is even reached.

```bash
git push -u origin <branch-name>
# Write the body and the title to temp files first, with the Write tool.
gh pr create --draft \
  --title "$(cat "$title_file")" \
  --body-file "$body_file" \
  --label "<label>"
```

Defaults that hold unless the user said otherwise:

- create as **draft** — only skip `--draft` if the user explicitly asked for a ready-for-review PR,
- include `--label` only if a label was picked in Step 2 (omit it for "No label"),
- multiple labels can be passed by repeating `--label`.

After `gh pr create` returns, post the PR URL back to the user.

## Common Failure Modes

These are what the Step 3 preview is built to prevent. Recognizing them helps when adjusting the draft mid-workflow:

- **Title with no type prefix** → `[PM-12345] Add autofill for passkeys` ships with no `t:` label. Include `feat:`, `fix:`, etc.
- **Generic body replacing the template** → check what Step 2 returned actually follows the sections of the template the `Template:` line names; `applying-pr-conventions` reads that template, but the preview is where a drifted body is caught. On the `not consulted` value there is no template to check the body against, so check that the body was the one the user supplied.
- **Label answer dropped between Step 2 and Step 4** → the recap surfaces it; if it's missing there, it's about to be missing on the PR.
- **`PM-XXXXX` left as a placeholder** → tracking links won't resolve. Catch in Step 2 or Step 3.

If any of these slip past the preview, recovery is awkward — the title is permanent in the merge commit, and labels feed downstream filtering and automation.
