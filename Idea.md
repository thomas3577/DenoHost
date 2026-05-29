# Idea: Replace dual `download-deno` scripts with a single .NET CLI

## Problem

We currently maintain two separate implementations for the Deno download/bootstrap logic:

- `tools/download-deno.ps1`
- `tools/download-deno.sh`

Both scripts contain the same core workflow:
- download Deno
- verify checksum
- extract the archive
- generate checksum and metadata
- sign metadata
- clean up temporary files

This is already causing duplication and makes changes error-prone. Any fix in one script must be mirrored in the other, which increases the chance of drift and subtle inconsistencies.

## Goal

Replace the duplicated script logic with a single .NET-based CLI tool that implements the shared bootstrap behavior once and can be used across platforms.

## Why .NET CLI

A .NET CLI seems like the better fit for this repository because:
- the project is already .NET-based
- cryptography, JSON handling, and filesystem logic are easier to maintain in C#
- the implementation can be tested more cleanly
- it reduces the need to keep PowerShell and Bash logic in sync

## Expected outcome

- one shared implementation for the download/bootstrap logic
- no duplicated business logic between shell scripts
- easier maintenance and lower risk of platform-specific drift
- clearer test coverage for the actual behavior

## Possible approaches

1. Build a small .NET CLI app that performs the full Deno bootstrap flow.
2. Keep thin platform wrappers only if needed for convenience.
3. Update CI and packaging to call the new CLI instead of the shell scripts directly.

## Notes

This issue is about reducing maintenance overhead and making the bootstrap process more robust. It is not primarily about changing behavior.
