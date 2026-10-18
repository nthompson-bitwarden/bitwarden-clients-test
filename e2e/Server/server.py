"""Portable local-server lifecycle. Requires only standard-library Python on the host."""

import sys

if sys.version_info < (3, 10):
    sys.exit("Missing prerequisite: Python 3.10+.")

import argparse
from contextlib import contextmanager
import fcntl
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.error

from configure import configure
from verify import verify

SERVER_REF = "9129efbd1531d5d8472345638efad7ef25a7337a"
SOURCE_URL = "https://github.com/bitwarden/server.git"
MUTATING_COMMANDS = {"up", "seed", "api", "down", "reset", "verify"}
REQUIRED_SOURCE_FILES = (
    "src/Admin/Dockerfile", "src/Api/Dockerfile", "src/Identity/Dockerfile",
    "src/Notifications/Dockerfile", "src/Events/Dockerfile", "src/Icons/Dockerfile",
    "util/Attachments/Dockerfile", "bitwarden_license/src/Sso/Dockerfile",
    "bitwarden_license/src/Scim/Dockerfile", "util/SeederApi/SeederApi.csproj",
    "util/SeederUtility/SeederUtility.csproj", "util/RustSdk/RustSdk.csproj",
)


class StartupError(Exception):
    """An actionable failure safe to show without credentials or API bodies."""


def run(arguments, **kwargs):
    return subprocess.run(arguments, check=True, **kwargs)


def preflight(command):
    tools = ["docker"]
    if command == "up":
        tools += ["git", "openssl"]
    for tool in tools:
        if shutil.which(tool) is None:
            raise StartupError(f"Missing prerequisite: {tool}. See e2e/Server/README.md.")
    checks = [
        (["docker", "compose", "version"], "Docker Compose v2+ is required."),
        (["docker", "info", "--format", "{{.OSType}}/{{.Architecture}}"],
         "Docker is unavailable. Start Docker Desktop on macOS or the Docker daemon on Linux/WSL."),
    ]
    if command in {"up", "api", "seed"}:
        checks.append((["docker", "buildx", "version"], "Docker Buildx is required to build the server images."))
    for arguments, failure in checks:
        try:
            result = run(arguments, capture_output=True, text=True, timeout=30)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise StartupError(failure) from None
        if arguments[1] == "compose":
            version = re.search(r"version\s+v?(\d+)", result.stdout, re.IGNORECASE)
            if version is None or int(version.group(1)) < 2:
                raise StartupError("Docker Compose v2+ is required.")
        if arguments[1] == "info":
            if result.stdout.strip() not in {"linux/amd64", "linux/x86_64", "linux/arm64", "linux/aarch64"}:
                raise StartupError("Docker must run Linux AMD64 or ARM64 containers. See e2e/Server/README.md.")


@contextmanager
def installation_lock(state):
    state.mkdir(parents=True, exist_ok=True, mode=0o700)
    with (state / "lock").open("a") as handle:
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise StartupError("Another server.sh command is running for this state directory.") from None
        try:
            yield
        finally:
            fcntl.flock(handle, fcntl.LOCK_UN)


