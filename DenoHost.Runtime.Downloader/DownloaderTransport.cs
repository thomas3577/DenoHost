namespace DenoHost.Runtime.Downloader;

internal static class DownloaderTransport
{
  private static readonly HttpClient HttpClient = new()
  {
    Timeout = TimeSpan.FromMinutes(5),
  };

  public static async Task DownloadFileAsync(string url, string destinationPath)
  {
    Console.WriteLine($"Downloading {url}");

    try
    {
      using var response = await HttpClient.GetAsync(url);
      response.EnsureSuccessStatusCode();

      await using var target = File.Create(destinationPath);
      await response.Content.CopyToAsync(target);
    }
    catch (TaskCanceledException ex)
    {
      throw new TimeoutException($"Download timed out after {HttpClient.Timeout.TotalMinutes} minutes: {url}", ex);
    }
  }
}
