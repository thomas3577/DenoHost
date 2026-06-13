using System;
using System.Runtime.CompilerServices;

namespace DenoHost.Tests;

internal static class TestEnvironment
{
  private const string ChecksumBypassEnvVarName = "DENOHOST_ALLOW_CHECKSUM_BYPASS";
  private const string StrictModeEnvVarName = "DENOHOST_STRICT_MODE";
  private const string BypassReasonEnvVarName = "DENOHOST_BYPASS_REASON";

  [ModuleInitializer]
  internal static void Initialize()
  {
    // Ensure external strict-mode config doesn't break the test suite (tests rely on bypass by default)
    if (string.Equals(Environment.GetEnvironmentVariable(StrictModeEnvVarName), "true", StringComparison.OrdinalIgnoreCase))
      Environment.SetEnvironmentVariable(StrictModeEnvVarName, null);

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ChecksumBypassEnvVarName)))
      Environment.SetEnvironmentVariable(ChecksumBypassEnvVarName, "true");

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BypassReasonEnvVarName)))
      Environment.SetEnvironmentVariable(BypassReasonEnvVarName, "Unit Tests");
  }
}
