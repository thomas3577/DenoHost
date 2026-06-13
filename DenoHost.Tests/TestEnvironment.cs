using System;
using System.Runtime.CompilerServices;

namespace DenoHost.Tests;

internal static class TestEnvironment
{
  private const string ChecksumBypassEnvVarName = "DENOHOST_ALLOW_CHECKSUM_BYPASS";
  private const string BypassReasonEnvVarName = "DENOHOST_BYPASS_REASON";

  [ModuleInitializer]
  internal static void Initialize()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ChecksumBypassEnvVarName)))
      Environment.SetEnvironmentVariable(ChecksumBypassEnvVarName, "true");

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BypassReasonEnvVarName)))
      Environment.SetEnvironmentVariable(BypassReasonEnvVarName, "Unit Tests");
  }
}
