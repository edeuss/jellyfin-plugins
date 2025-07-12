#!/bin/bash

# Build script for Jellyfin.Plugin.Animated.Music
set -e

# Parse version from Plugin.cs (extract only the version string)
PLUGIN_CS="Jellyfin.Plugin.Animated.Music/Plugin.cs"
VERSION=$(grep 'Version' "$PLUGIN_CS" | grep -oE '"[0-9]+(\.[0-9]+)*"' | tr -d '"')
if [ -z "$VERSION" ]; then
  echo "Error: Could not determine version from $PLUGIN_CS"
  exit 1
fi

ZIP_NAME="Jellyfin.Plugin.Animated.Music_${VERSION}.zip"

# Clean previous builds and zips
echo "Cleaning previous builds and packages..."
dotnet clean --configuration Release
rm -rf build/
rm -f Jellyfin.Plugin.Animated.Music_*.zip

# Check if .NET 8.0 is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET 8.0 SDK is not installed. Please install it first."
    exit 1
fi

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Build the project
echo "Building project..."
dotnet build --configuration Release --no-restore

# Create output directory
echo "Creating build directory..."
mkdir -p build

# Copy built files to output directory (excluding macOS metadata)
echo "Copying built files..."
cp -r Jellyfin.Plugin.Animated.Music/bin/Release/net8.0/* build/

# Remove any macOS metadata files that might have been copied
echo "Cleaning macOS metadata files..."
find build -name "._*" -delete
find build -name ".DS_Store" -delete
find build -name "__MACOSX" -type d -exec rm -rf {} + 2>/dev/null || true

# Create plugin package
echo "Creating plugin package $ZIP_NAME..."
cd build
zip -r "../$ZIP_NAME" . -x "*.DS_Store" "*._*" "__MACOSX/*"
cd ..

echo "Build completed successfully!"
echo "Plugin files are available in the 'build' directory."
echo "Plugin package: $ZIP_NAME"
echo ""
echo "To install the plugin:"
echo "1. Copy the contents of the 'build' directory to your Jellyfin plugins folder"
echo "   OR extract $ZIP_NAME to your Jellyfin plugins folder"
echo "2. Restart Jellyfin Server"
echo "3. The plugin will appear in the Plugins section of your Jellyfin dashboard" 