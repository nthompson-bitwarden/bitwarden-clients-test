"""Smoke-test real password login and vault sync for each CLI-seeded account."""

import base64
import hashlib
import json
from pathlib import Path
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid


def request(url, payload=None, token=None, form=False):
    headers = {"Bitwarden-Client-Version": "2026.10.0"}
    if payload is not None:
        headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
        payload = (urllib.parse.urlencode(payload) if form else json.dumps(payload)).encode()
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urllib.request.urlopen(urllib.request.Request(url, data=payload, headers=headers), timeout=30) as response:
        return json.load(response)


def verify(manifest):
    settings = json.loads(manifest.read_text())
    server = settings["serverUrl"]
    for client, account in settings["accounts"].items():
        email = account["email"].strip().lower()
        password = account["password"].encode()
        prelogin = request(f"{server}/identity/accounts/prelogin/password", {"email": email})
        if prelogin["kdf"] != 0:
            raise ValueError("CLI smoke verification expects PBKDF2 accounts")
        key = hashlib.pbkdf2_hmac("sha256", password, email.encode(), prelogin["kdfIterations"], 32)
        password_hash = base64.b64encode(hashlib.pbkdf2_hmac("sha256", key, password, 1, 32)).decode()
        login = request(f"{server}/identity/connect/token", {
            "grant_type": "password", "username": email, "password": password_hash,
            "scope": "api offline_access", "client_id": "cli", "deviceType": "8",
            "deviceIdentifier": str(uuid.uuid5(uuid.NAMESPACE_URL, f"e2e-smoke:{email}")),
            "deviceName": "E2E server smoke test",
        }, form=True)
        sync = request(f"{server}/api/sync", token=login["access_token"])
        if not sync.get("ciphers") or not sync.get("folders"):
            raise ValueError(f"{client} account has no CLI-seeded vault data")
        account["userId"] = str(uuid.UUID(sync["profile"]["id"]))
        print(f"{client}: password login and sync passed ({len(sync['ciphers'])} ciphers, {len(sync['folders'])} folders)")
    manifest.write_text(json.dumps(settings, indent=2) + "\n")


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1]))
    except urllib.error.HTTPError as error:
        print(f"Server verification failed: HTTP {error.code} at {error.url}", file=sys.stderr)
        sys.exit(1)
