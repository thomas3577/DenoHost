using DenoHost.Core;
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

public class TempFileFixture : IDisposable
{
  private readonly List<string> _tempFiles = new();
  private readonly string _tempDirectory;

  public TempFileFixture()
  {
    _tempDirectory = Path.Combine(Path.GetTempPath(), $"DenoTests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDirectory);
  }

  /// <summary>
  /// Creates a temporary file in the test temp directory and tracks it for cleanup.
  /// </summary>
  /// <param name="fileName">The file name (without path)</param>
  /// <param name="content">The file content</param>
  /// <returns>The full path to the created file</returns>
  public string CreateTempFile(string fileName, string content)
  {
    var filePath = Path.Combine(_tempDirectory, fileName);
    File.WriteAllText(filePath, content);
    _tempFiles.Add(filePath);
    return filePath;
  }

  /// <summary>
  /// Creates a temporary file with a unique name and tracks it for cleanup.
  /// </summary>
  /// <param name="prefix">The file prefix</param>
  /// <param name="extension">The file extension (including dot)</param>
  /// <param name="content">The file content</param>
  /// <returns>The full path to the created file</returns>
  public string CreateTempFile(string prefix, string extension, string content)
  {
    var fileName = $"{prefix}_{Guid.NewGuid():N}{extension}";
    return CreateTempFile(fileName, content);
  }

  /// <summary>
  /// Gets the temp directory for this test fixture.
  /// </summary>
  public string TempDirectory => _tempDirectory;

  public void Dispose()
  {
    // Clean up individual tracked files
    foreach (var file in _tempFiles)
    {
      try
      {
        if (File.Exists(file))
          File.Delete(file);
      }
      catch
      {
        // Ignore cleanup errors
      }
    }

    // Clean up the entire temp directory
    try
    {
      if (Directory.Exists(_tempDirectory))
        Directory.Delete(_tempDirectory, recursive: true);
    }
    catch
    {
      // Ignore cleanup errors
    }

    // Also clean up any orphaned deno_config files in the main temp directory
    try
    {
      var mainTempDir = Path.GetTempPath();
      var orphanedConfigs = Directory.GetFiles(mainTempDir, "deno_config_*.json");
      foreach (var file in orphanedConfigs)
      {
        try
        {
          // Only delete files older than 1 hour to avoid deleting files from concurrent tests
          var fileInfo = new FileInfo(file);
          if (DateTime.UtcNow - fileInfo.CreationTimeUtc > TimeSpan.FromHours(1))
          {
            File.Delete(file);
          }
        }
        catch
        {
          // Ignore cleanup errors
        }
      }
    }
    catch
    {
      // Ignore cleanup errors
    }
  }
}

public class DenoTests : IClassFixture<TempFileFixture>
{
  private readonly TempFileFixture _tempFileFixture;

  public DenoTests(TempFileFixture tempFileFixture)
  {
    _tempFileFixture = tempFileFixture;
  }

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
  public async Task Execute_WithEmptyArgsArray_ThrowsArgumentException()
  {
    var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute(Array.Empty<string>());
    });

