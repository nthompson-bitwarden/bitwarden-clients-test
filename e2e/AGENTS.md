# Working in e2e

This file applies to `e2e/` and its descendants. Read relevant project instructions before editing code outside this directory. Work from the user's requested scope; load the workflows below only when they match the task.

## Project and verification

- Read [BUILDING.md](BUILDING.md) for client build commands, prerequisites, settings, and teardown, and [Server/README.md](Server/README.md) for local server setup.
- This is a .NET 10 NUnit project using `Dramaturge` and `Dramaturge.NUnit`, currently pinned to 0.0.2 in [e2e.csproj](e2e.csproj). Verify installed APIs before using upstream-main examples.
- Tests inherit the client-specific `BaseTest` in `Web/`, `Browser/`, or `Desktop/`. Reuse their managed server/build/browser lifecycle, `Page`, `Group`, `ServerUrl`, and seeded `Account`. Fixtures are sequential because accounts and build outputs are shared. Do not add independent launches or parallel fixtures by following generic framework examples.
- From the repository root, `dotnet test e2e/e2e.csproj` runs the suite. Target a client with `--filter FullyQualifiedName~e2e.Web.Tests`, or infrastructure with `--filter TestCategory=Infrastructure`. These tests may build clients and start Docker/browser/Electron processes; choose checks appropriate to the change and available prerequisites. Do not claim tests passed if they were not run.
- Keep generated logs, local review reports, and fresh profiles under ignored `e2e/TestResults/`. Use `e2e/TestResults/agent-artifacts/` for agent reports unless the caller chooses another destination. Never put live credentials or vault data into source, reports, or transcripts.

## Using the shared workflows

The canonical skills live in `.agents/skills/<name>/SKILL.md`. Read the selected entrypoint and [portable execution contract](.agents/PORTABILITY.md) before following it, then load only the needed references. Resolve links relative to their containing file. The catalog also works for agents without automatic skill discovery: open the linked files directly. From the monorepo root, explicitly load this file or start the agent in `e2e/`.

Roles and former commands are skills, not proprietary subagent definitions. Use host-native tools and delegate only when available and permitted; otherwise execute the scoped passes sequentially and disclose reduced independence. Reviews default to local files. Publishing, pushing, filing issues, or changing external state requires authorization within the user's task; available tools, comment markers, and CI do not supply it. No skill metadata grants permissions. Existing user authorization persists.

### bitwarden-delivery-tools

| Skill                                                                      | Use when                                                                           |
| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| [applying-pr-conventions](.agents/skills/applying-pr-conventions/SKILL.md) | Compose PR titles, template bodies, and review labels.                             |
| [architecting-solutions](.agents/skills/architecting-solutions/SKILL.md)   | Design solutions within Bitwarden architectural constraints.                       |
| [committing-changes](.agents/skills/committing-changes/SKILL.md)           | Prepare meaningful, scoped commits using Bitwarden conventions.                    |
| [creating-pull-request](.agents/skills/creating-pull-request/SKILL.md)     | Review, preview, and submit a branch as a pull request.                            |
| [filing-breakdown-tasks](.agents/skills/filing-breakdown-tasks/SKILL.md)   | Turn a finished tasks.md into linked Jira ticket drafts.                           |
| [force-multiplier](.agents/skills/force-multiplier/SKILL.md)               | Plan and execute a consistent change across many authorized repositories/projects. |
| [labeling-changes](.agents/skills/labeling-changes/SKILL.md)               | Choose conventional commit types and t: labels.                                    |
| [perform-preflight](.agents/skills/perform-preflight/SKILL.md)             | Verify implementation before committing or preparing a PR.                         |

### bitwarden-code-review

