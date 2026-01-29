using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

public static class JsonOptions
{
  public static readonly JsonSerializerOptions Default = new()
  {
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };
}

/**
 * Represents the configuration for Deno, including compiler options, import maps, linting, formatting,
 * tasks, lock files, certificate error handling, testing configurations, and imports.
 *
 * Check against https://raw.githubusercontent.com/denoland/deno/main/cli/schemas/config-file.v1.json
 */
public class DenoConfig
{
  [JsonPropertyName("compilerOptions")]
  public Dictionary<string, object>? CompilerOptions { get; set; }

  [JsonPropertyName("importMap")]
  public string? ImportMap { get; set; }

  [JsonPropertyName("imports")]
  public Dictionary<string, string>? Imports { get; set; }

  [JsonPropertyName("scopes")]
  public Dictionary<string, Dictionary<string, string>>? Scopes { get; set; }

  [JsonPropertyName("exclude")]
  public List<string>? Exclude { get; set; }

  [JsonPropertyName("lint")]
  public object? Lint { get; set; }

  [JsonPropertyName("fmt")]
  public object? Fmt { get; set; }

  [JsonPropertyName("nodeModulesDir")]
  public object? NodeModulesDir { get; set; }

  [JsonPropertyName("allowScripts")]
  public object? AllowScripts { get; set; }

  [JsonPropertyName("vendor")]
  public bool? Vendor { get; set; }

  [JsonPropertyName("tasks")]
  public Dictionary<string, object>? Tasks { get; set; }

  [JsonPropertyName("test")]
  public TestConfig? Test { get; set; }

  [JsonPropertyName("publish")]
  public object? Publish { get; set; }

  [JsonPropertyName("deploy")]
  public object? Deploy { get; set; }

  [JsonPropertyName("bench")]
  public BenchConfig? Bench { get; set; }

  [JsonPropertyName("license")]
  public string? License { get; set; }

  [JsonPropertyName("lock")]
  public object? Lock { get; set; }

  [JsonPropertyName("unstable")]
  public List<string>? Unstable { get; set; }

  [JsonPropertyName("name")]
  public string? Name { get; set; }

  [JsonPropertyName("version")]
  public string? Version { get; set; }

  [JsonPropertyName("exports")]
  public object? Exports { get; set; }

  [JsonPropertyName("patch")]
  [Obsolete("This unstable property was renamed to \"links\" in Deno 2.3.6.")]
  public List<string>? Patch { get; set; }

  [JsonPropertyName("links")]
  public List<string>? Links { get; set; }

  [JsonPropertyName("workspace")]
  public object? Workspace { get; set; }

  [JsonPropertyName("compile")]
  public CompileConfig? Compile { get; set; }

  [JsonPropertyName("permissions")]
  public Dictionary<string, PermissionSet>? Permissions { get; set; }

  [JsonPropertyName("minimumDependencyAge")]
  public object? MinimumDependencyAge { get; set; }

  /// <summary>
  /// Captures any additional properties not explicitly defined.
  /// This ensures forward compatibility when Deno adds new configuration options.
  /// </summary>
  [JsonExtensionData]
  public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

  /// <summary>
  /// Gets a value indicating whether this configuration contains any unknown properties.
  /// Useful for detecting new Deno features that aren't yet supported.
  /// </summary>
  [JsonIgnore]
  public bool HasUnknownProperties => AdditionalProperties?.Count > 0;

  public string ToJson()
  {
    return JsonSerializer.Serialize(this, JsonOptions.Default);
  }
}
