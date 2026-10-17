#!/usr/bin/env python3
"""Validate the portable e2e instruction bundle without network or model calls."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit

try:
    import yaml
except ImportError:
    sys.exit("PyYAML is required; install e2e/.agents/requirements.txt in your Python environment.")

BASE = Path(__file__).resolve().parents[1]
ALLOWED_FIELDS = {"name", "description", "license", "compatibility", "metadata", "allowed-tools"}
NAME = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*\Z")
LINK = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
RUNTIME_TOKENS = re.compile(r"\$\{CLAUDE_|\$ARGUMENTS\b|\bmcp__|Skill\([^)]*\)|\bsubagent_type\b")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prose(text: str) -> str:
    """Exclude fenced/inline code so illustrative paths are not treated as imports."""
    lines = []
    fence = None
    for line in text.splitlines():
        match = re.match(r"^\s*(`{3,}|~{3,})", line)
        if match:
            marker = match[1][0]
            if fence is None:
                fence = marker
            elif fence == marker:
                fence = None
            continue
        if fence is None:
            lines.append(re.sub(r"`[^`]*`", "", line))
    return "\n".join(lines)


def validate(args: argparse.Namespace) -> int:
    errors = []
    warnings = []

    def check(condition: bool, message: str) -> None:
        if not condition:
            errors.append(message)

    def local_path(value: str) -> Path:
        path = (BASE / value).resolve()
        if not path.is_relative_to(BASE):
            raise ValueError(f"Manifest path escapes bundle: {value}")
        return path

    catalog = (BASE.parent / "AGENTS.md").read_text()
    entrypoints = sorted((BASE / "skills").glob("*/SKILL.md"))
    check(bool(entrypoints), "No discoverable skills found")
    seen = set()
    for path in entrypoints:
        label = str(path.relative_to(BASE))
        text = path.read_text()
        match = re.match(r"\A---\n(.*?)\n---\n(.*)\Z", text, re.S)
        if not match:
            errors.append(f"{label}: missing or malformed YAML delimiters")
            continue
        try:
            meta = yaml.safe_load(match[1])
        except yaml.YAMLError as exc:
            errors.append(f"{label}: invalid YAML: {exc}")
            continue
        if not isinstance(meta, dict):
            errors.append(f"{label}: frontmatter must be a mapping")
            continue
        check(not set(meta) - ALLOWED_FIELDS, f"{label}: unsupported frontmatter fields")
        name, description = meta.get("name"), meta.get("description")
        valid_name = isinstance(name, str) and 1 <= len(name) <= 64 and bool(NAME.fullmatch(name))
        check(valid_name, f"{label}: invalid standard skill name")
        check(name == path.parent.name, f"{label}: name must match directory")
        check(name not in seen, f"{label}: duplicate skill name {name}")
        if isinstance(name, str):
            seen.add(name)
        check(isinstance(description, str) and 1 <= len(description) <= 1024,
              f"{label}: description must be a string of 1–1024 characters")
        if "compatibility" in meta:
            value = meta["compatibility"]
            check(isinstance(value, str) and 1 <= len(value) <= 500, f"{label}: invalid compatibility")
        metadata = meta.get("metadata", {})
        check(isinstance(metadata, dict) and all(isinstance(k, str) and isinstance(v, str)
                                               for k, v in metadata.items()), f"{label}: invalid metadata")
        license_ref = meta.get("license")
        check(isinstance(license_ref, str) and (path.parent / license_ref).is_file(),
              f"{label}: bundled license reference is missing")
        check(bool(match[2].strip()), f"{label}: empty workflow body")
        check(f".agents/skills/{path.parent.name}/SKILL.md" in catalog, f"{label}: missing from AGENTS.md")
        check("../../PORTABILITY.md" in text, f"{label}: missing execution contract")
        if len(text.splitlines()) > 500:
            warnings.append(f"{label}: exceeds recommended 500-line entrypoint size")
        # The experimental field is legal in the spec, but this bundle deliberately grants nothing.
        check("allowed-tools" not in meta, f"{label}: bundle must not imply execution grants")

    active_docs = list((BASE / "skills").rglob("*.md")) + list((BASE / "references").rglob("*.md"))
    docs = active_docs + [BASE / "README.md", BASE / "PORTABILITY.md", BASE.parent / "AGENTS.md"]
    for path in docs:
        text = path.read_text()
        # Example documents intentionally contain hypothetical artifact paths.
        if "examples" not in path.relative_to(BASE.parent).parts:
            for target in LINK.findall(prose(text)):
                target = target.strip().strip("<>")
                if not target or urlsplit(target).scheme or target.startswith(("#", "/")):
                    continue
                check((path.parent / unquote(target.split("#", 1)[0])).exists(),
                      f"{path.relative_to(BASE.parent)}: broken local link {target}")
        if path in active_docs:
            check(not RUNTIME_TOKENS.search(text), f"{path.relative_to(BASE)}: unadapted runtime token")

    manifest_path = BASE / "upstream-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    check(manifest.get("schema_version") == 1, "Unknown manifest schema version")
    expected_sources = {repo: set() for repo in manifest["sources"]}
    destinations = set()
    for item in manifest["files"]:
        source = item["source"]
        repo = item["repository"]
        check(source not in expected_sources[repo], f"Duplicate source mapping: {repo}:{source}")
        expected_sources[repo].add(source)
        check(item["destination"] not in destinations, f"Duplicate destination mapping: {item['destination']}")
        destinations.add(item["destination"])
        dest = local_path(item["destination"])
        if not dest.is_file():
            errors.append(f"Missing imported artifact: {item['destination']}")
            continue
        digest = sha(dest)
        if args.refresh_hashes:
            item["destination_sha256"] = digest
        check(item.get("destination_sha256") == digest, f"Changed artifact hash: {item['destination']}")
        if item["kind"] == "license":
            check(item["source_sha256"] == digest, f"License is not verbatim: {item['destination']}")
    for repo, source_dir in [("bitwarden", args.bitwarden_source), ("dramaturge", args.dramaturge_source)]:
        if source_dir is None:
            continue
        root = source_dir.resolve()
        source_info = manifest["sources"][repo]
        revision = subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"],
                                  capture_output=True, text=True, check=False)
        check(revision.returncode == 0 and revision.stdout.strip() == source_info["revision"],
              f"{repo}: source checkout does not match pinned revision")
        actual = set()
        for subdir in source_info["paths"]:
            actual.update(str(p.relative_to(root)) for p in (root / subdir).rglob("*") if p.is_file())
        actual.update(str(p.relative_to(root)) for p in root.glob("LICENSE*") if p.is_file())
        excluded = set(source_info.get("excluded_paths", []))
        check(excluded <= actual, f"{repo}: excluded paths no longer exist: {sorted(excluded - actual)}")
        check(not excluded & expected_sources[repo], f"{repo}: retained sources must not also be excluded")
        actual -= excluded
        check(actual == expected_sources[repo],
              f"{repo}: incomplete source coverage; missing={sorted(actual - expected_sources[repo])}, "
              f"extra={sorted(expected_sources[repo] - actual)}")
        for item in manifest["files"]:
            if item["repository"] == repo:
                source_path = root / item["source"]
                check(source_path.is_file() and sha(source_path) == item["source_sha256"],
                      f"{repo}: source hash mismatch: {item['source']}")
    check((BASE.parent / ".claude/skills").is_symlink() and
          (BASE.parent / ".claude/skills").resolve() == (BASE / "skills").resolve(),
          "Claude skill bridge must point to the canonical skills directory")
    check("@AGENTS.md" in (BASE.parent / "CLAUDE.md").read_text(), "Missing Claude instruction import")
    if args.refresh_hashes and not errors:
        manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    for message in warnings:
        print(f"WARNING: {message}")
    for message in errors:
        print(f"ERROR: {message}", file=sys.stderr)
    if errors:
        print(f"Validation failed: {len(errors)} error(s).", file=sys.stderr)
        return 1
    print(f"Validated {len(entrypoints)} skills, {len(manifest['files'])} imported files, "
          "catalog, links, provenance, licenses, and discovery bridges.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bitwarden-source", type=Path, help="Optional pinned ai-plugins checkout")
    parser.add_argument("--dramaturge-source", type=Path, help="Optional pinned Dramaturge checkout")
    parser.add_argument("--refresh-hashes", action="store_true", help="Acknowledge adapted artifact edits")
    args = parser.parse_args()
    try:
        return validate(args)
    except (OSError, ValueError, KeyError, TypeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
