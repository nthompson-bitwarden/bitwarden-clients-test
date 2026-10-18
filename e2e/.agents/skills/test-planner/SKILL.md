---
name: test-planner
description: Build a concrete plan for test additions from a user prompt by assessing existing coverage and recommending test layers. Produces an approval-ready handoff; does not implement tests.
license: ../../licenses/bitwarden/LICENSE.txt
---

Read [the portability guide](../../PORTABILITY.md) before following this workflow.

Use the user's prompt as the scope: a feature, regression, component, changed paths, PR, or supplied
requirements. Resolve the testable behaviors and acceptance criteria from that input and the actual
repository. Ask only for requirements that materially affect the plan and cannot be established from
available evidence. Planning writes reports only; do not change test or product code or start an
implementation agent.

## Build the plan

1. Run [assessing-test-coverage](../assessing-test-coverage/SKILL.md) for the requested surface. This is
   the existing skill for assessing current coverage. Produce its coverage report with evidence of
   existing tests, missing behaviors, inspected revisions, and inaccessible sources. Distinguish
   “no test found in inspected scope” from “not inspected.” For local work, cite local test paths and
   line numbers at the inspected revision, including relevant uncommitted changes; distinguish those
   from confirmed default-branch coverage. Do not infer measured coverage from test-file counts.
2. Run [recommending-test-layers](../recommending-test-layers/SKILL.md), supplying that coverage report
   plus the user's requirements. This is the existing skill for recommending a test layer. Produce
   its recommendation report, choosing the narrowest sufficient layer for each gap and explaining
   any additional layer. Preserve `unverified` criticality when the severity guide is unavailable.
   Apply the portability contract for optional integrations and this standalone fork: use supplied
   evidence or local inspection when remote services are unavailable, and identify the limitations.
3. Synthesize both reports into
   `e2e/TestResults/agent-artifacts/test-plans/<slug>-<timestamp>-plan.md`, resolving the path from the
   repository root and creating its parent directory. Use a filesystem-safe slug and a timestamp such
   as `YYYY-MM-DD-HHMMSS`. Include:
   - Original request, bounded scope, inspected revision/working-tree state, and explicit exclusions.
   - Plan revision and status `PENDING APPROVAL`, with approval evidence initially unset.
   - Paths to the coverage and layer reports, assumptions, and unavailable evidence.
   - A table with stable item IDs, behavior/acceptance criterion, existing coverage or gap evidence,
     proposed scenario and observable assertions, selected layer and rationale, and target files.
   - Required fixtures, isolated test data, doubles or real boundaries, setup/teardown, and any
     infrastructure or product-code changes that need approval as part of this plan.
   - Concrete verification commands appropriate to the chosen targets, prerequisites, expected
     outcomes, and criteria for completion. Read repository build/test configuration before choosing
     commands; identify commands that require unavailable services or environments.
   - Deferred recommendations and blockers, including unverified surfaces that could affect scope.

Avoid redundant additions where coverage already proves the requirement. If no additions are
warranted, explain that result with evidence rather than inventing work. For NUnit additions, plan
around the existing client base classes and
[NUnit test style](../nunit-test-style/SKILL.md); do not introduce independent client launches or hide
assertions in page objects.

Return the full plan path, revision, report paths, and a concise approval summary to the user or
calling agent. The [test engineer](../../agents/bitwarden-test-engineer/AGENT.md) owns approval and
the subsequent software-engineer handoff; this skill ends at planning.
