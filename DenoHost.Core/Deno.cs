using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DenoHost.Core;

/// <summary>
/// Provides methods to execute Deno commands.
/// </summary>
public static class Deno
{
  /// <summary>
  /// Optional logger for Deno operations. Set this to enable logging.
  /// </summary>
  public static ILogger? Logger { get; set; }

  /// <summary>
  /// Executes a Deno command with the specified options.
  /// </summary>
  /// <param name="options">The execution options for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Execute(DenoExecuteOptions options)
  {
    await Execute<string>(options);
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
    ArgumentNullException.ThrowIfNull(options);

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
  /// <code>
  /// var command = "run --allow-read script.ts";
  /// await Deno.Execute(command);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command) => Execute<string>(command);

  /// <summary>
  /// Executes a Deno command and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <code>
  /// var command = "run --allow-read script.ts";
  /// var result = await Deno.Execute<MyResult>(command);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string command)
    => DenoExecutor.Execute<T>(null, command, typeof(T), null, null, null, default);

  /// <summary>
  /// Executes a Deno command with cancellation support.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, CancellationToken cancellationToken)
    => Execute<string>(command, cancellationToken);

  /// <summary>
  /// Executes a Deno command with cancellation support and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string command, CancellationToken cancellationToken)
    => DenoExecutor.Execute<T>(null, command, typeof(T), null, null, null, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <code>
  /// var command = "run --allow-read script.ts";
  /// var options = new DenoExecuteBaseOptions { WorkingDirectory = "/path/to/dir" };
  /// await Deno.Execute(command, options);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions)
    => Execute<string>(command, baseOptions);

  /// <summary>
  /// Executes a Deno command with base options and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <code>
  /// var command = "run --allow-read script.ts";
  /// var options = new DenoExecuteBaseOptions { WorkingDirectory = "/path/to/dir" };
  /// var result = await Deno.Execute<MyResult>(command, options);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions)
    => DenoExecutor.Execute<T>(baseOptions.WorkingDirectory, command, typeof(T), baseOptions?.JsonSerializerOptions, baseOptions?.Logger, null, default);

  /// <summary>
  /// Executes a Deno command with base options and cancellation support.
  /// </summary>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, CancellationToken cancellationToken)
    => Execute<string>(command, baseOptions, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options, cancellation support and returns result.
  /// </summary>
  public static Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, CancellationToken cancellationToken)
    => DenoExecutor.Execute<T>(baseOptions.WorkingDirectory, command, typeof(T), baseOptions?.JsonSerializerOptions, baseOptions?.Logger, null, cancellationToken);

  /// <summary>
  /// Executes a Deno command with additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <code>
  /// var command = "run";
  /// var args = new[] { "--allow-read", "script.ts" };
  /// await Deno.Execute(command, args);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, string[] args)
    => Execute<string>(command, args);

  /// <summary>
  /// Executes a Deno command with additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <code>
  /// var command = "run";
  /// var args = new[] { "--allow-read", "script.ts" };
  /// var result = await Deno.Execute<MyResult>(command, args);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string command, string[] args)
    => DenoExecutor.Execute<T>(null, command, typeof(T), null, null, args, default);

  /// <summary>
  /// Executes a Deno command with additional arguments and cancellation support.
  /// </summary>
  public static Task Execute(string command, string[] args, CancellationToken cancellationToken)
    => Execute<string>(command, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with additional arguments, cancellation support and returns the result.
  /// </summary>
  public static Task<T> Execute<T>(string command, string[] args, CancellationToken cancellationToken)
    => DenoExecutor.Execute<T>(null, command, typeof(T), null, null, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <code>
  /// var command = "run";
  /// var options = new DenoExecuteBaseOptions { WorkingDirectory = "/path/to/dir" };
  /// var args = new[] { "--allow-read", "script.ts" };
  /// await Deno.Execute(command, options, args);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, string[] args)
    => Execute<string>(command, baseOptions, args);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <code>
  /// var command = "run";
  /// var options = new DenoExecuteBaseOptions { WorkingDirectory = "/path/to/dir" };
  /// var args = new[] { "--allow-read", "script.ts" };
  /// var result = await Deno.Execute(command, options, args);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, string[] args)
    => DenoExecutor.Execute<T>(baseOptions.WorkingDirectory, command, typeof(T), baseOptions?.JsonSerializerOptions, baseOptions?.Logger, args, default);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments with cancellation support.
  /// </summary>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken)
    => Execute<string>(command, baseOptions, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments plus cancellation.
  /// </summary>
  public static Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken)
    => DenoExecutor.Execute<T>(baseOptions.WorkingDirectory, command, typeof(T), baseOptions?.JsonSerializerOptions, baseOptions?.Logger, args, cancellationToken);

  /// <summary>
  /// Executes Deno with the specified arguments.
  /// </summary>
  /// <param name="args">Arguments for Deno.</param>
  /// <code>
  /// var args = new ["run", "--allow-read", "script.ts"];
  /// await Deno.Execute(args);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string[] args)
    => Execute<string>(args);

  /// <summary>
  /// Executes Deno with the specified arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="args">Arguments for Deno.</param>
  /// <code>
  /// var args = new ["run", "--allow-read", "script.ts"];
  /// var result = await Deno.Execute<MyResult>(args);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string[] args)
    => DenoExecutor.Execute<T>(null, null, typeof(T), null, null, args, default);

  /// <summary>
  /// Executes Deno with arguments and cancellation support.
  /// </summary>
  public static Task Execute(string[] args, CancellationToken cancellationToken)
    => Execute<string>(args, cancellationToken);

  /// <summary>
  /// Executes Deno with arguments, cancellation support and returns result.
  /// </summary>
  public static Task<T> Execute<T>(string[] args, CancellationToken cancellationToken)
    => DenoExecutor.Execute<T>(null, null, typeof(T), null, null, args, cancellationToken);

  /// <summary>
  /// Executes Deno with base options and arguments.
  /// </summary>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Arguments for Deno.</param>
  /// <code>
  /// var options = new DenoExecuteBaseOptions { WorkingDirectory = "/path/to/dir" };
  /// var args = new ["run", "--allow-read", "script.ts"];
  /// await Deno.Execute(options, args);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(DenoExecuteBaseOptions baseOptions, string[] args)
    => Execute<string>(baseOptions, args);

  /// <summary>
  /// Executes Deno with base options and arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Arguments for Deno.</param>
  /// <code>
  /// var options = new DenoExecuteBaseOptions { WorkingDirectory = "/path/to/dir" };
  /// var args = new ["run", "--allow-read", "script.ts"];
  /// var result = await Deno.Execute<MyResult>(options, args);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(DenoExecuteBaseOptions baseOptions, string[] args)
    => baseOptions == null
      ? throw new ArgumentNullException(nameof(baseOptions))
      : DenoExecutor.Execute<T>(baseOptions.WorkingDirectory, null, typeof(T), null, null, args, default);

  /// <summary>
  /// Executes Deno with base options, arguments and cancellation support.
  /// </summary>
  public static Task Execute(DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken)
    => Execute<string>(baseOptions, args, cancellationToken);

  /// <summary>
  /// Executes Deno with base options, arguments, cancellation support and returns result.
  /// </summary>
  public static Task<T> Execute<T>(DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken)
    => baseOptions == null
      ? throw new ArgumentNullException(nameof(baseOptions))
      : DenoExecutor.Execute<T>(baseOptions.WorkingDirectory, null, typeof(T), null, null, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with a configuration file or path and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="configOrPath">Configuration as JSON or path to a configuration file.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <code>
  /// // Var 1:
  /// var command = "run";
  /// var configPath = "./deno.json";
  /// var args = new[] { "--allow-read", "script.ts" };
  /// await Deno.Execute(command, configPath, args);
  ///
  /// // Var 2:
  /// var command = "run";
  /// var configPath = "{ \"imports\": { \"@std/fs\": \"jsr:@std/fs@^1.0.18\" } }"; // JSON string
  /// var args = new[] { "--allow-read", "script.ts" };
  /// await Deno.Execute(command, configPath, args);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, string configOrPath, string[] args)
    => Execute<string>(command, configOrPath, args);

  /// <summary>
  /// Executes a Deno command with a configuration file or path and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="configOrPath">Configuration as JSON or path to a configuration file.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <code>
  /// // Var 1:
  /// var command = "run";
  /// var configPath = "./deno.json";
  /// var args = new[] { "--allow-read", "script.ts" };
  /// var result = await Deno.Execute<MyResult>(command, configPath, args);
  ///
  /// // Var 2:
  /// var command = "run";
  /// var configPath = "{ \"imports\": { \"@std/fs\": \"jsr:@std/fs@^1.0.18\" } }"; // JSON string
  /// var args = new[] { "--allow-read", "script.ts" };
  /// var result = await Deno.Execute<MyResult>(command, configPath, args);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command, string configOrPath, string[] args)
  {
    var configPath = Helper.EnsureConfigFile(configOrPath);
    var allArgs = Helper.AppendConfigArgument(args, configPath);
    var result = await DenoExecutor.Execute<T>(null, command, typeof(T), null, null, allArgs, default);

    Helper.DeleteIfTempFile(configPath, configOrPath);

    return result;
  }

  /// <summary>
  /// Executes a Deno command with a configuration file or path, additional arguments and cancellation support.
  /// </summary>
  public static async Task<T> Execute<T>(string command, string configOrPath, string[] args, CancellationToken cancellationToken)
  {
    var configPath = Helper.EnsureConfigFile(configOrPath);
    var allArgs = Helper.AppendConfigArgument(args, configPath);
    try
    {
      return await DenoExecutor.Execute<T>(null, command, typeof(T), null, null, allArgs, cancellationToken);
    }
    finally
    {
      Helper.DeleteIfTempFile(configPath, configOrPath);
    }
  }

  /// <summary>
  /// Executes a Deno command with a configuration object and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, DenoConfig config, string[] args)
    => Execute<string>(command, config, args);

  /// <summary>
  /// Executes a Deno command with a configuration object and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static async Task<T> Execute<T>(string command, DenoConfig config, string[] args)
  {
    var configPath = Helper.WriteTempConfig(config);
    var allArgs = Helper.AppendConfigArgument(args, configPath);

    try
    {
      var result = await DenoExecutor.Execute<T>(null, command, typeof(T), null, null, allArgs, default);

      return result;
    }
    finally
    {
      Helper.DeleteTempFile(configPath);
    }
  }

  /// <summary>
  /// Executes a Deno command with a configuration object, additional arguments and cancellation support.
  /// </summary>
  public static async Task<T> Execute<T>(string command, DenoConfig config, string[] args, CancellationToken cancellationToken)
  {
    var configPath = Helper.WriteTempConfig(config);
    var allArgs = Helper.AppendConfigArgument(args, configPath);
    try
    {
      return await DenoExecutor.Execute<T>(null, command, typeof(T), null, null, allArgs, cancellationToken);
    }
    finally
    {
      Helper.DeleteTempFile(configPath);
    }
  }
}
