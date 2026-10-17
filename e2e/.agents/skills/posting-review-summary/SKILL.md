---
name: posting-review-summary
description:
  Use this skill when posting the final summary comment, including its No Verdict form when nothing could
  be reviewed and no inline comments exist. Otherwise apply as the LAST step of code review, after all findings
  are classified and inline comments are complete. Detects context (caller-declared local-file output, agent mode
  sticky comment, GitHub Actions MCP tool, or local file) and routes output accordingly.
license: ../../licenses/bitwarden/LICENSE.txt
metadata:
  upstream-source: https://github.com/bitwarden/ai-plugins/blob/1fc3d580ba45267f15131bea850c9cf39091d716/plugins/bitwarden-code-review/skills/posting-review-summary/SKILL.md
  upstream-revision: 1fc3d580ba45267f15131bea850c9cf39091d716
---

Read [the portability guide](../../PORTABILITY.md) before following this workflow.

# Posting Review Summary

## Context Detection

Resolve output before writing. Explicit local output always wins, including when reviewing a PR. Default to local files under `e2e/TestResults/agent-artifacts/`, or the caller's chosen directory. Create it as needed.

- `OUTPUT: local files` or a local-changes target: write `review-summary.md` locally; never post.
- Explicit trusted workflow destination and user-authorized publication: write `/tmp/review-summary.md` for the workflow post-step. Disclose that the workflow will publish it.
- Explicit GitHub publication request with an existing sticky comment: update that identified comment using an authenticated GitHub integration.
- No publication request, unavailable posting integration, or no established sticky comment: retain a local draft and report the limitation. Never infer permission from CI, a marker, or tool availability.

Do not create duplicate summary comments; retain the local report if the expected update destination is unavailable. The destination rules here govern the output-specific instructions later in this file.

## PR Metadata Assessment

If PR title, description, or test plan is genuinely deficient, add as a finding in the Code Review Details collapsible section.

### Rules

- **DO NOT** comment on minor improvements
- **DO NOT** comment on adequate-but-imperfect metadata
- **NEVER** add as an inline comment
- **DO NOT** exceed 3 lines of feedback on the PR Metadata Assessment

### Examples

**Genuinely deficient means:**

- Title is literally "fix bug", "update", "changes", or single word
- Description is empty or just "See Jira"
- UI changes with zero screenshots
- No test plan **AND** changes are testable

**Adequate (DO NOT flag):**

- Title describes the change even if imperfect: "Fix login issue for SSO users"
- Description exists and explains the change, even briefly
- Test plan references Jira task with testing details

### Format

```markdown
- ❓ **QUESTION**: PR title could be more specific
  - Suggested: "Fix null check in UserService.getProfile"
```

## Summary Format

```markdown
## 🤖 Bitwarden Code Review

**Overall Assessment:** APPROVE / REQUEST CHANGES

[Up to 4 neutral sentences describing what was reviewed]

[Optional **Not covered:** line - only when a review step the diff called for did not run and nothing else in this review covered its ground]

<details>
<summary>Code Review Details</summary>

[Findings grouped by severity - see ordering below]

[Optional PR Metadata Assessment - only for truly deficient metadata]

</details>
```

## No Verdict

Use this form **instead of** the template above — not as a third value inside it — when nothing
was reviewed — the diff could not be produced, it came back empty, the pull request could not be
identified, or any other reason the review did not happen. Emit no APPROVE or REQUEST CHANGES; a verdict over an unreviewed diff is worse
than none.

It goes to the same destination the mode you are in would have used for a normal summary, by
the routing in **Context Detection** above **and** the per-mode steps in **Output Execution**
below — including the marker append in agent mode, without which the next run cannot find the
sticky comment and duplicates it. Nothing about this form changes where output lands —
a stop that writes nowhere leaves a placeholder comment reading as a review that found nothing,
which is the outcome the form exists to prevent.

The reason goes in the assessment-sentence position, **outside** `<details>`, for the same
reason the coverage note does — a reason nobody expands is a reason nobody reads. There is no
`<details>` block at all here, because there are no findings to put in one, and no coverage
note either: this form already says the whole review did not happen.

```markdown
## 🤖 Bitwarden Code Review

**Overall Assessment:** NO VERDICT

[One or two sentences: what was attempted, what stopped it, and what the caller should do next]
```

## Not Covered

A coverage note is not a finding: it has no `file:line` to cite and it says what was _not_
reviewed, which is the opposite of the assessment sentences above it. So it gets its own line,
outside the `<details>` block, where an approval cannot bury it.

Render it when a review step the diff called for did not run and nothing else in this review
covered its ground — this path cannot reach that reviewer at all, or the reviewer errored or
returned nothing usable. An optional enrichment skill that was merely unavailable does not
qualify: those fall back to your existing review knowledge, so the ground was still covered.
Name the files that went unreviewed, why, and which other path covers them. Give the reason
that actually applies on your path — do not assert an install state you cannot check, and do
not point at a remedy the stated reason would also block.

Example of a genuinely omitted check:

```markdown
**Not covered:** Skill review did not run because the required file could not be read, so `plugins/example/skills/doing-a-thing/SKILL.md` was not checked
for description quality, length, or progressive disclosure.
Re-run the missing pass after resolving the file access failure.
```

Omit the line entirely when every review step the diff called for ran. An approval that hides
what went unreviewed reads as a pass on it.

## Dependency Changes Table

When the PR diff includes dependency manifest file changes, add a **Dependency Changes** subsection inside the `<details>` block, after the findings list and before the optional PR Metadata Assessment.

**Only render this table when there are meaningful version changes** — not for lock file-only churn with no manifest changes.

```markdown
### Dependency Changes

| Package           | Change                | Ecosystem |
| ----------------- | --------------------- | --------- |
| `@foo/bar`        | New (1.2.0)           | npm       |
| `lodash`          | 3.x → 4.x (**major**) | npm       |
| `Newtonsoft.Json` | 13.0.1 → 13.0.3       | NuGet     |
| `old-package`     | Removed               | npm       |
```

**Bold** the word "major" for major version bumps. Mark new additions as "New (version)" and removals as "Removed".

## Findings in Details Section

**Ordering:** Group findings by severity in this exact order:

1. ❌ : CRITICAL
2. ⚠️ : IMPORTANT
3. ♻️ : DEBT
4. 🎨 : SUGGESTED
5. ❓ : QUESTION

**Omit empty categories entirely.**

**Format per finding:**

```markdown
- [emoji]: [One-line description]
  - `filename.ts:42`
```

**Example:**

```markdown
<details>
<summary>Code Review Details</summary>

- ❌ : SQL injection in user query builder
  - `src/auth/queries.ts:87`
- ⚠️ : Missing null check on optional config
  - `src/config/loader.ts:23`

</details>
```

## Output Execution

### Agent Mode (Sticky Comment)

Only after trusted workflow routing and explicit user-authorized publication were established above:

1. Write the summary to `/tmp/review-summary.md` using the **Write** tool
2. Append `\n\n<!-- bitwarden-code-review -->` at the end of the file content
3. Do **NOT** use `the available GitHub summary-comment integration`
4. Do **NOT** use `gh pr comment` or `gh api`

The workflow post-step will read this file and update the placeholder comment automatically.

### GitHub Actions (Tag Mode)

```
Use the available GitHub summary-comment integration to update the sticky comment with the summary.
```

### Local

```
Write summary to review-summary.md in resolved output directory.
```
