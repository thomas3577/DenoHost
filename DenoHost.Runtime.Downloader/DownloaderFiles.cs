namespace DenoHost.Runtime.Downloader;

internal static class DownloaderFiles
{
  public static string? FindExtractedBinary(string directory)
  {
    return Directory
      .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
      .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "deno", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(Path.GetFileName(path), "deno.exe", StringComparison.OrdinalIgnoreCase));
  }

  public static void ReplaceExtractedBinary(string extractedBinary, string targetExecutable)
  {
    if (File.Exists(targetExecutable))
    {
      File.Delete(targetExecutable);
    }

    File.Move(extractedBinary, targetExecutable);
  }

  public static void TryEnsureExecutablePermissions(string executablePath)
  {
    if (OperatingSystem.IsWindows())
    {
      return;
    }

    try
    {
      var mode = File.GetUnixFileMode(executablePath);
      mode |= UnixFileMode.UserExecute;
      mode |= UnixFileMode.GroupExecute;
      mode |= UnixFileMode.OtherExecute;
      File.SetUnixFileMode(executablePath, mode);
    }
    catch
    {
      // Keep behavior lenient like shell scripts.
    }
  }

  public static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Warning: Could not remove temporary file '{path}': {ex.Message}");
    }
  }
}
