#!/usr/bin/env python3
"""
TunnelFin Plugin Deployment Script

Builds the plugin, updates manifest, deploys to Jellyfin, restarts, and runs E2E tests.

Usage:
    python scripts/deploy.py [--skip-tests] [--version X.X.X.X]
"""

import os
import sys
import json
import subprocess
import hashlib
import argparse
import time
import requests
from pathlib import Path
from datetime import datetime, timezone


def load_env():
    """Load .env file from project root."""
    root = Path(__file__).parent.parent
    env_path = root / ".env"
    if env_path.exists():
        with open(env_path) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, value = line.split("=", 1)
                    os.environ.setdefault(key.strip(), value.strip())


def get_next_version(manifest_path: Path) -> str:
    """Get next version by incrementing the build number."""
    with open(manifest_path) as f:
        manifest = json.load(f)
    
    current = manifest[0]["versions"][0]["version"]
    parts = current.split(".")
    parts[-1] = str(int(parts[-1]) + 1)
    return ".".join(parts)


def build_plugin(version: str, root: Path) -> tuple[Path, str]:
    """Build the plugin and return zip path and checksum."""
    print(f"\n🔨 Building TunnelFin v{version}...")
    
    result = subprocess.run(
        ["./scripts/build-plugin.sh", version],
        cwd=root,
        capture_output=True,
        text=True
    )
    
    if result.returncode != 0:
        print(f"❌ Build failed:\n{result.stderr}")
        sys.exit(1)
    
    print(result.stdout)
    
    zip_path = root / f"tunnelfin_{version}.zip"
    if not zip_path.exists():
        print(f"❌ Expected zip not found: {zip_path}")
        sys.exit(1)
    
    # Calculate checksum
    with open(zip_path, "rb") as f:
        checksum = hashlib.md5(f.read()).hexdigest()
    
    print(f"✅ Built: {zip_path.name} (MD5: {checksum})")
    return zip_path, checksum


def update_manifest(manifest_path: Path, version: str, checksum: str):
    """Update manifest.json with new version."""
    print(f"\n📝 Updating manifest.json...")
    
    with open(manifest_path) as f:
        manifest = json.load(f)
    
    new_version = {
        "version": version,
        "changelog": f"v{version} - Service registration fix:\n- Added IChannel registration for TunnelFinChannel\n- Registered ITorrentEngine, IStreamManager, IIndexerManager services\n- Channel should now appear in Jellyfin UI",
        "targetAbi": "10.11.0.0",
        "sourceUrl": f"https://github.com/jefflouisma/TunnelFin/releases/download/v{version}/tunnelfin_{version}.zip",
        "checksum": checksum,
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    }
    
    # Insert at beginning of versions list
    manifest[0]["versions"].insert(0, new_version)
    
    with open(manifest_path, "w") as f:
        json.dump(manifest, f, indent=4)
    
    print(f"✅ Manifest updated with v{version}")


def deploy_to_jellyfin(zip_path: Path, jellyfin_url: str, username: str, password: str):
    """Deploy plugin to Jellyfin server."""
    print(f"\n🚀 Deploying to Jellyfin at {jellyfin_url}...")

    # Authenticate
    auth_url = f"{jellyfin_url}/Users/AuthenticateByName"
    headers = {
        "X-Emby-Authorization": 'MediaBrowser Client="TunnelFin Deploy", Device="Script", DeviceId="deploy-script", Version="1.0"'
    }

    auth_resp = requests.post(auth_url, json={"Username": username, "Pw": password}, headers=headers)
    if auth_resp.status_code != 200:
        print(f"❌ Authentication failed: {auth_resp.status_code}")
        sys.exit(1)

    token = auth_resp.json()["AccessToken"]
    headers["X-Emby-Token"] = token

    # Check for SSH deployment path
    ssh_host = os.environ.get("JELLYFIN_SSH_HOST")
    plugin_path = os.environ.get("JELLYFIN_PLUGIN_PATH")

    if ssh_host and plugin_path:
        print(f"📦 Deploying via SSH to {ssh_host}...")
        # Extract and copy via SSH
        result = subprocess.run([
            "ssh", ssh_host,
            f"rm -rf {plugin_path}/TunnelFin && mkdir -p {plugin_path}/TunnelFin"
        ], capture_output=True)

        if result.returncode == 0:
            # Copy plugin files
            src_dir = zip_path.parent / "plugin_package" / "TunnelFin"
            result = subprocess.run([
                "scp", "-r", f"{src_dir}/.", f"{ssh_host}:{plugin_path}/TunnelFin/"
            ], capture_output=True)

            if result.returncode == 0:
                print("✅ Plugin deployed via SSH")
            else:
                print(f"⚠️ SCP failed: {result.stderr.decode()}")
        else:
            print(f"⚠️ SSH command failed: {result.stderr.decode()}")
    else:
        print("📦 Plugin built. Manual installation required:")
        print(f"   1. Copy {zip_path.name} to Jellyfin server")
        print(f"   2. Extract to plugins directory")
        print(f"   3. Restart Jellyfin")
        print("")
        print("   Or set JELLYFIN_SSH_HOST and JELLYFIN_PLUGIN_PATH for auto-deploy")

    return headers


