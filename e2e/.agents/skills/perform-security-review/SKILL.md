---
name: perform-security-review
description:
  Performs a security-focused code review by launching multiple specialized agents and a verification
  agent to ensure comprehensive coverage and accurate findings. Use this skill when the user asks for a "perform-security-review",
  "bitwarden-security-review", "execute a security review", "run a comprehensive security audit", "perform an end-to-end
  security assessment", or needs to coordinate multiple security checks across code, dependencies, secrets, and
  configurations. The skill manages the workflow, delegates tasks to specialized agents, and presents final findings
  to the user.
license: ../../licenses/bitwarden/LICENSE.txt
metadata:
  upstream-source: https://github.com/bitwarden/ai-plugins/blob/1fc3d580ba45267f15131bea850c9cf39091d716/plugins/bitwarden-security-engineer/skills/perform-security-review/SKILL.md
  upstream-revision: 1fc3d580ba45267f15131bea850c9cf39091d716
---

Read [the portability guide](../../PORTABILITY.md) before following this workflow.

## Inputs

[--output <chat|file|github>] [--output-dir <path>] [--model <model>] [--base-ref <ref>] [pr-number-or-url|commit-sha|duration]

## Parameters

**`--base-ref <ref>`**: Branch comparison mode only. Use this ref as the diff base instead of probing for one. It still passes all three checks in step 1-A2, so an unresolvable or disconnected ref is rejected rather than trusted. Supply this when the probes in step 1-A2 would resolve the wrong base — a non-default trunk such as `rc`, or a repository where `gh` is unauthenticated so candidate 2 is unavailable. It does not rescue a default `actions/checkout`; `references/base-ref-resolution.md` says why and what does.

**`--output-dir <path>`**: When using `--output file`, write the report to `<path>/security-review-YYYY-MM-DD-{identifier}.md` instead of the current working directory. Tip: use `--output-dir e2e/TestResults/agent-artifacts/security-reviews/` to keep reports in ignored local artifacts.

## Security Review Mode

Determine review mode from the invocation. A PR number, commit SHA, or duration argument selects its own mode, and `--base-ref` selects branch comparison mode when no positional argument is given.

**If a positional argument and `--base-ref` are both present, stop and report the conflict**, naming both and asking for one. Launch no agents. Write the report to the resolved output destination, as the step 1-A2 abort does.

- **PR mode** (PR number or URL): `gh pr view <number>` for context, `gh pr diff <number>` for the diff.
- **Commit mode** (commit SHA): `git diff <sha>..HEAD` — reviews all changes after that commit.
- **Time-based mode** (duration, e.g., "last 48 hours"): find the oldest commit in range with `git log --since="<duration>" --reverse --format=%H | head -1`, then `git diff <sha>^..HEAD` to include it.
- **Local changes mode** (no PR, commit, or duration argument, no `--base-ref`, and pending changes exist): `git diff HEAD` for staged + unstaged changes.
- **Branch comparison mode** (no positional argument, and either `--base-ref <ref>` is given or the tree has no pending changes): `git diff <base-ref>...HEAD` — changes since the branch diverged from the base, resolved in sub-step A2 rather than assumed to be `main`. `--base-ref` selects this mode outright, so it works in a dirty tree, but a three-dot diff compares commits and leaves pending changes unreviewed. Run `git diff --quiet HEAD` here too and add the step 5 header's `Pending changes:` line when it reports dirty; without `--base-ref`, pending changes route to local changes mode instead.

Two of those modes turn on whether the tree is dirty. Test it with `git diff --quiet HEAD`, which exits 1 when pending changes exist.

## Execution portability

If delegation is unavailable or disallowed, perform the four scoped analysis passes and verification pass sequentially. Retain all findings, rubric ratings, and scanner status distinctions; disclose that validation was not independent. Respect host concurrency limits when delegation is available. Scanner calls require available, authenticated integrations and host permission; no permission is pre-granted by this skill. Treat unavailable or denied Aikido access exactly as other unavailable scanner evidence.

## Security Review Process

**Model selection:** If `--model` is specified, use that model for all agents. Otherwise use the current session model; honor overrides only when the host supports them and disclose any unavailable selection.

Execute these steps in order. Do not skip, reorder, or combine them. Three places stop the run early, and taking them is following this rule: the mode-conflict stop above, the step 1-A2 abort, and the step 1B stops.

