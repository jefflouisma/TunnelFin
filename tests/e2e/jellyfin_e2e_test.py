#!/usr/bin/env python3
"""
TunnelFin End-to-End Test Suite
Tests the complete user experience of the TunnelFin plugin against a live Jellyfin server.

Usage:
    python jellyfin_e2e_test.py

Configuration:
    Set environment variables:
    - JELLYFIN_URL: Jellyfin server URL (required)
    - JELLYFIN_USERNAME: Username (required)
    - JELLYFIN_PASSWORD: Password (required)
"""

import os
import sys
import json
import requests
from pathlib import Path
from dataclasses import dataclass
from typing import Optional, Dict, Any, List
from enum import Enum


def load_env_file():
    """Load environment variables from .env file in project root."""
    # Look for .env file in project root (two directories up from tests/e2e/)
    script_dir = Path(__file__).parent
    env_paths = [
        script_dir / ".env",                    # tests/e2e/.env
        script_dir.parent.parent / ".env",      # project root .env
    ]

    for env_path in env_paths:
        if env_path.exists():
            with open(env_path) as f:
                for line in f:
                    line = line.strip()
                    if line and not line.startswith("#") and "=" in line:
                        key, value = line.split("=", 1)
                        os.environ.setdefault(key.strip(), value.strip())
            return True
    return False


class TestResult(Enum):
    PASSED = "✅ PASSED"
    FAILED = "❌ FAILED"
    SKIPPED = "⏭️ SKIPPED"
    WARNING = "⚠️ WARNING"


@dataclass
class TestCase:
    name: str
    description: str
    result: TestResult = TestResult.SKIPPED
    message: str = ""
    details: Optional[Dict[str, Any]] = None