def restart_jellyfin(jellyfin_url: str, headers: dict):
    """Restart Jellyfin server."""
    print(f"\n🔄 Restarting Jellyfin...")
    
    restart_url = f"{jellyfin_url}/System/Restart"
    resp = requests.post(restart_url, headers=headers)
    
    if resp.status_code in [200, 204]:
        print("✅ Restart command sent")
        print("⏳ Waiting for server to restart...")
        time.sleep(15)  # Wait for restart
        
        # Wait for server to come back
        for i in range(30):
            try:
                ping = requests.get(f"{jellyfin_url}/System/Info", timeout=5)
                if ping.status_code == 200:
                    print("✅ Server is back online")
                    return True
            except:
                pass
            time.sleep(2)
        
        print("⚠️ Server may still be restarting")
    else:
        print(f"⚠️ Restart request returned: {resp.status_code}")
    
    return False


def run_e2e_tests(root: Path) -> bool:
    """Run E2E tests and return success status."""
    print(f"\n🧪 Running E2E tests...")

    result = subprocess.run(
        [sys.executable, "jellyfin_e2e_test.py"],
        cwd=root / "tests" / "e2e",
        capture_output=False
    )

    return result.returncode == 0


def main():
    parser = argparse.ArgumentParser(description="Deploy TunnelFin plugin")
    parser.add_argument("--skip-tests", action="store_true", help="Skip E2E tests")
    parser.add_argument("--skip-restart", action="store_true", help="Skip server restart")
    parser.add_argument("--version", help="Version to build (default: auto-increment)")
    args = parser.parse_args()

    root = Path(__file__).parent.parent
    manifest_path = root / "manifest.json"

    # Load environment
    load_env()
    jellyfin_url = os.environ.get("JELLYFIN_URL")
    username = os.environ.get("JELLYFIN_USERNAME")
    password = os.environ.get("JELLYFIN_PASSWORD")

    if not all([jellyfin_url, username, password]):
        print("❌ Missing JELLYFIN_URL, JELLYFIN_USERNAME, or JELLYFIN_PASSWORD")
        print("   Set in .env file or environment variables")
        sys.exit(1)

    # Determine version
    version = args.version or get_next_version(manifest_path)

    print("=" * 60)
    print(f"TunnelFin Deployment - v{version}")
    print("=" * 60)
    print(f"Target: {jellyfin_url}")

    # Build
    zip_path, checksum = build_plugin(version, root)

    # Update manifest
    update_manifest(manifest_path, version, checksum)

    # Deploy
    headers = deploy_to_jellyfin(zip_path, jellyfin_url, username, password)

    # Restart
    if not args.skip_restart:
        restart_jellyfin(jellyfin_url, headers)

    # Run tests
    if not args.skip_tests:
        success = run_e2e_tests(root)
        if not success:
            print("\n❌ E2E tests failed")
            sys.exit(1)

    print("\n" + "=" * 60)
    print("✅ Deployment complete!")
    print("=" * 60)

    # Git commands for tagging
    print(f"\nTo commit and tag this release:")
    print(f"  git add -A")
    print(f"  git commit -m 'Release v{version}'")
    print(f"  git tag -a v{version} -m 'v{version}'")
    print(f"  git push origin main --tags")


if __name__ == "__main__":
    main()