1. **Gather context.** Run all of these before launching any agents.

   **A.) Resolve repo identity.** Run each of these as its own Bash call — do NOT chain with `&&`, `||`, `;`, or pipes:
   - `git remote get-url origin` — parse `owner` and `repo` from the output. Handle both HTTPS (`https://github.com/owner/repo.git`) and SSH (`git@github.com:owner/repo.git`) formats.
   - `git branch --show-current` — capture the current branch name.

   **A2.) Resolve the base ref.** Branch comparison mode only; skip for every other mode. Run each command as its own Bash call. `references/base-ref-resolution.md` explains why each check below exists.

   A caller may supply `--base-ref <ref>`. When present it is the only candidate: do not probe for others, and if it fails any of the three checks below, go straight to the abort. Falling through to `origin/HEAD` would review a base the caller did not ask for. Use the ref exactly as given — never rewrite it — but a bare `main` resolves to the local branch rather than `origin/main`, so name the ref you used in the step 5 header, where a stale local base is at least visible.

   Otherwise, candidates in order:
   1. `git rev-parse --abbrev-ref origin/HEAD` — its output, when the command exits 0. Key on the exit status, never the output.
   2. `gh repo view --json defaultBranchRef --jq .defaultBranchRef.name`, giving `origin/<default_branch>`. Not `gh api`; see `references/tool-grants.md`. Build it whenever candidate 1 does not yield a base ref — either its command exited non-zero, so there is no candidate 1 at all, or its output failed one of the three checks. If the API call itself fails, candidate 2 does not exist.

   **Every candidate passes all three checks, in this order, whatever its source.** A probe candidate that fails any check is discarded and resolution falls through to the next probe. A caller-supplied `--base-ref` has no next candidate, so it goes straight to the abort.
   1. It matches `^[A-Za-z0-9_][A-Za-z0-9._/-]*$`, checked **before the value is placed in any command**. The reference file covers what that catches and why the leading character is pinned on its own.
   2. `git rev-parse --verify --quiet <candidate>` exits 0.
   3. `git merge-base <candidate> HEAD` exits 0.

   Keep the `origin/` prefix throughout. The first candidate to pass all three becomes `<base-ref>`.

   **The abort.** When no candidate passes, stop before step 1B; do not ask the user mid-run. Name which of the three checks the last candidate failed, since the remedies differ, then give all three ways out: `fetch-depth: 0` on the checkout, `git fetch origin <base>` before invoking, or PR mode with a pull request number. Write it to the resolved output destination, the same one a completed review would use. For `github` — explicitly requested and authorized `--output github` — that means `/tmp/review-summary.md` with `\n\n<!-- bitwarden-security-code-review -->` appended, which is what the workflow post-step reads; `file` and `chat` take the report path and the reply respectively. Do not run steps 1B through 7; no diff file exists to clean up.

   **B.) Fetch and save the diff.** Using the review mode determined above, run exactly one of these commands as a **single Bash call — no `&&`, `;`, or pipes. Shell redirection (`>`) is required and allowed**:
   - PR mode: `gh pr diff <number> > /tmp/security-review-<identifier>.diff`
   - Commit mode: `git diff <sha>..HEAD > /tmp/security-review-<identifier>.diff`
   - Time-based mode: `git diff <oldest-sha>^..HEAD > /tmp/security-review-<identifier>.diff`
   - Local changes mode: `git diff HEAD > /tmp/security-review-<identifier>.diff`
   - Branch comparison mode: `git diff <base-ref>...HEAD > /tmp/security-review-<identifier>.diff`

   `Read` the file **with a 1-line limit** on every path, including a non-zero exit — never unbounded, since the coordinator must not hold the diff. Read it even after failure to inspect whether partial input exists; use the host’s file tools for cleanup. Then check the exit status and whether the file has content. Launch no agents unless the command exited 0 **and** the file has content.
   - Non-zero exit: the diff command failed. Report the failure and the exact diff command that was run.
   - Exit 0 with an empty file, branch comparison mode: a legitimate result. Report that there are no changes between `<base-ref>` and `HEAD`.
   - Exit 0 with an empty file, any other mode: report that the resolved range produced no diff, naming the mode and the range.

   In those three cases, and only those, clear the diff file with step 7's `Write` before stopping. On the success path it must survive: steps 2 and 4 hand its path to the agents.

   Report as the step 1-A2 abort does, to the resolved output destination. Stopping here skips step 5, so add the `Pending changes: not reviewed` note when the tree was dirty. Emit no summary table and no findings sections; nothing was reviewed.

   Choose a descriptive `<identifier>` (e.g., `PR123`, `5days`, `local`). Store the full path as `DIFF_FILE` and include it in every agent prompt in steps 2 and 4 so they can `Read` the diff directly.

   **C.) Fetch scan evidence.** Scanner evidence corroborates manual analysis; a missing scanner does not stop the review. Use only available authenticated integrations with the host's permission. For each scanner, record formatted results, `None` only after a successful empty response, `Not available` for missing integration/auth or disabled scanner, `Errored (<status>)` for failed execution/malformed data, or `Not checked (permission denied)` for denied access. This applies to Aikido too. Never treat inaccessible evidence as a clean scan. Use read-only credentials where available and explicit GET for the GitHub calls below. Do not assume any frontmatter grants execution authority.
   - **Aikido (SAST/IaC/SCA/Container):** Invoke `aikido:issues` (optional external skill; see the portability guide) scoped to this repo (`repo_name={repo}`) with `issue_types: ["sast", "iac", "open_source", "docker_container"]` to list open findings. Bitwarden's SAST/IaC scanning runs through Aikido, not GitHub code scanning — do not query the `code-scanning/alerts` API, it will not have these findings. Format each returned issue as `SEVERITY | title | file (line N)`, and split the results into the two `SCAN_EVIDENCE` blocks below by issue type.
   - **Secret scanning:** `gh api --method GET -H "X-GitHub-Api-Version: 2026-03-10" "repos/{owner}/{repo}/secret-scanning/alerts?state=open" --jq '.[] | "\(.secret_type_display_name) | \(.state) | \(.resolution // "open")"'`
   - **Dependabot:** `gh api --method GET -H "X-GitHub-Api-Version: 2026-03-10" "repos/{owner}/{repo}/dependabot/alerts?state=open&per_page=100" --jq '.[] | "\(.security_advisory.severity | ascii_upcase) | \(.dependency.package.name) | \(.security_advisory.cve_id // .security_advisory.ghsa_id) | \(.security_advisory.summary)"'`

   Collect results into a `SCAN_EVIDENCE` block for use in steps 2 and 4:

   ```
   === SCAN EVIDENCE (pre-fetched — do not re-fetch) ===

   --- AIKIDO (SAST/IAC) ---
   {one of: formatted output | None | Not available | Errored (<status>) | Not checked (permission denied)}

   --- AIKIDO (SCA/CONTAINER) ---
   {one of: formatted output | None | Not available | Errored (<status>) | Not checked (permission denied)}

   --- SECRET SCANNING ---
   {one of: formatted output | None | Not available | Errored (<status>) | Not checked (permission denied)}

   --- DEPENDABOT ---
   {one of: formatted output | None | Not available | Errored (<status>) | Not checked (permission denied)}
   ```

