using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Shared serializer settings used when reading and writing Deno configuration.
/// </summary>
public static class JsonOptions
{
  /// <summary>
  /// Indented output with null properties omitted.
  /// </summary>
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
 * Check against https://raw.githubusercontent.com/denoland/deno/{version}/cli/schemas/config-file.v1.json
 */
public class DenoConfig
{
  /// <summary>Instructs the TypeScript compiler how to compile .ts files.</summary>
  [JsonPropertyName("compilerOptions")]
  public Dictionary<string, object>? CompilerOptions { get; set; }

  /// <summary>Location of an import map used when resolving modules. Overridden by <see cref="Imports"/> and <see cref="Scopes"/>.</summary>
  [JsonPropertyName("importMap")]
  public string? ImportMap { get; set; }

  /// <summary>A map of specifiers to their remapped specifiers.</summary>
  [JsonPropertyName("imports")]
  public Dictionary<string, string>? Imports { get; set; }

  /// <summary>Remaps a specifier within a specified scope only.</summary>
  [JsonPropertyName("scopes")]
  public Dictionary<string, Dictionary<string, string>>? Scopes { get; set; }

  /// <summary>Files, directories or globs ignored by all other configurations.</summary>
  [JsonPropertyName("exclude")]
  public List<string>? Exclude { get; set; }

  /// <summary>Configuration for <c>deno lint</c>.</summary>
  [JsonPropertyName("lint")]
  public object? Lint { get; set; }

  /// <summary>Configuration for <c>deno fmt</c>.</summary>
  [JsonPropertyName("fmt")]
  public object? Fmt { get; set; }

  /// <summary>How a local <c>node_modules</c> directory is used: <c>"none"</c>, <c>"auto"</c>, <c>"manual"</c> or a boolean.</summary>
  [JsonPropertyName("nodeModulesDir")]
  public object? NodeModulesDir { get; set; }

  /// <summary>npm packages whose lifecycle scripts may run during install.</summary>
  [JsonPropertyName("allowScripts")]
  public object? AllowScripts { get; set; }

  /// <summary>Enables a local vendor folder as a cache for remote modules and npm packages.</summary>
  [JsonPropertyName("vendor")]
  public bool? Vendor { get; set; }

  /// <summary>Configuration for <c>deno task</c>.</summary>
  [JsonPropertyName("tasks")]
  public Dictionary<string, object>? Tasks { get; set; }

  /// <summary>Configuration for <c>deno test</c>.</summary>
  [JsonPropertyName("test")]
  public TestConfig? Test { get; set; }

  /// <summary>Configuration for <c>deno publish</c>.</summary>
  [JsonPropertyName("publish")]
  public object? Publish { get; set; }

  /// <summary>Configuration for <c>deno deploy</c> and <c>deno sandbox</c>.</summary>
  [JsonPropertyName("deploy")]
  public object? Deploy { get; set; }

  /// <summary>Configuration for <c>deno bench</c>.</summary>
  [JsonPropertyName("bench")]
  public BenchConfig? Bench { get; set; }

  /// <summary>SPDX license identifier if this is a JSR package.</summary>
  [JsonPropertyName("license")]
  public string? License { get; set; }

  /// <summary>Whether to use a lock file, or the path to the lock file. Can be overridden by CLI arguments.</summary>
  [JsonPropertyName("lock")]
  public object? Lock { get; set; }

  /// <summary>Unstable features to enable.</summary>
  [JsonPropertyName("unstable")]
  public List<string>? Unstable { get; set; }

  /// <summary>The name of this JSR or workspace package.</summary>
  [JsonPropertyName("name")]
  public string? Name { get; set; }

  /// <summary>The version of this JSR package.</summary>
  [JsonPropertyName("version")]
  public string? Version { get; set; }

  /// <summary>The module exports of this JSR package.</summary>
  [JsonPropertyName("exports")]
  public object? Exports { get; set; }

  /// <summary>Renamed to <see cref="Links"/> in Deno 2.3.6.</summary>
  [JsonPropertyName("patch")]
  [Obsolete("This unstable property was renamed to \"links\" in Deno 2.3.6.")]
  public List<string>? Patch { get; set; }

  /// <summary>Paths, file URLs or glob patterns to folders containing JSR packages to use local versions of.</summary>
  [JsonPropertyName("links")]
  public List<string>? Links { get; set; }

  /// <summary>Members of this workspace, or the workspace configuration object.</summary>
  [JsonPropertyName("workspace")]
  public object? Workspace { get; set; }

  /// <summary>Configuration for <c>deno compile</c>.</summary>
  [JsonPropertyName("compile")]
  public CompileConfig? Compile { get; set; }

  /// <summary>Named permission sets selectable with <c>-P</c>/<c>--permission-set</c>. The name <c>default</c> applies when <c>-P</c> is passed without a value.</summary>
  [JsonPropertyName("permissions")]
  public Dictionary<string, PermissionSet>? Permissions { get; set; }

  /// <summary>Minimum age a dependency version must have before it is resolvable.</summary>
  [JsonPropertyName("minimumDependencyAge")]
  public object? MinimumDependencyAge { get; set; }

  /// <summary>Package names mapped to version requirements for the default catalog, used with the <c>catalog:</c> protocol.</summary>
  [JsonPropertyName("catalog")]
  public Dictionary<string, string>? Catalog { get; set; }

  /// <summary>Named catalogs used with the <c>catalog:&lt;name&gt;</c> protocol.</summary>
  [JsonPropertyName("catalogs")]
  public Dictionary<string, Dictionary<string, string>>? Catalogs { get; set; }

  /// <summary>Configuration for <c>deno coverage</c> and <c>deno test --coverage</c>.</summary>
  [JsonPropertyName("coverage")]
  public object? Coverage { get; set; }

  /// <summary>Configuration for <c>deno desktop</c>.</summary>
  [JsonPropertyName("desktop")]
  public object? Desktop { get; set; }

  /// <summary>Installs <c>jsr:</c> dependencies into <c>node_modules</c> via JSR's npm compatibility registry.</summary>
  [JsonPropertyName("jsrDepsInNodeModules")]
  public bool? JsrDepsInNodeModules { get; set; }

  /// <summary>Lets <c>deno add</c>, <c>install</c> and <c>remove</c> manage dependencies in <c>package.json</c> instead of <c>deno.json</c>.</summary>
  [JsonPropertyName("preferPackageJson")]
  public bool? PreferPackageJson { get; set; }

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

  /// <summary>Serializes this configuration to JSON using <see cref="JsonOptions.Default"/>.</summary>
  /// <returns>The configuration as an indented JSON string.</returns>
  public string ToJson()
  {
    return JsonSerializer.Serialize(this, JsonOptions.Default);
  }
}
