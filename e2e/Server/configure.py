"""Generate local-only installation settings; never overwrite an existing installation."""

import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import uuid


def write_env(path, values):
    # Compose's env-file parser supports single-quoted literal values.
    path.write_text("".join(f"{key}='{str(value).replace(chr(39), chr(92) + chr(39))}'\n"
                            for key, value in values.items()))
    path.chmod(0o600)


def configure(state):
    state.mkdir(parents=True, exist_ok=True)
    state.chmod(0o700)
    settings_path = state / "settings.json"
    if settings_path.exists():
        settings = json.loads(settings_path.read_text())
    else:
        settings = {
            "E2E_SERVER_PORT": os.environ.get("E2E_SERVER_PORT", "18090"),
            "E2E_MAIL_PORT": os.environ.get("E2E_MAIL_PORT", "18091"),
            "E2E_SEEDER_PORT": os.environ.get("E2E_SEEDER_PORT", "18092"),
            "E2E_WEB_IMAGE": os.environ.get("E2E_WEB_IMAGE", "ghcr.io/bitwarden/web:2026.9.0"),
            "E2E_DB_PASSWORD": "E2e!" + secrets.token_hex(24),
            "E2E_SEEDER_API_PASSWORD": secrets.token_hex(32),
            "identity_password": secrets.token_hex(32),
            "installation_id": os.environ.get("BW_INSTALLATION_ID", str(uuid.uuid4())),
            "installation_key": os.environ.get("BW_INSTALLATION_KEY", secrets.token_hex(32)),
            "internal_key": secrets.token_hex(32),
        }
    settings.setdefault("E2E_WEB_PORT", os.environ.get("E2E_WEB_PORT", "18080"))
    ports = ("E2E_SERVER_PORT", "E2E_MAIL_PORT", "E2E_SEEDER_PORT", "E2E_WEB_PORT")
    for key in ports:
        if not 1 <= int(settings[key]) <= 65535:
            raise ValueError(f"Invalid {key}")
    if len({int(settings[key]) for key in ports}) != len(ports):
        raise ValueError("Server, mail, Seeder API, and development Web ports must differ")
    uuid.UUID(settings["installation_id"])
    for key in ("E2E_DB_PASSWORD", "E2E_SEEDER_API_PASSWORD", "identity_password",
                "installation_key", "internal_key", "E2E_WEB_IMAGE"):
        if not isinstance(settings[key], str) or not settings[key].strip() or any(c in settings[key] for c in "\r\n"):
            raise ValueError(f"Invalid {key}")
    settings_path.write_text(json.dumps(settings, indent=2) + "\n")
    settings_path.chmod(0o600)

    identity = state / "identity"
    identity.mkdir(exist_ok=True)
    if not (identity / "identity.pfx").exists():
        cert_env = dict(os.environ, E2E_CERT_PASSWORD=settings["identity_password"])
        subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-sha256",
                        "-nodes", "-days", "3650", "-subj", "/CN=E2E IdentityServer",
                        "-keyout", str(identity / "identity.key"),
                        "-out", str(identity / "identity.crt")], check=True, capture_output=True)
        subprocess.run(["openssl", "pkcs12", "-export", "-out", str(identity / "identity.pfx"),
                        "-inkey", str(identity / "identity.key"), "-in", str(identity / "identity.crt"),
                        "-passout", "env:E2E_CERT_PASSWORD"], env=cert_env, check=True)
        # Upstream entrypoints drop privileges; only the private .state parent is host-accessible.
        (identity / "identity.pfx").chmod(0o644)
        (identity / "identity.key").unlink()

    url = f"http://localhost:{settings['E2E_SERVER_PORT']}"
    web_url = f"https://localhost:{settings['E2E_WEB_PORT']}"
    common = {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "globalSettings__selfHosted": "true",
        "globalSettings__databaseProvider": "sqlServer",
        "globalSettings__sqlServer__connectionString":
            f"Server=mssql;Database=vault;User Id=sa;Password={settings['E2E_DB_PASSWORD']};TrustServerCertificate=true",
        "globalSettings__baseServiceUri__vault": web_url,
        # The development vault and the released vault use the same localhost relying party.
        "globalSettings__fido2__origins__0": url,
        "globalSettings__fido2__origins__1": web_url,
        # Preserve the extension origins included by the upstream default FIDO configuration.
        "globalSettings__fido2__origins__2": "chrome-extension://nngceckbapebfimnlniiiahkandclblb/",
        "globalSettings__fido2__origins__3": "chrome-extension://hccnnhgbibccigepcmlgppchkpfdophk/",
        "globalSettings__fido2__origins__4": "chrome-extension://jbkfoedolllekgbhcbcoahefnbanhhlh/",
        "globalSettings__fido2__origins__5": "chrome-extension://ccnckbpmaceehanjmeomladnmlffdjgn/",
        "globalSettings__installation__id": settings["installation_id"],
        "globalSettings__installation__key": settings["installation_key"],
        "globalSettings__identityServer__certificatePassword": settings["identity_password"],
        "globalSettings__identityServer__certificateLocation": "/etc/bitwarden/identity/identity.pfx",
        "globalSettings__dataProtection__directory": "/etc/bitwarden/core/aspnet-dataprotection",
        "globalSettings__internalIdentityKey": settings["internal_key"],
        "globalSettings__oidcIdentityClientKey": settings["internal_key"],
        "globalSettings__duo__aKey": settings["internal_key"],
        "globalSettings__mail__replyToEmail": "e2e@example.com",
        "globalSettings__mail__smtp__host": "mail",
        "globalSettings__mail__smtp__port": "1025",
        "globalSettings__mail__smtp__ssl": "false",
        "globalSettings__disableUserRegistration": "false",
        "globalSettings__testPlayIdTrackingEnabled": "true",
        "globalSettings__enableCloudCommunication": "false",
        "globalSettings__pushRelayBaseUri": "",
        "globalSettings__attachment__baseUrl": f"{url}/attachments",
        "globalSettings__send__baseUrl": f"{url}/send",
    }
    # Keep backend endpoints independent of the manually started HTTPS development vault.
    for service in ("api", "identity", "admin", "notifications", "sso", "scim"):
        common[f"globalSettings__baseServiceUri__{service}"] = f"{url}/{service}"
    # Development appsettings otherwise select Azurite for CLI/API attachments.
    for service in ("attachment", "send", "organizationReport", "storage", "events", "notifications"):
        common[f"globalSettings__{service}__connectionString"] = ""
    write_env(state / "server.env", common)
    write_env(state / "compose.env", {key: value for key, value in settings.items() if key.startswith("E2E_")})
    manifest = {
        "serverUrl": url,
        "mailUrl": f"http://localhost:{settings['E2E_MAIL_PORT']}",
        "seederApiUrl": f"http://localhost:{settings['E2E_SEEDER_PORT']}",
        "seederApiUsername": "e2e",
        "seederApiPassword": settings["E2E_SEEDER_API_PASSWORD"],
    }
    (state / "test-environment.pending.json").write_text(json.dumps(manifest, indent=2) + "\n")
    (state / "test-environment.pending.json").chmod(0o600)


if __name__ == "__main__":
    configure(Path(sys.argv[1]).resolve())
