using System.Text.Json;
using Microsoft.Extensions.Logging;
using DenoHost.Core.Config;

namespace DenoHost.Core;

/// <summary>
/// Options shared by every Deno execution entry point.
/// </summary>
public class DenoExecuteBaseOptions
{
  /// <summary>
  /// Directory the Deno process is started in. Defaults to the current working directory.
  /// </summary>
  public string? WorkingDirectory { get; set; }

  /// <summary>
  /// Options used to deserialize stdout into the requested result type.
  /// When <c>null</c>, <see cref="System.Text.Json.JsonSerializer"/> applies its own defaults.
  /// </summary>
  public JsonSerializerOptions? JsonSerializerOptions { get; set; }

  /// <summary>
  /// Logger for this execution. Overrides <see cref="Deno.Logger"/> when set.
  /// </summary>
  public ILogger? Logger { get; set; }
}

/// <summary>
/// Composite options describing a single Deno invocation.
/// </summary>
public class DenoExecuteOptions : DenoExecuteBaseOptions
{
  /// <summary>
  /// The Deno subcommand to run, e.g. <c>run</c> or <c>test</c>.
  /// </summary>
  public string Command { get; set; } = string.Empty;

  /// <summary>
  /// Additional command line arguments appended after the command.
  /// </summary>
  public string[] Args { get; set; } = [];

  /// <summary>
  /// Configuration as a JSON string or as a path to a configuration file. Mutually exclusive with <see cref="Config"/>.
  /// </summary>
  public string? ConfigOrPath { get; set; }

  /// <summary>
  /// Configuration object written to a temporary file before execution. Mutually exclusive with <see cref="ConfigOrPath"/>.
  /// </summary>
  public DenoConfig? Config { get; set; }
}
