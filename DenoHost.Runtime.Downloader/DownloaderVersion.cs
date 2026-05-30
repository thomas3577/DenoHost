using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DenoHost.Runtime.Downloader;

internal static class DownloaderVersion
{
  public static bool TryReadCurrentVersion(string executablePath, out string version)
  {
    version = string.Empty;

    try
    {
      var fileVersion = FileVersionInfo.GetVersionInfo(executablePath).ProductVersion;
      if (!string.IsNullOrWhiteSpace(fileVersion))
      {
        var match = Regex.Match(fileVersion, "(\\d+\\.\\d+\\.\\d+)");
        if (match.Success)
        {
          version = match.Groups[1].Value;
          return true;
        }
      }
    }
    catch
    {
      // Ignore and fallback to process-based version detection.
    }

    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        Arguments = "--version",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
      };

      using var process = Process.Start(startInfo);
      if (process is null)
      {
        return false;
      }

      var stdoutTask = process.StandardOutput.ReadToEndAsync();
      var stderrTask = process.StandardError.ReadToEndAsync();
      process.WaitForExit();
      var output = stdoutTask.GetAwaiter().GetResult();
      _ = stderrTask.GetAwaiter().GetResult();
      if (process.ExitCode != 0)
      {
        return false;
      }

      var match = Regex.Match(output, "deno\\s+(\\d+\\.\\d+\\.\\d+)", RegexOptions.IgnoreCase);
      if (match.Success)
      {
        version = match.Groups[1].Value;
        return true;
      }
    }
    catch
    {
      // Ignore failures and force re-download.
    }

    return false;
  }
}