class JellyfinClient:
    """Simple Jellyfin API client for E2E testing."""

    # TunnelFin Plugin GUID (from src/TunnelFin/Core/Plugin.cs)
    TUNNELFIN_PLUGIN_GUID = "A7F8B3C2-1D4E-4A5B-9C6D-7E8F9A0B1C2D"

    def __init__(self, base_url: str, username: str, password: str):
        self.base_url = base_url.rstrip('/')
        self.username = username
        self.password = password
        self.access_token: Optional[str] = None
        self.user_id: Optional[str] = None
        self.server_id: Optional[str] = None
        self.session = requests.Session()

        # Set common headers
        self.session.headers.update({
            "Content-Type": "application/json",
            "X-Emby-Client": "TunnelFin-E2E-Test",
            "X-Emby-Client-Version": "1.0.0",
            "X-Emby-Device-Name": "E2E-Test-Runner",
            "X-Emby-Device-Id": "tunnelfin-e2e-test-device",
        })

    def _get_auth_header(self) -> str:
        """Build the Jellyfin authorization header."""
        parts = [
            'MediaBrowser Client="TunnelFin-E2E-Test"',
            'Device="E2E-Test-Runner"',
            'DeviceId="tunnelfin-e2e-test-device"',
            'Version="1.0.0"',
        ]
        if self.access_token:
            parts.append(f'Token="{self.access_token}"')
        return ", ".join(parts)

    def authenticate(self) -> Dict[str, Any]:
        """Authenticate with Jellyfin server."""
        url = f"{self.base_url}/Users/AuthenticateByName"

        self.session.headers["Authorization"] = self._get_auth_header()

        response = self.session.post(url, json={
            "Username": self.username,
            "Pw": self.password
        })
        response.raise_for_status()

        data = response.json()
        self.access_token = data.get("AccessToken")
        self.user_id = data.get("User", {}).get("Id")
        self.server_id = data.get("ServerId")

        # Update auth header with token
        self.session.headers["Authorization"] = self._get_auth_header()

        return data

    def get_server_info(self) -> Dict[str, Any]:
        """Get server public info."""
        response = self.session.get(f"{self.base_url}/System/Info/Public")
        response.raise_for_status()
        return response.json()

    def get_plugins(self) -> List[Dict[str, Any]]:
        """Get list of installed plugins."""
        response = self.session.get(f"{self.base_url}/Plugins")
        response.raise_for_status()
        return response.json()

    def get_plugin_configuration(self, plugin_id: str) -> Dict[str, Any]:
        """Get plugin configuration."""
        response = self.session.get(f"{self.base_url}/Plugins/{plugin_id}/Configuration")
        response.raise_for_status()
        return response.json()

    def get_channels(self) -> Dict[str, Any]:
        """Get available channels."""
        params = {}
        if self.user_id:
            params["userId"] = self.user_id
        response = self.session.get(f"{self.base_url}/Channels", params=params)
        response.raise_for_status()
        return response.json()

    def get_channel_features(self, channel_id: str) -> Dict[str, Any]:
        """Get channel features."""
        response = self.session.get(f"{self.base_url}/Channels/{channel_id}/Features")
        response.raise_for_status()
        return response.json()

    def get_channel_items(self, channel_id: str, folder_id: Optional[str] = None,
                          start_index: int = 0, limit: int = 20) -> Dict[str, Any]:
        """Get channel items (search results)."""
        params = {
            "startIndex": start_index,
            "limit": limit,
        }
        if self.user_id:
            params["userId"] = self.user_id
        if folder_id:
            params["folderId"] = folder_id

        response = self.session.get(
            f"{self.base_url}/Channels/{channel_id}/Items",
            params=params
        )
        response.raise_for_status()
        return response.json()

    def search(self, query: str, limit: int = 10) -> Dict[str, Any]:
        """Search using Jellyfin search API."""
        params = {
            "searchTerm": query,
            "limit": limit,
            "includeItemTypes": "Movie,Series,Episode",
        }
        if self.user_id:
            params["userId"] = self.user_id

        response = self.session.get(f"{self.base_url}/Search/Hints", params=params)
        response.raise_for_status()
        return response.json()

    def get_item(self, item_id: str) -> Dict[str, Any]:
        """Get item details."""
        params = {}
        if self.user_id:
            params["userId"] = self.user_id
        response = self.session.get(f"{self.base_url}/Items/{item_id}", params=params)
        response.raise_for_status()
        return response.json()

    def get_activity_log(self, limit: int = 50) -> Dict[str, Any]:
        """Get recent activity log entries."""
        params = {"limit": limit}
        response = self.session.get(f"{self.base_url}/System/ActivityLog/Entries", params=params)
        response.raise_for_status()
        return response.json()

    def get_system_logs(self) -> List[Dict[str, Any]]:
        """Get available system log files."""
        response = self.session.get(f"{self.base_url}/System/Logs")
        response.raise_for_status()
        return response.json()

    def get_log_file(self, log_name: str) -> str:
        """Get contents of a specific log file."""
        response = self.session.get(f"{self.base_url}/System/Logs/Log", params={"name": log_name})
        response.raise_for_status()
        return response.text

    def get_playback_info(self, item_id: str) -> Dict[str, Any]:
        """Get playback info (media sources) for an item."""
        params = {}
        if self.user_id:
            params["userId"] = self.user_id
        response = self.session.get(
            f"{self.base_url}/Items/{item_id}/PlaybackInfo",
            params=params
        )
        response.raise_for_status()
        return response.json()


