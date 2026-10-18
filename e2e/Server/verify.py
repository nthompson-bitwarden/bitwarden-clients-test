"""Verify Seeder API provisioning, real password login, vault sync, and play cleanup."""

import base64
import hashlib
import json
from pathlib import Path
import secrets
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid


def request(url, payload=None, token=None, form=False, auth=None, play_id=None, method=None):
    headers = {"Bitwarden-Client-Version": "2026.10.0"}
    if payload is not None:
        headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
        payload = (urllib.parse.urlencode(payload) if form else json.dumps(payload)).encode()
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if auth:
        headers["Authorization"] = "Basic " + base64.b64encode(auth.encode()).decode()
    if play_id:
        headers["X-Play-Id"] = play_id
    req = urllib.request.Request(url, data=payload, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=30) as response:
        body = response.read()
        return json.loads(body) if body else None


def verify(manifest):
    settings = json.loads(manifest.read_text())
    server = settings["serverUrl"]
    seeder = settings["seederApiUrl"]
    auth = f"{settings['seederApiUsername']}:{settings['seederApiPassword']}"
    play_id = "e2e-smoke-" + uuid.uuid4().hex
    email = f"{play_id}@example.com"
    password = "E2e-" + secrets.token_hex(24) + "!"

    def seed(template, arguments):
        return request(f"{seeder}/seed", {"template": template, "arguments": arguments},
                       auth=auth, play_id=play_id)

    try:
        user = seed("SingleUserScene", {"email": email, "password": password, "emailVerified": True})
        email = user["mangleMap"].get(email) or email
        account = user["result"]
        owner = {"userId": account["userId"], "userKeyB64": account["decryptedKeyB64"]}
        folder = seed("UserFolderScene", {**owner, "folderName": "Smoke folder"})
        seed("UserLoginCipherScene", {**owner, "name": "Smoke login", "folderId": folder["result"]["folderId"]})
        prelogin = request(f"{server}/identity/accounts/prelogin/password", {"email": email})
        if prelogin["kdf"] != 0:
            raise ValueError("Smoke verification expects PBKDF2 accounts")
        key = hashlib.pbkdf2_hmac("sha256", password.encode(), email.encode(), prelogin["kdfIterations"], 32)
        password_hash = base64.b64encode(hashlib.pbkdf2_hmac("sha256", key, password.encode(), 1, 32)).decode()
        login = request(f"{server}/identity/connect/token", {
            "grant_type": "password", "username": email, "password": password_hash,
            "scope": "api offline_access", "client_id": "cli", "deviceType": "8",
            "deviceIdentifier": str(uuid.uuid4()), "deviceName": "E2E server smoke test",
        }, form=True)
        sync = request(f"{server}/api/sync", token=login["access_token"])
        if not sync.get("ciphers") or not sync.get("folders"):
            raise ValueError("Seeder API account has no encrypted vault data")
        print("Seeder API provisioning, password login, and vault sync passed.")
    finally:
        request(f"{seeder}/seed/{play_id}", auth=auth, method="DELETE")
    remaining = request(f"{seeder}/query", {
        "template": "UserEmailVerificationQuery", "arguments": {"email": email}
    }, auth=auth, play_id=play_id)
    if remaining["emailVerified"]:
        raise ValueError("Temporary smoke account still exists after play cleanup")
    print("Temporary smoke account cleanup verified.")


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1]))
    except urllib.error.HTTPError as error:
        print(f"Server verification failed: HTTP {error.code} at {error.url}", file=sys.stderr)
        sys.exit(1)