    Assert.Contains("Either command or args must be provided", ex.Message);
  }

  [Fact]
  public async Task Execute_Generic_WithEmptyArgsArray_ThrowsArgumentException()
  {
    var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await Deno.Execute<string>(Array.Empty<string>());
    });

    Assert.Contains("Either command or args must be provided", ex.Message);
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
    _tempFileFixture.CreateTempFile("test_cwd.ts", "console.log(Deno.cwd());");
    var baseOptions = new DenoExecuteBaseOptions { WorkingDirectory = _tempFileFixture.TempDirectory };

    var result = await Deno.Execute<string>("run", baseOptions, ["--allow-read", "test_cwd.ts"]);
    Assert.Contains(_tempFileFixture.TempDirectory.TrimEnd(Path.DirectorySeparatorChar), result);
  }

  #endregion

  #region Cancellation Tests

  [Fact]
  public async Task Execute_WithCancellation_RespectsTimeout()
  {
    // Arrange: long-running eval (infinite loop)
    var script = "console.log('Starting...'); let i = 0; while(true) { i++; if (i % 10000000 === 0) console.log('Still running...', i); }";
    using var cts = new CancellationTokenSource(200); // cancel after 200ms

    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Act & Assert
    try
    {
      await Deno.Execute<string>("eval", [script], cts.Token);
    }
    catch (InvalidOperationException ex) when (ex.InnerException is OperationCanceledException)
    {
      // This is expected - cancellation worked correctly
    }
    catch (OperationCanceledException)
    {
      // This is also acceptable - direct cancellation
    }

    sw.Stop();

    // Assert that execution was cancelled within reasonable timeframe
    Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
      $"Execution should have been cancelled quickly, but took {sw.Elapsed.TotalMilliseconds}ms.");
  }

  [Fact]
  public async Task Execute_WithBaseOptionsAndCancellation_RespectsTimeout()
  {
    var baseOptions = new DenoExecuteBaseOptions { WorkingDirectory = Path.GetTempPath() };
    var script = "console.log('Starting...'); let i = 0; while(true) { i++; if (i % 10000000 === 0) console.log('Still running...', i); }";
    using var cts = new CancellationTokenSource(250);

    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Act & Assert
    try
    {
      await Deno.Execute<string>("eval", baseOptions, [script], cts.Token);
    }
    catch (InvalidOperationException ex) when (ex.InnerException is OperationCanceledException)
    {
      // This is expected - cancellation worked correctly
    }
    catch (OperationCanceledException)
    {
      // This is also acceptable - direct cancellation
    }

    sw.Stop();

    // Assert that execution was cancelled within reasonable timeframe
    Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3),
      $"Execution should have been cancelled quickly, but took {sw.Elapsed.TotalMilliseconds}ms.");
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

    var scriptPath = _tempFileFixture.CreateTempFile("test_config.ts", "console.log('Config test passed');");

    await Deno.Execute("run", config, ["--allow-net", scriptPath]);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_WithJsonConfigString_UsesConfig()
  {
    var jsonConfig = """{ "imports": { "@test/": "https://deno.land/std@0.200.0/" } }""";
    var scriptPath = _tempFileFixture.CreateTempFile("test_json_config.ts", "console.log('JSON config test passed');");

    await Deno.Execute("run", jsonConfig, ["--allow-net", scriptPath]);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_WithInvalidJsonConfig_ThrowsException()
  {
    var invalidJson = "{ invalid json }";

    try
    {
      await Assert.ThrowsAsync<InvalidOperationException>(async () =>
      {
        await Deno.Execute("run", invalidJson, ["script.ts"]);
      });
    }
    finally
    {
      // Clean up any temporary config file that might have been created
      // The Helper.EnsureConfigFile method creates a temp file for invalid JSON
      var tempDir = Path.GetTempPath();
      var configFiles = Directory.GetFiles(tempDir, "deno_config_*.json");

      foreach (var file in configFiles)
      {
        try
        {
          var content = File.ReadAllText(file);
          if (content == invalidJson)
          {
            File.Delete(file);
          }
        }
        catch
        {
          // Ignore cleanup errors
        }
      }
    }
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

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
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

  [Fact]
  public async Task Execute_WithConfigObject_ExecutesCorrectly()
  {
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string> { ["@std/"] = "https://deno.land/std/" }
    };

    var options = new DenoExecuteOptions
    {
      Command = "--version",
      Config = config
    };

    var result = await Deno.Execute<string>(options);

    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_WithConfigOrPath_ExecutesCorrectly()
  {
    var configPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid():N}.json");
    var configContent = """{ "imports": { "@std/": "https://deno.land/std/" } }""";

    try
    {
      await File.WriteAllTextAsync(configPath, configContent);

      var options = new DenoExecuteOptions
      {
        Command = "--version",
        ConfigOrPath = configPath
      };

      var result = await Deno.Execute<string>(options);

      Assert.NotNull(result);
      Assert.Contains("deno", result.ToLower());
    }
    finally
    {
      if (File.Exists(configPath))
        File.Delete(configPath);
    }
  }

  [Fact]
  public async Task Execute_SimpleCommand_ExecutesCorrectly()
  {
    await Deno.Execute("--version");

    // This should cover the simple Execute(string command) method
    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_WithBaseOptions_ExecutesCorrectly()
  {
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath()
    };

    await Deno.Execute("--version", baseOptions);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_Generic_WithBaseOptions_ExecutesCorrectly()
  {
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath()
    };

    var result = await Deno.Execute<string>("--version", baseOptions);

    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_VoidMethod_WithDenoExecuteOptions_ExecutesCorrectly()
  {
    var options = new DenoExecuteOptions
    {
      Command = "--version"
    };

    await Deno.Execute(options);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_WithStringArray_ExecutesCorrectly()
  {
    var args = new[] { "--version" };

    var result = await Deno.Execute<string>(args);

    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_VoidMethod_WithCommandBaseOptionsAndArgs_ExecutesCorrectly()
  {
    var command = "run";
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath()
    };
    var args = new[] { "--help" };

    await Deno.Execute(command, baseOptions, args);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_VoidMethod_WithBaseOptionsAndArgs_ExecutesCorrectly()
  {
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath()
    };
    var args = new[] { "--version" };

    await Deno.Execute(baseOptions, args);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_VoidMethod_WithArgs_ExecutesCorrectly()
  {
    var args = new[] { "--version" };

    await Deno.Execute(args);

    // No exception means success for this void method
    Assert.True(true); // Explicit assertion to satisfy linter
  }

  [Fact]
  public async Task Execute_Generic_WithBaseOptionsAndArgs_ExecutesCorrectly()
  {
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath()
    };
    var args = new[] { "--version" };

    var result = await Deno.Execute<string>(baseOptions, args);

    Assert.NotNull(result);
    Assert.Contains("deno", result.ToLower());
  }

  [Fact]
  public async Task Execute_Generic_WithNullBaseOptionsAndArgs_ThrowsArgumentNullException()
  {
    var args = new[] { "--version" };

    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
    {
      await Deno.Execute<string>((DenoExecuteBaseOptions)null!, args);
    });
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

  #region Large Output Tests

  [Fact]
  public async Task Execute_WithLargeStdout_HandlesCorrectly()
  {
    // Test with large stdout output (>64KB) to ensure no deadlock
    var size = 100000; // 100KB
    var script = $"console.log('x'.repeat({size}));";

    var tempDir = Path.GetTempPath();
    var scriptPath = Path.Combine(tempDir, $"large_output_{Guid.NewGuid():N}.ts");

    File.WriteAllText(scriptPath, script);

    try
    {
      var result = await Deno.Execute<string>("run", ["--allow-read", scriptPath]);

      Assert.NotNull(result);
      Assert.Equal(size, result.Trim().Length);
      Assert.All(result.Trim(), c => Assert.Equal('x', c));
    }
    finally
    {
      if (File.Exists(scriptPath))
        File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithLargeStderr_HandlesCorrectly()
  {
    // Test with large stderr output to ensure no deadlock
    var size = 50000; // 50KB
    var script = $"console.error('e'.repeat({size})); console.log('success');";

    var tempDir = Path.GetTempPath();
    var scriptPath = Path.Combine(tempDir, $"large_stderr_{Guid.NewGuid():N}.ts");

    File.WriteAllText(scriptPath, script);

    try
    {
      var result = await Deno.Execute<string>("run", ["--allow-read", scriptPath]);

      Assert.NotNull(result);
      Assert.Equal("success", result.Trim());
    }
    finally
    {
      if (File.Exists(scriptPath))
        File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithLargeStdoutAndStderr_HandlesCorrectly()
  {
    // Test with both large stdout and stderr simultaneously
    var stdoutSize = 30000; // 30KB
    var stderrSize = 30000; // 30KB
    var script = $@"
            console.error('e'.repeat({stderrSize}));
            console.log('x'.repeat({stdoutSize}));
        ";

    var tempDir = Path.GetTempPath();
    var scriptPath = Path.Combine(tempDir, $"large_both_{Guid.NewGuid():N}.ts");

    File.WriteAllText(scriptPath, script);

    try
    {
      var result = await Deno.Execute<string>("run", ["--allow-read", scriptPath]);

      Assert.NotNull(result);
      Assert.Equal(stdoutSize, result.Trim().Length);
      Assert.All(result.Trim(), c => Assert.Equal('x', c));
    }
    finally
    {
      if (File.Exists(scriptPath))
        File.Delete(scriptPath);
    }
  }

  [Fact]
  public async Task Execute_WithMultipleOutputBursts_HandlesCorrectly()
  {
    // Test with multiple output bursts to stress test the parallel reading
    var script = @"
            for (let i = 0; i < 10; i++) {
                console.log('stdout_'.repeat(1000));
                console.error('stderr_'.repeat(1000));
            }
            console.log('final');
        ";

    var tempDir = Path.GetTempPath();
    var scriptPath = Path.Combine(tempDir, $"burst_output_{Guid.NewGuid():N}.ts");

    File.WriteAllText(scriptPath, script);

    try
    {
      var result = await Deno.Execute<string>("run", ["--allow-read", scriptPath]);

      Assert.NotNull(result);
      Assert.EndsWith("final", result.Trim());
    }
    finally
    {
      if (File.Exists(scriptPath))
        File.Delete(scriptPath);
    }
  }

  #endregion
}
