namespace DenoHost.Runtime.Downloader;

internal static class Program
{
  private static async Task<int> Main(string[] args)
  {
    try
    {
      var options = DownloaderOptions.Parse(args);
      if (options is null)
      {
        PrintUsage();
        return 2;
      }

      await DownloaderWorkflow.ExecuteAsync(options);
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Error: {ex.Message}");
      return 1;
    }
  }

  private static void PrintUsage()
  {
    Console.Error.WriteLine("Usage: DenoHost.Runtime.Downloader --executable-path <path> --download-filename <file> --deno-version <version> [--runtime-rid <rid>]");
  }
}
