# Report Template

## Model Header

`{model}` identifies the model actually used, or `host-managed` if unknown. List stage differences when known:

```
**Model:** session-model                              <!-- all stages at global -->
**Model:** session-model (audit: selected-audit-model)   <!-- differing stages listed -->
```

## Severity Icons

- 🛑 **Blocker** — Must fix before merge
- ⚠️ **Important** — Potential issue, should fix
- ♻️ **Refactor** — Code restructuring needed

## Source-Agent Friendly Names

Every finding carries a `source_agent` value (per `finding-shape.md`). Render it on each finding using the friendly label below — it tells the reader which subagent caught the issue, which aids triage and per-agent calibration.

| `source_agent` | Rendered label                |
| -------------- | ----------------------------- |
| `architect`    | Architecture agent            |
| `quality`      | Code quality agent            |
| `bug`          | Bug analysis agent            |
| `security`     | Security & logic agent        |
| `config`       | agent configuration agent     |
| `skill`        | Skill review agent            |
| `validation`   | Validation agent (collateral) |

## Template

```markdown
# Code Review: {PR title} (#{number}) <!-- or "Code Review: Local Changes — {YYYY-MM-DD}" -->

**Date:** {YYYY-MM-DD} | **Reviewed by:** {agent} | **Model:** {model}

## Summary

| Severity     | Count |
| ------------ | ----- |
| 🛑 Blocker   | {n}   |
| ⚠️ Important | {n}   |
| ♻️ Refactor  | {n}   |

{1-5 sentences for overall assessment.}

<!-- Only when a changed SKILL.md went unreviewed; name just the files not covered. A review that translated to zero findings is a pass, not a gap. Omit entirely otherwise. -->

**Not covered:** {What the review did not look at, and why, naming each file. E.g. "Skill review did not run — the skill-quality pass was unavailable, so `plugins/example/skills/doing-a-thing/SKILL.md` and `plugins/example/skills/doing-another/SKILL.md` were not checked for description quality, length, or progressive disclosure."}

## Findings

### 🛑 Blockers

#### {One-line summary (<100 chars)}

`{file/path.ext}:{line}`
**Caught by:** {Friendly agent label}

  <details><summary>Details</summary>
  {Explanation, why it matters, suggested fix. Include code snippets where helpful.}
  </details>

### ⚠️ Important

### ♻️ Refactor

<!-- Only if there are rejected findings. Omit entirely if all confirmed. -->

## Reviewed and Dismissed

   <details><summary>🔍 {n} initial findings dismissed after validation</summary>

   <!-- Repeat the stanza below once per dismissed finding. -->

#### {One-line summary}

`{file/path.ext}:{line}`
**Caught by:** {Friendly agent label}
**Original severity:** {🛑|⚠️|♻️} {Blocker|Important|Refactor}
**Original confidence:** {n}/100
**Dismissed at:** {Step 4 validation | Step 5 severity audit}
**Dismissed because:** {One-sentence rejection reason}

   </details>
```
