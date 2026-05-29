using System.IO.Compression;

namespace DenoHost.Runtime.Downloader;

internal static class DownloaderWorkflow
{
  private const string BaseUrl = "https://github.com/denoland/deno/releases/download";
  internal const string BaseUrlForTests = BaseUrl;

  public static async Task ExecuteAsync(DownloaderOptions options)
  {
    // Reuse an existing binary when it already matches the requested Deno version.
    // This keeps normal builds fast and preserves the old script behavior.
    if (File.Exists(options.ExecutablePath) &&
        DownloaderVersion.TryReadCurrentVersion(options.ExecutablePath, out var currentVersion) &&
        string.Equals(currentVersion, options.DenoVersion, StringComparison.Ordinal))
    {
      Console.WriteLine($"Deno binary found at {options.ExecutablePath}, version matches {options.DenoVersion}. No download needed.");
      DownloaderLogic.FinalizeArtifacts(options.ExecutablePath, options.DenoVersion, options.DownloadFilename, options.RuntimeRid, BaseUrl);
      Console.WriteLine($"Deno setup complete at {options.ExecutablePath}");
      return;
    }

    var downloadUrl = $"{BaseUrl}/v{options.DenoVersion}/{options.DownloadFilename}";
    var checksumUrl = $"{downloadUrl}.sha256sum";
    var extractDirectory = Path.GetDirectoryName(options.ExecutablePath) ?? throw new InvalidOperationException("Executable directory could not be resolved.");
    Directory.CreateDirectory(extractDirectory);

    var tempZip = Path.Combine(Path.GetTempPath(), $"deno-{Guid.NewGuid():N}.zip");
    var tempChecksum = $"{tempZip}.sha256sum";

    try
    {
      // Keep downloads separate so a failed checksum never touches the installed binary.
      await DownloaderTransport.DownloadFileAsync(downloadUrl, tempZip);
      await DownloaderTransport.DownloadFileAsync(checksumUrl, tempChecksum);

      var expectedHash = DownloaderLogic.ExtractExpectedSha256(tempChecksum, options.DownloadFilename);
      var actualHash = DownloaderChecksums.ComputeSha256(tempZip);
      if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
      {
        throw new InvalidOperationException($"Checksum verification failed for '{options.DownloadFilename}'. Expected: {expectedHash} Actual: {actualHash}");
      }

      Console.WriteLine($"Checksum verification passed for {options.DownloadFilename}");

      Console.WriteLine($"Extracting to {extractDirectory}");
      ZipFile.ExtractToDirectory(tempZip, extractDirectory, overwriteFiles: true);

      // Archives sometimes contain extra files next to the binary; rename only the real executable.
      var extractedBinary = DownloaderFiles.FindExtractedBinary(extractDirectory);
      if (extractedBinary is not null && !string.Equals(extractedBinary, options.ExecutablePath, StringComparison.OrdinalIgnoreCase))
      {
        DownloaderFiles.ReplaceExtractedBinary(extractedBinary, options.ExecutablePath);
      }

      if (!File.Exists(options.ExecutablePath))
      {
        throw new FileNotFoundException($"Could not locate extracted Deno executable at '{options.ExecutablePath}'.");
      }

      DownloaderFiles.TryEnsureExecutablePermissions(options.ExecutablePath);
      DownloaderLogic.FinalizeArtifacts(options.ExecutablePath, options.DenoVersion, options.DownloadFilename, options.RuntimeRid, BaseUrl);
    }
    finally
    {
      DownloaderFiles.TryDelete(tempZip);
      DownloaderFiles.TryDelete(tempChecksum);
    }

    Console.WriteLine($"Deno setup complete at {options.ExecutablePath}");
  }
}