| Skill                                                                                            | Use when                                                                      |
| ------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| [addressing-code-review-comments](.agents/skills/addressing-code-review-comments/SKILL.md)       | Evaluate, implement, and respond to review feedback with evidence.            |
| [avoiding-false-positives](.agents/skills/avoiding-false-positives/SKILL.md)                     | Validate findings and stacked-PR completeness assumptions.                    |
| [bitwarden-code-reviewer](.agents/skills/bitwarden-code-reviewer/SKILL.md)                       | Perform evidence-based code review with classification and validation.        |
| [classifying-review-findings](.agents/skills/classifying-review-findings/SKILL.md)               | Assign Bitwarden finding categories and severity.                             |
| [code-review](.agents/skills/code-review/SKILL.md)                                               | Review an identified GitHub PR; draft locally unless posting is requested.    |
| [code-review-local](.agents/skills/code-review-local/SKILL.md)                                   | Review local changes or a PR and write only local files.                      |
| [performing-multi-agent-code-review](.agents/skills/performing-multi-agent-code-review/SKILL.md) | Run architecture, quality, bug, security, validation, and severity passes.    |
| [posting-bitwarden-review-comments](.agents/skills/posting-bitwarden-review-comments/SKILL.md)   | Format and route inline findings to local drafts or authorized GitHub output. |
| [posting-review-summary](.agents/skills/posting-review-summary/SKILL.md)                         | Format final review summaries and No Verdict results.                         |
| [reviewing-dependency-changes](.agents/skills/reviewing-dependency-changes/SKILL.md)             | Review dependency process, version changes, and lockfile hygiene.             |

### bitwarden-software-engineer

| Skill                                                                              | Use when                                                           |
| ---------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| [bitwarden-software-engineer](.agents/skills/bitwarden-software-engineer/SKILL.md) | Implement stories, tasks, bugs, and regressions with verification. |

### bitwarden-security-engineer

| Skill                                                                                      | Use when                                                                |
| ------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------- |
| [analyzing-code-security](.agents/skills/analyzing-code-security/SKILL.md)                 | Trace exploitable security defects using OWASP/CWE checklists.          |
| [bitwarden-security-context](.agents/skills/bitwarden-security-context/SKILL.md)           | Load P01–P06 principles, vocabulary, and data classifications.          |
| [bitwarden-security-engineer](.agents/skills/bitwarden-security-engineer/SKILL.md)         | Coordinate application security analysis and remediation.               |
| [detecting-secrets](.agents/skills/detecting-secrets/SKILL.md)                             | Detect and remediate exposed credentials and unsafe secret handling.    |
| [perform-security-review](.agents/skills/perform-security-review/SKILL.md)                 | Coordinate scoped security analysis and a verification pass.            |
| [reviewing-dependencies](.agents/skills/reviewing-dependencies/SKILL.md)                   | Analyze package vulnerabilities and software supply-chain risks.        |
| [reviewing-security-architecture](.agents/skills/reviewing-security-architecture/SKILL.md) | Review authentication, encryption, authorization, and trust boundaries. |

### bitwarden-testing-tools

| Skill                                                                        | Use when                                                    |
| ---------------------------------------------------------------------------- | ----------------------------------------------------------- |
| [assessing-test-coverage](.agents/skills/assessing-test-coverage/SKILL.md)   | Assess existing automated and manual coverage for a change. |
| [recommending-test-layers](.agents/skills/recommending-test-layers/SKILL.md) | Choose which tests belong at each layer for a change.       |

### dramaturge

| Skill                                                            | Use when                                                                   |
| ---------------------------------------------------------------- | -------------------------------------------------------------------------- |
| [dramaturge](.agents/skills/dramaturge/SKILL.md)                 | Write and debug C# browser automation and NUnit tests over WebDriver BiDi. |
| [dramaturge-session](.agents/skills/dramaturge-session/SKILL.md) | Explore a live browser through CLI sessions and export NUnit test drafts.  |

## Maintaining the bundle

See [.agents/README.md](.agents/README.md) for source revisions, adaptation decisions, optional integrations, license notices, and update instructions. Validate instruction-only changes with `python3 e2e/.agents/scripts/validate.py` from the repository root (requires PyYAML).
