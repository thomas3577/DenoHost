using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DenoHost.Core;

internal static class Helper
{
  internal static string BuildArguments(string[]? args, string? command = null)
  {
    var argsStr = string.Join(" ", args ?? []);
    if (command == null)
      return argsStr;

    return $"{command} {argsStr}".Trim();
  }

  internal static string[] AppendConfigArgument(string[] args, string configPath)
  {
    if (string.IsNullOrWhiteSpace(configPath))
      return args;

    return [.. args, "--config", configPath];
  }

  internal static string GetDenoPath()
  {
    var rid = GetRuntimeId();
    var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "deno.exe" : "deno";
    var path = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", filename);

    if (!File.Exists(path))
      throw new FileNotFoundException("Deno executable not found.", path);

    return path;
  }

  internal static string GetRuntimeId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "win-x64";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      return "linux-x64";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return RuntimeInformation.OSArchitecture == Architecture.Arm64
          ? "osx-arm64"
          : "osx-x64";

    throw new PlatformNotSupportedException("Unsupported OS platform.");
  }

  internal static string WriteTempConfig(DenoConfig config)
  {
    var tempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");

    File.WriteAllText(tempPath, config.ToJson());

    return tempPath;
  }

  internal static bool IsJsonPathLike(string input)
  {
    if (string.IsNullOrWhiteSpace(input))
      return false;

    input = input.Trim();

    if (input.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
      return true;

    return false;
  }

  internal static string EnsureConfigFile(string configOrPath)
  {
    if (!IsJsonPathLike(configOrPath))
    {
      var tempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");
      File.WriteAllText(tempPath, configOrPath);
      return tempPath;
    }

    if (!File.Exists(configOrPath))
      throw new FileNotFoundException("The specified configuration path does not exist.", configOrPath);

    return configOrPath;
  }

  internal static void DeleteIfTempFile(string resolvedPath, string original)
  {
    if (!IsJsonPathLike(original))
      DeleteTempFile(resolvedPath);
  }

  internal static void DeleteTempFile(string resolvedPath)
  {
    try
    {
      if (File.Exists(resolvedPath))
        File.Delete(resolvedPath);
    }
    catch (Exception ex)
    {
      // throw new IOException($"Failed to delete temporary file: {resolvedPath}", ex);
    }
  }
}
