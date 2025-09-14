using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Configuration for `deno test`.
/// </summary>
public class TestConfig
{
  /// <summary>
  /// Gets or sets the list of files, directories or globs that will be searched for tests.
  /// </summary>
  [JsonPropertyName("include")]
  public List<string>? Include { get; set; }

  /// <summary>
  /// Gets or sets the list of files, directories or globs that will not be searched for tests.
  /// </summary>
  [JsonPropertyName("exclude")]
  public List<string>? Exclude { get; set; }

  /// <summary>
  /// Gets or sets the permissions configuration for test execution.
  /// </summary>
  [JsonPropertyName("permissions")]
  public DenoHost.Core.Config.PermissionNameOrSet? Permissions { get; set; }
}
