using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace deno;

public class Deno
{
    public static string GetDenoPath()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "deno.exe");
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Deno.exe not found!");

        return exePath;
    }

    public static async Task<int> Run(string scriptPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetDenoPath(),
                Arguments = $"run {scriptPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

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
