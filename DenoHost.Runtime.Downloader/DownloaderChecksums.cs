using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DenoHost.Runtime.Downloader;

internal static class DownloaderChecksums
{
  public static string ExtractExpectedSha256(string checksumContent, string targetFileName)
  {
    var escapedFile = Regex.Escape(targetFileName);
    var byFile = Regex.Match(checksumContent, $@"(?im)^\s*([a-f0-9]{{64}})\s+\*?{escapedFile}\s*$");
    if (byFile.Success)
    {
      return byFile.Groups[1].Value.ToLowerInvariant();
    }

    var escapedBase = Regex.Escape(Path.GetFileName(targetFileName));
    var byBase = Regex.Match(checksumContent, $@"(?im)^\s*([a-f0-9]{{64}})\s+\*?{escapedBase}\s*$");
    if (byBase.Success)
    {
      return byBase.Groups[1].Value.ToLowerInvariant();
    }

    var psStyle = Regex.Match(checksumContent, "(?im)^\\s*Hash\\s*:\\s*([a-f0-9]{64})\\s*$");
    if (psStyle.Success)
    {
      return psStyle.Groups[1].Value.ToLowerInvariant();
    }

    var firstHash = Regex.Match(checksumContent, "(?im)^\\s*([a-f0-9]{64})\\b");
    if (firstHash.Success)
    {
      return firstHash.Groups[1].Value.ToLowerInvariant();
    }

    throw new InvalidOperationException($"Unable to extract SHA-256 for '{targetFileName}' from checksum source.");
  }

  public static string ComputeSha256(string filePath)
  {
    using var stream = File.OpenRead(filePath);
    var hash = SHA256.HashData(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }
}
