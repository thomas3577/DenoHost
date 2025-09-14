using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Core.Config;

/// <summary>
/// Represents a permission set name to use or inline permission set.
/// Corresponds to $defs/permissionNameOrSet in the Deno configuration schema.
/// </summary>
[JsonConverter(typeof(PermissionNameOrSetJsonConverter))]
public class PermissionNameOrSet
{
    /// <summary>
    /// Gets or sets the permission name when using a named permission set.
    /// </summary>
    public string? PermissionName { get; set; }

    /// <summary>
    /// Gets or sets the inline permission set configuration.
    /// </summary>
    public PermissionSet? PermissionSet { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is a named permission reference.
    /// </summary>
    [JsonIgnore]
    public bool IsPermissionName => !string.IsNullOrEmpty(PermissionName);

    /// <summary>
    /// Gets a value indicating whether this is an inline permission set.
    /// </summary>
    [JsonIgnore]
    public bool IsPermissionSet => PermissionSet != null;

    /// <summary>
    /// Creates a permission configuration from a permission name.
    /// </summary>
    /// <param name="permissionName">The name of the permission set to use.</param>
    /// <returns>A new PermissionNameOrSet instance.</returns>
    public static PermissionNameOrSet FromName(string permissionName)
    {
        return new PermissionNameOrSet { PermissionName = permissionName };
    }

    /// <summary>
    /// Creates a permission configuration from an inline permission set.
    /// </summary>
    /// <param name="permissionSet">The inline permission set configuration.</param>
    /// <returns>A new PermissionNameOrSet instance.</returns>
    public static PermissionNameOrSet FromSet(PermissionSet permissionSet)
    {
        return new PermissionNameOrSet { PermissionSet = permissionSet };
    }
}

/// <summary>
/// JSON converter for PermissionNameOrSet to handle the anyOf schema pattern.
/// </summary>
public class PermissionNameOrSetJsonConverter : JsonConverter<PermissionNameOrSet>
{
    public override PermissionNameOrSet? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var permissionName = reader.GetString();
                return !string.IsNullOrEmpty(permissionName) ? PermissionNameOrSet.FromName(permissionName) : null;
            case JsonTokenType.StartObject:
                var permissionSet = JsonSerializer.Deserialize<PermissionSet>(ref reader, options);
                return permissionSet != null ? PermissionNameOrSet.FromSet(permissionSet) : null;
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, PermissionNameOrSet value, JsonSerializerOptions options)
    {
        if (value.IsPermissionName)
        {
            writer.WriteStringValue(value.PermissionName);
        }
        else if (value.IsPermissionSet)
        {
            JsonSerializer.Serialize(writer, value.PermissionSet, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
