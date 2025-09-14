using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Represents an allow/deny permission configuration object.
/// Corresponds to $defs/allowDenyPermissionConfig in the Deno configuration schema.
/// </summary>
public class AllowDenyPermissionConfig
{
  /// <summary>
  /// Gets or sets the permission configuration value for allowing specific permissions.
  /// </summary>
  [JsonPropertyName("allow")]
  public PermissionConfigValue? Allow { get; set; }

  /// <summary>
  /// Gets or sets the permission configuration value for denying specific permissions.
  /// </summary>
  [JsonPropertyName("deny")]
  public PermissionConfigValue? Deny { get; set; }
}