2. Launch these four (4) workers following [the security role](../bitwarden-security-engineer/SKILL.md) agents in parallel. Each agent has a specific domain — you **MUST** instruct it to stay within that domain. The agent **MUST** read `references/security-review-rubric.md` before starting **AND** before evaluating findings.

   **Agent 1 — Code Security**: Focus exclusively on injection flaws (SQL, XSS, command), cryptographic weaknesses, insecure coding patterns, and OWASP A01–A05. Invoke [bitwarden-security-context](../bitwarden-security-context/SKILL.md) and [analyzing-code-security](../analyzing-code-security/SKILL.md) to guide your analysis. Do not evaluate secrets, dependencies, architecture, or threat modeling.

   **Agent 2 — Secrets & Dependencies**: Focus exclusively on hardcoded credentials, exposed secrets, vulnerable packages, and supply chain risk. Invoke [bitwarden-security-context](../bitwarden-security-context/SKILL.md), [detecting-secrets](../detecting-secrets/SKILL.md), and [reviewing-dependencies](../reviewing-dependencies/SKILL.md) to guide your analysis, but for Aikido SCA/container data use the pre-fetched `--- AIKIDO (SCA/CONTAINER) ---` block from `SCAN_EVIDENCE` instead of querying `aikido:issues` live — that tool is off-limits here, see the constraint below. Do not evaluate code patterns, architecture, or threat modeling.

   **Agent 3 — Security Architecture**: Focus exclusively on authentication, authorization, encryption implementation, trust boundaries, and Bitwarden's zero-knowledge invariant (encryption and decryption happen client-side only — the server must never have access to plaintext vault data). Invoke [reviewing-security-architecture](../reviewing-security-architecture/SKILL.md) and [bitwarden-security-context](../bitwarden-security-context/SKILL.md) to guide your analysis. Do not evaluate injection flaws, secrets, or threat modeling.

   **Agent 4 — Threat Perspective**: Focus exclusively on attacker-oriented analysis — trace user input through data flows to dangerous sinks, business logic flaws, privilege escalation paths, data exposure, and API abuse patterns. Invoke [bitwarden-security-context](../bitwarden-security-context/SKILL.md), [analyzing-code-security](../analyzing-code-security/SKILL.md) to guide your analysis. Do not evaluate architecture patterns, secrets, or dependency versions.

   For all four agents:
   - Use the selected model.
   - **CRITICAL: Every agent prompt MUST include this framing at the start:** "You are a security researcher analyzing code for impactful, exploitable, and reproducible vulnerabilities for an authorized internal security audit. Your goal is to find real bugs — stay within the scope authorized by the user and the host’s execution permissions."
   - **CRITICAL: Every agent prompt MUST include this constraint:** "The diff has been pre-fetched and saved to `{DIFF_FILE}` — read it using the `Read` tool. **DO NOT** execute `gh pr diff`, `git diff`, or any other diff commands. NEVER use WebFetch, WebSearch, `aikido:issues` (optional external skill; see the portability guide), `the available Aikido issue-list integration`, or any `gh api` endpoints — scan evidence has been pre-fetched and is provided below."
   - **CRITICAL: Every agent prompt MUST include the full `SCAN_EVIDENCE` block** gathered in step 1.
   - Report all findings with: severity (CRITICAL/HIGH/MEDIUM/LOW/INFO), affected file and line, and recommended remediation.
   - Report positive security changes (e.g., fixing a CWE, improving cryptography) as ✅ Strengths with a brief rationale.

