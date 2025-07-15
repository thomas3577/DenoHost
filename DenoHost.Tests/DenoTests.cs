using DenoHost.Core;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Tests;

class TestResult
{
  [JsonPropertyName("message")]
  public required string Message { get; set; }

  [JsonPropertyName("hasImports")]
  public bool HasImports { get; set; }
}

public class DenoTests
{
  #region Helper Method Tests

  [Fact]
  public void IsJsonFileLike_DetectsJsonFiles()
  {
    var method = typeof(Deno).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    Assert.False((bool)method.Invoke(null, ["{ \"foo\": 1 }"])!); // JSON content
    Assert.True((bool)method.Invoke(null, ["foo.json"])!);        // JSON file
    Assert.True((bool)method.Invoke(null, ["foo.jsonc"])!);       // JSONC file
  }

  [Theory]
  [InlineData(new string[] { "arg1", "arg2" }, "cmd", "cmd arg1 arg2")]
  [InlineData(new string[] { "--flag", "value" }, "run", "run --flag value")]
  [InlineData(new string[0], "version", "version")]
  [InlineData(null, "help", "help")]
  [InlineData(new string[] { "script.ts" }, null, "script.ts")]
  [InlineData(new string[0], null, "")]
  [InlineData(null, null, "")]
  public void BuildArguments_CombinesArgsAndCommandCorrectly(string[]? args, string? command, string expected)
  {
    var method = typeof(Deno).GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, command]) as string;
    Assert.Equal(expected, result);
  }

  [Fact]
  public void GetRuntimeId_ReturnsValidRuntimeId()
  {
    var method = typeof(Deno).GetMethod("GetRuntimeId", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, null) as string;
    Assert.NotNull(result);
    Assert.True(result is "win-x64" or "linux-x64" or "osx-arm64" or "osx-x64" or "linux-arm64");
  }

  #endregion

  #region Validation Tests

  [Fact]
  public async Task Execute_WithNullOptions_ThrowsArgumentNullException()
  {
    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
    {
      await Deno.Execute((DenoExecuteOptions)null!);
    });
  }

  [Fact]
  public async Task Execute_WithNullBaseOptions_ThrowsArgumentNullException()
  {
    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
    {
      await Deno.Execute((DenoExecuteBaseOptions)null!, ["--version"]);
    });
  }

  [Fact]
  public async Task Execute_WithConflictingConfigOptions_ThrowsArgumentException()
  {
    var options = new DenoExecuteOptions
    {
      Command = "run",
      Config = new DenoConfig { Imports = new Dictionary<string, string> { ["test"] = "test" } },
      ConfigOrPath = "./deno.json"
    };

    var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Contains("Either 'config' or 'configOrPath' should be provided, not both", ex.Message);
  }

  [Fact]
  public async Task Execute_WithInvalidCommand_ThrowsInvalidOperationException()
  {
    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
      await Deno.Execute("invalidcommand");
    });
  }

  [Fact]
  public async Task Execute_WithDynamicType_ThrowsNotSupportedException()
  {
    var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
    {
      await Deno.Execute<dynamic>("--version");
    });

    Assert.Contains("Dynamic types are not supported", ex.Message);
    Assert.Contains("Use JsonElement, Dictionary<string, object>, or a concrete class instead", ex.Message);
  }

  #endregion

  #region Basic Execution Tests

  [Fact]
  public async Task Execute_WithVersion_ReturnsDenoVersion()
  {
    var result = await Deno.Execute<string>("--version");

    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithWorkingDirectory_RespectsWorkingDirectory()
  {
    var tempDir = Path.GetTempPath();
    var baseOptions = new DenoExecuteBaseOptions { WorkingDirectory = tempDir };
    var scriptPath = Path.Combine(tempDir, "test_cwd.ts");

    File.WriteAllText(scriptPath, "console.log(Deno.cwd());");

    try
    {
      var result = await Deno.Execute<string>("run", baseOptions, ["--allow-read", "test_cwd.ts"]);
      Assert.Contains(tempDir.TrimEnd(Path.DirectorySeparatorChar), result);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  #endregion

  #region Config Tests

  [Fact]
  public async Task Execute_WithDenoConfig_UsesConfig()
  {
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@test/"] = "https://deno.land/std@0.200.0/"
      }
    };

    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_config.ts");
    File.WriteAllText(scriptPath, "console.log('Config test passed');");

    try
    {
      await Deno.Execute("run", config, ["--allow-net", scriptPath]);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithJsonConfigString_UsesConfig()
  {
    var jsonConfig = """{ "imports": { "@test/": "https://deno.land/std@0.200.0/" } }""";
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_json_config.ts");
    File.WriteAllText(scriptPath, "console.log('JSON config test passed');");

    try
    {
      await Deno.Execute("run", jsonConfig, ["--allow-net", scriptPath]);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithInvalidJsonConfig_ThrowsException()
  {
    var invalidJson = "{ invalid json }";

    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
      await Deno.Execute("run", invalidJson, ["script.ts"]);
    });
  }

  [Fact]
  public async Task Execute_WithConfigPath_LoadsExternalConfig()
  {
    var configPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid():N}.json");
    var configContent = """
    {
      "imports": {
        "@std/": "https://deno.land/std@0.200.0/"
      },
      "tasks": {
        "test": "deno test"
      }
    }
    """;

    File.WriteAllText(configPath, configContent);

    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "config_path_test.ts");
    File.WriteAllText(scriptPath, "console.log('Config path test passed');");

    try
    {
      await Deno.Execute("run", configPath, ["--allow-read", scriptPath]);
    }
    finally
    {
      File.Delete(configPath);
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithComplexImportMap_ResolvesCorrectly()
  {
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@/"] = "./",
        ["@lib/"] = "./lib/",
        ["std/"] = "https://deno.land/std@0.200.0/"
      }
    };

    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "import_map_test.ts");
    File.WriteAllText(scriptPath, """
      console.log(JSON.stringify({ 
        message: 'Import map test', 
        hasImports: true 
      }));
    """);

    try
    {
      var result = await Deno.Execute<TestResult>("run", config, ["--allow-read", scriptPath]);

      Assert.Equal("Import map test", result.Message);
      Assert.True(result.HasImports);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  #endregion

  #region Return Type Tests

  [Fact]
  public async Task Execute_WithGenericReturnType_DeserializesCorrectly()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "generic_return_test.ts");
    File.WriteAllText(scriptPath, """
      const result = {
        name: "Test",
        value: 42,
        active: true,
        items: ["a", "b", "c"]
      };
      console.log(JSON.stringify(result));
    """);

    try
    {
      var result = await Deno.Execute<Dictionary<string, object>>("run", ["--allow-read", scriptPath]);

      Assert.NotNull(result);
      Assert.Equal("Test", result["name"].ToString());
      Assert.Equal(42, ((JsonElement)result["value"]).GetInt32());
      Assert.True(((JsonElement)result["active"]).GetBoolean());
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithStringReturnType_ReturnsRawOutput()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "string_return_test.ts");
    File.WriteAllText(scriptPath, "console.log('Raw string output without JSON');");

    try
    {
      var result = await Deno.Execute<string>("run", ["--allow-read", scriptPath]);

      Assert.NotNull(result);
      Assert.Contains("Raw string output without JSON", result);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithCustomJsonSerializerOptions_WorksCorrectly()
  {
    var options = new DenoExecuteOptions
    {
      Command = "eval",
      Args = ["\"console.log(JSON.stringify({ CamelCase: 'value' }));\""],
      JsonSerializerOptions = new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
      }
    };

    var result = await Deno.Execute<Dictionary<string, object>>(options);
    Assert.True(result.ContainsKey("CamelCase"));
  }

  #endregion

  #region Error Handling Tests

  [Fact]
  public async Task Execute_WithStdErr_CapturesError()
  {
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
      await Deno.Execute("eval", ["\"console.error('Test error'); Deno.exit(1);\""]);
    });

    Assert.Contains("An error occurred during Deno execution after", ex.Message);
    Assert.Contains("Test error", ex.InnerException?.Message);
  }

  [Fact]
  public async Task Execute_WithSpecialCharacters_HandlesCorrectly()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "special_chars_test.ts");
    File.WriteAllText(scriptPath, """
      console.log(JSON.stringify({ 
        unicode: "🦕 Deno 🚀", 
        newlines: "line1\nline2\r\nline3",
        quotes: "He said \"Hello\"",
        backslashes: "C:\\Windows\\System32"
      }));
    """, System.Text.Encoding.UTF8);

    try
    {
      var result = await Deno.Execute<Dictionary<string, object>>("run", ["--allow-read", scriptPath]);

      Assert.Contains("🦕", result["unicode"].ToString());
      Assert.Contains("line1\nline2", result["newlines"].ToString());
      Assert.Contains("\"Hello\"", result["quotes"].ToString());
      Assert.Contains("C:\\Windows\\System32", result["backslashes"].ToString());
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  #endregion

  #region Concurrency Tests

  [Fact]
  public async Task Execute_ConcurrentExecutions_HandleCorrectly()
  {
    var tasks = new List<Task<string>>();
    for (int i = 0; i < 3; i++)
    {
      tasks.Add(Deno.Execute<string>("--version"));
    }

    var results = await Task.WhenAll(tasks);

    Assert.All(results, result =>
    {
      Assert.NotNull(result);
      Assert.Contains("deno", result.ToLower());
    });
  }

  #endregion

  #region Temp File Management Tests

  [Fact]
  public void TempFileManagement_WritesAndDeletesCorrectly()
  {
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string> { ["test"] = "value" }
    };

    var method = typeof(Deno).GetMethod("WriteTempConfig", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var tempPath = method.Invoke(null, [config]) as string;

    try
    {
      Assert.NotNull(tempPath);
      Assert.True(File.Exists(tempPath));
      Assert.Contains("deno_config_", tempPath);
      Assert.EndsWith(".json", tempPath);

      var content = File.ReadAllText(tempPath);
      Assert.Contains("test", content);
      Assert.Contains("value", content);
    }
    finally
    {
      if (tempPath != null && File.Exists(tempPath))
        File.Delete(tempPath);
    }
  }

  [Fact]
  public void EnsureConfigFile_HandlesJsonStringAndFilePath()
  {
    var method = typeof(Deno).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Test with JSON string
    var jsonConfig = """{ "imports": { "@std/": "https://deno.land/std/" } }""";
    var result = method.Invoke(null, [jsonConfig]) as string;

    try
    {
      Assert.NotNull(result);
      Assert.True(File.Exists(result));
      Assert.Contains("deno_config_", result);

      var content = File.ReadAllText(result);
      Assert.Contains("@std/", content);
    }
    finally
    {
      if (result != null && File.Exists(result))
        File.Delete(result);
    }

    // Test with file path
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"valid_config_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempConfigPath, """{ "imports": {} }""");

    try
    {
      var fileResult = method.Invoke(null, [tempConfigPath]) as string;
      Assert.Equal(tempConfigPath, fileResult);
    }
    finally
    {
      File.Delete(tempConfigPath);
    }
  }

  #endregion

  #region Logger Tests

  [Fact]
  public void Logger_Property_CanBeSetAndCleared()
  {
    var originalLogger = Deno.Logger;

    try
    {
      Deno.Logger = null;
      Assert.Null(Deno.Logger);
    }
    finally
    {
      Deno.Logger = originalLogger;
    }
  }

  #endregion
}
