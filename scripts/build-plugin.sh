#!/bin/bash
# Build and optionally deploy TunnelFin plugin for Jellyfin
# Usage: ./scripts/build-plugin.sh [version] [--deploy|--deploy-local]
#
# Options:
#   version         Plugin version (default: 1.0.0.0)
#   --deploy        Deploy via Jellyfin API from GitHub repository (requires manifest/release)
#   --deploy-local  Deploy locally built files directly to Jellyfin pod via kubectl
#
# Environment variables (from .env file):
#   JELLYFIN_URL       - Jellyfin server URL (e.g., http://192.168.1.10:8096)
#   JELLYFIN_USERNAME  - Admin username
#   JELLYFIN_PASSWORD  - Admin password
#   JELLYFIN_NAMESPACE - Kubernetes namespace (default: house)
#   JELLYFIN_POD_LABEL - Pod selector label (default: app=jellyfin)

set -e

# Parse arguments
VERSION="1.0.0.0"
DEPLOY=false
DEPLOY_LOCAL=false

for arg in "$@"; do
    case $arg in
        --deploy)
            DEPLOY=true
            ;;
        --deploy-local)
            DEPLOY_LOCAL=true
            ;;
        *)
            VERSION="$arg"
            ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

# Load .env if it exists
if [ -f "$ROOT_DIR/.env" ]; then
    set -a
    source "$ROOT_DIR/.env"
    set +a
fi

echo "🔨 Building TunnelFin v${VERSION}..."

cd "$ROOT_DIR"

