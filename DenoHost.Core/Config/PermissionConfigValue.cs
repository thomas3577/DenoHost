using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Represents a permission configuration value that can be either a boolean or an array of strings.
/// Corresponds to $defs/permissionConfigValue in the Deno configuration schema.
/// </summary>
[JsonConverter(typeof(PermissionConfigValueJsonConverter))]
public class PermissionConfigValue
{
  /// <summary>
  /// Gets or sets the boolean value when the permission is allowed or denied globally.
  /// </summary>
  public bool? BooleanValue { get; set; }

  /// <summary>
  /// Gets or sets the array of strings when specific items are allowed or denied.
  /// </summary>
  public List<string>? ArrayValue { get; set; }

  /// <summary>
  /// Gets a value indicating whether this is a boolean permission configuration.
  /// </summary>
  [JsonIgnore]
  public bool IsBoolean => BooleanValue.HasValue;

  /// <summary>
  /// Gets a value indicating whether this is an array permission configuration.
  /// </summary>
  [JsonIgnore]
  public bool IsArray => ArrayValue != null;

  /// <summary>
  /// Creates a permission configuration with a boolean value.
  /// </summary>
  /// <param name="value">The boolean value to allow or deny the permission.</param>
  /// <returns>A new PermissionConfigValue instance.</returns>
  public static PermissionConfigValue FromBoolean(bool value)
  {
    return new PermissionConfigValue { BooleanValue = value };
  }

  /// <summary>
  /// Creates a permission configuration with an array of strings.
  /// </summary>
  /// <param name="values">The array of items to allow or deny.</param>
  /// <returns>A new PermissionConfigValue instance.</returns>
  public static PermissionConfigValue FromArray(List<string> values)
  {
    return new PermissionConfigValue { ArrayValue = values };
  }

  /// <summary>
  /// Creates a permission configuration with an array of strings.
  /// </summary>
  /// <param name="values">The array of items to allow or deny.</param>
  /// <returns>A new PermissionConfigValue instance.</returns>
  public static PermissionConfigValue FromArray(params string[] values)
  {
    return new PermissionConfigValue { ArrayValue = new List<string>(values) };
  }
}

/// <summary>
/// JSON converter for PermissionConfigValue to handle the oneOf schema pattern.
/// </summary>
public class PermissionConfigValueJsonConverter : JsonConverter<PermissionConfigValue>
{
  public override PermissionConfigValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    switch (reader.TokenType)
    {
      case JsonTokenType.True:
        return PermissionConfigValue.FromBoolean(true);
      case JsonTokenType.False:
        return PermissionConfigValue.FromBoolean(false);
      case JsonTokenType.StartArray:
        var array = JsonSerializer.Deserialize<List<string>>(ref reader, options);
        return array != null ? PermissionConfigValue.FromArray(array) : null;
      default:
        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }
  }

  public override void Write(Utf8JsonWriter writer, PermissionConfigValue value, JsonSerializerOptions options)
  {
    if (value.IsBoolean)
    {
      writer.WriteBooleanValue(value.BooleanValue!.Value);
    }
    else if (value.IsArray)
    {
      JsonSerializer.Serialize(writer, value.ArrayValue, options);
    }
    else
    {
      writer.WriteNullValue();
    }
  }
}
