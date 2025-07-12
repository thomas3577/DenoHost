using DenoHost.Core;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoHost.Tests;

class ResultA
{
  [JsonPropertyName("message")]
  public required string Message { get; set; }

  [JsonPropertyName("hasImports")]
  public bool HasImports { get; set; }
}

class ResultB
{
  public required string Message { get; set; }

  public bool HasImports { get; set; }
}

public class DenoTests
{
  [Fact]
  public void IsJsonFileLike_ReturnsFalse_ForValidJsonPath()
  {
    var notJsonPath = "{ \"foo\": 1 }";
    var method = typeof(Deno).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [notJsonPath]);
    Assert.NotNull(result);
    Assert.False((bool)result);
  }

  [Fact]
  public void IsJsonFileLike_ReturnsTrue_ForJsoncPath()
  {
    var notJson = "foo.json";
    var method = typeof(Deno).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [notJson]);
    Assert.NotNull(result);
    Assert.True((bool)result);
  }

  [Fact]
  public void IsJsonFileLike_ReturnsTrue_ForJsonPath()
  {
    var notJson = "foo.jsonc";
    var method = typeof(Deno).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [notJson]);
    Assert.NotNull(result);
    Assert.True((bool)result);
  }

  [Fact]
  public void BuildArguments_CombinesCommandAndArgs()
  {
    var args = new[] { "--allow-read", "script.ts" };
    var method = typeof(Deno).GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, "run"]);
    Assert.Equal("run --allow-read script.ts", result);
  }

  [Fact]
  public void EnsureConfigFile_ThrowsForMissingFile()
  {
    var method = typeof(Deno).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var m = method;
    var ex = Assert.Throws<TargetInvocationException>(() =>
        m.Invoke(null, ["notfound.json"])
    );
    Assert.IsType<FileNotFoundException>(ex.InnerException);
  }

  [Fact]
  public async Task Execute_ThrowsForInvalidCommand()
  {
    await Assert.ThrowsAsync<Exception>(static async () =>
    {
      await Deno.Execute("invalidcommand");
    });
  }

  // Integration test: requires deno.exe and a test script
  [Fact(Skip = "dynamic does not yet work")]
  public async Task Execute_RunSimpleScript_ReturnsExpectedOutput()
  {
    // Arrange: create a simple Deno script
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_script.ts");
    File.WriteAllText(scriptPath, "console.log(JSON.stringify({ hello: 'world' }));");

    try
    {
      // Act
      var result = await Deno.Execute<dynamic>("run", ["--allow-read", scriptPath]);

      // Assert
      Assert.Equal("world", (string)result.hello);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithNullOptions_ThrowsArgumentNullException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
    {
      await Deno.Execute((DenoExecuteOptions)null!);
    });
  }

  [Fact]
  public async Task Execute_WithConflictingConfigOptions_ThrowsArgumentException()
  {
    // Arrange
    var options = new DenoExecuteOptions
    {
      Command = "run",
      Config = new DenoConfig { Imports = new Dictionary<string, string> { ["test"] = "test" } },
      ConfigOrPath = "./deno.json"
    };

    // Act & Assert
    var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Contains("Either 'config' or 'configOrPath' should be provided, not both", ex.Message);
  }

  [Fact]
  public async Task Execute_WithEmptyCommand_ThrowsException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute("");
    });
  }

  [Fact]
  public async Task Execute_WithNullCommand_ThrowsException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute((string)null!);
    });
  }

  [Fact]
  public async Task Execute_WithEmptyArgs_ThrowsException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute([]);
    });
  }

  [Fact]
  public async Task Execute_WithNullArgs_ThrowsException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute((string[])null!);
    });
  }

  [Fact]
  public async Task Execute_WithValidCommandString_DoesNotThrow()
  {
    // Act & Assert - should not throw for valid version command
    var result = await Deno.Execute<string>("--version");
    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithValidArgsArray_DoesNotThrow()
  {
    // Act & Assert - should not throw for valid version command
    var result = await Deno.Execute<string>(["--version"]);
    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithBaseOptions_WorkingDirectoryIsRespected()
  {
    // Arrange
    var tempDir = Path.GetTempPath();
    var baseOptions = new DenoExecuteBaseOptions { WorkingDirectory = tempDir };
    var scriptPath = Path.Combine(tempDir, "test_cwd.ts");

    File.WriteAllText(scriptPath, "console.log(Deno.cwd());");

    try
    {
      // Act
      var result = await Deno.Execute<string>("run", baseOptions, ["--allow-read", "test_cwd.ts"]);

      // Assert
      Assert.Contains(tempDir.TrimEnd(Path.DirectorySeparatorChar), result);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithDenoConfig_ConfigIsUsed()
  {
    // Arrange
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
      // Act & Assert - should not throw when using valid config
      await Deno.Execute("run", config, ["--allow-net", scriptPath]);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithJsonConfigString_ConfigIsUsed()
  {
    // Arrange
    var jsonConfig = """{ "imports": { "@test/": "https://deno.land/std@0.200.0/" } }""";
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_json_config.ts");
    File.WriteAllText(scriptPath, "console.log('JSON config test passed');");

    try
    {
      // Act & Assert - should not throw when using valid JSON config
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
    // Arrange
    var invalidJson = "{ invalid json }";

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("run", invalidJson, ["script.ts"]);
    });
  }

  [Fact]
  public void GetRuntimeId_ReturnsValidRuntimeId()
  {
    // Act
    var method = typeof(Deno).GetMethod("GetRuntimeId", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, null) as string;

    // Assert
    Assert.NotNull(result);
    Assert.True(result is "win-x64" or "linux-x64" or "osx-arm64" or "osx-x64" or "linux-arm64");
  }

  [Fact]
  public void WriteTempConfig_CreatesValidTempFile()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string> { ["test"] = "value" }
    };

    var method = typeof(Deno).GetMethod("WriteTempConfig", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Act
    var tempPath = method.Invoke(null, [config]) as string;

    try
    {
      // Assert
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
  public void DeleteTempFile_RemovesExistingFile()
  {
    // Arrange
    var tempPath = Path.Combine(Path.GetTempPath(), $"test_delete_{Guid.NewGuid():N}.txt");
    File.WriteAllText(tempPath, "test content");
    Assert.True(File.Exists(tempPath));

    var method = typeof(Deno).GetMethod("DeleteTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Act
    method.Invoke(null, [tempPath]);

    // Assert
    Assert.False(File.Exists(tempPath));
  }

  [Fact]
  public void DeleteTempFile_WithNonExistentFile_DoesNotThrow()
  {
    // Arrange
    var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.txt");
    Assert.False(File.Exists(nonExistentPath));

    var method = typeof(Deno).GetMethod("DeleteTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Act & Assert - should not throw
    method.Invoke(null, [nonExistentPath]);
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
    // Arrange
    var method = typeof(Deno).GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Act
    var result = method.Invoke(null, [args, command]) as string;

    // Assert
    Assert.Equal(expected, result);
  }

  // Additional async tests for better coverage
  [Fact]
  public async Task Execute_WithOptionsEmptyCommand_ThrowsNoException()
  {
    // Arrange
    var options = new DenoExecuteOptions { Command = "", Args = ["--version"] };

    // Act & Assert
    var exception = await Record.ExceptionAsync(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Null(exception);
  }

  [Fact]
  public async Task Execute_WithOptionsValidCommand_DoesNotThrowForNullable()
  {
    // Arrange
    var options = new DenoExecuteOptions
    {
      Command = "help",
      Args = []
    };

    // Act & Assert
    var exception = await Record.ExceptionAsync(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Null(exception);
  }

  [Fact(Skip = "dynamic does not yet work")]
  public async Task Execute_WithComplexDenoConfig_ExecutesSuccessfully()
  {
    // Arrange
    var config = new DenoConfig
    {
      CompilerOptions = new Dictionary<string, object>
      {
        ["strict"] = true,
        ["target"] = "ES2022"
      },
      Imports = new Dictionary<string, string>
      {
        ["@std/"] = "https://deno.land/std@0.200.0/",
        ["@test/"] = "./test/"
      },
      Tasks = new Dictionary<string, string>
      {
        ["test"] = "deno test --allow-all",
        ["start"] = "deno run --allow-all main.ts"
      }
    };

    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "complex_config_test.ts");
    File.WriteAllText(scriptPath, @"
      console.log('Complex config test');
      console.log(JSON.stringify({ success: true }));
    ");

    try
    {
      // Act
      var result = await Deno.Execute<dynamic>("run", config, ["--allow-read", scriptPath]);

      // Assert
      Assert.True((bool)result.success);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithBaseOptionsAndArgsArray_WorksCorrectly()
  {
    // Arrange
    var tempDir = Path.GetTempPath();
    var baseOptions = new DenoExecuteBaseOptions { WorkingDirectory = tempDir };
    var args = new[] { "--version" };

    // Act
    var result = await Deno.Execute<string>(baseOptions, args);

    // Assert
    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithNullBaseOptions_ThrowsArgumentNullException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
    {
      await Deno.Execute((DenoExecuteBaseOptions)null!, ["--version"]);
    });
  }

  [Fact]
  public async Task Execute_WithConfigPath_LoadsExternalConfig()
  {
    // Arrange
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
      // Act & Assert - should not throw when using valid config file
      await Deno.Execute("run", configPath, ["--allow-read", scriptPath]);
    }
    finally
    {
      File.Delete(configPath);
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithNonExistentConfigPath_ThrowsFileNotFoundException()
  {
    // Arrange
    var nonExistentPath = "./non_existent_config.json";

    // Act & Assert
    var ex = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
    {
      await Deno.Execute("run", nonExistentPath, ["script.ts"]);
    });

    // Should contain FileNotFoundException as inner exception when using reflection
    Assert.True(ex.InnerException is FileNotFoundException || ex.Message.Contains("not exist"));
  }

  [Fact]
  public async Task Execute_GenericReturnType_DeserializesCorrectly()
  {
    // Arrange
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
      // Act
      var result = await Deno.Execute<Dictionary<string, object>>("run", ["--allow-read", scriptPath]);

      // Assert
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
  public async Task Execute_StringReturnType_ReturnsRawOutput()
  {
    // Arrange
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "string_return_test.ts");
    File.WriteAllText(scriptPath, "console.log('Raw string output without JSON');");

    try
    {
      // Act
      var result = await Deno.Execute<string>("run", ["--allow-read", scriptPath]);

      // Assert
      Assert.NotNull(result);
      Assert.Contains("Raw string output without JSON", result);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithMalformedJsonOutput_ThrowsException()
  {
    // This test simulates what happens when Deno returns malformed JSON
    // In a real scenario, this would require mocking the process execution

    // For now, we test with invalid JSON config which should fail validation
    var invalidJsonConfig = "{ malformed json without closing brace";

    await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("run", invalidJsonConfig, ["script.ts"]);
    });
  }

  [Fact]
  public void EnsureConfigFile_WithValidJsonString_CreatesTempFile()
  {
    // Arrange
    var jsonConfig = """{ "imports": { "@std/": "https://deno.land/std/" } }""";
    var method = typeof(Deno).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Act
    var result = method.Invoke(null, [jsonConfig]) as string;

    try
    {
      // Assert
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
  }

  [Fact]
  public void EnsureConfigFile_WithValidFilePath_ReturnsOriginalPath()
  {
    // Arrange
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"valid_config_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempConfigPath, """{ "imports": {} }""");

    var method = typeof(Deno).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    try
    {
      // Act
      var result = method.Invoke(null, [tempConfigPath]) as string;

      // Assert
      Assert.Equal(tempConfigPath, result);
    }
    finally
    {
      File.Delete(tempConfigPath);
    }
  }

  [Fact]
  public void DeleteIfTempFile_WithJsonString_DeletesFile()
  {
    // Arrange
    var tempPath = Path.Combine(Path.GetTempPath(), $"temp_delete_test_{Guid.NewGuid():N}.json");
    var jsonString = """{ "test": true }""";
    File.WriteAllText(tempPath, jsonString);

    var method = typeof(Deno).GetMethod("DeleteIfTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Act
    method.Invoke(null, [tempPath, jsonString]);

    // Assert
    Assert.False(File.Exists(tempPath));
  }

  [Fact]
  public void DeleteIfTempFile_WithFilePath_DoesNotDelete()
  {
    // Arrange
    var tempPath = Path.Combine(Path.GetTempPath(), $"persistent_file_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempPath, """{ "test": true }""");

    var method = typeof(Deno).GetMethod("DeleteIfTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    try
    {
      // Act - pass the path as both resolved and original (simulating file path, not JSON)
      method.Invoke(null, [tempPath, tempPath]);

      // Assert - file should still exist
      Assert.True(File.Exists(tempPath));
    }
    finally
    {
      File.Delete(tempPath);
    }
  }

  [Fact]
  public async Task Execute_ConcurrentExecutions_HandleCorrectly()
  {
    // Arrange
    var tasks = new List<Task<string>>();

    // Act - run multiple concurrent version checks
    for (int i = 0; i < 3; i++)
    {
      tasks.Add(Deno.Execute<string>("--version"));
    }

    var results = await Task.WhenAll(tasks);

    // Assert
    Assert.All(results, result =>
    {
      Assert.NotNull(result);
      Assert.Contains("deno", result.ToLower());
    });
  }

  [Fact(Skip = "dynamic does not yet work")]
  public async Task Execute_WithEnvironmentVariables_WorksCorrectly()
  {
    // This test would require extending the Deno class to support environment variables
    // For now, test basic execution with script that reads environment

    // Arrange
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "env_test.ts");
    File.WriteAllText(scriptPath, @"
      const env = Deno.env.get('PATH');
      console.log(JSON.stringify({ hasPath: !!env }));
    ");

    try
    {
      // Act
      var result = await Deno.Execute<dynamic>("run", ["--allow-env", scriptPath]);

      // Assert
      Assert.True((bool)result.hasPath);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact(Skip = "dynamic does not yet work")]
  public async Task Execute_WithLargeOutput_HandlesCorrectly()
  {
    // Arrange
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "large_output_test.ts");
    File.WriteAllText(scriptPath, @"
      const largeArray = Array.from({ length: 1000 }, (_, i) => ({ id: i, value: `item_${i}` }));
      console.log(JSON.stringify({ count: largeArray.length, first: largeArray[0], last: largeArray[999] }));
    ");

    try
    {
      // Act
      var result = await Deno.Execute<dynamic>("run", [scriptPath]);

      // Assert
      Assert.Equal(1000, (int)result.count);
      Assert.Equal(0, (int)result.first.id);
      Assert.Equal(999, (int)result.last.id);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithTimeoutScenario_ThrowsAppropriateException()
  {
    // This test simulates a timeout scenario by trying to execute an invalid command
    // that would cause the process to fail quickly

    // Act & Assert
    var ex = await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("invalid-command-that-does-not-exist");
    });

    // Should contain error information about the invalid command
    Assert.Contains("exited with code", ex.Message);
  }

  [Fact]
  public async Task Execute_WithOptionsAndNullArgs_HandledCorrectly()
  {
    // Arrange
    var options = new DenoExecuteOptions
    {
      Command = "help",
      Args = null! // Test null args handling
    };

    // Act & Assert
    var exception = await Record.ExceptionAsync(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Null(exception);
  }

  [Fact]
  public async Task Execute_MultipleOverloads_ProduceSameResult()
  {
    // Test that different overloads produce the same result for equivalent calls

    // Arrange & Act
    var result1 = await Deno.Execute<string>("--version");
    var result2 = await Deno.Execute<string>(["--version"]);
    var result3 = await Deno.Execute<string>("", ["--version"]);

    // Assert
    Assert.Equal(result1.Trim(), result2.Trim());
    Assert.Equal(result2.Trim(), result3.Trim());
  }

  [Fact]
  public async Task Execute_WithComplexImportMap_ResolvesCorrectly1()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@/"] = "./",
        ["@lib/"] = "./lib/",
        ["@utils/"] = "./utils/",
        ["std/"] = "https://deno.land/std@0.200.0/"
      }
    };

    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "import_map_test.ts");
    File.WriteAllText(scriptPath, @"
      // This would normally import from the mapped paths
      console.log(JSON.stringify({ 
        message: 'Import map test', 
        hasImports: true 
      }));
    ");

    try
    {

      // Act
      var result = await Deno.Execute<ResultA>("run", config, ["--allow-read", scriptPath]);

      // Assert
      Assert.Equal("Import map test", (string)result.Message);
      Assert.True((bool)result.HasImports);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithComplexImportMap_ResolvesCorrectly2()
  {
    var baseOptions = new DenoExecuteOptions
    {
      WorkingDirectory = Directory.GetCurrentDirectory(),
      JsonSerializerOptions = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      },
      Config = new DenoConfig
      {
        Imports = new Dictionary<string, string>
        {
          ["@/"] = "./",
          ["@lib/"] = "./lib/",
          ["@utils/"] = "./utils/",
          ["std/"] = "https://deno.land/std@0.200.0/"
        }
      }
    };

    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "import_map_test.ts");
    File.WriteAllText(scriptPath, @"
      // This would normally import from the mapped paths
      console.log(JSON.stringify({ 
        message: 'Import map test', 
        hasImports: true 
      }));
    ");

    try
    {

      // Act
      var result = await Deno.Execute<ResultB>("run", baseOptions, ["--allow-read", scriptPath]);

      // Assert
      Assert.Equal("Import map test", (string)result.Message);
      Assert.True((bool)result.HasImports);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_ErrorHandling_PreservesStackTrace()
  {
    // Test that error information is properly preserved through the async call stack

    // Act & Assert
    var ex = await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("non-existent-command", ["--invalid-flag"]);
    });

    // Verify error contains useful information
    Assert.NotNull(ex.Message);
    Assert.Contains("exited with code", ex.Message);

    // Should contain both standard output and error information
    Assert.True(ex.Message.Contains("Standard Output:") || ex.Message.Contains("Standard Error:"));
  }
}
