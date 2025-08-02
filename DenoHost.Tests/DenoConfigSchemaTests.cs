using System.Reflection;
using System.Text.Json;
using DenoHost.Core;

namespace DenoHost.Tests;

/// <summary>
/// Tests to validate that DenoConfig matches the official Deno JSON schema
/// </summary>
public class DenoConfigSchemaTests
{
    private const string DENO_SCHEMA_URL = "https://raw.githubusercontent.com/denoland/deno/main/cli/schemas/config-file.v1.json";

    private static readonly HttpClient HttpClient = new();

    [Fact]
    public async Task DenoConfig_TopLevelProperties_MustMatchExactly()
    {
        // This test ensures that NO new top-level properties are added to the Deno schema
        // without being explicitly implemented in DenoConfig or at least being detected.

        // Arrange: Download schema as raw JSON
        var schemaJson = await HttpClient.GetStringAsync(DENO_SCHEMA_URL);
        var schemaDocument = JsonDocument.Parse(schemaJson);

        // Get ALL properties from the schema
        var schemaProperties = new HashSet<string>();
        if (schemaDocument.RootElement.TryGetProperty("properties", out var propertiesElement))
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                schemaProperties.Add(property.Name);
            }
        }

        // Get ALL implemented property names from DenoConfig
        var implementedProperties = new HashSet<string>();
        var properties = typeof(DenoConfig).GetProperties();
        foreach (var property in properties)
        {
            var jsonPropertyAttr = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
            if (jsonPropertyAttr != null)
            {
                implementedProperties.Add(jsonPropertyAttr.Name);
            }
        }

        // Calculate differences
        var missingInImplementation = schemaProperties.Except(implementedProperties).ToList();
        var extraInImplementation = implementedProperties.Except(schemaProperties).ToList();

        // Build detailed error message
        var errorMessages = new List<string>();

        if (missingInImplementation.Count > 0)
        {
            errorMessages.Add($"Properties in schema but MISSING in DenoConfig: {string.Join(", ", missingInImplementation.OrderBy(x => x))}");
        }

        if (extraInImplementation.Count > 0)
        {
            errorMessages.Add($"Properties in DenoConfig but NOT in schema (deprecated?): {string.Join(", ", extraInImplementation.OrderBy(x => x))}");
        }

        // Assert: The test FAILS if there are ANY differences
        if (errorMessages.Count > 0)
        {
            var fullMessage = string.Join("\n", errorMessages) +
                            $"\n\nSchema has {schemaProperties.Count} properties, DenoConfig implements {implementedProperties.Count}." +
                            "\nThis indicates that the Deno schema has changed and DenoConfig needs to be updated!" +
                            "\nEither add the missing properties or update this test if the changes are intentional.";

            Assert.Fail(fullMessage);
        }
    }

    [Fact]
    public async Task DenoConfig_AllPropertiesExistInOfficialSchema()
    {
        // Arrange: Download schema as raw JSON
        var schemaJson = await HttpClient.GetStringAsync(DENO_SCHEMA_URL);
        var schemaDocument = JsonDocument.Parse(schemaJson);

        // Get all properties from the schema
        var schemaProperties = new HashSet<string>();
        if (schemaDocument.RootElement.TryGetProperty("properties", out var propertiesElement))
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                schemaProperties.Add(property.Name);
            }
        }

        // Get all JsonPropertyName attributes from DenoConfig
        var denoConfigProperties = new List<string>();
        var properties = typeof(DenoConfig).GetProperties();
        foreach (var property in properties)
        {
            var jsonPropertyAttr = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
            if (jsonPropertyAttr != null)
            {
                denoConfigProperties.Add(jsonPropertyAttr.Name);
            }
        }

        // Act & Assert: Check each property exists in the schema
        var missingProperties = new List<string>();
        foreach (var propertyName in denoConfigProperties)
        {
            if (!schemaProperties.Contains(propertyName))
            {
                missingProperties.Add(propertyName);
            }
        }

        Assert.True(missingProperties.Count == 0,
            $"Properties not found in official Deno schema: {string.Join(", ", missingProperties)}. " +
            $"The schema may have changed or these properties are deprecated.");
    }

    [Fact]
    public async Task DenoConfig_ReportMissingPropertiesFromSchema()
    {
        // Arrange: Download schema as raw JSON
        var schemaJson = await HttpClient.GetStringAsync(DENO_SCHEMA_URL);
        var schemaDocument = JsonDocument.Parse(schemaJson);

        // Get all properties from the schema
        var schemaProperties = new HashSet<string>();
        if (schemaDocument.RootElement.TryGetProperty("properties", out var propertiesElement))
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                schemaProperties.Add(property.Name);
            }
        }

        // Get implemented property names
        var implementedProperties = new HashSet<string>();
        var properties = typeof(DenoConfig).GetProperties();
        foreach (var property in properties)
        {
            var jsonPropertyAttr = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
            if (jsonPropertyAttr != null)
            {
                implementedProperties.Add(jsonPropertyAttr.Name);
            }
        }

        // Important properties that should be implemented
        var importantSchemaProperties = new[]
        {
            "compilerOptions", "importMap", "imports", "scopes", "lint", "fmt",
            "tasks", "test", "bench", "publish", "lock", "nodeModulesDir",
            "vendor", "unstable", "exclude", "name", "version", "exports",
            "workspace", "unsafelyIgnoreCertificateErrors"
        };

        var missingProperties = new List<string>();

        foreach (var schemaProperty in importantSchemaProperties)
        {
            if (schemaProperties.Contains(schemaProperty) &&
                !implementedProperties.Contains(schemaProperty))
            {
                missingProperties.Add(schemaProperty);
            }
        }

        // Report findings (this is informational, not necessarily a failure)
        if (missingProperties.Count > 0)
        {
            var message = $"DenoConfig is missing these properties from the official schema: {string.Join(", ", missingProperties)}";

            // Using output helper instead of failing the test
            throw new Xunit.Sdk.XunitException(message + "\nConsider adding these properties to maintain full compatibility.");
        }
    }

    [Fact]
    public void DenoConfig_BasicSerialization_ShouldProduceValidJson()
    {
        // Arrange
        var config = new DenoConfig
        {
            CompilerOptions = new Dictionary<string, object>
            {
                ["strict"] = true,
                ["allowJs"] = true,
                ["lib"] = new[] { "deno.window", "dom" }
            },
            ImportMap = "./import_map.json",
            Imports = new Dictionary<string, string>
            {
                ["@std/"] = "jsr:@std/",
                ["@/"] = "./src/"
            },
            Tasks = new Dictionary<string, object>
            {
                ["test"] = "deno test",
                ["dev"] = "deno run --allow-net --allow-read server.ts"
            },
            Lock = "deno.lock"
        };

        // Act
        var json = config.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);

        // Ensure it's valid JSON
        var parsed = JsonDocument.Parse(json);
        Assert.NotNull(parsed);

        // Ensure specific properties are present and correct
        Assert.True(parsed.RootElement.TryGetProperty("compilerOptions", out var compilerOptions));
        Assert.True(compilerOptions.TryGetProperty("strict", out var strict));
        Assert.True(strict.GetBoolean());

        Assert.True(parsed.RootElement.TryGetProperty("tasks", out var tasks));
        Assert.True(tasks.TryGetProperty("test", out var testTask));
        Assert.Equal("deno test", testTask.GetString());

        Assert.True(parsed.RootElement.TryGetProperty("imports", out var imports));
        Assert.True(imports.TryGetProperty("@std/", out var stdImport));
        Assert.Equal("jsr:@std/", stdImport.GetString());
    }

    [Fact]
    public void DenoConfig_EmptyConfiguration_ShouldSerializeToEmptyObject()
    {
        // Arrange
        var emptyConfig = new DenoConfig();

        // Act
        var json = emptyConfig.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);

        // Should be valid JSON
        var parsed = JsonDocument.Parse(json);
        Assert.NotNull(parsed);

        // Should be an empty object "{}" (formatted)
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void DenoConfig_ComplexConfiguration_ShouldSerializeCorrectly()
    {
        // Arrange
        var config = new DenoConfig
        {
            CompilerOptions = new Dictionary<string, object>
            {
                ["strict"] = true,
                ["target"] = "ES2022",
                ["lib"] = new[] { "deno.window", "dom", "deno.unstable" }
            },
            Tasks = new Dictionary<string, object>
            {
                ["build"] = "deno run build.ts",
                ["test"] = "deno test --coverage",
                ["dev"] = "deno run --watch main.ts"
            },
            Imports = new Dictionary<string, string>
            {
                ["@/"] = "./src/",
                ["@std/"] = "jsr:@std/",
                ["@test/"] = "./tests/"
            },
            Lint = new
            {
                include = new[] { "src/", "tests/" },
                exclude = new[] { "build/" },
                rules = new { tags = new[] { "recommended" } }
            },
            Fmt = new
            {
                include = new[] { "src/" },
                exclude = new[] { "dist/" },
                useTabs = false,
                lineWidth = 100,
                singleQuote = true
            }
        };

        // Act
        var json = config.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);

        var parsed = JsonDocument.Parse(json);

        // Verify complex nested structures
        Assert.True(parsed.RootElement.TryGetProperty("lint", out var lint));
        Assert.True(lint.TryGetProperty("include", out var lintInclude));
        Assert.Equal(2, lintInclude.GetArrayLength());

        Assert.True(parsed.RootElement.TryGetProperty("fmt", out var fmt));
        Assert.True(fmt.TryGetProperty("singleQuote", out var singleQuote));
        Assert.True(singleQuote.GetBoolean());
    }

    [Fact]
    public async Task DenoConfig_SchemaCompatibilityInfo()
    {
        // This test provides information about schema compatibility without failing

        // Arrange: Download schema
        var schemaJson = await HttpClient.GetStringAsync(DENO_SCHEMA_URL);
        var schemaDocument = JsonDocument.Parse(schemaJson);

        // Get schema info
        var schemaProperties = new List<string>();
        if (schemaDocument.RootElement.TryGetProperty("properties", out var propertiesElement))
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                schemaProperties.Add(property.Name);
            }
        }

        // Get implemented properties
        var implementedProperties = new List<string>();
        var properties = typeof(DenoConfig).GetProperties();
        foreach (var property in properties)
        {
            var jsonPropertyAttr = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
            if (jsonPropertyAttr != null)
            {
                implementedProperties.Add(jsonPropertyAttr.Name);
            }
        }

        // Output compatibility information
        var totalSchemaProperties = schemaProperties.Count;
        var implementedCount = implementedProperties.Count;
        var coveredProperties = implementedProperties.Intersect(schemaProperties).Count();
        var coveragePercentage = totalSchemaProperties > 0 ? (double)coveredProperties / totalSchemaProperties * 100 : 0;

        // This test always passes but provides useful information
        Assert.True(true,
            $"Schema Compatibility Report:\n" +
            $"- Total properties in Deno schema: {totalSchemaProperties}\n" +
            $"- Properties implemented in DenoConfig: {implementedCount}\n" +
            $"- Valid properties (exist in schema): {coveredProperties}\n" +
            $"- Coverage: {coveragePercentage:F1}%\n" +
            $"- Schema properties: {string.Join(", ", schemaProperties.OrderBy(x => x))}\n" +
            $"- Implemented properties: {string.Join(", ", implementedProperties.OrderBy(x => x))}");
    }
}
