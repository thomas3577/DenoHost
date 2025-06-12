#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace deno;

public static class DenoWrapper
{
  private const string DenoExecutableName = "deno.EXE";

  public static async Task Execute(string command, params string[] args)
  {
    await InternalExecute(command, null, false, args);
  }

  public static async Task<T?> Execute<T>(string command, bool expectResult = true, params string[] args)
  {
    var result = await InternalExecute(command, typeof(T), expectResult, args);
    return result != null ? (T?)result : default;
  }

  private static async Task<object?> InternalExecute(string command, Type? resultType, bool expectResult, params string[] args)
  {
    var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = GetDenoPath(),
        Arguments = BuildArguments(command, args),
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

    if (!string.IsNullOrWhiteSpace(error))
      throw new Exception($"Deno Error: {error}");

    if (!expectResult || resultType == null || string.IsNullOrWhiteSpace(output))
      return null;

    return JsonSerializer.Deserialize(output, resultType);
  }

  private static string BuildArguments(string command, string[] args)
  {
    var quotedArgs = args.Select(arg => $"\"{arg}\"");
    return $"{command} {string.Join(" ", quotedArgs)}";
  }

  private static string GetDenoPath()
  {
    var exePath = Path.Combine(AppContext.BaseDirectory, DenoExecutableName);
    if (!File.Exists(exePath))
      throw new FileNotFoundException("Deno executable not found!");

    return exePath;
  }
}