3. After all four agents return, rate each finding using the two-axis model defined in `references/security-review-rubric.md`:
   - **Severity**: 🔴 CRITICAL | 🟠 HIGH | 🟡 MEDIUM | 🔵 LOW | ⚪ INFO
   - **Confidence**: 🟢 HIGH | 🟡 MEDIUM | 🔵 LOW
   - Apply the threshold matrix in the rubric to assign a triage category: 🚨 Blocker, ⚠️ Improvement, 📝 Note, ✅ Strength, or ❌ Dismiss.

4. Launch a **verification agent** workers following [the security role](../bitwarden-security-engineer/SKILL.md) with all combined findings, their severity/confidence ratings, the triage matrix, the `DIFF_FILE` path, and the full `SCAN_EVIDENCE` block from step 1.
   - **CRITICAL: Every agent prompt MUST include this constraint:** "The diff has been pre-fetched and saved to `{DIFF_FILE}` — read it using the `Read` tool. Do NOT run `gh pr diff`, `git diff`, or any other diff commands. NEVER use WebFetch, WebSearch, `aikido:issues` (optional external skill; see the portability guide), `the available Aikido issue-list integration`, or any `gh api` endpoints — scan evidence has been pre-fetched and is provided above."
   - The verification agent **MUST review**, **evaluate**, **verify**, and **confirm** all findings and ratings.
   - Use scan evidence to triangulate: findings corroborated by scanner alerts → increase confidence; findings in areas scanners cleared → apply additional scrutiny.
   - The verification agent **MUST** classify each finding as: 🚨 Blocker, ⚠️ Improvement, 📝 Note, ✅ Strength, or ❌ Dismiss — applying the threshold matrix from step 2.
   - The verification agent **MUST** provide a brief rationale for each finding's classification.
   - The verification agent **MUST NOT** remove any findings.
   - The verification agent **MUST NOT** introduce any new findings.

