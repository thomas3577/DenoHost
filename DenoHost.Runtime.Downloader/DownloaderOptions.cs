namespace DenoHost.Runtime.Downloader;

internal sealed record DownloaderOptions(string ExecutablePath, string DownloadFilename, string DenoVersion, string? RuntimeRid)
{
  public static DownloaderOptions? Parse(string[] args)
  {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < args.Length; index++)
    {
      var key = args[index];
      if (!key.StartsWith("--", StringComparison.Ordinal))
      {
        return null;
      }

      if (index + 1 >= args.Length)
      {
        return null;
      }

      map[key] = args[++index];
    }

    if (!map.TryGetValue("--executable-path", out var executablePath) || string.IsNullOrWhiteSpace(executablePath))
    {
      return null;
    }

    if (!map.TryGetValue("--download-filename", out var downloadFilename) || string.IsNullOrWhiteSpace(downloadFilename))
    {
      return null;
    }

    if (!map.TryGetValue("--deno-version", out var denoVersion) || string.IsNullOrWhiteSpace(denoVersion))
    {
      return null;
    }

    map.TryGetValue("--runtime-rid", out var runtimeRid);
    return new DownloaderOptions(executablePath, downloadFilename, denoVersion, runtimeRid);
  }
}
