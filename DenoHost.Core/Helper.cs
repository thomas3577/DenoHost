using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DenoHost.Core.Config;

namespace DenoHost.Core;

internal static class Helper
{
  internal const string ChecksumBypassEnvVarName = "DENOHOST_ALLOW_CHECKSUM_BYPASS";
  internal const string MetadataPublicKeyEnvVarName = "DENOHOST_METADATA_SIGNING_PUBLIC_KEY_PEM";
  private const string BuiltInMetadataSigningPublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQBRnj6ILWatyOt1WieU/cWoyLQwf6n
oEw6eGbECClUd4f2XuBccjDSdgj2GPiQXOGKJ1I+Wh/sb0EC1SM1B2hjPBEBvozG
q6+o54AUQ16b2iPzt3g7TumcfZB0qxr5XmhcWMnPtPvmc5fXgGtInEjlKAl3dr20
XxGSOTFMItGfBKP0gKM=
-----END PUBLIC KEY-----
""";
  private const string MetadataFileName = "deno.metadata.json";
  private const string MetadataSignatureFileName = "deno.metadata.sig";

  internal static string BuildArguments(string[]? args, string? command = null)
  {
    var argsArray = BuildArgumentsArray(args, command);
    return string.Join(' ', argsArray);
  }

  internal static string[] SplitCommandLine(string commandLine)
  {
    if (string.IsNullOrWhiteSpace(commandLine))
      return [];

    System.Collections.Generic.List<string> parts = [];
    var current = new System.Text.StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < commandLine.Length; i++)
    {
      char c = commandLine[i];

      if (inQuotes)
      {
        if (c == '\\' && i + 1 < commandLine.Length)
        {
          char next = commandLine[i + 1];
          if (next == '"' || next == '\\')
          {
            current.Append(next);
            i++;
            continue;
          }
        }

        if (c == '"')
        {
          inQuotes = false;
          continue;
        }

        current.Append(c);
        continue;
      }

      if (char.IsWhiteSpace(c))
      {
        if (current.Length > 0)
        {
          parts.Add(current.ToString());
          current.Clear();
        }
        continue;
      }

      if (c == '"')
      {
        inQuotes = true;
        continue;
      }

      current.Append(c);
    }

    if (inQuotes)
      throw new ArgumentException("Command contains an unterminated quote.", nameof(commandLine));

    if (current.Length > 0)
      parts.Add(current.ToString());

    return [.. parts];
  }

  internal static string[] BuildArgumentsArray(string[]? args, string? command = null)
  {
    if (command == null)
      return args ?? [];

    var commandParts = SplitCommandLine(command);
    if (commandParts.Length == 0)
      return args ?? [];

    var result = new string[commandParts.Length + (args?.Length ?? 0)];
    Array.Copy(commandParts, 0, result, 0, commandParts.Length);

    if (args != null)
      Array.Copy(args, 0, result, commandParts.Length, args.Length);

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
    ValidateExecutableIntegrity(normalizedPath);

    return normalizedPath;
  }

  internal static bool IsChecksumBypassEnabled()
  {
    var value = Environment.GetEnvironmentVariable(ChecksumBypassEnvVarName);
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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

  private static void ValidateExecutableIntegrity(string executablePath)
  {
    if (IsChecksumBypassEnabled())
      return;

    var executableDirectory = Path.GetDirectoryName(executablePath)
      ?? throw new InvalidOperationException($"Unable to resolve executable directory for '{executablePath}'.");
    var metadataPath = Path.Combine(executableDirectory, MetadataFileName);
    var metadataSignaturePath = Path.Combine(executableDirectory, MetadataSignatureFileName);
    var hasMetadataFile = File.Exists(metadataPath);
    var hasMetadataSignature = File.Exists(metadataSignaturePath);

    if (hasMetadataFile ^ hasMetadataSignature)
    {
      throw new SecurityException(
        $"Signed metadata artifacts are incomplete for '{executablePath}'. Expected both '{MetadataFileName}' and '{MetadataSignatureFileName}'.");
    }

    if (hasMetadataFile && hasMetadataSignature)
    {
      ValidateSignedExecutableMetadata(executablePath, metadataPath, metadataSignaturePath);
      return;
    }

    throw new SecurityException(
      $"Signed metadata is required but missing for '{executablePath}'. Expected both '{MetadataFileName}' and '{MetadataSignatureFileName}'.");
  }

  private static void ValidateSignedExecutableMetadata(string executablePath, string metadataPath, string metadataSignaturePath)
  {
    var metadataBytes = File.ReadAllBytes(metadataPath);
    var signatureText = File.ReadAllText(metadataSignaturePath).Trim();
    if (string.IsNullOrWhiteSpace(signatureText))
      throw new SecurityException($"Metadata signature file '{metadataSignaturePath}' is empty.");

    byte[] signatureBytes;
    try
    {
      signatureBytes = Convert.FromBase64String(signatureText);
    }
    catch (FormatException ex)
    {
      throw new SecurityException($"Metadata signature file '{metadataSignaturePath}' does not contain valid Base64.", ex);
    }

    var publicKeyPem = GetMetadataPublicKeyPem();
    if (string.IsNullOrWhiteSpace(publicKeyPem))
    {
      throw new SecurityException(
        $"Signed metadata was found for '{executablePath}', but no public key is configured. " +
        $"Set {MetadataPublicKeyEnvVarName} or provide a built-in public key.");
    }

    using var ecdsa = ECDsa.Create();
    ecdsa.ImportFromPem(publicKeyPem);

    var signatureValid = ecdsa.VerifyData(metadataBytes, signatureBytes, HashAlgorithmName.SHA256);
    if (!signatureValid)
      throw new SecurityException($"Metadata signature validation failed for '{metadataPath}'.");

    RuntimeMetadata? metadata;
    try
    {
      metadata = JsonSerializer.Deserialize<RuntimeMetadata>(metadataBytes);
    }
    catch (JsonException ex)
    {
      throw new SecurityException($"Metadata file '{metadataPath}' is invalid JSON.", ex);
    }

    if (metadata is null)
      throw new SecurityException($"Metadata file '{metadataPath}' is empty or invalid.");

    if (!string.Equals(metadata.FileName, Path.GetFileName(executablePath), StringComparison.OrdinalIgnoreCase))
      throw new SecurityException($"Metadata file name '{metadata.FileName}' does not match executable '{Path.GetFileName(executablePath)}'.");

    if (!string.Equals(metadata.Rid, GetRuntimeId(), StringComparison.OrdinalIgnoreCase))
      throw new SecurityException($"Metadata RID '{metadata.Rid}' does not match current runtime '{GetRuntimeId()}'.");

    var actualHash = ComputeSha256(executablePath);
    if (!string.Equals(metadata.Sha256, actualHash, StringComparison.OrdinalIgnoreCase))
    {
      throw new SecurityException(
        $"Metadata hash validation failed for Deno executable '{executablePath}'. Expected '{metadata.Sha256}', got '{actualHash}'.");
    }
  }

  private static string? GetMetadataPublicKeyPem()
  {
    var envValue = Environment.GetEnvironmentVariable(MetadataPublicKeyEnvVarName);
    if (!string.IsNullOrWhiteSpace(envValue))
      return envValue;

    return string.IsNullOrWhiteSpace(BuiltInMetadataSigningPublicKeyPem)
      ? null
      : BuiltInMetadataSigningPublicKeyPem;
  }

  private static string ReadExpectedChecksum(string checksumPath, string executableName)
  {
    var lines = File.ReadAllLines(checksumPath);

    foreach (var rawLine in lines)
    {
      var line = rawLine.Trim();
      if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        continue;

      var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length == 0)
        continue;

      if (!IsSha256(parts[0]))
        continue;

      if (parts.Length == 1)
        return parts[0].ToLowerInvariant();

      var candidateName = parts[1].TrimStart('*');
      if (string.Equals(candidateName, executableName, StringComparison.OrdinalIgnoreCase))
        return parts[0].ToLowerInvariant();
    }

    throw new InvalidOperationException(
      $"Unable to parse expected SHA-256 checksum for '{executableName}' from '{checksumPath}'.");
  }

  private static bool IsSha256(string value)
  {
    if (value.Length != 64)
      return false;

    foreach (var ch in value)
    {
      if (!Uri.IsHexDigit(ch))
        return false;
    }

    return true;
  }

  private static string ComputeSha256(string filePath)
  {
    using var stream = File.OpenRead(filePath);
    var hash = SHA256.HashData(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
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

  internal static bool IsTempDenoConfigPath(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      return false;

    try
    {
      var fullPath = Path.GetFullPath(path);
      var tempRoot = Path.GetFullPath(Path.GetTempPath());

      if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
        return false;

      var fileName = Path.GetFileName(fullPath);
      if (!fileName.StartsWith("deno_config_", StringComparison.OrdinalIgnoreCase))
        return false;

      return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
             fileName.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }

  internal static string EnsureConfigFile(string configOrPath)
  {
    if (string.IsNullOrWhiteSpace(configOrPath))
      throw new ArgumentException("Configuration cannot be null or empty.", nameof(configOrPath));

    var trimmed = configOrPath.Trim();

    // Prefer treating as JSON when it clearly looks like JSON.
    if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '['))
    {
      try
      {
        ValidateJson(trimmed);
      }
      catch (JsonException ex)
      {
        throw new InvalidOperationException("Invalid JSON configuration provided.", ex);
      }

      var tempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");
      File.WriteAllText(tempPath, trimmed);
      return tempPath;
    }

    // If it points to an existing file, treat as a path.
    if (File.Exists(trimmed))
      return trimmed;

    // If it looks like a JSON path but doesn't exist, fail as path.
    if (IsJsonPathLike(trimmed))
      throw new FileNotFoundException("The specified configuration path does not exist.", trimmed);

    // Fall back: try to parse as JSON.
    try
    {
      ValidateJson(trimmed);
    }
    catch (JsonException ex)
    {
      throw new InvalidOperationException("Invalid JSON configuration provided.", ex);
    }

    var fallbackTempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");
    File.WriteAllText(fallbackTempPath, trimmed);
    return fallbackTempPath;
  }

  private static void ValidateJson(string json)
  {
    using var _ = JsonDocument.Parse(json);
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

  private sealed class RuntimeMetadata
  {
    [JsonPropertyName("metadataVersion")]
    public int MetadataVersion { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("rid")]
    public string Rid { get; set; } = string.Empty;

    [JsonPropertyName("denoVersion")]
    public string DenoVersion { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public string CreatedAtUtc { get; set; } = string.Empty;
  }
}
