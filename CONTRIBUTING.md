# Contributing to DenoHost

Thank you for your interest in contributing to DenoHost!

Contributions are welcome—whether you want to report bugs, suggest features, or
submit pull requests.

## How to contribute

1. **Report bugs or request features**
   - Please use the
     [bug report template](./.github/ISSUE_TEMPLATE/bug_report.md) or
     [feature request template](./.github/ISSUE_TEMPLATE/feature_request.md).

2. **Contribute code**
   - Fork the repository
   - Create a new branch (e.g., `fix/my-bug`)
   - Add your changes (including tests/examples if possible)
   - Open a pull request and describe your changes briefly

## Guidelines

- Write clear, well-documented code (C# / TypeScript)
- Follow the existing project structure
- Add unit tests for new features or bug fixes if possible
- Clearly describe what and why you changed something in your pull request

## Release Safety Process

To avoid shipping broken NuGet packages, releases follow strict gates documented in [Release Safety](./.github/release-safety.md).

In short:

1. Publish `vX.Y.Z-alpha.N` first.
2. Validate alpha CI checks and NuGet verification.
3. Publish stable only from the exact same commit.

## Need help?

If you have any questions, feel free to open an issue!
