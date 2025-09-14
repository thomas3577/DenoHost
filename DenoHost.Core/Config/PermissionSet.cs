using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Represents a collection of permissions.
/// Corresponds to $defs/permissionSet in the Deno configuration schema.
/// </summary>
public class PermissionSet
{
  /// <summary>
  /// Gets or sets a value indicating whether to allow all permissions for the program to run unrestricted.
  /// </summary>
  [JsonPropertyName("all")]
  public bool? All { get; set; }

  /// <summary>
  /// Gets or sets the read permission configuration.
  /// </summary>
  [JsonPropertyName("read")]
  public AllowDenyPermissionConfigValue? Read { get; set; }

  /// <summary>
  /// Gets or sets the write permission configuration.
  /// </summary>
  [JsonPropertyName("write")]
  public AllowDenyPermissionConfigValue? Write { get; set; }

  /// <summary>
  /// Gets or sets the import permission configuration.
  /// </summary>
  [JsonPropertyName("import")]
  public AllowDenyPermissionConfigValue? Import { get; set; }

  /// <summary>
  /// Gets or sets the environment variable permission configuration.
  /// </summary>
  [JsonPropertyName("env")]
  public AllowDenyPermissionConfigValue? Env { get; set; }

  /// <summary>
  /// Gets or sets the network permission configuration.
  /// </summary>
  [JsonPropertyName("net")]
  public AllowDenyPermissionConfigValue? Net { get; set; }

  /// <summary>
  /// Gets or sets the run (subprocess) permission configuration.
  /// </summary>
  [JsonPropertyName("run")]
  public AllowDenyPermissionConfigValue? Run { get; set; }

  /// <summary>
  /// Gets or sets the FFI (Foreign Function Interface) permission configuration.
  /// </summary>
  [JsonPropertyName("ffi")]
  public AllowDenyPermissionConfigValue? Ffi { get; set; }

  /// <summary>
  /// Gets or sets the system information permission configuration.
  /// </summary>
  [JsonPropertyName("sys")]
  public AllowDenyPermissionConfigValue? Sys { get; set; }
}
