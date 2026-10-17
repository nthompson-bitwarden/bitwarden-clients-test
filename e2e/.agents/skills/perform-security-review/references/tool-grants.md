# Why the Tool Grants Look Like That

Background for the `allowed-tools` block and step 1-C of `perform-security-review`. The
operative rules are in `SKILL.md`; this file holds the reasoning.

## A `*` in a Bash rule absorbs whole flags

This is the fact everything below rests on, and it is easy to get wrong. A rule containing `*`
is compiled to an anchored regular expression with each `*` replaced by `.*` under the `s` flag.
`.*` therefore spans spaces, quotes, and entire arguments — it does not stop at a path
separator, a word boundary, or a closing quote.

So a rule cannot pin "this argument is the endpoint and nothing else may follow." Wherever a
`*` appears, an attacker-influenced command can insert flags into it and satisfy the literal
part of the pattern from inside a later argument.

Concretely, this rule looks endpoint-scoped and is not:

```
Bash(gh api --method GET -H "X-GitHub-Api-Version: 2026-03-10" "repos/*/*/code-scanning/alerts?*)
```

It compiles to:

```
^gh api --method GET -H "X-GitHub-Api-Version: 2026-03-10" "repos/.*/.*/code-scanning/alerts\?.*$
```

and matches all of these:

| command                                                                                            | effect              |
| -------------------------------------------------------------------------------------------------- | ------------------- |
| `gh api --method GET -H "…" "repos/O/R/code-scanning/alerts?state=open" --jq '.[]'`                | intended            |
| `gh api --method GET -H "…" "repos/O/R" -X DELETE --jq '"/code-scanning/alerts?y"'`                | `DELETE /repos/O/R` |
| `gh api --method GET -H "…" "repos/O/R" --method DELETE --template '{{.}}/code-scanning/alerts?z'` | `DELETE /repos/O/R` |

The second `*` swallows `R" -X DELETE --jq '"`, and the required literal
`code-scanning/alerts?` is satisfied inside the `--jq` program. `gh` resolves `--method` and
`-X` last-wins, so the request that goes out is a `DELETE` — and `DELETE /repos/{owner}/{repo}`
deletes the repository.

## Portable permission model

This adaptation removes `allowed-tools`; none of the skills pre-approves execution. Actual permissions come from the host and credential scope. Aikido, GitHub, and other integrations may all be unavailable or denied. Record each outcome honestly; an unavailable scanner is not a clean scan.

Prefer read-only `GH_TOKEN` scope and minimal workflow permissions for review jobs. A prose instruction or shell prefix is not a sandbox. `gh repo view <origin-owner/repo> --json defaultBranchRef` cannot select a destructive HTTP verb and is preferred for branch discovery. Explicit GET requests for secret scanning and Dependabot still require host permission and authenticated access.

Git diff/log options can write files (for example, `--output`), so validate refs and pass untrusted values as data. Never use contributor text as command syntax. Clear only the temporary diff created by the current run with the host's file-writing API; do not use wildcard recursive cleanup commands.
