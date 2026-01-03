#!/bin/bash
# Build TunnelFin plugin for Jellyfin
# Usage: ./scripts/build-plugin.sh [version]

set -e

VERSION=${1:-"1.0.0.0"}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

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

# MonoTorrent and dependencies
cp publish/MonoTorrent*.dll plugin_package/TunnelFin/
cp publish/Mono.Nat.dll plugin_package/TunnelFin/
cp publish/ReusableTasks.dll plugin_package/TunnelFin/
cp publish/BitFaster.Caching.dll plugin_package/TunnelFin/

# Cryptography
cp publish/NSec.Cryptography.dll plugin_package/TunnelFin/

# Polly (resilience)
cp publish/Polly*.dll plugin_package/TunnelFin/

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
echo ""
echo "To install in Jellyfin:"
echo "1. Add repository: https://raw.githubusercontent.com/jefflouisma/TunnelFin/main/manifest.json"
echo "2. Or manually copy plugin_package/TunnelFin/ to your Jellyfin plugins directory"
echo ""
echo "Update manifest.json checksum to: ${CHECKSUM}"

