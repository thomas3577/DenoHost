using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoWrapper;

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

  [JsonPropertyName("lint")]
  public object? Lint { get; set; }

  [JsonPropertyName("fmt")]
  public object? Fmt { get; set; }

  [JsonPropertyName("tasks")]
  public Dictionary<string, string>? Tasks { get; set; }

  [JsonPropertyName("lock")]
  public string? Lock { get; set; }

  [JsonPropertyName("unsafelyIgnoreCertificateErrors")]
  public List<string>? UnsafelyIgnoreCertificateErrors { get; set; }

  [JsonPropertyName("test")]
  public object? Test { get; set; }

  [JsonPropertyName("imports")]
  public Dictionary<string, string>? Imports { get; set; }

  public string ToJson()
  {
    return JsonSerializer.Serialize(this, JsonOptions.Default);
  }
}
