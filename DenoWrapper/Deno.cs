using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DenoWrapper;

public static class Deno
{
  private const string DenoExecutableName = "deno.exe";

  public static async Task Execute(string command, params string[] args)
  {
    await InternalExecute(command, null, false, args);
  }

  public static async Task Execute(string command, DenoConfig config, params string[] args)
  {
    var configPath = WriteTempConfig(config);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();

    await InternalExecute(command, null, false, allArgs);

    try
    {
      await InternalExecute(command, null, false, allArgs);
    }
    finally
    {
      if (File.Exists(configPath))
        File.Delete(configPath);
    }
  }

  public static async Task<T?> Execute<T>(string command, bool expectResult = true, params string[] args)
  {
    var result = await InternalExecute(command, typeof(T), expectResult, args);

    return result != null ? (T?)result : default;
  }

  public static async Task<T?> Execute<T>(string command, DenoConfig config, bool expectResult = true, params string[] args)
  {
    var configPath = WriteTempConfig(config);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();

    try
    {
      var result = await InternalExecute(command, typeof(T), expectResult, allArgs);

      return result != null ? (T?)result : default;
    }
    finally
    {
      if (File.Exists(configPath))
        File.Delete(configPath);
    }
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

  private static string WriteTempConfig(DenoConfig config)
  {
    string tempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");

    File.WriteAllText(tempPath, config.ToJson());

    return tempPath;
  }
}
