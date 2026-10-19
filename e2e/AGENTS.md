# Working in e2e

This file applies to `e2e/` and its descendants. Read relevant project instructions before editing code outside this directory. Work from the user's requested scope; load the workflows below only when they match the task.

## Project and verification

- Read [BUILDING.md](BUILDING.md) for client build commands, prerequisites, settings, and teardown, and [Server/README.md](Server/README.md) for local server setup.
- This is a .NET 10 NUnit project using `Dramaturge` and `Dramaturge.NUnit`, currently pinned to 0.0.2 in [e2e.csproj](e2e.csproj). Verify installed APIs before using upstream-main examples.
- Tests inherit the client-specific `BaseTest` in `Web/`, `Browser/`, or `Desktop/`, reusing `Page`, `Group`, `ServerUrl`, and per-test Seeder API `Account` / `Seeds`. Web's base inherits Dramaturge NUnit `PageTest`, using the registered shared browser group and an isolated browser/page per test. Web tests read service configuration, provision isolated data through `SeederApi/`, and navigate to a manually started Web server; start the backend and Web script first as documented in `BUILDING.md`. Do not add Web builds, server startup, readiness polling, or service teardown to tests. Browser and Desktop retain their managed backend/build setup and custom per-test launches. Client base classes set `[Parallelizable(ParallelScope.All)]` and `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`. Keep mutable resources per test and isolate backend mutations; build preparation remains synchronized by the runtime. Do not add independent launches by following generic framework examples.
- Each client namespace has its own `SetupFixture.cs` for browser/headless defaults and per-client environment overrides. Only Web registers `DramaturgeSetUpFixture` (0.0.2 permits one registration). Browser's selection must also control its extension build target. Desktop attaches to Electron's Chromium engine; headless Linux runs use `xvfb-run`.
- From the repository root, `dotnet test e2e/e2e.csproj` runs the suite. Target a client with `--filter FullyQualifiedName~e2e.Web.Tests`, or infrastructure with `--filter TestCategory=Infrastructure`. These tests may build clients and start Docker/browser/Electron processes; choose checks appropriate to the change and available prerequisites. Do not claim tests passed if they were not run.
- Keep generated logs, local review reports, and fresh profiles under ignored `e2e/TestResults/`. Use `e2e/TestResults/agent-artifacts/` for agent reports unless the caller chooses another destination. Never put live credentials or vault data into source, reports, or transcripts.

## Test structure

- Keep inheritance to the client `BaseTest` only; do not add feature or scenario base classes. Keep fixture setup, resources, and assertions in concrete test classes, and reusable UI interactions in page/component objects.
- Follow [nunit-test-style](.agents/skills/nunit-test-style/SKILL.md) for straight-line shared `[SetUp]` steps, scenario fixtures, and inherited parallel/lifecycle defaults. Setup must not contain conditional test-case dispatch.
- Keep tests in explicit `//Arrange`, `//Act`, and `//Assert` sections, in that order. Set up inputs and page/component objects in Arrange, perform interactions in Act, and verify outcomes in Assert.
- When an Arrange, Act, or Assert block contains multiple logical stages, separate those stages with descriptive, unnumbered substep comments, such as `// Act - Rotate the API key` and `// Act - Retrieve the key after reloading`. Keep the main section markers above; name the purpose of each stage rather than using numbered steps.
- Name variables, fields, properties, and parameters that hold custom page objects with a `Page` suffix and custom component objects with a `Component` suffix, including aliases and assignment targets. For example, use `var vaultPage = new VaultPage(Page)`, `_loginPage`, `SecurityKeysPage`, and `LoginFormComponent`; use `var apiKeyDialogComponent = new ApiKeyDialog(Page)` for a component. This rule does not apply to raw Dramaturge `Page` instances or element locators.
- Keep all assertions and expectations in test classes, never in page or component classes. Page/component objects expose locators and interaction methods; use navigation or element waits when an interaction needs synchronization rather than hiding an expectation in a helper.
- Keep UI iteration, branching, and observation aggregation in page/component objects. Tests declare readable scenario data and call named operations; page/component helpers may return observations or booleans, but must never contain assertions or expectations. Do not hide conditional test-case dispatch in setup helpers.
- Preserve the user's test and method names when refactoring unless a rename is requested.

## Using the shared workflows

This is a permanent standalone fork. PRs must always target the repository identified by `origin`, never `upstream` or the GitHub fork parent. Qualify GitHub commands with origin's repository and choose base branches within origin. Do not ask to add `ai-review` or `ai-review-vnext` labels; those labels do not exist here. See the [shared target rules](.agents/PORTABILITY.md#standalone-fork-and-github-targets).

