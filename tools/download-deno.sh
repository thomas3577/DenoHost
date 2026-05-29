#!/bin/bash

set -e

EXECUTABLE_PATH="$1"
DOWNLOAD_FILENAME="$2"
DENO_VERSION="$3"
RUNTIME_RID="$4"

compute_sha256() {
  local file_path="$1"

  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file_path" | awk '{print tolower($1)}'
    return
  fi

  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$file_path" | awk '{print tolower($1)}'
    return
  fi

  echo "Error: Neither sha256sum nor shasum is available for checksum verification." >&2
  exit 1
}

write_executable_checksum() {
  local executable="$1"
  local output_file="$2"
  local executable_name
  local executable_hash

  executable_name=$(basename "$executable")
  executable_hash=$(compute_sha256 "$executable")
  printf "%s  %s\n" "$executable_hash" "$executable_name" > "$output_file"
  echo "Wrote executable checksum to $output_file"
}

extract_expected_hash() {
  local checksum_file="$1"
  local target_file="$2"
  local expected_hash

  if [ ! -f "$checksum_file" ]; then
    echo "Error: Checksum file not found: $checksum_file" >&2
    exit 1
  fi

  # First try: match by filename
  expected_hash=$(awk -v file="$target_file" '
    BEGIN { IGNORECASE=1 }
    {
      # Remove leading * from filename if present (BSD format)
      candidate = $2
      gsub(/^\*/, "", candidate)
      # Normalize path separators
      gsub(/\\/, "/", candidate)
      gsub(/\\/, "/", file)
      if (tolower(candidate) == tolower(file) && $1 ~ /^[A-Fa-f0-9]{64}$/) {
        print tolower($1)
        exit
      }
    }
  ' "$checksum_file")

  if [ -n "$expected_hash" ]; then
    echo "$expected_hash"
    return
  fi

  # Second try: match by just filename basename if full path didn't work
  expected_hash=$(awk -v file="$(basename "$target_file")" '
    BEGIN { IGNORECASE=1 }
    {
      candidate = $2
      gsub(/^\*/, "", candidate)
      # Get just the basename for comparison
      gsub(/.*\//, "", candidate)
      gsub(/.*\\/, "", candidate)
      if (tolower(candidate) == tolower(file) && $1 ~ /^[A-Fa-f0-9]{64}$/) {
        print tolower($1)
        exit
      }
    }
  ' "$checksum_file")

  if [ -n "$expected_hash" ]; then
    echo "$expected_hash"
    return
  fi

  # Third try: PowerShell Get-FileHash format: "Hash      : <HEX64>"
  expected_hash=$(awk '
    BEGIN { IGNORECASE=1 }
    /^\s*Hash\s*:\s*[A-Fa-f0-9]{64}/ {
      match($0, /[A-Fa-f0-9]{64}/)
      hash = substr($0, RSTART, RLENGTH)
      print tolower(hash)
      exit
    }
  ' "$checksum_file")

  if [ -n "$expected_hash" ]; then
    echo "$expected_hash"
    return
  fi

  # Fourth try: just take first valid SHA256 (fallback for single-file checksum files)
  expected_hash=$(awk '
    {
      if ($1 ~ /^[A-Fa-f0-9]{64}$/) {
        print tolower($1)
        exit
      }
    }
  ' "$checksum_file")

  if [ -z "$expected_hash" ]; then
    echo "Error: Could not parse expected SHA-256 from $checksum_file for $target_file" >&2
    echo "Checksum file content:" >&2
    head -5 "$checksum_file" >&2
    exit 1
  fi

  echo "$expected_hash"
}

resolve_runtime_rid() {
  local executable="$1"
  local provided_rid="$2"

  if [ -n "$provided_rid" ]; then
    echo "$provided_rid"
    return
  fi

  local runtime_dir
  runtime_dir=$(basename "$(dirname "$executable")")

  if [[ "$runtime_dir" == DenoHost.Runtime.* ]]; then
    echo "${runtime_dir#DenoHost.Runtime.}"
    return
  fi

  echo "unknown"
}

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  value="${value//$'\r'/\\r}"
  value="${value//$'\t'/\\t}"
  printf "%s" "$value"
}

write_runtime_metadata() {
  local executable="$1"
  local deno_version="$2"
  local runtime_rid="$3"
  local archive_name="$4"
  local executable_name
  local executable_hash
  local metadata_path
  local created_at
  local source_url

  executable_name=$(basename "$executable")
  executable_hash=$(compute_sha256 "$executable")
  metadata_path="$(dirname "$executable")/deno.metadata.json"
  created_at=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  source_url="https://github.com/denoland/deno/releases/download/v${deno_version}/${archive_name}"

  cat > "$metadata_path" <<EOF
{"metadataVersion":1,"fileName":"$(json_escape "$executable_name")","rid":"$(json_escape "$runtime_rid")","denoVersion":"$(json_escape "$deno_version")","sha256":"$(json_escape "$executable_hash")","source":"$(json_escape "$source_url")","createdAtUtc":"$(json_escape "$created_at")"}
EOF

  # Keep human-readable log output off stdout so callers using command substitution
  # receive only the metadata path.
  echo "Wrote runtime metadata to $metadata_path" >&2
  printf "%s\n" "$metadata_path"
}

sign_runtime_metadata() {
  local metadata_path="$1"
  local signature_path
  local key_file

  if [ -z "$DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM" ]; then
    echo "Warning: DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM is not set; skipping runtime metadata signature." >&2
    return 0
  fi

  signature_path="$(dirname "$metadata_path")/deno.metadata.sig"

  if ! command -v openssl >/dev/null 2>&1; then
    echo "Error: openssl is required for metadata signing when DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM is set." >&2
    exit 1
  fi

  key_file=$(mktemp)
  # Ensure the private key temp file is removed even if openssl fails or the script
  # is interrupted; the trap is cleared after successful cleanup.
  trap 'rm -f "$key_file"' EXIT ERR INT TERM
  printf "%s" "$DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM" > "$key_file"

  openssl dgst -sha256 -sign "$key_file" -binary "$metadata_path" | openssl base64 -A > "$signature_path"
  rm -f "$key_file"
  trap - EXIT ERR INT TERM

  echo "Wrote runtime metadata signature to $signature_path"
}

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
        write_executable_checksum "$EXECUTABLE_PATH" "$EXECUTABLE_PATH.sha256sum"
        RESOLVED_RID=$(resolve_runtime_rid "$EXECUTABLE_PATH" "$RUNTIME_RID")
        METADATA_PATH=$(write_runtime_metadata "$EXECUTABLE_PATH" "$DENO_VERSION" "$RESOLVED_RID" "$DOWNLOAD_FILENAME")
        sign_runtime_metadata "$METADATA_PATH"
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

DOWNLOAD_URL="https://github.com/denoland/deno/releases/download/v$DENO_VERSION/$DOWNLOAD_FILENAME"
CHECKSUM_URL="$DOWNLOAD_URL.sha256sum"
# Use unique temp file name to avoid conflicts when multiple projects build in parallel
TMP_ZIP="/tmp/deno-$(uuidgen | cut -d'-' -f1).zip"
TMP_CHECKSUM="$TMP_ZIP.sha256sum"
EXTRACT_DIR=$(dirname "$EXECUTABLE_PATH")

echo "Downloading Deno from $DOWNLOAD_URL"
curl --fail --show-error --location "$DOWNLOAD_URL" -o "$TMP_ZIP"

echo "Downloading checksum from $CHECKSUM_URL"
curl --fail --show-error --location "$CHECKSUM_URL" -o "$TMP_CHECKSUM"

EXPECTED_HASH=$(extract_expected_hash "$TMP_CHECKSUM" "$DOWNLOAD_FILENAME")
ACTUAL_HASH=$(compute_sha256 "$TMP_ZIP")

if [ "$EXPECTED_HASH" != "$ACTUAL_HASH" ]; then
  echo "Error: Checksum verification failed for $DOWNLOAD_FILENAME. Expected: $EXPECTED_HASH Actual: $ACTUAL_HASH" >&2
  exit 1
fi

echo "Checksum verification passed for $DOWNLOAD_FILENAME"

echo "Extracting to $EXTRACT_DIR"
unzip -o "$TMP_ZIP" -d "$EXTRACT_DIR"

downloaded_exe=$(find "$EXTRACT_DIR" -type f \( -name "deno" -o -name "deno.exe" \) | head -n 1)

# Some archives contain files like deno.metadata.json and deno.sha256sum next to the binary.
# Only rename the actual binary, never the first file that merely starts with deno.
if [ -n "$downloaded_exe" ] && [ "$downloaded_exe" != "$EXECUTABLE_PATH" ]; then
  mv -f "$downloaded_exe" "$EXECUTABLE_PATH"
fi

write_executable_checksum "$EXECUTABLE_PATH" "$EXECUTABLE_PATH.sha256sum"
RESOLVED_RID=$(resolve_runtime_rid "$EXECUTABLE_PATH" "$RUNTIME_RID")
METADATA_PATH=$(write_runtime_metadata "$EXECUTABLE_PATH" "$DENO_VERSION" "$RESOLVED_RID" "$DOWNLOAD_FILENAME")
sign_runtime_metadata "$METADATA_PATH"

# Clean up temp file with error handling
if ! rm "$TMP_ZIP" 2>/dev/null; then
  echo "Warning: Could not remove temp file $TMP_ZIP"
fi

if ! rm "$TMP_CHECKSUM" 2>/dev/null; then
  echo "Warning: Could not remove temp checksum file $TMP_CHECKSUM"
fi

# Ensure the binary is executable
chmod +x "$EXECUTABLE_PATH"
echo "Deno setup complete at $EXECUTABLE_PATH"
