#!/usr/bin/env python3
"""
Build and optionally deploy TunnelFin plugin for Jellyfin.

Usage: python scripts/build-plugin.py [version] [--deploy|--deploy-local]

Options:
    version         Plugin version (default: 1.0.0.0)
    --deploy        Deploy via Jellyfin API from GitHub repository (requires manifest/release)
    --deploy-local  Deploy locally built files directly to Jellyfin via GitHub release

Environment variables (from .env file):
    JELLYFIN_URL       - Jellyfin server URL (e.g., http://192.168.1.10:8096)
    JELLYFIN_USERNAME  - Admin username
    JELLYFIN_PASSWORD  - Admin password
"""

import os
import sys
import json
import glob
import shutil
import hashlib
import zipfile
import subprocess
import time
import requests
from pathlib import Path
from datetime import datetime, timezone
from typing import Optional, Dict, Any


def load_env_file(root_dir: Path) -> None:
    """Load environment variables from .env file."""
    env_path = root_dir / ".env"
    if env_path.exists():
        with open(env_path) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, value = line.split("=", 1)
                    os.environ.setdefault(key.strip(), value.strip())


def run_command(cmd: list, cwd: Path = None, check: bool = True) -> subprocess.CompletedProcess:
    """Run a shell command and return the result."""
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    if check and result.returncode != 0:
        print(f"❌ Command failed: {' '.join(cmd)}")
        print(f"   stdout: {result.stdout}")
        print(f"   stderr: {result.stderr}")
        sys.exit(1)
    return result


