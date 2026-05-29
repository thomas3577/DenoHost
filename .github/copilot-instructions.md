# Copilot Instructions

- Keep it simple (KISS).
- Add or update tests for any new or modified public function, bug fix, or behavior change. Skip tests for pure refactors, comments, or formatting.
- Update documentation in the nearest relevant location: README for user-facing changes, inline doc comments for APIs, CHANGELOG for releases.
- After every code change, run the project's build command and full test suite before finishing. If commands are unknown, ask the user.
- If the build or tests fail, attempt to fix the failure. If unable to fix after a reasonable attempt, stop and report the failure with diagnostic output to the user.
- Add concise code comments only to explain intent, invariants, non-obvious behavior, compatibility contracts, parsing fallbacks, or build/order constraints.
