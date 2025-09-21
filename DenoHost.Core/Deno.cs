using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DenoHost.Core.Config;

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
  /// Executes a Deno command with composite execution options.
  /// </summary>
  /// <param name="options">The execution options (command, args, config etc.).</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(DenoExecuteOptions options, CancellationToken cancellationToken = default)
    => Execute<string>(options, cancellationToken);

  /// <summary>
  /// Executes a Deno command with the specified options and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="options">The execution options for Deno.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
  /// <exception cref="ArgumentException">Thrown if both Config and ConfigOrPath are set.</exception>
  public static Task<T> Execute<T>(DenoExecuteOptions options, CancellationToken cancellationToken = default)
    => ExecuteCore<T>(
      options?.Command ?? throw new ArgumentNullException(nameof(options)),
      options.Args,
      options,
      options.Config,
      options.ConfigOrPath,
      cancellationToken);

  /// <summary>
  /// Executes a Deno command.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <code>
  /// var command = "run --allow-read script.ts";
  /// await Deno.Execute(command);
  /// </code>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, CancellationToken cancellationToken = default) => Execute<string>(command, cancellationToken);

  /// <summary>
  /// Executes a Deno command and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  public static Task<T> Execute<T>(string command, CancellationToken cancellationToken = default)
    => ExecuteCore<T>(command, null, null, null, null, cancellationToken);

  /// <summary>
  /// Executes a Deno command with cancellation support.
  /// </summary>
  /// <param name="command">The Deno command (e.g. <c>"run --allow-read script.ts"</c>).</param>
  /// <param name="cancellationToken">Token to cancel the operation (e.g. timeout / user abort).</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
  /// await Deno.Execute("run --allow-read script.ts", cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  // Cancellation now part of the unified optional parameter above.

  /// <summary>
  /// Executes a Deno command with base options.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options (working directory, logger, serializer).</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, CancellationToken cancellationToken = default)
    => Execute<string>(command, baseOptions, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options (working directory, logger, serializer).</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  public static Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(baseOptions);
    return ExecuteCore<T>(command, null, baseOptions, null, null, cancellationToken);
  }

  /// <summary>
  /// Executes a Deno command with additional arguments.
  /// </summary>
  /// <param name="command">The Deno command (e.g. <c>run</c>).</param>
  /// <param name="args">Additional arguments (permissions, script file, etc.).</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, string[] args, CancellationToken cancellationToken = default)
    => Execute<string>(command, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="args">Additional arguments.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  public static Task<T> Execute<T>(string command, string[] args, CancellationToken cancellationToken = default)
    => ExecuteCore<T>(command, args, null, null, null, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options.</param>
  /// <param name="args">Additional arguments.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken = default)
    => Execute<string>(command, baseOptions, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options.</param>
  /// <param name="args">Additional arguments.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  public static Task<T> Execute<T>(string command, DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(baseOptions);
    return ExecuteCore<T>(command, args, baseOptions, null, null, cancellationToken);
  }

  /// <summary>
  /// Executes Deno with the specified full argument array.
  /// </summary>
  /// <param name="args">Full Deno argument list (e.g. <c>["run", "script.ts"]</c>).</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string[] args, CancellationToken cancellationToken = default)
    => Execute<string>(args, cancellationToken);

  /// <summary>
  /// Executes Deno with the specified arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="args">Full Deno argument list.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  public static Task<T> Execute<T>(string[] args, CancellationToken cancellationToken = default)
    => ExecuteCore<T>(null!, args, null, null, null, cancellationToken);

  /// <summary>
  /// Executes Deno with base options and arguments.
  /// </summary>
  /// <param name="baseOptions">Base options.</param>
  /// <param name="args">Full Deno argument list.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken = default)
    => Execute<string>(baseOptions, args, cancellationToken);

  /// <summary>
  /// Executes Deno with base options and arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Type to deserialize JSON stdout into.</typeparam>
  /// <param name="baseOptions">Base options.</param>
  /// <param name="args">Full Deno argument list.</param>
  /// <param name="cancellationToken">Optional token to cancel the operation.</param>
  /// <returns>A task producing the deserialized result.</returns>
  public static Task<T> Execute<T>(DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(baseOptions);
    return ExecuteCore<T>(null!, args, baseOptions, null, null, cancellationToken);
  }

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
  public static Task Execute(string command, string configOrPath, string[] args, CancellationToken cancellationToken = default)
    => Execute<string>(command, configOrPath, args, cancellationToken);

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
  public static Task<T> Execute<T>(string command, string configOrPath, string[] args, CancellationToken cancellationToken = default)
    => ExecuteCore<T>(command, args, null, null, configOrPath, cancellationToken);

  /// <summary>
  /// Executes a Deno command with a configuration object and additional arguments.
  /// </summary>
  /// <param name="command">The Deno command.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static Task Execute(string command, DenoConfig config, string[] args, CancellationToken cancellationToken = default)
    => Execute<string>(command, config, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with a configuration object and additional arguments and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  public static Task<T> Execute<T>(string command, DenoConfig config, string[] args, CancellationToken cancellationToken = default)
    => ExecuteCore<T>(command, args, null, config, null, cancellationToken);

  /// <summary>
  /// Core unified execution helper handling all combinations of command/args/baseOptions/config.
  /// </summary>
  private static async Task<T> ExecuteCore<T>(
    string? command,
    string[]? args,
    DenoExecuteBaseOptions? baseOptions,
    DenoConfig? configObject,
    string? configOrPath,
    CancellationToken cancellationToken)
  {
    if (command == null && (args == null || args.Length == 0))
      throw new ArgumentException("Either command or args must be provided.");

    string? tempPath;
    string? resolvedConfigPath = null;
    bool deleteResolved = false;

    try
    {
      if (configObject != null && configOrPath != null)
        throw new ArgumentException("Either 'config' or 'configOrPath' should be provided, not both.");

      if (configObject != null)
      {
        tempPath = Helper.WriteTempConfig(configObject);
        resolvedConfigPath = tempPath;
        deleteResolved = true;
      }
      else if (!string.IsNullOrWhiteSpace(configOrPath))
      {
        var ensured = Helper.EnsureConfigFile(configOrPath);
        resolvedConfigPath = ensured;
        deleteResolved = !Helper.IsJsonPathLike(configOrPath);
      }

      if (resolvedConfigPath != null)
        args = Helper.AppendConfigArgument(args ?? [], resolvedConfigPath);

      return await DenoExecutor.Execute<T>(
        baseOptions?.WorkingDirectory,
        command,
        typeof(T),
        baseOptions?.JsonSerializerOptions,
        baseOptions?.Logger ?? Logger,
        args,
        cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      if (deleteResolved && resolvedConfigPath != null)
      {
        try
        {
          Helper.DeleteTempFile(resolvedConfigPath);
        }
        catch
        {
          // Swallow cleanup exceptions (logged inside Helper)
        }
      }
    }
  }
}