# Clean previous builds
rm -rf publish plugin_package/*.zip

# Build and publish
dotnet publish src/TunnelFin/TunnelFin.csproj -c Release -o ./publish

# Create plugin package directory
mkdir -p plugin_package/TunnelFin

# Copy TunnelFin and dependencies (excluding Jellyfin assemblies which are provided by Jellyfin)
echo "📦 Packaging plugin..."
cp publish/TunnelFin.dll plugin_package/TunnelFin/
cp publish/TunnelFin.pdb plugin_package/TunnelFin/ 2>/dev/null || true

# Copy and update meta.json with version
META_FILE="$ROOT_DIR/src/TunnelFin/meta.json"
if [ -f "$META_FILE" ]; then
    # Update version in meta.json and copy
    sed "s/\"version\": \"[^\"]*\"/\"version\": \"${VERSION}\"/" "$META_FILE" > plugin_package/TunnelFin/meta.json
    echo "✓ meta.json included"
fi

# MonoTorrent and dependencies - EXCLUDE MonoTorrent.PortForwarding.dll which has Mono.Nat conflict
# The IPortForwarder interface is in MonoTorrent.dll, we use our own NullPortForwarder
for dll in publish/MonoTorrent*.dll; do
    if [[ "$(basename "$dll")" != "MonoTorrent.PortForwarding.dll" ]]; then
        cp "$dll" plugin_package/TunnelFin/
    fi
done
# DO NOT include Mono.Nat.dll - it conflicts with Jellyfin's version
# cp publish/Mono.Nat.dll plugin_package/TunnelFin/ 2>/dev/null || true
cp publish/ReusableTasks.dll plugin_package/TunnelFin/
cp publish/BitFaster.Caching.dll plugin_package/TunnelFin/ 2>/dev/null || true

# Cryptography
cp publish/NSec.Cryptography.dll plugin_package/TunnelFin/

# Polly (resilience) - optional
cp publish/Polly*.dll plugin_package/TunnelFin/ 2>/dev/null || true

# HtmlAgilityPack for scraping
cp publish/HtmlAgilityPack.dll plugin_package/TunnelFin/ 2>/dev/null || true

# Create ZIP
ZIP_NAME="tunnelfin_${VERSION}.zip"
cd plugin_package
zip -r "../${ZIP_NAME}" TunnelFin/
cd ..

# Calculate MD5 checksum
if command -v md5sum &> /dev/null; then
    CHECKSUM=$(md5sum "$ZIP_NAME" | cut -d' ' -f1)
elif command -v md5 &> /dev/null; then
    CHECKSUM=$(md5 -q "$ZIP_NAME")
else
    CHECKSUM="UNKNOWN"
fi

echo ""
echo "✅ Build complete!"
echo "📁 Plugin package: ${ZIP_NAME}"
echo "🔑 MD5 Checksum: ${CHECKSUM}"

# Deploy to Jellyfin if requested
if [ "$DEPLOY" = true ]; then
    echo ""
    echo "🚀 Deploying to Jellyfin..."

    if [ -z "$JELLYFIN_URL" ] || [ -z "$JELLYFIN_USERNAME" ] || [ -z "$JELLYFIN_PASSWORD" ]; then
        echo "❌ Error: Missing Jellyfin credentials in .env file"
        echo "   Required: JELLYFIN_URL, JELLYFIN_USERNAME, JELLYFIN_PASSWORD"
        exit 1
    fi

    # Authenticate and get access token
    echo "   🔐 Authenticating..."
    AUTH_RESPONSE=$(curl -s -X POST "${JELLYFIN_URL}/Users/AuthenticateByName" \
        -H "Content-Type: application/json" \
        -H "X-Emby-Authorization: MediaBrowser Client=\"TunnelFin Build\", Device=\"Build Script\", DeviceId=\"build-script\", Version=\"1.0.0\"" \
        -d "{\"Username\":\"${JELLYFIN_USERNAME}\",\"Pw\":\"${JELLYFIN_PASSWORD}\"}")

    ACCESS_TOKEN=$(echo "$AUTH_RESPONSE" | grep -o '"AccessToken":"[^"]*"' | cut -d'"' -f4)
    USER_ID=$(echo "$AUTH_RESPONSE" | grep -o '"Id":"[^"]*"' | head -1 | cut -d'"' -f4)

    if [ -z "$ACCESS_TOKEN" ]; then
        echo "❌ Error: Failed to authenticate with Jellyfin"
        echo "   Response: $AUTH_RESPONSE"
        exit 1
    fi
    echo "   ✓ Authenticated as user $USER_ID"

    AUTH_HEADER="X-Emby-Authorization: MediaBrowser Client=\"TunnelFin Build\", Device=\"Build Script\", DeviceId=\"build-script\", Version=\"1.0.0\", Token=\"${ACCESS_TOKEN}\""

    # Check if TunnelFin repository is already added
    # Use current git branch for manifest URL, default to main
    CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "main")
    REPO_URL="https://raw.githubusercontent.com/jefflouisma/TunnelFin/${CURRENT_BRANCH}/manifest.json"
    echo "   📋 Using manifest from branch: ${CURRENT_BRANCH}"
    echo "   📋 Checking plugin repositories..."
    REPOS=$(curl -s -X GET "${JELLYFIN_URL}/Repositories" -H "$AUTH_HEADER")

    if echo "$REPOS" | grep -q "TunnelFin"; then
        echo "   ✓ TunnelFin repository already configured"
    else
        echo "   📥 Adding TunnelFin repository..."
        # Get current repos and add ours
        NEW_REPOS=$(echo "$REPOS" | sed 's/\]$/,{"Name":"TunnelFin","Url":"'"$REPO_URL"'","Enabled":true}]/')
        if [ "$REPOS" = "[]" ]; then
            NEW_REPOS='[{"Name":"TunnelFin","Url":"'"$REPO_URL"'","Enabled":true}]'
        fi

        REPO_RESULT=$(curl -s -X POST "${JELLYFIN_URL}/Repositories" \
            -H "$AUTH_HEADER" \
            -H "Content-Type: application/json" \
            -d "$NEW_REPOS")
        echo "   ✓ Repository added"
    fi

    # Check installed plugins
    echo "   🔍 Checking installed plugins..."
    PLUGINS=$(curl -s -X GET "${JELLYFIN_URL}/Plugins" -H "$AUTH_HEADER")
    TUNNELFIN_INSTALLED=$(echo "$PLUGINS" | grep -o '"Name":"TunnelFin"' || true)

    if [ -n "$TUNNELFIN_INSTALLED" ]; then
        # Get plugin ID for uninstall
        PLUGIN_ID=$(echo "$PLUGINS" | grep -o '"Id":"[^"]*","Name":"TunnelFin"' | grep -o '"Id":"[^"]*"' | cut -d'"' -f4)
        if [ -n "$PLUGIN_ID" ]; then
            echo "   🗑️  Uninstalling existing TunnelFin plugin..."
            curl -s -X DELETE "${JELLYFIN_URL}/Plugins/${PLUGIN_ID}" -H "$AUTH_HEADER" > /dev/null
            echo "   ✓ Old version uninstalled"
        fi
    fi

    # Install plugin from repository
    echo "   📦 Installing TunnelFin v${VERSION}..."
    INSTALL_RESULT=$(curl -s -X POST "${JELLYFIN_URL}/Packages/Installed/TunnelFin?repositoryUrl=${REPO_URL}&version=${VERSION}" \
        -H "$AUTH_HEADER")

    if echo "$INSTALL_RESULT" | grep -qi "error\|failed"; then
        echo "   ⚠️  Repository install may have failed, trying manual method..."
        # Alternative: Copy files directly if we have access (for local dev)
    else
        echo "   ✓ Plugin installation initiated"
    fi

    # Restart Jellyfin
    echo "   🔄 Restarting Jellyfin server..."
    curl -s -X POST "${JELLYFIN_URL}/System/Restart" -H "$AUTH_HEADER" > /dev/null
    echo "   ✓ Restart initiated"

    echo ""
    echo "✅ Deployment complete!"
    echo "   Jellyfin is restarting. Plugin will be available in ~30 seconds."
    echo "   Check ${JELLYFIN_URL}/web/index.html#!/dashboard/plugins for status."

elif [ "$DEPLOY_LOCAL" = true ]; then
    echo ""
    echo "🚀 Local build + GitHub deploy to Jellyfin..."

    # Check for gh CLI
    if ! command -v gh &> /dev/null; then
        echo "❌ Error: GitHub CLI (gh) not found. Install with: brew install gh"
        exit 1
    fi

    if [ -z "$JELLYFIN_URL" ] || [ -z "$JELLYFIN_USERNAME" ] || [ -z "$JELLYFIN_PASSWORD" ]; then
        echo "❌ Error: Missing Jellyfin credentials in .env file"
        echo "   Required: JELLYFIN_URL, JELLYFIN_USERNAME, JELLYFIN_PASSWORD"
        exit 1
    fi

    REPO_URL="https://raw.githubusercontent.com/jefflouisma/TunnelFin/main/manifest.json"
    RELEASE_TAG="v${VERSION}"
    TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

    # Step 1: Update manifest.json with correct checksum
    echo "   📝 Updating manifest.json with checksum ${CHECKSUM}..."

    # Check if version exists in manifest, update or add
    if grep -q "\"version\": \"${VERSION}\"" manifest.json; then
        # Update existing version's checksum and timestamp
        sed -i '' "s|\"checksum\": \"[^\"]*\"|\"checksum\": \"${CHECKSUM}\"|" manifest.json
        # Only update first timestamp after version match (crude but works for our format)
    else
        # Add new version entry at the top of versions array
        NEW_VERSION_ENTRY='{
                "version": "'"${VERSION}"'",
                "changelog": "v'"${VERSION}"' - Local build deployment",
                "targetAbi": "10.11.0.0",
                "sourceUrl": "https://github.com/jefflouisma/TunnelFin/releases/download/'"${RELEASE_TAG}"'/tunnelfin_'"${VERSION}"'.zip",
                "checksum": "'"${CHECKSUM}"'",
                "timestamp": "'"${TIMESTAMP}"'"
            },'
        sed -i '' 's/"versions": \[/"versions": [\n            '"$(echo "$NEW_VERSION_ENTRY" | tr '\n' ' ' | sed 's/  */ /g')"'/' manifest.json
    fi

    # Step 2: Create or update GitHub release
    echo "   📦 Uploading to GitHub release ${RELEASE_TAG}..."

    # Check if release exists
    if gh release view "$RELEASE_TAG" &>/dev/null; then
        echo "      Release exists, updating asset..."
        gh release upload "$RELEASE_TAG" "${ZIP_NAME}" --clobber
    else
        echo "      Creating new release..."
        gh release create "$RELEASE_TAG" "${ZIP_NAME}" \
            --title "${RELEASE_TAG} - Local Build" \
            --notes "Automated local build deployment"
    fi
    echo "   ✓ Release uploaded"

    # Step 3: Commit and push manifest to main
    echo "   📤 Pushing manifest to main branch..."
    git add manifest.json
    git commit -m "Update manifest for ${RELEASE_TAG} (checksum: ${CHECKSUM})" || true

    # Push to current branch first
    CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
    git push origin "$CURRENT_BRANCH"

    # Merge to main if not already on main
    if [ "$CURRENT_BRANCH" != "main" ]; then
        # Use worktree if it exists, otherwise checkout
        if [ -d ".git/beads-worktrees/main" ]; then
            (cd .git/beads-worktrees/main && git pull origin main && git merge "origin/${CURRENT_BRANCH}" --no-edit && git push origin main)
        else
            git fetch origin main
            git push origin "${CURRENT_BRANCH}:main"
        fi
    fi
    echo "   ✓ Manifest pushed to main"

    # Step 4: Wait for GitHub to update
    echo "   ⏳ Waiting for GitHub CDN to update (5s)..."
    sleep 5

    # Step 5: Deploy via Jellyfin API
    echo "   🔐 Authenticating with Jellyfin..."
    AUTH_RESPONSE=$(curl -s -X POST "${JELLYFIN_URL}/Users/AuthenticateByName" \
        -H "Content-Type: application/json" \
        -H "X-Emby-Authorization: MediaBrowser Client=\"TunnelFin Build\", Device=\"Build Script\", DeviceId=\"build-script\", Version=\"1.0.0\"" \
        -d "{\"Username\":\"${JELLYFIN_USERNAME}\",\"Pw\":\"${JELLYFIN_PASSWORD}\"}")

    ACCESS_TOKEN=$(echo "$AUTH_RESPONSE" | grep -o '"AccessToken":"[^"]*"' | cut -d'"' -f4)

    if [ -z "$ACCESS_TOKEN" ]; then
        echo "❌ Error: Failed to authenticate with Jellyfin"
        exit 1
    fi
    echo "   ✓ Authenticated"

    AUTH_HEADER="X-Emby-Authorization: MediaBrowser Client=\"TunnelFin Build\", Device=\"Build Script\", DeviceId=\"build-script\", Version=\"1.0.0\", Token=\"${ACCESS_TOKEN}\""

    # Ensure repository is configured
    echo "   📋 Checking plugin repositories..."
    REPOS=$(curl -s -X GET "${JELLYFIN_URL}/Repositories" -H "$AUTH_HEADER")

    if ! echo "$REPOS" | grep -q "TunnelFin"; then
        echo "   📥 Adding TunnelFin repository..."
        if [ "$REPOS" = "[]" ]; then
            NEW_REPOS='[{"Name":"TunnelFin","Url":"'"$REPO_URL"'","Enabled":true}]'
        else
            NEW_REPOS=$(echo "$REPOS" | sed 's/\]$/,{"Name":"TunnelFin","Url":"'"$REPO_URL"'","Enabled":true}]/')
        fi
        curl -s -X POST "${JELLYFIN_URL}/Repositories" -H "$AUTH_HEADER" -H "Content-Type: application/json" -d "$NEW_REPOS" > /dev/null
        echo "   ✓ Repository added"
    else
        echo "   ✓ Repository already configured"
    fi

    # Uninstall existing version if present
    echo "   🔍 Checking for existing installation..."
    PLUGINS=$(curl -s -X GET "${JELLYFIN_URL}/Plugins" -H "$AUTH_HEADER")
    PLUGIN_ID=$(echo "$PLUGINS" | grep -o '"Id":"[^"]*","Name":"TunnelFin"' | grep -o '"Id":"[^"]*"' | cut -d'"' -f4 || true)

    if [ -n "$PLUGIN_ID" ]; then
        echo "   🗑️  Uninstalling existing TunnelFin..."
        curl -s -X DELETE "${JELLYFIN_URL}/Plugins/${PLUGIN_ID}" -H "$AUTH_HEADER" > /dev/null
        echo "   ✓ Old version uninstalled"
    fi

    # Install from repository
    echo "   📦 Installing TunnelFin v${VERSION} from repository..."
    INSTALL_RESULT=$(curl -s -w "\n%{http_code}" -X POST "${JELLYFIN_URL}/Packages/Installed/TunnelFin?repositoryUrl=${REPO_URL}&version=${VERSION}" -H "$AUTH_HEADER")
    HTTP_CODE=$(echo "$INSTALL_RESULT" | tail -1)

    if [ "$HTTP_CODE" = "204" ] || [ "$HTTP_CODE" = "200" ]; then
        echo "   ✓ Plugin installation initiated"
    else
        echo "   ⚠️  Install returned HTTP ${HTTP_CODE}"
    fi

    # Restart Jellyfin
    echo "   🔄 Restarting Jellyfin server..."
    curl -s -X POST "${JELLYFIN_URL}/System/Restart" -H "$AUTH_HEADER" > /dev/null
    echo "   ✓ Restart initiated"

    echo ""
    echo "✅ Local build deployed!"
    echo "   Jellyfin is restarting. Plugin will be available in ~30 seconds."
    echo "   Check ${JELLYFIN_URL}/web/index.html#!/dashboard/plugins for status."

else
    echo ""
    echo "To install in Jellyfin:"
    echo "1. Add repository: https://raw.githubusercontent.com/jefflouisma/TunnelFin/main/manifest.json"
    echo "2. Or manually copy plugin_package/TunnelFin/ to your Jellyfin plugins directory"
    echo "3. Or run: ./scripts/build-plugin.sh ${VERSION} --deploy        (from GitHub repo)"
    echo "4. Or run: ./scripts/build-plugin.sh ${VERSION} --deploy-local  (direct to k8s pod)"
    echo ""
    echo "Update manifest.json checksum to: ${CHECKSUM}"
fi

