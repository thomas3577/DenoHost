using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Configuration for `deno compile`.
/// </summary>
public class CompileConfig
{
  /// <summary>
  /// Gets or sets the permissions configuration for the compiled executable.
  /// </summary>
  [JsonPropertyName("permissions")]
  public PermissionNameOrSet? Permissions { get; set; }
}
