using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using DenoHost.Core.Config;

namespace DenoHost.Core;

internal static class Helper
{
  internal static string BuildArguments(string[]? args, string? command = null)
  {
    var argsArray = BuildArgumentsArray(args, command);
    return string.Join(' ', argsArray);
  }

  internal static string[] BuildArgumentsArray(string[]? args, string? command = null)
  {
    if (command == null)
      return args ?? [];

    var result = new string[1 + (args?.Length ?? 0)];
    result[0] = command;

    if (args != null)
      Array.Copy(args, 0, result, 1, args.Length);

    return result;
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
    var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "deno.exe" : "deno";
    var basePath = Path.GetFullPath(AppContext.BaseDirectory);
    var path = Path.Combine(basePath, "runtimes", rid, "native", fileName);

    // Ensure the path is within the expected directory structure (prevent path traversal)
    var normalizedPath = Path.GetFullPath(path);
    if (!normalizedPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Invalid path detected. Potential path traversal attempt.");

    if (!File.Exists(normalizedPath))
      throw new FileNotFoundException("Deno executable not found.", normalizedPath);

    // Validate file permissions and executable status
    ValidateExecutableFile(normalizedPath);

    return normalizedPath;
  }

  internal static string GetRuntimeId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      // Deno currently only supports Windows x64, not ARM64
      if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        throw new PlatformNotSupportedException("Windows ARM64 is not supported by Deno. Only Windows x64 is available.");

      return "win-x64";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      return RuntimeInformation.OSArchitecture == Architecture.Arm64
        ? "linux-arm64"
        : "linux-x64";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return RuntimeInformation.OSArchitecture == Architecture.Arm64
        ? "osx-arm64"
        : "osx-x64";

    throw new PlatformNotSupportedException("Unsupported OS platform.");
  }

  private static void ValidateExecutableFile(string filePath)
  {
    try
    {
      var fileInfo = new FileInfo(filePath);

      // Check if file is readable
      if (!fileInfo.Exists)
        throw new FileNotFoundException("Executable file not found.", filePath);

      // On Windows, ensure it's an .exe file
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        if (!filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
          throw new InvalidOperationException("Deno executable must be a .exe file on Windows.");
      }
      else
      {
        // On Unix-like systems, check if file has execute permissions
        // This is a basic check - in a production environment you might want more sophisticated validation
        if (fileInfo.Length == 0)
          throw new InvalidOperationException("Deno executable file is empty.");
      }
    }
    catch (UnauthorizedAccessException ex)
    {
      throw new UnauthorizedAccessException($"Cannot access Deno executable at {filePath}. Check file permissions.", ex);
    }
  }

  internal static string WriteTempConfig(DenoConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    var tempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");
    var configJson = config.ToJson();

    // Validate the JSON before writing
    try
    {
      JsonDocument.Parse(configJson);
    }
    catch (JsonException ex)
    {
      throw new InvalidOperationException("DenoConfig produced invalid JSON.", ex);
    }

    File.WriteAllText(tempPath, configJson);
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
      // Validate JSON before writing to temp file
      try
      {
        JsonDocument.Parse(configOrPath);
      }
      catch (JsonException ex)
      {
        throw new InvalidOperationException("Invalid JSON configuration provided.", ex);
      }

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
    catch (UnauthorizedAccessException ex)
    {
      throw new IOException($"Access denied when trying to delete temporary file: {resolvedPath}", ex);
    }
    catch (DirectoryNotFoundException)
    {
      // File or directory doesn't exist, which is fine for cleanup
      // Log this as debug information but don't throw
    }
    catch (Exception ex)
    {
      throw new IOException($"Failed to delete temporary file: {resolvedPath}", ex);
    }
  }
}
