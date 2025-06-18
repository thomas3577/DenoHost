using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DenoWrapper;

public class DenoExecuteOptions
{
  public string? WorkingDirectory { get; set; }
  public string Command { get; set; } = string.Empty;
  public bool ExpectResult { get; set; } = true;
  public string? configOrPath { get; set; }
  public DenoConfig? config { get; set; }
  public string[] Args { get; set; } = Array.Empty<string>();
}

public static class Deno
{
  private const string DenoExecutableName = "deno.exe";

  public static async Task Execute(DenoExecuteOptions options)
  {
    if (options == null)
      throw new ArgumentNullException(nameof(options));

    var workingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
    var command = options.Command;
    var expectResult = options.ExpectResult;
    var configOrPath = options.configOrPath;
    var config = options.config;
    var args = options.Args;

    if (config != null && !string.IsNullOrWhiteSpace(configOrPath))
      throw new ArgumentException("Either 'config' or 'configOrPath' should be provided, not both.");

    if (config != null)
      await Execute(workingDirectory, command, config, args);
    else if (!string.IsNullOrWhiteSpace(configOrPath))
      await Execute(workingDirectory, command, configOrPath, args);
    else
      await Execute(workingDirectory, command, args);
  }

  public static async Task<T?> Execute<T>(DenoExecuteOptions options)
  {
    if (options == null)
      throw new ArgumentNullException(nameof(options));

    var workingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
    var command = options.Command;
    var expectResult = options.ExpectResult;
    var configOrPath = options.configOrPath;
    var config = options.config;
    var args = options.Args;

    if (config != null && !string.IsNullOrWhiteSpace(configOrPath))
      throw new ArgumentException("Either 'config' or 'configOrPath' should be provided, not both.");

    if (config != null)
      return await Execute<T?>(workingDirectory, command, config, true, args);
    else if (!string.IsNullOrWhiteSpace(configOrPath))
      return await Execute<T>(workingDirectory, command, configOrPath, true, args);
    else
      return await Execute<T>(workingDirectory, command, true, args);
  }

  public static async Task Execute(string workingDirectory, string command, params string[] args)
  {
    await InternalExecute(workingDirectory, command, null, false, args);
  }

  public static async Task Execute(string workingDirectory, string command, string configOrPath, params string[] args)
  {
    // TODO(thu): Exception if --config lready exists in args
    var configPath = EnsureConfigFile(configOrPath);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();
    await InternalExecute(workingDirectory, command, null, false, allArgs);

    DeleteIfTempFile(configPath, configOrPath);
  }

  public static async Task Execute(string workingDirectory, string command, DenoConfig config, params string[] args)
  {
    // TODO(thu): Exception if --config lready exists in args
    var configPath = WriteTempConfig(config);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();

    try
    {
      await InternalExecute(workingDirectory, command, null, false, allArgs);
    }
    finally
    {
      DeleteTempFile(configPath);
    }
  }

  public static async Task<T?> Execute<T>(string workingDirectory, string command, bool expectResult = true, params string[] args)
  {
    var result = await InternalExecute(workingDirectory, command, typeof(T), expectResult, args);

    return result != null ? (T?)result : default;
  }

  public static async Task<T?> Execute<T>(string workingDirectory, string command, string configOrPath, bool expectResult = true, params string[] args)
  {
    var configPath = EnsureConfigFile(configOrPath);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();
    var result = await InternalExecute(workingDirectory, command, typeof(T), expectResult, allArgs);

    DeleteIfTempFile(configPath, configOrPath);

    return result != null ? (T?)result : default;
  }

  public static async Task<T?> Execute<T>(string workingDirectory, string command, DenoConfig config, bool expectResult = true, params string[] args)
  {
    var configPath = WriteTempConfig(config);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();

    try
    {
      var result = await InternalExecute(workingDirectory, command, typeof(T), expectResult, allArgs);

      return result != null ? (T?)result : default;
    }
    finally
    {
      DeleteTempFile(configPath);
    }
  }

  private static async Task<object?> InternalExecute(string workingDirectory, string command, Type? resultType, bool expectResult, params string[] args)
  {
    var fileName = GetDenoPath();
    var arguments = BuildArguments(command, args);

    var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = fileName,
        Arguments = arguments,
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
    return $"{command} {string.Join(" ", args)}";
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

  private static bool IsJsonLike(string input)
  {
    input = input.Trim();

    return (input.StartsWith('{') && input.EndsWith('}')) ||
           (input.StartsWith('[') && input.EndsWith(']'));
  }

  private static string EnsureConfigFile(string configOrPath)
  {
    if (IsJsonLike(configOrPath))
    {
      string tempPath = Path.Combine(Path.GetTempPath(), $"deno_config_{Guid.NewGuid():N}.json");
      File.WriteAllText(tempPath, configOrPath);
      return tempPath;
    }

    if (!File.Exists(configOrPath))
      throw new FileNotFoundException("The specified configuration path does not exist.", configOrPath);

    return configOrPath;
  }

  private static void DeleteIfTempFile(string resolvedPath, string original)
  {
    if (IsJsonLike(original))
    {
      DeleteTempFile(resolvedPath);
    }
  }

  private static void DeleteTempFile(string resolvedPath)
  {
    try
    {
      if (File.Exists(resolvedPath))
        File.Delete(resolvedPath);
    }
    catch { /* ignore */ }
  }
}
