using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace deno;

public static class Deno
{
  private const string DenoExecutableName = "deno.EXE";

  private static string GetDenoPath()
  {
    var exePath = Path.Combine(AppContext.BaseDirectory, DenoExecutableName);
    if (!File.Exists(exePath))
      throw new FileNotFoundException("Deno executable not found!");

    return exePath;
  }

  public static async Task<int> Run(params string[] args)
  {
    var psi = new ProcessStartInfo
    {
      FileName = GetDenoPath(),
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    foreach (var arg in args)
    {
      psi.ArgumentList.Add(arg);
    }

    var process = new Process { StartInfo = psi };

    process.Start();
    string output = await process.StandardOutput.ReadToEndAsync();
    string error = await process.StandardError.ReadToEndAsync();
    process.WaitForExit();

    Console.WriteLine(output);
    if (!string.IsNullOrWhiteSpace(error))
      Console.Error.WriteLine(error);

    return process.ExitCode;
  }
}
