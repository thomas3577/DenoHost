#!/bin/bash

set -e

EXECUTABLE_PATH="$1"
DOWNLOAD_FILENAME="$2"
DEV_DENO_VERSION="$3"

# === DEBUG OUTPUT ===
echo "=== Deno Download Debug ==="
echo "EXECUTABLE_PATH: $EXECUTABLE_PATH"
echo "DOWNLOAD_FILENAME: $DOWNLOAD_FILENAME"
echo "DEV_DENO_VERSION: $DEV_DENO_VERSION"
echo "Working directory: $(pwd)"
echo "============================"

if [ -f "$EXECUTABLE_PATH" ]; then
  echo "Deno already exists at $EXECUTABLE_PATH"
  exit 0
fi

# Get last Git-Tag (e.g. "v2.4.1-alpha.1")
echo "Step 1: Getting Git tag..."
GIT_TAG=$(git describe --tags --abbrev=0 2>/dev/null)
echo "Git tag found: '$GIT_TAG'"

# Fallback-Version, falls kein Tag gefunden wurde
if [[ -z "$GIT_TAG" ]]; then
  echo "Could not determine Git tag. Using fallback."
  GIT_TAG="v$DEV_DENO_VERSION" # Fallback
  echo "Using fallback tag: '$GIT_TAG'"
fi

# Extract only the main version (e.g. 2.4.1 from v2.4.1-alpha.1)
echo "Step 2: Parsing version from tag '$GIT_TAG'..."
if [[ "$GIT_TAG" =~ ^v?([0-9]+\.[0-9]+\.[0-9]+) ]]; then
  DENO_VERSION="${BASH_REMATCH[1]}"
  echo "Extracted version: '$DENO_VERSION'"
else
  echo "Could not parse version from Git tag '$GIT_TAG'."
  exit 1
fi

DOWNLOAD_URL="https://github.com/denoland/deno/releases/download/v$DENO_VERSION/$DOWNLOAD_FILENAME"
TMP_ZIP="/tmp/deno.zip"
EXTRACT_DIR=$(dirname "$EXECUTABLE_PATH")

echo "Step 3: Download preparation..."
echo "Download URL: $DOWNLOAD_URL"
echo "Temp file: $TMP_ZIP"
echo "Extract dir: $EXTRACT_DIR"

echo "Step 4: Starting download..."
curl -L "$DOWNLOAD_URL" -o "$TMP_ZIP"
echo "Download completed"

echo "Step 5: Extracting to $EXTRACT_DIR"
unzip -o "$TMP_ZIP" -d "$EXTRACT_DIR"
echo "Extraction completed"

echo "Step 6: Cleanup and permissions..."
rm "$TMP_ZIP"
chmod +x "$EXECUTABLE_PATH"

echo "Step 7: Verification..."
if [ -f "$EXECUTABLE_PATH" ]; then
  echo "SUCCESS: Deno setup complete at $EXECUTABLE_PATH"
else
  echo "ERROR: Deno executable not found at $EXECUTABLE_PATH"
  exit 1
fi