def calculate_md5(filepath: Path) -> str:
    """Calculate MD5 checksum of a file."""
    md5_hash = hashlib.md5()
    with open(filepath, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            md5_hash.update(chunk)
    return md5_hash.hexdigest()


def update_csproj_version(root_dir: Path, version: str) -> None:
    """Update the version in TunnelFin.csproj."""
    csproj_path = root_dir / "src" / "TunnelFin" / "TunnelFin.csproj"
    content = csproj_path.read_text()

    # Update Version, AssemblyVersion, and FileVersion
    import re
    content = re.sub(r'<Version>[^<]+</Version>', f'<Version>{version}</Version>', content)
    content = re.sub(r'<AssemblyVersion>[^<]+</AssemblyVersion>', f'<AssemblyVersion>{version}</AssemblyVersion>', content)
    content = re.sub(r'<FileVersion>[^<]+</FileVersion>', f'<FileVersion>{version}</FileVersion>', content)

    csproj_path.write_text(content)


def build_plugin(root_dir: Path, version: str) -> tuple[Path, str]:
    """Build the plugin and return (zip_path, checksum)."""
    print(f"🔨 Building TunnelFin v{version}...")

    # Update version in .csproj BEFORE building
    update_csproj_version(root_dir, version)

    # Clean previous builds
    publish_dir = root_dir / "publish"
    package_dir = root_dir / "plugin_package"
    if publish_dir.exists():
        shutil.rmtree(publish_dir)
    if package_dir.exists():
        shutil.rmtree(package_dir)

    # Remove old zip files
    for old_zip in root_dir.glob("tunnelfin_*.zip"):
        old_zip.unlink()

    # Build and publish
    run_command([
        "dotnet", "publish",
        "src/TunnelFin/TunnelFin.csproj",
        "-c", "Release",
        "-o", "./publish"
    ], cwd=root_dir)

    # Create plugin package directory
    plugin_dir = package_dir / "TunnelFin"
    plugin_dir.mkdir(parents=True, exist_ok=True)

    print("📦 Packaging plugin...")

    # Copy TunnelFin DLL and PDB
    shutil.copy(publish_dir / "TunnelFin.dll", plugin_dir)
    pdb_file = publish_dir / "TunnelFin.pdb"
    if pdb_file.exists():
        shutil.copy(pdb_file, plugin_dir)

    # Copy and update meta.json with version
    meta_file = root_dir / "src" / "TunnelFin" / "meta.json"
    if meta_file.exists():
        with open(meta_file) as f:
            meta = json.load(f)
        meta["version"] = version
        with open(plugin_dir / "meta.json", "w") as f:
            json.dump(meta, f, indent=2)
        print("✓ meta.json included")

    # Copy MonoTorrent dependencies (exclude PortForwarding which conflicts with Mono.Nat)
    for dll in publish_dir.glob("MonoTorrent*.dll"):
        if dll.name != "MonoTorrent.PortForwarding.dll":
            shutil.copy(dll, plugin_dir)

    # Copy other dependencies
    deps = [
        "ReusableTasks.dll",
        "BitFaster.Caching.dll",
        "NSec.Cryptography.dll",
        "HtmlAgilityPack.dll",
    ]
    for dep in deps:
        dep_path = publish_dir / dep
        if dep_path.exists():
            shutil.copy(dep_path, plugin_dir)

    # Copy Polly DLLs
    for polly_dll in publish_dir.glob("Polly*.dll"):
        shutil.copy(polly_dll, plugin_dir)

    # Create ZIP
    zip_name = f"tunnelfin_{version}.zip"
    zip_path = root_dir / zip_name

    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
        for file in plugin_dir.rglob("*"):
            if file.is_file():
                arcname = f"TunnelFin/{file.relative_to(plugin_dir)}"
                zf.write(file, arcname)

    checksum = calculate_md5(zip_path)

    print()
    print("✅ Build complete!")
    print(f"📁 Plugin package: {zip_name}")
    print(f"🔑 MD5 Checksum: {checksum}")

    return zip_path, checksum


class JellyfinDeployer:
    """Handle Jellyfin API interactions for plugin deployment."""

    def __init__(self, base_url: str, username: str, password: str):
        self.base_url = base_url.rstrip('/')
        self.username = username
        self.password = password
        self.access_token: Optional[str] = None
        self.user_id: Optional[str] = None
        self.session = requests.Session()
        self.session.headers.update({
            "Content-Type": "application/json",
        })

    def _get_auth_header(self) -> str:
        """Build the Jellyfin authorization header."""
        parts = [
            'MediaBrowser Client="TunnelFin Build"',
            'Device="Build Script"',
            'DeviceId="build-script"',
            'Version="1.0.0"',
        ]
        if self.access_token:
            parts.append(f'Token="{self.access_token}"')
        return ", ".join(parts)

    def authenticate(self) -> bool:
        """Authenticate with Jellyfin server."""
        print("   🔐 Authenticating...")
        self.session.headers["X-Emby-Authorization"] = self._get_auth_header()

        try:
            response = self.session.post(
                f"{self.base_url}/Users/AuthenticateByName",
                json={"Username": self.username, "Pw": self.password}
            )
            response.raise_for_status()
            data = response.json()
            self.access_token = data.get("AccessToken")
            self.user_id = data.get("User", {}).get("Id")
            self.session.headers["X-Emby-Authorization"] = self._get_auth_header()
            print(f"   ✓ Authenticated as user {self.user_id}")
            return True
        except Exception as e:
            print(f"❌ Error: Failed to authenticate with Jellyfin: {e}")
            return False

    def get_repositories(self) -> list:
        """Get configured plugin repositories."""
        response = self.session.get(f"{self.base_url}/Repositories")
        response.raise_for_status()
        return response.json()

    def set_repositories(self, repos: list) -> None:
        """Set plugin repositories."""
        self.session.post(f"{self.base_url}/Repositories", json=repos)

    def ensure_repository(self, repo_url: str) -> None:
        """Ensure TunnelFin repository is configured with correct URL."""
        print("   📋 Checking plugin repositories...")
        repos = self.get_repositories()

        # Find existing TunnelFin repo
        existing_idx = None
        for i, r in enumerate(repos):
            if "TunnelFin" in r.get("Name", ""):
                existing_idx = i
                break

        if existing_idx is not None:
            # Check if URL needs updating
            if repos[existing_idx].get("Url") != repo_url:
                print(f"   🔄 Updating TunnelFin repository URL to jsdelivr CDN...")
                repos[existing_idx]["Url"] = repo_url
                repos[existing_idx]["Enabled"] = True
                self.set_repositories(repos)
                print("   ✓ Repository URL updated")
            else:
                print("   ✓ TunnelFin repository already configured")
        else:
            print("   📥 Adding TunnelFin repository...")
            repos.append({"Name": "TunnelFin", "Url": repo_url, "Enabled": True})
            self.set_repositories(repos)
            print("   ✓ Repository added")

    def get_plugins(self) -> list:
        """Get installed plugins."""
        response = self.session.get(f"{self.base_url}/Plugins")
        response.raise_for_status()
        return response.json()

    def find_tunnelfin_plugin(self) -> Optional[str]:
        """Find TunnelFin plugin ID if installed."""
        plugins = self.get_plugins()
        for plugin in plugins:
            if plugin.get("Name", "").lower() == "tunnelfin":
                return plugin.get("Id")
        return None

    def uninstall_plugin(self, plugin_id: str) -> None:
        """Uninstall a plugin by ID."""
        print("   🗑️  Uninstalling existing TunnelFin...")
        self.session.delete(f"{self.base_url}/Plugins/{plugin_id}")
        print("   ✓ Old version uninstalled")

    def install_plugin(self, repo_url: str, version: str) -> bool:
        """Install plugin from repository."""
        print(f"   📦 Installing TunnelFin v{version}...")
        response = self.session.post(
            f"{self.base_url}/Packages/Installed/TunnelFin",
            params={"repositoryUrl": repo_url, "version": version}
        )
        if response.status_code in (200, 204):
            print("   ✓ Plugin installation initiated")
            return True
        else:
            print(f"   ⚠️  Install returned HTTP {response.status_code}")
            return False

    def restart_server(self) -> None:
        """Restart Jellyfin server."""
        print("   🔄 Restarting Jellyfin server...")
        try:
            self.session.post(f"{self.base_url}/System/Restart", timeout=5)
        except Exception:
            # Connection may be refused/reset during restart - this is expected
            pass
        print("   ✓ Restart initiated")

    def wait_for_server(self, timeout: int = 90) -> bool:
        """Wait for server to come back online after restart."""
        start = time.time()
        print(f"      Waiting for server (up to {timeout}s)...", end="", flush=True)
        while time.time() - start < timeout:
            try:
                resp = self.session.get(f"{self.base_url}/System/Info/Public", timeout=5)
                if resp.status_code == 200:
                    elapsed = int(time.time() - start)
                    print(f" ready ({elapsed}s)")
                    return True
            except Exception:
                pass
            print(".", end="", flush=True)
            time.sleep(2)
        print(" timeout!")
        return False

    def wait_for_plugin_state(self, plugin_name: str, expected_state: str, timeout: int = 60) -> bool:
        """Wait for a plugin to reach expected state (e.g., 'Active', 'Deleted')."""
        start = time.time()
        print(f"      Waiting for plugin state '{expected_state}' (up to {timeout}s)...", end="", flush=True)
        while time.time() - start < timeout:
            try:
                plugins = self.get_plugins()
                plugin = next((p for p in plugins if p.get("Name", "").lower() == plugin_name.lower()), None)

                if expected_state == "Deleted":
                    if plugin is None:
                        elapsed = int(time.time() - start)
                        print(f" done ({elapsed}s)")
                        return True
                elif plugin and plugin.get("Status") == expected_state:
                    elapsed = int(time.time() - start)
                    print(f" done ({elapsed}s)")
                    return True
            except Exception:
                pass
            print(".", end="", flush=True)
            time.sleep(2)
        print(" timeout!")
        return False

    def wait_for_plugin_version(self, plugin_name: str, version: str, timeout: int = 60) -> bool:
        """Wait for a specific plugin version to be installed and active."""
        start = time.time()
        print(f"      Waiting for v{version} to be active (up to {timeout}s)...", end="", flush=True)
        while time.time() - start < timeout:
            try:
                plugins = self.get_plugins()
                for p in plugins:
                    if p.get("Name", "").lower() == plugin_name.lower():
                        if p.get("Version") == version and p.get("Status") == "Active":
                            elapsed = int(time.time() - start)
                            print(f" done ({elapsed}s)")
                            return True
            except Exception:
                pass
            print(".", end="", flush=True)
            time.sleep(2)
        print(" timeout!")
        return False

    def re_authenticate(self) -> bool:
        """Re-authenticate after server restart."""
        self.session.headers["X-Emby-Authorization"] = self._get_auth_header()
        try:
            response = self.session.post(
                f"{self.base_url}/Users/AuthenticateByName",
                json={"Username": self.username, "Pw": self.password}
            )
            response.raise_for_status()
            data = response.json()
            self.access_token = data.get("AccessToken")
            self.session.headers["X-Emby-Authorization"] = self._get_auth_header()
            return True
        except Exception:
            return False


def wait_for_cdn_update(manifest_url: str, expected_checksum: str, version: str, timeout: int = 90) -> bool:
    """Wait for jsdelivr CDN to serve updated manifest AND zip file is downloadable."""
    start = time.time()
    release_tag = f"v{version}"
    zip_url = f"https://github.com/jefflouisma/TunnelFin/releases/download/{release_tag}/tunnelfin_{version}.zip"

    print(f"      Verifying release artifacts are available (up to {timeout}s)...")

    # First, purge jsdelivr cache explicitly
    purge_url = manifest_url.replace("cdn.jsdelivr.net", "purge.jsdelivr.net")
    try:
        requests.get(purge_url, timeout=10)
        print("      Purged jsdelivr cache")
    except Exception:
        pass

    # Phase 1: Wait for manifest to have correct checksum
    print(f"      Checking manifest for checksum {expected_checksum[:8]}...", end="", flush=True)
    manifest_ready = False
    while time.time() - start < timeout * 0.6:
        try:
            # Use cache-busting for jsdelivr
            url = f"{manifest_url}"
            resp = requests.get(url, timeout=10, headers={"Cache-Control": "no-cache", "Pragma": "no-cache"})
            if resp.status_code == 200:
                manifest = resp.json()
                if manifest and len(manifest) > 0:
                    versions = manifest[0].get("versions", [])
                    if versions and versions[0].get("checksum") == expected_checksum:
                        elapsed = int(time.time() - start)
                        print(f" ready ({elapsed}s)")
                        manifest_ready = True
                        break
        except Exception:
            pass
        print(".", end="", flush=True)
        time.sleep(2)

    if not manifest_ready:
        print(" timeout!")
        return False

    # Phase 2: Verify zip file is downloadable (HEAD request)
    print(f"      Verifying zip is downloadable...", end="", flush=True)
    while time.time() - start < timeout:
        try:
            resp = requests.head(zip_url, timeout=10, allow_redirects=True)
            if resp.status_code == 200:
                elapsed = int(time.time() - start)
                print(f" ready ({elapsed}s)")
                return True
        except Exception:
            pass
        print(".", end="", flush=True)
        time.sleep(2)

    print(" timeout!")
    return False


def update_manifest(root_dir: Path, version: str, checksum: str) -> None:
    """Update manifest.json with version and checksum."""
    manifest_path = root_dir / "manifest.json"
    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    release_tag = f"v{version}"

    print(f"   📝 Updating manifest.json with checksum {checksum}...")

    with open(manifest_path) as f:
        manifest = json.load(f)

    # Find or create version entry
    versions = manifest[0].get("versions", [])
    version_entry = None
    for v in versions:
        if v.get("version") == version:
            version_entry = v
            break

    if version_entry:
        # Update existing version
        version_entry["checksum"] = checksum
        version_entry["timestamp"] = timestamp
    else:
        # Add new version at the top
        new_entry = {
            "version": version,
            "changelog": f"v{version} - Local build deployment",
            "targetAbi": "10.11.0.0",
            "sourceUrl": f"https://github.com/jefflouisma/TunnelFin/releases/download/{release_tag}/tunnelfin_{version}.zip",
            "checksum": checksum,
            "timestamp": timestamp
        }
        versions.insert(0, new_entry)
        manifest[0]["versions"] = versions

    with open(manifest_path, "w") as f:
        json.dump(manifest, f, indent=4)

    print("   ✓ Manifest updated")


def deploy_via_repo(root_dir: Path, version: str, branch: str = None) -> None:
    """Deploy plugin via Jellyfin API from GitHub repository."""
    print()
    print("🚀 Deploying to Jellyfin...")

    jellyfin_url = os.environ.get("JELLYFIN_URL")
    username = os.environ.get("JELLYFIN_USERNAME")
    password = os.environ.get("JELLYFIN_PASSWORD")

    if not all([jellyfin_url, username, password]):
        print("❌ Error: Missing Jellyfin credentials in .env file")
        print("   Required: JELLYFIN_URL, JELLYFIN_USERNAME, JELLYFIN_PASSWORD")
        sys.exit(1)

    # Get current branch
    if not branch:
        result = run_command(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=root_dir, check=False)
        branch = result.stdout.strip() if result.returncode == 0 else "main"

    repo_url = f"https://cdn.jsdelivr.net/gh/jefflouisma/TunnelFin@{branch}/manifest.json"
    print(f"   📋 Using manifest from branch: {branch}")

    deployer = JellyfinDeployer(jellyfin_url, username, password)
    if not deployer.authenticate():
        sys.exit(1)

    deployer.ensure_repository(repo_url)

    # Check for existing installation
    print("   🔍 Checking installed plugins...")
    plugin_id = deployer.find_tunnelfin_plugin()
    if plugin_id:
        deployer.uninstall_plugin(plugin_id)

    deployer.install_plugin(repo_url, version)
    deployer.restart_server()

    print()
    print("✅ Deployment complete!")
    print("   Jellyfin is restarting. Plugin will be available in ~30 seconds.")
    print(f"   Check {jellyfin_url}/web/index.html#!/dashboard/plugins for status.")


def deploy_local(root_dir: Path, version: str, zip_path: Path, checksum: str) -> None:
    """Build locally, upload to GitHub release, update manifest, deploy via Jellyfin API."""
    print()
    print("🚀 Local build + GitHub deploy to Jellyfin...")

    # Check for gh CLI
    result = run_command(["which", "gh"], check=False)
    if result.returncode != 0:
        print("❌ Error: GitHub CLI (gh) not found. Install with: brew install gh")
        sys.exit(1)

    jellyfin_url = os.environ.get("JELLYFIN_URL")
    username = os.environ.get("JELLYFIN_USERNAME")
    password = os.environ.get("JELLYFIN_PASSWORD")

    if not all([jellyfin_url, username, password]):
        print("❌ Error: Missing Jellyfin credentials in .env file")
        print("   Required: JELLYFIN_URL, JELLYFIN_USERNAME, JELLYFIN_PASSWORD")
        sys.exit(1)

    # Use jsdelivr CDN - it purges cache automatically on git push (unlike raw.githubusercontent.com)
    # Use raw.githubusercontent.com for immediate updates, bypassing jsDelivr cache
    repo_url = "https://raw.githubusercontent.com/jefflouisma/TunnelFin/main/manifest.json"
    release_tag = f"v{version}"

    # Step 1: Update manifest.json
    update_manifest(root_dir, version, checksum)

    # Step 2: Create or update GitHub release
    print(f"   📦 Uploading to GitHub release {release_tag}...")
    result = run_command(["gh", "release", "view", release_tag], cwd=root_dir, check=False)
    if result.returncode == 0:
        print("      Release exists, updating asset...")
        run_command(["gh", "release", "upload", release_tag, str(zip_path), "--clobber"], cwd=root_dir)
    else:
        print("      Creating new release...")
        run_command([
            "gh", "release", "create", release_tag, str(zip_path),
            "--title", f"{release_tag} - Local Build",
            "--notes", "Automated local build deployment"
        ], cwd=root_dir)
    print("   ✓ Release uploaded")

    # Step 3: Commit and push manifest to main
    print("   📤 Pushing manifest to main branch...")
    run_command(["git", "add", "manifest.json"], cwd=root_dir)
    run_command(
        ["git", "commit", "-m", f"Update manifest for {release_tag} (checksum: {checksum})"],
        cwd=root_dir, check=False
    )

    # Get current branch
    result = run_command(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=root_dir)
    current_branch = result.stdout.strip()
    run_command(["git", "push", "origin", current_branch], cwd=root_dir)

    # Merge to main if not already on main
    if current_branch != "main":
        worktree_path = root_dir / ".git" / "beads-worktrees" / "main"
        if worktree_path.exists():
            run_command(["git", "pull", "origin", "main"], cwd=worktree_path)
            run_command(["git", "merge", f"origin/{current_branch}", "--no-edit"], cwd=worktree_path)
            run_command(["git", "push", "origin", "main"], cwd=worktree_path)
        else:
            run_command(["git", "fetch", "origin", "main"], cwd=root_dir)
            run_command(["git", "push", "origin", f"{current_branch}:main"], cwd=root_dir)
    print("   ✓ Manifest pushed to main")

    # Step 4: Wait for GitHub CDN to update (verify manifest AND zip are available)
    print("   ⏳ Verifying GitHub CDN has updated...")
    if not wait_for_cdn_update(repo_url, checksum, version, timeout=120):
        print("   ❌ CDN not ready - Jellyfin install will likely fail!")
        print("   💡 Try running again in a minute, or manually install from Jellyfin UI")

    # Step 5: Deploy via Jellyfin API (two-phase: uninstall+restart, then install+restart)
    deployer = JellyfinDeployer(jellyfin_url, username, password)
    if not deployer.authenticate():
        sys.exit(1)

    deployer.ensure_repository(repo_url)

    # Phase 1: Uninstall existing plugin and restart to clean up
    print("   🔍 Checking for existing installation...")
    plugin_id = deployer.find_tunnelfin_plugin()
    if plugin_id:
        deployer.uninstall_plugin(plugin_id)
        deployer.restart_server()
        if not deployer.wait_for_server(timeout=90):
            print("   ⚠️  Server didn't respond, continuing anyway...")
        else:
            deployer.re_authenticate()
            # Wait for plugin to actually be removed
            deployer.wait_for_plugin_state("TunnelFin", "Deleted", timeout=30)

    # Phase 2: Install new version and restart
    deployer.install_plugin(repo_url, version)
    deployer.restart_server()
    if not deployer.wait_for_server(timeout=90):
        print("   ⚠️  Server didn't respond, continuing anyway...")
    else:
        deployer.re_authenticate()

    # Wait for plugin to become active with correct version
    print("   🔍 Verifying installation...")
    if deployer.wait_for_plugin_version("TunnelFin", version, timeout=60):
        print(f"   ✅ TunnelFin v{version} is Active!")
    else:
        # Final check
        plugin_id = deployer.find_tunnelfin_plugin()
        if plugin_id:
            plugins = deployer.get_plugins()
            for p in plugins:
                if p.get("Id") == plugin_id:
                    installed_version = p.get("Version")
                    status = p.get("Status")
                    print(f"   ⚠️  TunnelFin v{installed_version} status: {status}")
        else:
            print("   ⚠️  TunnelFin not found after install")

    print()
    print("✅ Deployment complete!")
    print(f"   Check {jellyfin_url}/web/index.html#!/dashboard/plugins for status.")


def print_manual_install_instructions(version: str, checksum: str) -> None:
    """Print manual installation instructions."""
    print()
    print("To install in Jellyfin:")
    print("1. Add repository: https://cdn.jsdelivr.net/gh/jefflouisma/TunnelFin@main/manifest.json")
    print("2. Or manually copy plugin_package/TunnelFin/ to your Jellyfin plugins directory")
    print(f"3. Or run: python scripts/build-plugin.py {version} --deploy        (from GitHub repo)")
    print(f"4. Or run: python scripts/build-plugin.py {version} --deploy-local  (direct deploy)")
    print()
    print(f"Update manifest.json checksum to: {checksum}")


def parse_version(version_str: str) -> tuple:
    """Parse version string into comparable tuple."""
    version_str = version_str.lstrip('v')
    parts = version_str.split('.')
    try:
        return tuple(int(p) for p in parts[:4])
    except ValueError:
        return (0, 0, 0, 0)


def get_next_version(root_dir: Path) -> str:
    """Get the next version by bumping from the latest git tag OR GitHub release."""
    versions = []

    # Check git tags
    result = run_command(
        ["git", "tag", "--sort=-v:refname"],
        cwd=root_dir,
        check=False
    )
    if result.returncode == 0 and result.stdout.strip():
        for tag in result.stdout.strip().split('\n'):
            if tag.startswith('v'):
                versions.append(parse_version(tag))

    # Check GitHub releases (may have releases without tags)
    result = run_command(
        ["gh", "release", "list", "--limit", "20", "--json", "tagName"],
        cwd=root_dir,
        check=False
    )
    if result.returncode == 0 and result.stdout.strip():
        try:
            releases = json.loads(result.stdout)
            for rel in releases:
                tag = rel.get("tagName", "")
                if tag.startswith('v'):
                    versions.append(parse_version(tag))
        except json.JSONDecodeError:
            pass

    if not versions:
        return "1.0.0.0"

    # Get highest version
    latest = max(versions)

    # Bump the last component
    parts = list(latest)
    while len(parts) < 4:
        parts.append(0)
    parts[3] += 1

    return '.'.join(str(p) for p in parts[:4])


def main():
    """Main entry point."""
    # Determine paths first (needed for version detection)
    script_dir = Path(__file__).parent.resolve()
    root_dir = script_dir.parent

    # Load .env
    load_env_file(root_dir)

    # Parse arguments
    version = None
    do_deploy = False
    do_deploy_local = False

    for arg in sys.argv[1:]:
        if arg == "--deploy":
            do_deploy = True
        elif arg == "--deploy-local":
            do_deploy_local = True
        elif not arg.startswith("-"):
            version = arg

    # Auto-detect version if not provided
    if version is None:
        version = get_next_version(root_dir)
        print(f"📌 Auto-detected next version: {version}")

    # Build plugin
    zip_path, checksum = build_plugin(root_dir, version)

    # Deploy or print instructions
    if do_deploy:
        deploy_via_repo(root_dir, version)
    elif do_deploy_local:
        deploy_local(root_dir, version, zip_path, checksum)
    else:
        print_manual_install_instructions(version, checksum)


if __name__ == "__main__":
    main()

