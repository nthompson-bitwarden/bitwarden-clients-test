# Shared e2e agent workflows

Start with [e2e/AGENTS.md](../AGENTS.md). This bundle adapts the skills, roles, review commands, and supporting resources from the five requested Bitwarden plugin directories and Dramaturge's `skills/` tree, at pinned revisions. It contains 30 discoverable skills: 25 original skill entrypoints, three roles converted to skills, and two review commands converted to skills. Supporting references and examples for the retained skills are included; their source-to-destination mapping is in [upstream-manifest.json](upstream-manifest.json).

## Format and discovery

Researched 2026-10-17 against the primary specifications:

- [Agent Skills specification](https://agentskills.io/specification): folders with `SKILL.md`, required name/description YAML, optional references/scripts/assets, and progressive disclosure.
- [Client integration guidance](https://agentskills.io/client-implementation/adding-skills-support): `.agents/skills/` is a cross-client discovery convention. The specification itself does not mandate a storage location.
- [AGENTS.md](https://agents.md/): scoped, ordinary Markdown instructions for coding agents.

The canonical layout is `e2e/.agents/skills/<name>/SKILL.md`, with explicit links from `e2e/AGENTS.md`. No universal standard exists for launching subagents or installing plugin manifests. Role and command behavior is represented as ordinary skills so agents can execute it with their own capabilities. Clients without nested discovery must explicitly read the entrypoint or configure this directory. A minimal `e2e/CLAUDE.md` import provides the entrypoint; load skills through the linked catalog in `e2e/AGENTS.md`. No root-level instructions or global agent settings were changed.

## Pinned sources and licenses

| Source                                                                                                                        | Revision                                   | Included paths                                                                                                                                                                       | Terms                                                         |
| ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------- |
| [bitwarden/ai-plugins](https://github.com/bitwarden/ai-plugins/tree/1fc3d580ba45267f15131bea850c9cf39091d716)                 | `1fc3d580ba45267f15131bea850c9cf39091d716` | `plugins/bitwarden-delivery-tools`, `plugins/bitwarden-code-review`, `plugins/bitwarden-software-engineer`, `plugins/bitwarden-security-engineer`, `plugins/bitwarden-testing-tools` | [Bitwarden License Agreement](licenses/bitwarden/LICENSE.txt) |
| [webdriverbidi-net/dramaturge](https://github.com/webdriverbidi-net/dramaturge/tree/b95fcbbd6694873f3ca848ef1437e08eba2607cd) | `b95fcbbd6694873f3ca848ef1437e08eba2607cd` | `skills/`                                                                                                                                                                            | [MIT](licenses/dramaturge/LICENSE)                            |

The Bitwarden source license is retained verbatim, including its internal development/testing terms. Portable, open instruction formats do not relicense the source content. Each entrypoint records its immutable upstream URL and revision plus a local license reference. The manifest records source hashes for the retained files and explicitly lists excluded upstream paths.

## Adaptations

- Treat this repository as a permanent standalone fork: PRs explicitly target origin, with no upstream-parent fallback. PR creation has no AI review label prompt or label application because those labels do not exist here.
- Flattened skill discovery into one canonical directory without name collisions. Converted agent roles and review commands into standard skills; retained their supporting files.
- Normalized YAML, including malformed unquoted upstream descriptions. Merged custom routing criteria into supported descriptions/body. Removed client execution frontmatter such as `tools`, `model`, `skills`, `context`, `background`, `agent`, and command-only fields.
- Replaced skill invocations with local relative links; resolved moved shared security references and plugin-root paths. Replaced client-specific tool names and plugin-data paths with host capabilities and repository-local artifacts.
- Reworked review entrypoints, model selection, optional reviewer dependencies, output routing, and permission assumptions. Preserved finding schemas, severity rubrics, validation stages, local-output boundaries, explicit-base validation, and No Verdict handling. Bundled a [configuration and skill review checklist](references/agent-configuration-review.md) instead of requiring proprietary reviewer plugins.
- Added sequential fallbacks when delegation is unavailable or disallowed, with disclosure that validation is not independent. Scanner unavailability/denial never counts as a clean result. External publication requires authorization rather than being inferred from CI/tool availability.
- Adapted Dramaturge instructions to the existing NUnit base classes and pinned 0.0.2 dependencies. Main-branch examples remain references, with explicit version checks before use. CLI session support is optional and separate from the library packages.

Read [PORTABILITY.md](PORTABILITY.md) for runtime mappings and optional Jira/Confluence, Aikido, LaunchDarkly, Testmo, and GitHub dependencies. Local drafts and supplied evidence remain usable without those services; do not claim unavailable remote data was checked.

## Included resources

Original plugin descriptors, client settings, plugin-level documentation, and evaluation fixtures/runners are not bundled. Their source paths are listed as exclusions in the manifest; retrieve them from the pinned upstream revision if needed.

The fork omits feature-flag management, initiative/ownership transitions, plugin and HackerOne audits, threat-model artifact generation, scanner triage, and manual test-case authoring workflows. No hooks, MCP servers, packages, or global tools are installed by this bundle.

## Validation and updates

The validator requires Python 3 and PyYAML (dependency range in [requirements.txt](requirements.txt)); it makes no network or model calls:

```bash
python3 e2e/.agents/scripts/validate.py
```

It checks standard frontmatter, discovery/catalog coverage, local Markdown links outside examples/code blocks, portable runtime invariants, license links and integrity, manifest destination hashes, and optional upstream source coverage accounting for the listed exclusions. To compare against fetched source checkouts:

```bash
python3 e2e/.agents/scripts/validate.py --bitwarden-source /path/to/ai-plugins --dramaturge-source /path/to/dramaturge
```

For updates, fetch the desired source revisions into temporary checkouts, compare the manifest's source hashes to those trees, and review every changed source file. Update the retained skills/resources and license files as appropriate; repeat the portability adaptations above instead of overwriting local skills wholesale. Record new source revisions/hashes, maintain one mapping per retained source file, and review the exclusion list when upstream files change. Keep local additions outside the source mapping, update the catalog when skills change, then refresh adapted destination hashes with `--refresh-hashes` and validate again. Refreshing hashes acknowledges local edits; it does not prove their quality or update upstream source pins. Review the diff before committing.