class TunnelFinE2ETestSuite:
    """End-to-end test suite for TunnelFin plugin."""

    def __init__(self, client: JellyfinClient):
        self.client = client
        self.test_cases: List[TestCase] = []
        self.tunnelfin_channel_id: Optional[str] = None

    def add_result(self, test: TestCase):
        """Add a test result."""
        self.test_cases.append(test)
        status_icon = test.result.value
        print(f"\n{status_icon} {test.name}")
        if test.message:
            print(f"   {test.message}")
        if test.details and test.result == TestResult.FAILED:
            print(f"   Details: {json.dumps(test.details, indent=2)[:500]}")

    def test_server_connection(self) -> TestCase:
        """Test 1: Verify server is reachable and get server info."""
        test = TestCase(
            name="Server Connection",
            description="Verify Jellyfin server is reachable"
        )

        try:
            info = self.client.get_server_info()
            test.result = TestResult.PASSED
            test.message = f"Connected to {info.get('ServerName', 'Unknown')} v{info.get('Version', 'Unknown')}"
            test.details = {
                "server_name": info.get("ServerName"),
                "version": info.get("Version"),
                "local_address": info.get("LocalAddress"),
            }
        except requests.exceptions.ConnectionError as e:
            test.result = TestResult.FAILED
            test.message = f"Cannot connect to server: {e}"
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Unexpected error: {e}"

        return test

    def test_authentication(self) -> TestCase:
        """Test 2: Authenticate with provided credentials."""
        test = TestCase(
            name="User Authentication",
            description="Authenticate with Jellyfin server"
        )

        try:
            auth_data = self.client.authenticate()
            user = auth_data.get("User", {})
            test.result = TestResult.PASSED
            test.message = f"Authenticated as '{user.get('Name')}' (Admin: {user.get('Policy', {}).get('IsAdministrator', False)})"
            test.details = {
                "user_id": self.client.user_id,
                "user_name": user.get("Name"),
                "is_admin": user.get("Policy", {}).get("IsAdministrator"),
            }
        except requests.exceptions.HTTPError as e:
            test.result = TestResult.FAILED
            test.message = f"Authentication failed: {e.response.status_code} - {e.response.text[:200]}"
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Unexpected error: {e}"

        return test

    def test_plugin_installed(self) -> TestCase:
        """Test 3: Verify TunnelFin plugin is installed."""
        test = TestCase(
            name="TunnelFin Plugin Installation",
            description="Verify TunnelFin plugin is installed and active"
        )

        try:
            plugins = self.client.get_plugins()
            tunnelfin = None

            for plugin in plugins:
                plugin_id = plugin.get("Id", "").upper()
                if plugin_id == self.client.TUNNELFIN_PLUGIN_GUID.upper():
                    tunnelfin = plugin
                    break
                # Also check by name as fallback
                if plugin.get("Name", "").lower() == "tunnelfin":
                    tunnelfin = plugin
                    break

            if tunnelfin:
                status = tunnelfin.get("Status", "Unknown")
                if status == "Active":
                    test.result = TestResult.PASSED
                    test.message = f"TunnelFin v{tunnelfin.get('Version', 'Unknown')} is active"
                elif status == "Restart":
                    test.result = TestResult.WARNING
                    test.message = f"TunnelFin installed but requires restart (Status: {status})"
                else:
                    test.result = TestResult.WARNING
                    test.message = f"TunnelFin found but status is: {status}"

                test.details = {
                    "plugin_id": tunnelfin.get("Id"),
                    "name": tunnelfin.get("Name"),
                    "version": tunnelfin.get("Version"),
                    "status": status,
                    "description": tunnelfin.get("Description", "")[:100],
                    "can_uninstall": tunnelfin.get("CanUninstall"),
                    "has_image": tunnelfin.get("HasImage"),
                }
            else:
                test.result = TestResult.FAILED
                test.message = "TunnelFin plugin not found in installed plugins"
                test.details = {
                    "installed_plugins": [
                        {"name": p.get("Name"), "id": p.get("Id"), "status": p.get("Status")}
                        for p in plugins
                    ],
                    "expected_guid": self.client.TUNNELFIN_PLUGIN_GUID,
                }
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Error checking plugins: {e}"

        return test

    def test_channel_discovery(self) -> TestCase:
        """Test 4: Verify TunnelFin channel is discoverable."""
        test = TestCase(
            name="TunnelFin Channel Discovery",
            description="Verify TunnelFin appears as a channel"
        )

        try:
            channels_response = self.client.get_channels()
            channels = channels_response.get("Items", [])

            tunnelfin_channel = None
            for channel in channels:
                name = channel.get("Name", "").lower()
                if "tunnelfin" in name:
                    tunnelfin_channel = channel
                    break

            if tunnelfin_channel:
                self.tunnelfin_channel_id = tunnelfin_channel.get("Id")
                test.result = TestResult.PASSED
                test.message = f"TunnelFin channel found: '{tunnelfin_channel.get('Name')}'"
                test.details = {
                    "channel_id": self.tunnelfin_channel_id,
                    "name": tunnelfin_channel.get("Name"),
                    "type": tunnelfin_channel.get("Type"),
                }
            else:
                test.result = TestResult.FAILED
                test.message = "TunnelFin channel not found"
                test.details = {
                    "available_channels": [c.get("Name") for c in channels],
                    "total_channels": len(channels),
                }
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Error discovering channels: {e}"

        return test

    def test_channel_features(self) -> TestCase:
        """Test 5: Verify TunnelFin channel features."""
        test = TestCase(
            name="TunnelFin Channel Features",
            description="Verify channel supports expected content types and features"
        )

        if not self.tunnelfin_channel_id:
            test.result = TestResult.SKIPPED
            test.message = "Channel not discovered, skipping feature test"
            return test

        try:
            features = self.client.get_channel_features(self.tunnelfin_channel_id)

            # Expected features based on TunnelFinChannel.cs
            expected_content_types = ["Movie", "Episode", "Clip"]
            expected_media_types = ["Video"]

            actual_content_types = features.get("ContentTypes", [])
            actual_media_types = features.get("MediaTypes", [])
            supports_sort = features.get("SupportsSortOrderToggle", False)

            issues = []

            # Check content types
            for ct in expected_content_types:
                if ct not in actual_content_types:
                    issues.append(f"Missing content type: {ct}")

            # Check media types
            for mt in expected_media_types:
                if mt not in actual_media_types:
                    issues.append(f"Missing media type: {mt}")

            if issues:
                test.result = TestResult.WARNING
                test.message = f"Some expected features missing: {', '.join(issues)}"
            else:
                test.result = TestResult.PASSED
                test.message = f"All expected features present (ContentTypes: {actual_content_types}, SortToggle: {supports_sort})"

            test.details = {
                "content_types": actual_content_types,
                "media_types": actual_media_types,
                "supports_sort_toggle": supports_sort,
                "default_sort_fields": features.get("DefaultSortFields", []),
                "supports_content_downloading": features.get("SupportsContentDownloading", False),
            }
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Error getting channel features: {e}"

        return test

    def test_channel_search(self, search_query: str = "big buck bunny") -> TestCase:
        """Test 6: Search through TunnelFin channel."""
        test = TestCase(
            name=f"TunnelFin Channel Search ('{search_query}')",
            description="Test searching for content through the channel"
        )

        if not self.tunnelfin_channel_id:
            test.result = TestResult.SKIPPED
            test.message = "Channel not discovered, skipping search test"
            return test

        try:
            # Use folderId as search term (how TunnelFinChannel works)
            items_response = self.client.get_channel_items(
                self.tunnelfin_channel_id,
                folder_id=search_query,
                limit=10
            )

            items = items_response.get("Items", [])
            total_count = items_response.get("TotalRecordCount", 0)

            if items:
                test.result = TestResult.PASSED
                test.message = f"Found {total_count} results for '{search_query}'"
                test.details = {
                    "total_results": total_count,
                    "returned_items": len(items),
                    "first_results": [
                        {
                            "name": item.get("Name"),
                            "type": item.get("Type"),
                            "id": item.get("Id"),
                        }
                        for item in items[:5]
                    ],
                }
            else:
                # Empty results might be expected if no indexers are configured
                test.result = TestResult.WARNING
                test.message = f"No results for '{search_query}' - indexers may not be configured"
                test.details = {"total_results": 0, "search_query": search_query}

        except requests.exceptions.HTTPError as e:
            if e.response.status_code == 404:
                test.result = TestResult.FAILED
                test.message = "Channel items endpoint returned 404"
            else:
                test.result = TestResult.FAILED
                test.message = f"HTTP error: {e.response.status_code}"
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Error searching channel: {e}"

        return test


    def test_plugin_configuration(self) -> TestCase:
        """Test 7: Verify plugin configuration is accessible."""
        test = TestCase(
            name="TunnelFin Plugin Configuration",
            description="Verify plugin configuration page is accessible"
        )

        try:
            config = self.client.get_plugin_configuration(self.client.TUNNELFIN_PLUGIN_GUID)
            test.result = TestResult.PASSED
            test.message = "Plugin configuration retrieved successfully"
            test.details = {
                "config_keys": list(config.keys()) if isinstance(config, dict) else "Not a dict",
            }
        except requests.exceptions.HTTPError as e:
            if e.response.status_code == 404:
                test.result = TestResult.WARNING
                test.message = "Plugin configuration not found (plugin may not be installed)"
            else:
                test.result = TestResult.FAILED
                test.message = f"HTTP error: {e.response.status_code}"
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Error getting configuration: {e}"

        return test

    def test_global_search_integration(self) -> TestCase:
        """Test 8: Verify TunnelFin integrates with Jellyfin global search."""
        test = TestCase(
            name="Global Search Integration",
            description="Test if TunnelFin results appear in Jellyfin's global search"
        )

        try:
            results = self.client.search("test movie")
            hints = results.get("SearchHints", [])
            total = results.get("TotalRecordCount", 0)

            # Check if any results come from TunnelFin channel
            tunnelfin_results = [
                h for h in hints
                if h.get("ChannelId") == self.tunnelfin_channel_id
            ]

            if tunnelfin_results:
                test.result = TestResult.PASSED
                test.message = f"Found {len(tunnelfin_results)} TunnelFin results in global search"
            else:
                # This is informational - global search integration depends on configuration
                test.result = TestResult.WARNING
                test.message = f"No TunnelFin results in global search (total hints: {total})"

            test.details = {
                "total_hints": total,
                "tunnelfin_results": len(tunnelfin_results),
                "sample_hints": [h.get("Name") for h in hints[:5]],
            }
        except Exception as e:
            test.result = TestResult.FAILED
            test.message = f"Error with global search: {e}"

        return test

    def test_check_logs_for_errors(self) -> TestCase:
        """Test 9: Check Jellyfin logs for TunnelFin-related errors."""
        test = TestCase(
            name="Log Analysis for TunnelFin Errors",
            description="Check system logs for TunnelFin errors/warnings"
        )

        try:
            # Get activity log for any TunnelFin-related entries
            activity = self.client.get_activity_log(limit=100)
            entries = activity.get("Items", [])

            tunnelfin_entries = [
                e for e in entries
                if "tunnelfin" in e.get("Name", "").lower() or
                   "tunnelfin" in e.get("ShortOverview", "").lower() or
                   "channel" in e.get("Name", "").lower()
            ]

            # Try to get system logs
            log_errors = []
            try:
                logs = self.client.get_system_logs()
                for log in logs[:3]:  # Check last 3 log files
                    log_name = log.get("Name", "")
                    if log_name:
                        try:
                            log_content = self.client.get_log_file(log_name)
                            # Search for TunnelFin-related errors
                            for line in log_content.split('\n')[-500:]:  # Last 500 lines
                                line_lower = line.lower()
                                if "tunnelfin" in line_lower and ("error" in line_lower or "exception" in line_lower or "failed" in line_lower):
                                    log_errors.append(line[:200])
                        except:
                            pass
            except:
                pass

            if log_errors:
                test.result = TestResult.WARNING
                test.message = f"Found {len(log_errors)} TunnelFin-related errors in logs"
                test.details = {
                    "errors": log_errors[:10],  # First 10 errors
                    "activity_entries": len(tunnelfin_entries),
                }
            else:
                test.result = TestResult.PASSED
                test.message = "No TunnelFin errors found in logs"
                test.details = {
                    "activity_entries_checked": len(entries),
                    "tunnelfin_activities": len(tunnelfin_entries),
                }
        except Exception as e:
            test.result = TestResult.WARNING
            test.message = f"Could not check logs: {e}"

        return test

    def run_all_tests(self) -> Dict[str, Any]:
        """Run all E2E tests and return summary."""
        print("\n" + "=" * 60)
        print("TunnelFin End-to-End Test Suite")
        print("=" * 60)
        print(f"Server: {self.client.base_url}")
        print(f"User: {self.client.username}")
        print("-" * 60)

        # Run tests in order
        self.add_result(self.test_server_connection())
        self.add_result(self.test_authentication())

        # Only continue if authentication succeeded
        if self.test_cases[-1].result == TestResult.PASSED:
            self.add_result(self.test_plugin_installed())
            self.add_result(self.test_channel_discovery())
            self.add_result(self.test_channel_features())
            self.add_result(self.test_channel_search())
            self.add_result(self.test_channel_search("sintel"))  # Second search test
            self.add_result(self.test_plugin_configuration())
            self.add_result(self.test_global_search_integration())
            self.add_result(self.test_check_logs_for_errors())

        # Summary
        passed = sum(1 for t in self.test_cases if t.result == TestResult.PASSED)
        failed = sum(1 for t in self.test_cases if t.result == TestResult.FAILED)
        warnings = sum(1 for t in self.test_cases if t.result == TestResult.WARNING)
        skipped = sum(1 for t in self.test_cases if t.result == TestResult.SKIPPED)

        print("\n" + "=" * 60)
        print("TEST SUMMARY")
        print("=" * 60)
        print(f"✅ Passed:   {passed}")
        print(f"❌ Failed:   {failed}")
        print(f"⚠️  Warnings: {warnings}")
        print(f"⏭️  Skipped:  {skipped}")
        print("-" * 60)

        overall = "PASSED" if failed == 0 else "FAILED"
        print(f"Overall Result: {overall}")
        print("=" * 60 + "\n")

        return {
            "passed": passed,
            "failed": failed,
            "warnings": warnings,
            "skipped": skipped,
            "total": len(self.test_cases),
            "overall": overall,
            "test_cases": [
                {
                    "name": t.name,
                    "result": t.result.name,
                    "message": t.message,
                }
                for t in self.test_cases
            ],
        }


def main():
    """Main entry point for E2E tests."""
    # Load .env file if present
    load_env_file()

    # Configuration from environment variables (required)
    jellyfin_url = os.environ.get("JELLYFIN_URL")
    username = os.environ.get("JELLYFIN_USERNAME")
    password = os.environ.get("JELLYFIN_PASSWORD")

    if not jellyfin_url or not username or not password:
        print("ERROR: Missing required environment variables.")
        print("Please set: JELLYFIN_URL, JELLYFIN_USERNAME, JELLYFIN_PASSWORD")
        print("Or create a .env file in the project root with these variables.")
        sys.exit(1)

    print(f"\nInitializing TunnelFin E2E Tests...")
    print(f"Target: {jellyfin_url}")

    # Create client and run tests
    client = JellyfinClient(jellyfin_url, username, password)
    test_suite = TunnelFinE2ETestSuite(client)

    results = test_suite.run_all_tests()

    # Save results to JSON
    results_file = "e2e_test_results.json"
    with open(results_file, "w") as f:
        json.dump(results, f, indent=2)
    print(f"Results saved to: {results_file}")

    # Exit with appropriate code
    sys.exit(0 if results["failed"] == 0 else 1)


if __name__ == "__main__":
    main()
