#!/bin/bash

set -e

EXECUTABLE_PATH="$1"
DOWNLOAD_URL="$2"

if [ -f "$EXECUTABLE_PATH" ]; then
  echo "Deno already exists at $EXECUTABLE_PATH"
  exit 0
fi

TMP_ZIP="/tmp/deno.zip"
EXTRACT_DIR=$(dirname "$EXECUTABLE_PATH")

echo "Downloading Deno from $DOWNLOAD_URL"
curl -L "$DOWNLOAD_URL" -o "$TMP_ZIP"

echo "Extracting to $EXTRACT_DIR"
unzip -o "$TMP_ZIP" -d "$EXTRACT_DIR"
rm "$TMP_ZIP"

chmod +x "$EXECUTABLE_PATH"
echo "Deno setup complete at $EXECUTABLE_PATH"
