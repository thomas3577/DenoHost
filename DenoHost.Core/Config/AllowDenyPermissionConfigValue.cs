using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Represents a permission configuration value that can be either a PermissionConfigValue or an AllowDenyPermissionConfig.
/// Corresponds to $defs/allowDenyPermissionConfigValue in the Deno configuration schema.
/// </summary>
[JsonConverter(typeof(AllowDenyPermissionConfigValueJsonConverter))]
public class AllowDenyPermissionConfigValue
{
    /// <summary>
    /// Gets or sets the simple permission configuration value.
    /// </summary>
    public PermissionConfigValue? SimpleValue { get; set; }

    /// <summary>
    /// Gets or sets the allow/deny permission configuration.
    /// </summary>
    public AllowDenyPermissionConfig? ObjectValue { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is a simple permission configuration.
    /// </summary>
    [JsonIgnore]
    public bool IsSimple => SimpleValue != null;

    /// <summary>
    /// Gets a value indicating whether this is an allow/deny object configuration.
    /// </summary>
    [JsonIgnore]
    public bool IsObject => ObjectValue != null;

    /// <summary>
    /// Creates an allow/deny permission configuration from a simple value.
    /// </summary>
    /// <param name="value">The simple permission configuration value.</param>
    /// <returns>A new AllowDenyPermissionConfigValue instance.</returns>
    public static AllowDenyPermissionConfigValue FromSimple(PermissionConfigValue value)
    {
        return new AllowDenyPermissionConfigValue { SimpleValue = value };
    }

    /// <summary>
    /// Creates an allow/deny permission configuration from an object.
    /// </summary>
    /// <param name="value">The allow/deny permission configuration object.</param>
    /// <returns>A new AllowDenyPermissionConfigValue instance.</returns>
    public static AllowDenyPermissionConfigValue FromObject(AllowDenyPermissionConfig value)
    {
        return new AllowDenyPermissionConfigValue { ObjectValue = value };
    }

    /// <summary>
    /// Creates an allow/deny permission configuration from a boolean value.
    /// </summary>
    /// <param name="value">The boolean value to allow or deny the permission.</param>
    /// <returns>A new AllowDenyPermissionConfigValue instance.</returns>
    public static AllowDenyPermissionConfigValue FromBoolean(bool value)
    {
        return FromSimple(PermissionConfigValue.FromBoolean(value));
    }

    /// <summary>
    /// Creates an allow/deny permission configuration from an array of strings.
    /// </summary>
    /// <param name="values">The array of items to allow or deny.</param>
    /// <returns>A new AllowDenyPermissionConfigValue instance.</returns>
    public static AllowDenyPermissionConfigValue FromArray(params string[] values)
    {
        return FromSimple(PermissionConfigValue.FromArray(values));
    }
}

/// <summary>
/// JSON converter for AllowDenyPermissionConfigValue to handle the oneOf schema pattern.
/// </summary>
public class AllowDenyPermissionConfigValueJsonConverter : JsonConverter<AllowDenyPermissionConfigValue>
{
    public override AllowDenyPermissionConfigValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.StartArray:
                var simpleValue = JsonSerializer.Deserialize<PermissionConfigValue>(ref reader, options);
                return simpleValue != null ? AllowDenyPermissionConfigValue.FromSimple(simpleValue) : null;
            case JsonTokenType.StartObject:
                var objectValue = JsonSerializer.Deserialize<AllowDenyPermissionConfig>(ref reader, options);
                return objectValue != null ? AllowDenyPermissionConfigValue.FromObject(objectValue) : null;
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, AllowDenyPermissionConfigValue value, JsonSerializerOptions options)
    {
        if (value.IsSimple)
        {
            JsonSerializer.Serialize(writer, value.SimpleValue, options);
        }
        else if (value.IsObject)
        {
            JsonSerializer.Serialize(writer, value.ObjectValue, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
