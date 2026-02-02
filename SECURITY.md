# Security Policy

DenoHost distributes both .NET libraries and bundled Deno runtime binaries.
We take security seriously and encourage responsible reporting.

## Supported Versions

Security fixes are provided only for the **latest published NuGet packages** and GitHub release.

| Component            | Supported Versions      |
|----------------------|-------------------------|
| DenoHost.Core        | Latest NuGet version ✔️ |
| DenoHost.Runtime.*   | Latest NuGet version ✔️ |
| Older releases       | Not supported ❌        |

Because we ship native and third-party binaries, older packages are **not** patched retroactively.

## Reporting a Vulnerability

Please report vulnerabilities privately:

GitHub Security Advisories:
<https://github.com/thomas3577/DenoHost/security/advisories/new>

Do **not** open public issues or pull requests containing security details.

Include when possible:

- Short description and impact
- Steps to reproduce or PoC
- Affected package name and version
- Environment details (OS/arch, .NET version, Deno version if relevant)
- Relevant logs or stack traces

If GitHub advisories are not available, contact the maintainers privately by email.

## Scope

### In Scope

Security issues that originate from:

- Code in this repository
- `DenoHost.Core`
- `DenoHost.Runtime.*` packages
- Packaged Deno runtime binaries as distributed by DenoHost

### Out of Scope

- Vulnerabilities in the upstream **Deno runtime**
- Vulnerabilities in **.NET runtime** or NuGet dependencies
- Issues requiring system-level or privileged access
- DoS/DDoS, performance limitations, or resource exhaustion
- Automated scanner alerts without a clear security impact

If a vulnerability originates from upstream Deno, we will direct you to the relevant security team.

## Disclosure Policy

- We follow coordinated disclosure.
- Details remain private until a fix or mitigation is released.
- After remediation, we publish a GitHub Security Advisory.
- CVE requests may be handled via GitHub’s CVE service.

## Safe Harbor

If you research and report according to this policy:

- Your actions will be considered authorized.
- We will not pursue legal action.
- We appreciate responsible security work and will credit you unless anonymity is requested.

## Questions

For non-sensitive questions about this policy, please open an issue or discussion **without disclosing vulnerability details**.
