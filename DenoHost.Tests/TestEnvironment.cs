using System;
using System.Runtime.CompilerServices;

namespace DenoHost.Tests;

internal static class TestEnvironment
{
  private const string ChecksumBypassEnvVarName = "DENOHOST_ALLOW_CHECKSUM_BYPASS";

  [ModuleInitializer]
  internal static void Initialize()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ChecksumBypassEnvVarName)))
      Environment.SetEnvironmentVariable(ChecksumBypassEnvVarName, "true");
  }
}
