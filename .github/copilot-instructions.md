# Copilot Instructions

## General Principles
- Keep it simple (KISS).
- Add or update tests for any new or modified public function, bug fix, or behavior change. Skip tests for pure refactors, comments, or formatting.
- Update documentation in the nearest relevant location: README for user-facing changes, inline doc comments for APIs, CHANGELOG for releases.

## Execution & Verification (MCP / CLI)
- After every code change, run the project's build command and full test suite before finishing; when commands are unknown, ask the user.
- When build or tests fail, attempt to fix the failure, and after a reasonable attempt, stop and report diagnostic output to the user.

## Code Style & Comments
- Add concise code comments only to explain intent, invariants, non-obvious behavior, compatibility contracts, parsing fallbacks, or build/order constraints.
- Follow modern C# best practices (e.g., file-scoped namespaces, pattern matching, primary constructors where applicable).

## Code Review Guidelines (When asked for /review or diffs)
When reviewing code, analyze the diff or workspace and report issues in these categories:
1. **.NET Efficiency:** Look for improper async/await usage (e.g., missing ConfigureAwait if applicable, blocking via .Result), unnecessary allocations (use ReadOnlySpan<T> or ValueTask where beneficial), and missing IDisposable/IAsyncDisposable handling.
2. **Robustness & Edge Cases:** Check for potential NullReferenceExceptions (respect nullable reference types), unhandled exceptions in async paths, and missing input validation.
3. **Maintainability:** Identify high cyclomatic complexity, deeply nested loops, or violations of the project's established architecture.
4. **Testability:** Warn if a change makes code harder to unit test (e.g., hardcoded dependencies instead of Dependency Injection).
