#!/bin/bash

set -e

EXECUTABLE_PATH="$1"
DOWNLOAD_FILENAME="$2"
DEV_DENO_VERSION="$3"

if [ -f "$EXECUTABLE_PATH" ]; then
  echo "Deno already exists at $EXECUTABLE_PATH"
  exit 0
fi

# Get last Git-Tag (e.g. "v2.4.1-alpha.1")
GIT_TAG=$(git describe --tags --abbrev=0 2>/dev/null)

# Fallback-Version, falls kein Tag gefunden wurde
if [[ -z "$GIT_TAG" ]]; then
  echo "Could not determine Git tag. Using fallback."
  GIT_TAG="v$DEV_DENO_VERSION" # Fallback
fi

# Extract only the main version (e.g. 2.4.1 from v2.4.1-alpha.1)
if [[ "$GIT_TAG" =~ ^v?([0-9]+\.[0-9]+\.[0-9]+) ]]; then
  DENO_VERSION="${BASH_REMATCH[1]}"
else
  echo "Could not parse version from Git tag '$GIT_TAG'."
  exit 1
fi

DOWNLOAD_URL="https://github.com/denoland/deno/releases/download/v$DENO_VERSION/$DOWNLOAD_FILENAME"
TMP_ZIP="/tmp/deno.zip"
EXTRACT_DIR=$(dirname "$EXECUTABLE_PATH")

echo "Downloading Deno from $DOWNLOAD_URL"
curl -L "$DOWNLOAD_URL" -o "$TMP_ZIP"

echo "Extracting to $EXTRACT_DIR"
unzip -o "$TMP_ZIP" -d "$EXTRACT_DIR"
rm "$TMP_ZIP"

chmod +x "$EXECUTABLE_PATH"
echo "Deno setup complete at $EXECUTABLE_PATH"
