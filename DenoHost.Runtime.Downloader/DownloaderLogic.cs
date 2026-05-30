using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Runtime.Downloader;

internal static class DownloaderLogic
{
  // The metadata shape must stay compatible with the runtime validation code in DenoHost.Core.
  internal sealed record RuntimeMetadata(
    [property: JsonPropertyName("metadataVersion")] int MetadataVersion,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("rid")] string Rid,
    [property: JsonPropertyName("denoVersion")] string DenoVersion,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("createdAtUtc")] string CreatedAtUtc);

  public static RuntimeMetadata CreateRuntimeMetadata(string executablePath, string denoVersion, string runtimeRid, string archiveName, string sha256, string createdAtUtc, string baseUrl)
  {
    // Keep the source URL and field names aligned with the legacy scripts so packages remain byte-compatible.
    return new RuntimeMetadata(
      MetadataVersion: 1,
      FileName: Path.GetFileName(executablePath),
      Rid: runtimeRid,
      DenoVersion: denoVersion,
      Sha256: sha256,
      Source: $"{baseUrl}/v{denoVersion}/{archiveName}",
      CreatedAtUtc: createdAtUtc);
  }

  public static string SerializeRuntimeMetadata(RuntimeMetadata metadata)
  {
    return JsonSerializer.Serialize(metadata);
  }

  public static string SignMetadata(byte[] metadataBytes, string privateKeyPem)
  {
    // The signature is stored as raw base64, matching the existing PowerShell and shell helpers.
    using var ecdsa = ECDsa.Create();
    ecdsa.ImportFromPem(privateKeyPem);
    var signature = ecdsa.SignData(metadataBytes, HashAlgorithmName.SHA256);
    return Convert.ToBase64String(signature);
  }

  public static void FinalizeArtifacts(string executablePath, string denoVersion, string downloadFilename, string? runtimeRid, string baseUrl)
  {
    // Artifact generation is centralized so the version-matched and freshly-downloaded paths stay identical.
    var executableSha256 = DownloaderChecksums.ComputeSha256(executablePath);
    WriteExecutableChecksum(executablePath, executableSha256);
    var resolvedRid = ResolveRuntimeRid(executablePath, runtimeRid);
    var metadataPath = WriteRuntimeMetadata(executablePath, denoVersion, resolvedRid, downloadFilename, baseUrl, executableSha256);
    SignRuntimeMetadata(metadataPath);
  }

  public static string ExtractExpectedSha256(string checksumFilePath, string targetFileName)
  {
    if (!File.Exists(checksumFilePath))
    {
      throw new FileNotFoundException($"Checksum file not found: '{checksumFilePath}'");
    }

    var content = File.ReadAllText(checksumFilePath);
    return ExtractExpectedSha256FromContent(content, targetFileName);
  }

  internal static string ExtractExpectedSha256FromContent(string checksumContent, string targetFileName)
  {
    return DownloaderChecksums.ExtractExpectedSha256(checksumContent, targetFileName);
  }

  public static string ResolveRuntimeRid(string executablePath, string? providedRid)
  {
    if (!string.IsNullOrWhiteSpace(providedRid))
    {
      return providedRid;
    }

    // Infer the RID from the runtime package folder name so the metadata keeps the old default behavior.
    var executableDirectory = Path.GetDirectoryName(executablePath);
    if (!string.IsNullOrWhiteSpace(executableDirectory))
    {
      var runtimeDirectoryName = Path.GetFileName(executableDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
      if (!string.IsNullOrWhiteSpace(runtimeDirectoryName) && runtimeDirectoryName.StartsWith("DenoHost.Runtime.", StringComparison.Ordinal))
      {
        return runtimeDirectoryName["DenoHost.Runtime.".Length..];
      }
    }

    return "unknown";
  }

  private static void WriteExecutableChecksum(string executablePath, string executableSha256)
  {
    if (!File.Exists(executablePath))
    {
      throw new FileNotFoundException($"Executable not found at '{executablePath}'.");
    }

    var line = $"{executableSha256}  {Path.GetFileName(executablePath)}{Environment.NewLine}";
    File.WriteAllText($"{executablePath}.sha256sum", line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Wrote executable checksum to {executablePath}.sha256sum");
  }

  private static string WriteRuntimeMetadata(string executablePath, string denoVersion, string runtimeRid, string archiveName, string baseUrl, string executableSha256)
  {
    var metadata = CreateRuntimeMetadata(
      executablePath,
      denoVersion,
      runtimeRid,
      archiveName,
      executableSha256,
      DateTime.UtcNow.ToString("o"),
      baseUrl);

    var metadataPath = Path.Combine(Path.GetDirectoryName(executablePath)!, "deno.metadata.json");
    var json = SerializeRuntimeMetadata(metadata);
    File.WriteAllText(metadataPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Wrote runtime metadata to {metadataPath}");
    return metadataPath;
  }

  private static void SignRuntimeMetadata(string metadataPath)
  {
    var signaturePath = Path.Combine(Path.GetDirectoryName(metadataPath)!, "deno.metadata.sig");
    var privateKeyPem = Environment.GetEnvironmentVariable("DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM");
    if (string.IsNullOrWhiteSpace(privateKeyPem))
    {
      throw new InvalidOperationException("DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM is required because runtime metadata signatures are required by DenoHost.Core.");
    }
    }
    var metadataBytes = File.ReadAllBytes(metadataPath);
    var base64 = SignMetadata(metadataBytes, privateKeyPem);
    File.WriteAllText(signaturePath, base64, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    Console.WriteLine($"Wrote runtime metadata signature to {signaturePath}");
  }
}
