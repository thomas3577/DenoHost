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
  /// <param name="command">The Deno command (e.g. <c>"run --allow-read script.ts"</c>).</param>
  /// <param name="cancellationToken">Token to cancel the operation (e.g. timeout / user abort).</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
  /// await Deno.Execute("run --allow-read script.ts", cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  public static Task Execute(string command, CancellationToken cancellationToken)
    => Execute<string>(command, cancellationToken);

  /// <summary>
  /// Executes a Deno command with cancellation support and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process (deserialized from JSON stdout).</typeparam>
  /// <param name="command">The Deno command (e.g. <c>"run mod.ts"</c>).</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
  /// var result = await Deno.Execute<MyDto>("run mod.ts", cts.Token);
  /// </code>
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
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base execution options (working directory, serializer, logger).</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
  /// var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = "/src" };
  /// await Deno.Execute("run script.ts", baseOpts, cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, CancellationToken cancellationToken)
    => Execute<string>(command, baseOptions, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options, cancellation support and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type (JSON mapped).</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base execution options (working directory, serializer, logger).</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
  /// var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = "." };
  /// var dto = await Deno.Execute<MyDto>("run mod.ts", baseOpts, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
  /// <param name="command">The base Deno command (e.g. <c>"run"</c>).</param>
  /// <param name="args">Additional arguments appended after the command.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
  /// await Deno.Execute("run", new[]{"--allow-read","script.ts"}, cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  public static Task Execute(string command, string[] args, CancellationToken cancellationToken)
    => Execute<string>(command, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with additional arguments, cancellation support and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type.</typeparam>
  /// <param name="command">The base Deno command.</param>
  /// <param name="args">Additional Deno arguments.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
  /// var data = await Deno.Execute<MyData>("run", new[]{"script.ts"}, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
  /// var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = "./scripts" };
  /// await Deno.Execute("run", baseOpts, new[]{"script.ts"}, cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  public static Task Execute(string command, DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken)
    => Execute<string>(command, baseOptions, args, cancellationToken);

  /// <summary>
  /// Executes a Deno command with base options and additional arguments plus cancellation and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type.</typeparam>
  /// <param name="command">The Deno command.</param>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
  /// var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = "." };
  /// var dto = await Deno.Execute<MyDto>("run", baseOpts, new[]{"script.ts"}, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
  /// <param name="args">Full Deno arguments array (first element typically the sub-command like <c>run</c>).</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
  /// await Deno.Execute(new[]{"run","script.ts"}, cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  public static Task Execute(string[] args, CancellationToken cancellationToken)
    => Execute<string>(args, cancellationToken);

  /// <summary>
  /// Executes Deno with arguments, cancellation support and returns result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type.</typeparam>
  /// <param name="args">Full Deno arguments array.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
  /// var dto = await Deno.Execute<MyDto>(new[]{"run","script.ts"}, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Full Deno arguments array.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
  /// var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = "./scripts" };
  /// await Deno.Execute(baseOpts, new[]{"run","script.ts"}, cts.Token);
  /// </code>
  /// <returns>A task representing the asynchronous command execution.</returns>
  public static Task Execute(DenoExecuteBaseOptions baseOptions, string[] args, CancellationToken cancellationToken)
    => Execute<string>(baseOptions, args, cancellationToken);

  /// <summary>
  /// Executes Deno with base options, arguments, cancellation support and returns result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type.</typeparam>
  /// <param name="baseOptions">Base options such as working directory.</param>
  /// <param name="args">Full Deno arguments array.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
  /// var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = "." };
  /// var dto = await Deno.Execute<MyDto>(baseOpts, new[]{"run","script.ts"}, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
  /// <typeparam name="T">The expected return type.</typeparam>
  /// <param name="command">The Deno command (e.g. <c>"run"</c>).</param>
  /// <param name="configOrPath">JSON configuration string or path to a <c>deno.json</c> file.</param>
  /// <param name="args">Additional Deno arguments (e.g. permissions, script file).</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
  /// var dto = await Deno.Execute<MyDto>("run", "./deno.json", new[]{"script.ts"}, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
  /// <typeparam name="T">The expected return type.</typeparam>
  /// <param name="command">The Deno command (e.g. <c>"run"</c>).</param>
  /// <param name="config">The strongly typed Deno configuration object that will be written to a temp file.</param>
  /// <param name="args">Additional Deno arguments.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <code>
  /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
  /// var cfg = new DenoConfig();
  /// var dto = await Deno.Execute<MyDto>("run", cfg, new[]{"script.ts"}, cts.Token);
  /// </code>
  /// <returns>The deserialized result of the Deno process.</returns>
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
