using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DenoWrapper;

/// <summary>
/// Provides methods to execute Deno commands.
/// </summary>
public static class Deno
{
  private const string DenoExecutableName = "deno.exe";

  /// <summary>
  /// Executes a Deno command with the specified options.
  /// </summary>
  /// <param name="options">The execution options for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(DenoExecuteOptions options)
  {
    await Execute<object>(options);
  }

  /// <summary>
  /// Executes a Deno command with the specified options and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="options">The execution options for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
  /// <exception cref="ArgumentException">Thrown if both Config and ConfigOrPath are set.</exception>
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

    return await Execute<T>(command, args);
  }

  /// <summary>
  /// Executes a Deno command.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(string command)
  {
    await Execute<object>(command);
  }

  /// <summary>
  /// Executes a Deno command and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command)
  {
    return await InternalExecute<T>(null, command, typeof(T));
  }

  /// <summary>
  /// Executes a Deno command with base options.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(string command, DenoExecuteBaseOptions baseOptions)
  {
    await Execute<object>(command, baseOptions);
  }

  /// <summary>
  /// Executes a Deno command with base options and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions)
  {
    return await InternalExecute<T>(baseOptions.WorkingDirectory, command, typeof(T));
  }

  /// <summary>
  /// Executes a Deno command with additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(string command, params string[] args)
  {
    await Execute<object>(command, args);
  }

  /// <summary>
  /// Executes a Deno command with additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command, params string[] args)
  {
    return await InternalExecute<T>(null, command, typeof(T), args);
  }

  /// <summary>
  /// Executes a Deno command with base options and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(string command, DenoExecuteBaseOptions baseOptions, params string[] args)
  {
    await Execute<object>(command, baseOptions, args);
  }

  /// <summary>
  /// Executes a Deno command with base options and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, params string[] args)
  {
    return await InternalExecute<T>(baseOptions.WorkingDirectory, command, typeof(T), args);
  }

  /// <summary>
  /// Executes Deno with the specified arguments.
  /// </summary>
  /// <param name="args">Arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(params string[] args)
  {
    await Execute<object>(args);
  }

  /// <summary>
  /// Executes Deno with the specified arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="args">Arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(params string[] args)
  {
    return await InternalExecute<T>(null, null, typeof(T), args);
  }

  /// <summary>
  /// Executes Deno with base options and arguments.
  /// </summary>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(DenoExecuteBaseOptions baseOptions, params string[] args)
  {
    await Execute<object>(baseOptions, args);
  }

  /// <summary>
  /// Executes Deno with base options and arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(DenoExecuteBaseOptions baseOptions, params string[] args)
  {
    return await InternalExecute<T>(baseOptions.WorkingDirectory, null, typeof(T), args);
  }

  /// <summary>
  /// Executes a Deno command with a configuration file or path and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="configOrPath">Configuration as JSON or path to a configuration file.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(string command, string configOrPath, params string[] args)
  {
    await Execute<object>(command, configOrPath, args);
  }

  /// <summary>
  /// Executes a Deno command with a configuration file or path and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="configOrPath">Configuration as JSON or path to a configuration file.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command, string configOrPath, params string[] args)
  {
    var configPath = EnsureConfigFile(configOrPath);
    var allArgs = args.Prepend("--config").Prepend(configPath).ToArray();
    var result = await InternalExecute<T>(null, command, typeof(T), allArgs);

    DeleteIfTempFile(configPath, configOrPath);

    return result;
  }

  /// <summary>
  /// Executes a Deno command with a configuration object and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(string command, DenoConfig config, params string[] args)
  {
    await Execute<object>(command, config, args);
  }

  /// <summary>
  /// Executes a Deno command with a configuration object and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
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