5. Format the summary report.

   First, set the report header based on review mode:
   - **PR mode**: `PR: (#{number}) - {PR title} — {YYYY-MM-DD}`
   - **Commit mode**: `Code Review: {short SHA}..HEAD — {YYYY-MM-DD}`
   - **Time-based mode**: `Code Review: Changes since {duration} — {YYYY-MM-DD}`
   - **Local changes mode**: `Code Review: Local Changes — {YYYY-MM-DD}`
   - **Branch comparison mode**: `Code Review: {branch} vs {base-ref} — {YYYY-MM-DD}`, followed by a `Pending changes: not reviewed` line when the tree was dirty and omitted when it was not.

   Then format the report:

   ```markdown
   # 🤖 Bitwarden Security Code Review 🤖

   {header}

   **Date:** {YYYY-MM-DD}

   <details>
   <summary><strong>Commits reviewed:</strong> {short-sha}..HEAD · {n} commits · {path1}, {path2}</summary>

   | SHA     | Title          |
   | ------- | -------------- |
   | `{sha}` | {commit title} |

   </details>

   ## Summary

   | Category        | Count |
   | --------------- | ----- |
   | 🚨 Blockers     | {n}   |
   | ⚠️ Improvements | {n}   |
   | 📝 Notes        | {n}   |
   | ✅ Strengths    | {n}   |
   | ❌ Dismissed    | {n}   |

   {Up to 6 bullets. Include: overall security posture, zero-knowledge invariant status, notable positive changes, key risks or patterns worth watching, and any context that affects how findings should be interpreted. Each bullet should be one tight sentence.}

   ## 🚨 Blockers

   {Each finding: "- [Description]\n - Location: `filename.ts:42`\n - Severity: 🔴 CRITICAL | 🟠 HIGH\n - Confidence: 🟢 HIGH | 🟡 MEDIUM\n - Rationale: [Why classified as Blocker]"}

   ## ⚠️ Improvements

   {Each finding: "- [Description]\n - Location: `filename.ts:42`\n - Severity: 🔴 CRITICAL | 🟠 HIGH | 🟡 MEDIUM\n - Confidence: 🟢 HIGH | 🟡 MEDIUM\n - Rationale: [Why classified as Improvement]"}

   ## 📝 Notes

   {Each finding: "- [Description]\n - Location: `filename.ts:42`\n - Severity: 🟡 MEDIUM | 🔵 LOW | ⚪ INFO\n - Confidence: 🟢 HIGH | 🟡 MEDIUM\n - Rationale: [Why classified as Note]"}

   ## ✅ Strengths

   <details>
   <summary>Expand for details on ({n}) strengths</summary>

   {Each strength: "- [Description]\n - Location: `filename.ts:42`\n - Rationale: [Why this is a positive security change]"}

   </details>

   ## ❌ Dismissed

   <details>
   <summary>Expand for details on ({n}) dismissed findings</summary>

   {Each finding: "- [Description]\n - Location: `filename.ts:42`\n - Severity: 🔴 CRITICAL | 🟠 HIGH | 🟡 MEDIUM | 🔵 LOW | ⚪ INFO\n - Confidence: 🔵 LOW\n - Rationale: [Why dismissed]"}

   </details>
   ```

   Omit any section with zero findings entirely — do not render an empty heading. For `<details>` sections, omit them entirely if the count is zero.

6. Check the `--output` argument to determine the output destination. If `--output` is omitted, default to `chat` regardless of CI. Use `github` only when the user authorized publication through a configured workflow; otherwise retain the report locally or in chat.

   ### Output: `chat`

   Default when `--output` is omitted.
   1. Return the report directly to the user in the chat.
   2. Do **NOT** write any files.

   ### Output: `file`
   1. If `--output-dir <path>` is specified, write to `<path>/security-review-YYYY-MM-DD-{identifier}.md`. Otherwise write to the current working directory.
   2. `{identifier}` is the PR number (e.g., `PR123`), commit SHA (short), or `local`.
   3. Do **NOT** use `gh pr comment`, `gh api`, or any MCP posting tool.
   4. Confirm the file path to the user after writing.

   ### Output: `github`

   Selected only with explicit authorization for the configured publishing workflow.
   1. Write the report to `/tmp/review-summary.md` using the **Write** tool.
   2. Append `\n\n<!-- bitwarden-security-code-review -->` at the end of the file content.
   3. Do **NOT** use `gh pr comment`, `gh api`, or any MCP posting tool.
   4. Confirm to the user: "Report written to `/tmp/review-summary.md` for workflow pickup."

   The workflow post-step will read this file and update the placeholder comment automatically.

7. Clear the temporary diff file created by this run. If step 1B created `DIFF_FILE`, use the **Write tool** to overwrite it with an empty string, in every output mode and whether or not findings were reported. The file remains, empty. A run that aborted in step 1-A2 never created one. Do not report this step.
