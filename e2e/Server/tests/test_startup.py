"""Run with python3 -m unittest discover -s e2e/Server/tests -v; no Docker required."""

from contextlib import redirect_stderr, redirect_stdout
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import configure
import server


class StartupTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="e2e server tests ")
        self.addCleanup(self.temporary.cleanup)
        self.state = Path(self.temporary.name) / "state"
        self.environment = patch.dict(os.environ, {}, clear=True)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.lifecycle = server.Server(Path(server.__file__).parent, self.state, "isolated-test")

    def configure(self):
        # Exercise configuration without a host OpenSSL dependency in unit tests.
        def certificate(arguments, **kwargs):
            if "-export" in arguments:
                (self.state / "identity" / "identity.pfx").write_bytes(b"test certificate")
            elif "-keyout" in arguments:
                (self.state / "identity" / "identity.key").write_bytes(b"test key")
        with patch.object(configure.subprocess, "run", side_effect=certificate):
            configure.configure(self.state)

    def test_fresh_configuration_is_private_and_only_writes_candidate_manifest(self):
        self.configure()
        self.assertFalse(self.lifecycle.manifest.exists())
        self.assertTrue(self.lifecycle.candidate.exists())
        self.assertEqual(self.state.stat().st_mode & 0o777, 0o700)
        for name in ["settings.json", "server.env", "compose.env", "test-environment.pending.json"]:
            self.assertEqual((self.state / name).stat().st_mode & 0o777, 0o600)
        self.assertFalse((self.state / "identity/identity.key").exists())

    def test_repeated_configuration_preserves_credentials_and_certificate(self):
        self.configure()
        settings = (self.state / "settings.json").read_bytes()
        certificate = (self.state / "identity/identity.pfx").read_bytes()
        os.environ["E2E_SERVER_PORT"] = "29999"
        with patch.object(configure.subprocess, "run") as openssl:
            configure.configure(self.state)
        openssl.assert_not_called()
        self.assertEqual((self.state / "settings.json").read_bytes(), settings)
        self.assertEqual((self.state / "identity/identity.pfx").read_bytes(), certificate)

    def test_persisted_configuration_is_validated(self):
        self.configure()
        settings_path = self.state / "settings.json"
        settings = json.loads(settings_path.read_text())
        settings["E2E_MAIL_PORT"] = settings["E2E_SERVER_PORT"]
        settings_path.write_text(json.dumps(settings))
        with self.assertRaises(ValueError):
            configure.configure(self.state)

    def test_fresh_invalid_ports_do_not_persist_settings(self):
        os.environ["E2E_SERVER_PORT"] = "99999"
        with self.assertRaises(ValueError):
            configure.configure(self.state)
        self.assertFalse((self.state / "settings.json").exists())

    def test_lock_rejects_second_owner_and_is_released_after_failure(self):
        with self.assertRaisesRegex(RuntimeError, "test failure"):
            with server.installation_lock(self.state):
                with self.assertRaises(server.StartupError):
                    with server.installation_lock(self.state):
                        self.fail("Second owner acquired lock")
                raise RuntimeError("test failure")
        with server.installation_lock(self.state):
            pass

    def test_missing_prerequisite_does_not_create_state(self):
        os.environ["E2E_STATE_DIR"] = str(self.state)
        with patch.object(server.shutil, "which", return_value=None), redirect_stderr(io.StringIO()) as output:
            self.assertEqual(server.main(["up"]), 1)
        self.assertIn("Missing prerequisite: docker", output.getvalue())
        self.assertFalse(self.state.exists())

    def test_daemon_failure_is_actionable_and_does_not_expose_output(self):
        error = subprocess.CalledProcessError(1, ["docker"], stderr="private daemon details")
        with patch.object(server.shutil, "which", return_value="/test/tool"), \
                patch.object(server, "run", side_effect=[subprocess.CompletedProcess([], 0, stdout="Docker Compose version v5.3.1"), error]):
            with self.assertRaisesRegex(server.StartupError, "Start Docker Desktop") as failure:
                server.preflight("up")
        self.assertNotIn("private daemon details", str(failure.exception))

    def test_old_compose_is_rejected(self):
        with patch.object(server.shutil, "which", return_value="/test/tool"), \
                patch.object(server, "run", return_value=subprocess.CompletedProcess([], 0, stdout="Docker Compose version v1.29.2")):
            with self.assertRaisesRegex(server.StartupError, "Compose v2"):
                server.preflight("up")

    def test_completed_source_is_validated_and_never_reset(self):
        source = self.state / "server"
        self.create_source(source)
        with patch.object(server, "run", return_value=subprocess.CompletedProcess([], 0, stdout="test-revision\n")) as run:
            with redirect_stdout(io.StringIO()):
                server.prepare_source(self.state)
        self.assertEqual(run.call_args.args[0][-2:], ["rev-parse", "HEAD"])
        self.assertEqual(run.call_count, 1)
        self.assertEqual((self.state / "server-revision.txt").read_text(), "test-revision\n")

    def create_source(self, source):
        (source / ".git").mkdir(parents=True)
        for name in server.REQUIRED_SOURCE_FILES:
            target = source / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.touch()

    def test_interrupted_staging_does_not_block_new_source_checkout(self):
        self.state.mkdir()
        old_staging = self.state / ".server-clone-interrupted"
        old_staging.mkdir()
        (old_staging / "user-marker").touch()
        def git(arguments, **kwargs):
            if arguments[1] == "init":
                self.create_source(Path(arguments[-1]))
            return subprocess.CompletedProcess(arguments, 0, stdout="test-revision\n")
        with patch.object(server, "run", side_effect=git), redirect_stdout(io.StringIO()):
            server.prepare_source(self.state)
        self.assertTrue((self.state / "server/src/Api/Dockerfile").exists())
        self.assertTrue((old_staging / "user-marker").exists())

    def test_failed_clone_removes_only_its_own_staging_directory(self):
        self.state.mkdir()
        marker = self.state / "unrelated"
        marker.touch()
        with patch.object(server, "run", side_effect=subprocess.CalledProcessError(1, ["git"])):
            with self.assertRaises(subprocess.CalledProcessError):
                server.prepare_source(self.state)
        self.assertEqual(list(self.state.iterdir()), [marker])

    def test_incomplete_existing_checkout_is_preserved(self):
        source = self.state / "server"
        source.mkdir(parents=True)
        marker = source / "local-edits"
        marker.write_text("preserve")
        with self.assertRaises(server.StartupError):
            server.prepare_source(self.state)
        self.assertEqual(marker.read_text(), "preserve")

    def test_manifest_is_published_only_after_verification(self):
        self.configure()
        with patch.object(server, "configure"), patch.object(server, "prepare_source"), \
                patch.object(self.lifecycle, "compose") as compose, patch.object(self.lifecycle, "build_images"):
            def verify(candidate):
                self.assertEqual(candidate, self.lifecycle.candidate)
                self.assertFalse(self.lifecycle.manifest.exists())
            with patch.object(server, "verify", side_effect=verify), redirect_stdout(io.StringIO()):
                self.lifecycle.up()
        self.assertTrue(self.lifecycle.manifest.exists())
        self.assertFalse(self.lifecycle.candidate.exists())
        self.assertEqual(compose.call_count, 3)

    def test_failed_build_health_or_verification_cannot_publish_readiness(self):
        for stage in ["source", "config", "build", "health", "nginx", "verification"]:
            with self.subTest(stage=stage):
                self.configure()
                self.lifecycle.manifest.write_text("stale manifest")
                fail = RuntimeError("private failure details")
                compose_results = [None, None, None]
                if stage in ["config", "health", "nginx"]:
                    compose_results[["config", "health", "nginx"].index(stage)] = fail
                with patch.object(server, "configure"), \
                        patch.object(server, "prepare_source", side_effect=fail if stage == "source" else None), \
                        patch.object(self.lifecycle, "compose", side_effect=compose_results), \
                        patch.object(self.lifecycle, "build_images", side_effect=fail if stage == "build" else None), \
                        patch.object(server, "verify", side_effect=fail if stage == "verification" else None):
                    with self.assertRaises(RuntimeError):
                        self.lifecycle.up()
                self.assertFalse(self.lifecycle.manifest.exists())
                self.assertFalse(self.lifecycle.candidate.exists())

    def test_reset_removes_readiness_but_down_preserves_settings(self):
        self.configure()
        self.lifecycle.manifest.write_text("test manifest")
        with patch.object(self.lifecycle, "compose") as compose:
            self.lifecycle.execute("down", [])
            self.assertTrue(self.lifecycle.manifest.exists())
            self.lifecycle.execute("reset", [])
        self.assertTrue((self.state / "settings.json").exists())
        self.assertFalse(self.lifecycle.manifest.exists())
        self.assertEqual(compose.call_args.args, ("down", "--volumes", "--remove-orphans"))

    def test_builds_at_most_two_images_at_once_and_skips_profiled_cli(self):
        model = {"services": {"admin": {"build": {}}, "api": {"build": {}},
                              "identity": {"build": {}}, "web": {"image": "test"},
                              "seeder-cli": {"build": {}, "profiles": ["tools"]}}}
        with patch.object(self.lifecycle, "compose", return_value=subprocess.CompletedProcess([], 0, stdout=json.dumps(model))) as compose:
            self.lifecycle.build_images()
        calls = [call.args for call in compose.call_args_list]
        self.assertEqual(calls, [("config", "--format", "json"), ("build", "admin", "api"), ("build", "identity")])

    def test_isolated_state_and_project_are_passed_to_compose(self):
        with patch.object(server, "run") as run:
            self.lifecycle.compose("ps", "--all")
        arguments = run.call_args.args[0]
        self.assertEqual(arguments[arguments.index("--project-name") + 1], "isolated-test")
        self.assertEqual(run.call_args.kwargs["env"]["E2E_STATE_DIR"], str(self.state))


if __name__ == "__main__":
    unittest.main()