def prepare_source(state):
    source = state / "server"
    if not source.exists():
        # Only completed checkouts become 'server'; interrupted staging directories are never reused.
        staging = Path(tempfile.mkdtemp(prefix=".server-clone-", dir=state))
        try:
            run(["git", "init", "--quiet", str(staging)])
            run(["git", "-C", str(staging), "remote", "add", "origin", SOURCE_URL])
            run(["git", "-C", str(staging), "fetch", "--depth=1", "origin", os.environ.get("E2E_SERVER_REF", SERVER_REF)])
            run(["git", "-C", str(staging), "checkout", "--detach", "FETCH_HEAD"])
            validate_source(staging)
            staging.rename(source)
        finally:
            if staging.exists():
                shutil.rmtree(staging)
    validate_source(source)
    revision = run(["git", "-C", str(source), "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    (state / "server-revision.txt").write_text(revision + "\n")
    print(f"Server source revision: {revision}", flush=True)


def validate_source(source):
    if not (source / ".git").exists() or any(not (source / name).is_file() for name in REQUIRED_SOURCE_FILES):
        raise StartupError(
            "Server checkout is incomplete or incompatible. Preserve any local edits, move the checkout "
            "out of the state directory, and rerun server.sh up to fetch the pinned source.")


class Server:
    def __init__(self, directory, state, project):
        self.directory = directory
        self.state = state
        self.project = project
        self.manifest = state / "test-environment.json"
        self.candidate = state / "test-environment.pending.json"
        self.environment = dict(os.environ, E2E_SERVER_DIR=str(directory), E2E_STATE_DIR=str(state))

    def compose(self, *arguments, **kwargs):
        return run(["docker", "compose", "--project-name", self.project,
                    "--env-file", str(self.state / "compose.env"),
                    "--file", str(self.directory / "compose.yml"), *arguments], env=self.environment, **kwargs)

    def build_images(self):
        # Limit the build graph itself: a Compose/Bake parallel limit alone may still
        # compile every service at once and exhaust a typical developer Docker VM.
        model = json.loads(self.compose("config", "--format", "json", capture_output=True, text=True).stdout)
        services = [name for name, service in model["services"].items()
                    if "build" in service and not service.get("profiles")]
        for offset in range(0, len(services), 2):
            self.compose("build", *services[offset:offset + 2])

    def up(self):
        # An unsuccessful reconfiguration must not leave a stale readiness manifest behind.
        self.manifest.unlink(missing_ok=True)
        try:
            print("Preparing local server configuration and source...", flush=True)
            try:
                configure(self.state)
            except (ValueError, KeyError, TypeError):
                raise StartupError(
                    "Invalid server settings. Check settings.json (or first-run environment overrides): "
                    "use distinct valid ports, an installation UUID, and nonempty credentials/image settings.") from None
            prepare_source(self.state)
            self.compose("config", "--quiet")
            self.build_images()
            self.compose("up", "--detach", "--no-build", "--wait", "--wait-timeout", "600")
            # nginx resolves service names at startup; refresh after containers change addresses.
            self.compose("up", "--detach", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "120", "nginx")
            print("Verifying Seeder provisioning, login, sync, and cleanup...", flush=True)
            verify(self.candidate)
            self.candidate.replace(self.manifest)
        finally:
            self.candidate.unlink(missing_ok=True)
        print(f"Server ready. Test service configuration: {self.manifest}", flush=True)

    def execute(self, command, arguments):
        if command == "up":
            self.up()
            return
        if not (self.state / "compose.env").is_file():
            raise StartupError("No local server installation. Run server.sh up first.")
        if command == "verify":
            if not self.manifest.is_file():
                raise StartupError("No verified server installation. Run server.sh up first.")
            verify(self.manifest)
        elif command == "seed":
            if not arguments:
                raise StartupError("Pass SeederUtility arguments, for example: server.sh seed preset --list")
            self.compose("run", "--rm", "--build", "-T", "seeder-cli", *arguments)
        elif command == "api":
            self.compose("up", "--detach", "--build", "--wait", "--wait-timeout", "600", "seeder-api")
        elif command == "down":
            self.compose("down")
        elif command == "reset":
            self.compose("down", "--volumes", "--remove-orphans")
            self.manifest.unlink(missing_ok=True)
        elif command == "status":
            self.compose("ps", "--all")
        elif command == "logs":
            self.compose("logs", "--follow", "--tail", "100", *arguments)
        elif command == "config":
            self.compose("config")  # Explicitly requested output includes local secrets.


def main(argv=None):
    parser = argparse.ArgumentParser(
        prog="./e2e/Server/server.sh",
        description="Build and manage the isolated local e2e server. See e2e/Server/README.md.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""Commands:
  up       Fetch source, build, migrate, start, and verify (default)
  seed     Run explicit SeederUtility arguments
  api      Build and start the Seeder API
  down     Stop the stack, preserving test data
  reset    Stop and DELETE this project's database and vault volumes
  status   Show containers
  verify   Verify temporary provisioning, login, sync, and cleanup
  logs     Follow logs, optionally restricted to named services
  config   Print the Compose model, INCLUDING LOCAL SECRETS
  help     Show this help""")
    parser.add_argument("command", nargs="?", default="up",
                        choices=["up", "seed", "api", "down", "reset", "status", "verify", "logs", "config", "help"])
    parser.add_argument("arguments", nargs=argparse.REMAINDER, help="Seeder CLI arguments or log service names")
    args = parser.parse_args(argv)
    if args.command == "help":
        parser.print_help()
        return 0
    if args.arguments and args.command not in {"seed", "logs"}:
        parser.error(f"{args.command} accepts no additional arguments")
    directory = Path(__file__).resolve().parent
    state = Path(os.environ.get("E2E_STATE_DIR", directory / ".state")).expanduser().resolve()
    server = Server(directory, state, os.environ.get("E2E_COMPOSE_PROJECT", "bitwarden-e2e"))
    try:
        preflight(args.command)
        if args.command in MUTATING_COMMANDS:
            with installation_lock(state):
                server.execute(args.command, args.arguments)
        else:
            server.execute(args.command, args.arguments)
    except StartupError as error:
        print(str(error), file=sys.stderr)
        return 1
    except urllib.error.HTTPError as error:
        print(f"Server verification failed: HTTP {error.code}. Check api, identity, and seeder-api logs, then rerun up.", file=sys.stderr)
        return 1
    except urllib.error.URLError:
        print("Cannot reach the server for verification. Check server.sh status and logs, then rerun up.", file=sys.stderr)
        return 1
    except subprocess.CalledProcessError:
        # Do not stringify command arguments or captured output: they may contain secrets.
        print("Server command failed. Check the preceding output and run server.sh status or logs; rerun up after fixing the failure.", file=sys.stderr)
        return 1
    except Exception:
        # Configuration/API exception messages can contain secret values or response bodies.
        print("Server setup or verification failed. Check e2e/Server/README.md and server.sh status or logs. No readiness manifest was published by this attempt.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