Create PRs with `gh api --method POST "repos/<origin-owner/repo>/pulls"` from the first attempt. Do not use `gh pr create`, including with `--repo`, because it queries the upstream parent and can trigger upstream SAML enforcement. Use explicit head/base fields and title/body files as documented in the [PR creation workflow](.agents/skills/creating-pull-request/SKILL.md).

The canonical skills live in `.agents/skills/<name>/SKILL.md`. Read the selected entrypoint and [portable execution contract](.agents/PORTABILITY.md) before following it, then load only the needed references. Resolve links relative to their containing file. The catalog also works for agents without automatic skill discovery: open the linked files directly. From the monorepo root, explicitly load this file or start the agent in `e2e/`.

The software engineer, code reviewer, and test engineer are agents, defined under `.agents/agents/<name>/AGENT.md` and exposed to Codex through project-level `.codex/agents/*.toml` files. Delegate implementation to `bitwarden-software-engineer`, review to `bitwarden-code-reviewer`, and approval-gated test planning and delivery to `bitwarden-test-engineer` when available and permitted. The security role and former commands remain skills. Use host-native tools and delegate only when available and permitted; otherwise execute the scoped passes sequentially and disclose reduced independence. Reviews default to local files. Publishing, pushing, filing issues, or changing external state requires authorization within the user's task; available tools, comment markers, and CI do not supply it. No skill metadata grants permissions. Existing user authorization persists.

### Agents

| Agent                                                                              | Use when                                                               |
| ---------------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| [bitwarden-software-engineer](.agents/agents/bitwarden-software-engineer/AGENT.md) | Implement stories, tasks, bugs, and regressions with verification.     |
| [bitwarden-code-reviewer](.agents/agents/bitwarden-code-reviewer/AGENT.md)         | Perform evidence-based code review with classification and validation. |
| [bitwarden-test-engineer](.agents/agents/bitwarden-test-engineer/AGENT.md)         | Plan test additions, obtain approval, and delegate implementation.     |

Codex discovers these agents from the repository root through [.codex/agents](../.codex/agents). Other hosts can load the same definitions through their native delegation tools. Pass the assigned scope, references, output destination, and existing authorization explicitly; do not assume a child receives the parent conversation. If delegation is unavailable, follow the linked definition sequentially and disclose reduced independence.

### bitwarden-delivery-tools

| Skill                                                                      | Use when                                                                           |
| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| [applying-pr-conventions](.agents/skills/applying-pr-conventions/SKILL.md) | Compose PR titles and template bodies for this fork.                               |
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
| [classifying-review-findings](.agents/skills/classifying-review-findings/SKILL.md)               | Assign Bitwarden finding categories and severity.                             |
| [code-review](.agents/skills/code-review/SKILL.md)                                               | Review an identified GitHub PR; draft locally unless posting is requested.    |
| [code-review-local](.agents/skills/code-review-local/SKILL.md)                                   | Review local changes or a PR and write only local files.                      |
| [performing-multi-agent-code-review](.agents/skills/performing-multi-agent-code-review/SKILL.md) | Run architecture, quality, bug, security, validation, and severity passes.    |
| [posting-bitwarden-review-comments](.agents/skills/posting-bitwarden-review-comments/SKILL.md)   | Format and route inline findings to local drafts or authorized GitHub output. |
| [posting-review-summary](.agents/skills/posting-review-summary/SKILL.md)                         | Format final review summaries and No Verdict results.                         |
| [reviewing-dependency-changes](.agents/skills/reviewing-dependency-changes/SKILL.md)             | Review dependency process, version changes, and lockfile hygiene.             |

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

| Skill                                                                        | Use when                                                                                  |
| ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| [nunit-test-style](.agents/skills/nunit-test-style/SKILL.md)                 | Write or refactor NUnit fixtures, shared setup, and Arrange/Act/Assert tests.             |
| [assessing-test-coverage](.agents/skills/assessing-test-coverage/SKILL.md)   | Assess existing automated and manual coverage for a change.                               |
| [recommending-test-layers](.agents/skills/recommending-test-layers/SKILL.md) | Choose which tests belong at each layer for a change.                                     |
| [test-planner](.agents/skills/test-planner/SKILL.md)                         | Combine coverage assessment and layer recommendations into a concrete test-addition plan. |

### dramaturge

| Skill                                                            | Use when                                                                   |
| ---------------------------------------------------------------- | -------------------------------------------------------------------------- |
| [dramaturge](.agents/skills/dramaturge/SKILL.md)                 | Write and debug C# browser automation and NUnit tests over WebDriver BiDi. |
| [dramaturge-session](.agents/skills/dramaturge-session/SKILL.md) | Explore a live browser through CLI sessions and export NUnit test drafts.  |

## Maintaining the bundle

See [.agents/README.md](.agents/README.md) for source revisions, adaptation decisions, optional integrations, license notices, and update instructions. Validate instruction-only changes with `python3 e2e/.agents/scripts/validate.py` from the repository root (requires PyYAML).
