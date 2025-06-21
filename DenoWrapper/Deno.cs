using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DenoWrapper;

public static class Deno
{
  private const string DenoExecutableName = "deno.exe";

  public static async Task<T> Execute<T>(DenoExecuteOptions options)
  {
    if (options == null)
      throw new ArgumentNullException(nameof(options));

    var command = options.Command;
    var configOrPath = options.ConfigOrPath;
    var config = options.Config;
    var args = options.Args;

    if (config != null && !string.IsNullOrWhiteSpace(configOrPath))
      throw new ArgumentException("Either 'config' or 'configOrPath' should be provided, not both.");

    if (config != null)
      return await Execute<T>(command, config, args);
    else if (!string.IsNullOrWhiteSpace(configOrPath))
      return await Execute<T>(command, configOrPath, args);
    else
      return await Execute<T>(command, args);
  }

  public static async Task<T> Execute<T>(string command)
  {
    return await InternalExecute<T>(null, command, typeof(T));
  }

  public static async Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions)
  {
    return await InternalExecute<T>(baseOptions.WorkingDirectory, command, typeof(T));
  }

  public static async Task<T> Execute<T>(string command, params string[] args)
  {
    return await InternalExecute<T>(null, command, typeof(T), args);
  }

  public static async Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, params string[] args)
  {
    return await InternalExecute<T>(baseOptions.WorkingDirectory, command, typeof(T), args);
  }

  public static async Task<T> Execute<T>(params string[] args)
  {
    return await InternalExecute<T>(null, null, typeof(T), args);
  }

  public static async Task<T> Execute<T>(DenoExecuteBaseOptions baseOptions, params string[] args)
  {
    return await InternalExecute<T>(baseOptions.WorkingDirectory, null, typeof(T), args);
  }

  public static async Task<T> Execute<T>(string command, string configOrPath, params string[] args)
  {
    var configPath = EnsureConfigFile(configOrPath);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();
    var result = await InternalExecute<T>(null, command, typeof(T), allArgs);

    DeleteIfTempFile(configPath, configOrPath);

    return result;
  }

  public static async Task<T> Execute<T>(string command, DenoConfig config, params string[] args)
  {
    var configPath = WriteTempConfig(config);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();

    try
    {
      var result = await InternalExecute<T>(null, command, typeof(T), allArgs);

      return result;
    }
    finally
    {
      DeleteTempFile(configPath);
    }
  }

  private static async Task<T> InternalExecute<T>(string? workingDirectory, string? command, Type? resultType, params string[] args)
  {
    workingDirectory ??= Directory.GetCurrentDirectory();

    var fileName = GetDenoPath();
    var arguments = BuildArguments(args, command);

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

    if (resultType == null)
      throw new ArgumentNullException(nameof(resultType), "Result type cannot be null.");

    var deserializedResult = JsonSerializer.Deserialize(output, resultType);

    return deserializedResult != null
      ? (T)deserializedResult
      : throw new InvalidOperationException("Deserialization returned null.");
  }

  private static string BuildArguments(string[] args, string? command = null)
  {
    var argsStr = string.Join(" ", args);
    if (command == null)
    {
      return argsStr;
    }

    return $"{command} {argsStr}";
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
