#!/bin/bash

set -e

EXECUTABLE_PATH="$1"
DOWNLOAD_FILENAME="$2"
DEV_DENO_VERSION="$3"

if [ -f "$EXECUTABLE_PATH" ]; then
  echo "Deno binary found at $EXECUTABLE_PATH, checking version..."

  # Get current version from existing binary
  if CURRENT_VERSION_OUTPUT=$("$EXECUTABLE_PATH" --version 2>/dev/null); then
    if [[ "$CURRENT_VERSION_OUTPUT" =~ deno\ ([0-9]+\.[0-9]+\.[0-9]+) ]]; then
      CURRENT_VERSION="${BASH_REMATCH[1]}"
      echo "Current Deno version: $CURRENT_VERSION"
      echo "Required Deno version: $DENO_VERSION"

      if [ "$CURRENT_VERSION" = "$DENO_VERSION" ]; then
        echo "Version matches! No download needed."
        echo "Deno setup complete at $EXECUTABLE_PATH"
        exit 0
      else
        echo "Version mismatch! Will download correct version."
      fi
    else
      echo "Warning: Could not determine current Deno version. Will re-download."
    fi
  else
    echo "Warning: Error checking Deno version. Will re-download."
  fi
fi

# Get last Git-Tag (e.g. "v2.4.1-alpha.1")
GIT_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")

# Fallback-Version, falls kein Tag gefunden wurde
if [[ -z "$GIT_TAG" ]]; then
  echo "Could not determine Git tag. Using fallback ('v$DEV_DENO_VERSION')."
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
# Use unique temp file name to avoid conflicts when multiple projects build in parallel
TMP_ZIP="/tmp/deno-$(uuidgen | cut -d'-' -f1).zip"
EXTRACT_DIR=$(dirname "$EXECUTABLE_PATH")

echo "Downloading Deno from $DOWNLOAD_URL"
curl -L "$DOWNLOAD_URL" -o "$TMP_ZIP"

echo "Extracting to $EXTRACT_DIR"
unzip -o "$TMP_ZIP" -d "$EXTRACT_DIR"

# Clean up temp file with error handling
if ! rm "$TMP_ZIP" 2>/dev/null; then
  echo "Warning: Could not remove temp file $TMP_ZIP"
fi

# Ensure the binary is executable
chmod +x "$EXECUTABLE_PATH"
echo "Deno setup complete at $EXECUTABLE_PATH"
