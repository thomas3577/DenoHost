namespace DenoHost.Runtime.Downloader;

internal static class DownloaderTransport
{
  public static async Task DownloadFileAsync(string url, string destinationPath)
  {
    Console.WriteLine($"Downloading {url}");

    using var client = new HttpClient();
    using var response = await client.GetAsync(url);
    response.EnsureSuccessStatusCode();

    await using var target = File.Create(destinationPath);
    await response.Content.CopyToAsync(target);
  }
}
