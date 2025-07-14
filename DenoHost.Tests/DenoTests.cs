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

    var ex = Assert.Throws<TargetInvocationException>(() =>
        method.Invoke(null, ["notfound.json"])
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

  [Fact]
  public async Task Execute_WithNullOptions_ThrowsArgumentNullException()
  {
    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
    {
      await Deno.Execute((DenoExecuteOptions)null!);
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
  public async Task Execute_WithEmptyCommand_ThrowsException()
  {
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute("");
    });
  }

  [Fact]
  public async Task Execute_WithNullCommand_ThrowsException()
  {
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute((string)null!);
    });
  }

  [Fact]
  public async Task Execute_WithEmptyArgs_ThrowsException()
  {
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute([]);
    });
  }

  [Fact]
  public async Task Execute_WithNullArgs_ThrowsException()
  {
    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute((string[])null!);
    });
  }

  [Fact]
  public async Task Execute_WithValidCommandString_DoesNotThrow()
  {
    var result = await Deno.Execute<string>("--version");
    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithValidArgsArray_DoesNotThrow()
  {
    var result = await Deno.Execute<string>(["--version"]);
    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithBaseOptions_WorkingDirectoryIsRespected()
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

  [Fact]
  public async Task Execute_WithDenoConfig_ConfigIsUsed()
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
  public async Task Execute_WithJsonConfigString_ConfigIsUsed()
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

    await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("run", invalidJson, ["script.ts"]);
    });
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

  [Fact]
  public void WriteTempConfig_CreatesValidTempFile()
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
  public void DeleteTempFile_RemovesExistingFile()
  {
    var tempPath = Path.Combine(Path.GetTempPath(), $"test_delete_{Guid.NewGuid():N}.txt");
    File.WriteAllText(tempPath, "test content");
    Assert.True(File.Exists(tempPath));

    var method = typeof(Deno).GetMethod("DeleteTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    method.Invoke(null, [tempPath]);

    Assert.False(File.Exists(tempPath));
  }

  [Fact]
  public void DeleteTempFile_WithNonExistentFile_DoesNotThrow()
  {
    var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.txt");
    Assert.False(File.Exists(nonExistentPath));

    var method = typeof(Deno).GetMethod("DeleteTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);
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
    var method = typeof(Deno).GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, command]) as string;

    Assert.Equal(expected, result);
  }

  [Fact]
  public async Task Execute_WithOptionsEmptyCommand_ThrowsNoException()
  {
    var options = new DenoExecuteOptions { Command = "", Args = ["--version"] };

    var exception = await Record.ExceptionAsync(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Null(exception);
  }

  [Fact]
  public async Task Execute_WithOptionsValidCommand_DoesNotThrowForNullable()
  {
    var options = new DenoExecuteOptions
    {
      Command = "help",
      Args = []
    };

    var exception = await Record.ExceptionAsync(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Null(exception);
  }

  [Fact]
  public async Task Execute_WithBaseOptionsAndArgsArray_WorksCorrectly()
  {
    var tempDir = Path.GetTempPath();
    var baseOptions = new DenoExecuteBaseOptions { WorkingDirectory = tempDir };
    var args = new[] { "--version" };

    var result = await Deno.Execute<string>(baseOptions, args);

    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
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
  public async Task Execute_WithNonExistentConfigPath_ThrowsFileNotFoundException()
  {
    var nonExistentPath = "./non_existent_config.json";

    var ex = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
    {
      await Deno.Execute("run", nonExistentPath, ["script.ts"]);
    });

    Assert.True(ex.InnerException is FileNotFoundException || ex.Message.Contains("not exist"));
  }

  [Fact]
  public async Task Execute_GenericReturnType_DeserializesCorrectly()
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
  public async Task Execute_StringReturnType_ReturnsRawOutput()
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
  public async Task Execute_WithMalformedJsonOutput_ThrowsException()
  {
    var invalidJsonConfig = "{ malformed json without closing brace";

    await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("run", invalidJsonConfig, ["script.ts"]);
    });
  }

  [Fact]
  public void EnsureConfigFile_WithValidJsonString_CreatesTempFile()
  {
    var jsonConfig = """{ "imports": { "@std/": "https://deno.land/std/" } }""";
    var method = typeof(Deno).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

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
  }

  [Fact]
  public void EnsureConfigFile_WithValidFilePath_ReturnsOriginalPath()
  {
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"valid_config_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempConfigPath, """{ "imports": {} }""");

    var method = typeof(Deno).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    try
    {
      var result = method.Invoke(null, [tempConfigPath]) as string;

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
    var tempPath = Path.Combine(Path.GetTempPath(), $"temp_delete_test_{Guid.NewGuid():N}.json");
    var jsonString = """{ "test": true }""";
    File.WriteAllText(tempPath, jsonString);

    var method = typeof(Deno).GetMethod("DeleteIfTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    method.Invoke(null, [tempPath, jsonString]);

    Assert.False(File.Exists(tempPath));
  }

  [Fact]
  public void DeleteIfTempFile_WithFilePath_DoesNotDelete()
  {
    var tempPath = Path.Combine(Path.GetTempPath(), $"persistent_file_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempPath, """{ "test": true }""");

    var method = typeof(Deno).GetMethod("DeleteIfTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    try
    {
      method.Invoke(null, [tempPath, tempPath]);

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

  [Fact]
  public async Task Execute_WithTimeoutScenario_ThrowsAppropriateException()
  {
    var ex = await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("invalid-command-that-does-not-exist");
    });

    Assert.Contains("exited with code", ex.Message);
  }

  [Fact]
  public async Task Execute_WithOptionsAndNullArgs_HandledCorrectly()
  {
    var options = new DenoExecuteOptions
    {
      Command = "help",
      Args = null!
    };

    var exception = await Record.ExceptionAsync(async () =>
    {
      await Deno.Execute(options);
    });

    Assert.Null(exception);
  }

  [Fact]
  public async Task Execute_MultipleOverloads_ProduceSameResult()
  {
    var result1 = await Deno.Execute<string>("--version");
    var result2 = await Deno.Execute<string>(["--version"]);
    var result3 = await Deno.Execute<string>("", ["--version"]);

    Assert.Equal(result1.Trim(), result2.Trim());
    Assert.Equal(result2.Trim(), result3.Trim());
  }

  [Fact]
  public async Task Execute_WithComplexImportMap_ResolvesCorrectly1()
  {
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
      var result = await Deno.Execute<ResultA>("run", config, ["--allow-read", scriptPath]);

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
      var result = await Deno.Execute<ResultB>("run", baseOptions, ["--allow-read", scriptPath]);

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
    var ex = await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("non-existent-command", ["--invalid-flag"]);
    });

    Assert.NotNull(ex.Message);
    Assert.Contains("exited with code", ex.Message);

    Assert.True(ex.Message.Contains("Standard Output:") || ex.Message.Contains("Standard Error:"));
  }

  [Fact]
  public async Task Execute_RunSimpleScript_ReturnsExpectedOutput()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_script.ts");
    File.WriteAllText(scriptPath, "console.log(JSON.stringify({ hello: 'world' }));");

    try
    {
      var result = await Deno.Execute<JsonElement>("run", ["--allow-read", scriptPath]);

      Assert.Equal("world", result.GetProperty("hello").GetString());
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_RunSimpleScript_WithDictionary()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_script.ts");
    File.WriteAllText(scriptPath, "console.log(JSON.stringify({ hello: 'world' }));");

    var result = await Deno.Execute<Dictionary<string, object>>("run", ["--allow-read", scriptPath]);

    var helloElement = (JsonElement)result["hello"];
    Assert.Equal("world", helloElement.GetString());
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

  [Fact]
  public async Task Execute_WithObjectType_ThrowsNotSupportedException()
  {
    var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
    {
      await Deno.Execute<object>("--version");
    });

    Assert.Contains("Dynamic types are not supported", ex.Message);
    Assert.Contains("Use JsonElement, Dictionary<string, object>, or a concrete class instead", ex.Message);
  }

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

  [Fact]
  public async Task Execute_WithVeryLargeJsonOutput_HandlesCorrectly()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "large_output_test.ts");
    var largeArray = string.Join(",", Enumerable.Range(1, 100).Select(i => $"\"{i}\""));
    File.WriteAllText(scriptPath, $"console.log(JSON.stringify([{largeArray}]));");

    try
    {
      var result = await Deno.Execute<string[]>("run", ["--allow-read", scriptPath]);

      Assert.Equal(100, result.Length);
      Assert.Equal("1", result[0]);
      Assert.Equal("100", result[99]);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithSpecialCharactersInOutput_HandlesCorrectly()
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

  [Fact]
  public async Task Execute_WithEmptyConfig_DoesNotThrow()
  {
    var emptyConfig = new DenoConfig();

    await Deno.Execute("--version", emptyConfig, []);
  }

  [Fact]
  public async Task Execute_WithConfigContainingNullValues_HandlesCorrectly()
  {
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string> { ["test"] = null! }
    };

    await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("run", config, ["script.ts"]);
    });
  }

  [Fact]
  public async Task Execute_WithProcessThatExitsImmediately_HandlesCorrectly()
  {
    var result = await Deno.Execute<string>("--version");
    Assert.NotNull(result);
  }

  [Fact]
  public async Task Execute_WithCommandThatWritesToStdErr_CapturesError1()
  {
    var ex = await Assert.ThrowsAsync<Exception>(async () =>
    {
      await Deno.Execute("eval", ["\"console.error('Test error'); Deno.exit(1);\""]);
    });

    Assert.Contains("Test error", ex.Message);
  }

  [Fact]
  public async Task Execute_WithCommandThatWritesToStdErr_CapturesError2()
  {
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "stderr_test.ts");
    File.WriteAllText(scriptPath, """
      console.error("Test error");
      Deno.exit(1);
    """);

    try
    {
      var ex = await Assert.ThrowsAsync<Exception>(async () =>
      {
        await Deno.Execute("run", ["--allow-read", scriptPath]);
      });

      Assert.Contains("Test error", ex.Message);
    }
    finally
    {
      File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_ConcurrentWithDifferentConfigs_WorksCorrectly()
  {
    var config1 = new DenoConfig { Imports = new Dictionary<string, string> { ["test1"] = "value1" } };
    var config2 = new DenoConfig { Imports = new Dictionary<string, string> { ["test2"] = "value2" } };

    var task1 = Deno.Execute("--version", config1, []);
    var task2 = Deno.Execute("--version", config2, []);

    await Task.WhenAll(task1, task2);
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

  [Fact]
  public async Task Execute_WithOptionsAndDynamicType_ThrowsNotSupportedException()
  {
    var options = new DenoExecuteOptions
    {
      Command = "--version",
      Args = []
    };

    var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
    {
      await Deno.Execute<dynamic>(options);
    });

    Assert.Contains("Dynamic types are not supported", ex.Message);
  }

  [Fact]
  public async Task Execute_WithConfigAndDynamicType_ThrowsNotSupportedException()
  {
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string> { ["test"] = "value" }
    };

    var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
    {
      await Deno.Execute<dynamic>("run", config, ["--version"]);
    });

    Assert.Contains("Dynamic types are not supported", ex.Message);
  }

  [Fact]
  public async Task Execute_WithBaseOptionsAndDynamicType_ThrowsNotSupportedException()
  {
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Directory.GetCurrentDirectory()
    };

    var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
    {
      await Deno.Execute<dynamic>(baseOptions, ["--version"]);
    });

    Assert.Contains("Dynamic types are not supported", ex.Message);
  }

  [Theory]
  [InlineData(typeof(string), false)]
  [InlineData(typeof(JsonElement), false)]
  [InlineData(typeof(int), false)]
  [InlineData(typeof(bool), false)]
  [InlineData(typeof(ResultA), false)]
  [InlineData(typeof(Dictionary<string, object>), false)]
  [InlineData(typeof(object), true)]
  public void TypeValidation_ChecksCorrectTypes(Type type, bool shouldBeInvalid)
  {
    var isDynamicType = type == typeof(object) || type.Name == "Object";
    Assert.Equal(shouldBeInvalid, isDynamicType);
  }

  [Fact]
  public void AppendConfigArgument_WithValidConfigPath_AddsConfigFlag()
  {
    var args = new[] { "--allow-read", "script.ts" };
    var configPath = "./deno.json";

    var method = typeof(Deno).GetMethod("AppendConfigArgument", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, configPath]) as string[];

    Assert.NotNull(result);
    Assert.Contains("--config", result);
    Assert.Contains(configPath, result);
    Assert.Contains("--allow-read", result);
    Assert.Contains("script.ts", result);
  }

  [Fact]
  public void AppendConfigArgument_WithEmptyConfigPath_ReturnsOriginalArgs()
  {
    var args = new[] { "--allow-read", "script.ts" };
    var configPath = "";

    var method = typeof(Deno).GetMethod("AppendConfigArgument", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, configPath]) as string[];

    Assert.Equal(args, result);
  }
}
