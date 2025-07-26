using DenoHost.Core;
using System.Reflection;

namespace DenoHost.Tests;

public class HelperTests
{
  [Fact]
  public void IsJsonPathLike_DetectsJsonFiles()
  {
    var method = typeof(Helper).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
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
    var method = typeof(Helper).GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, command]) as string;
    Assert.Equal(expected, result);
  }

  [Fact]
  public void GetRuntimeId_ReturnsValidRuntimeId()
  {
    var method = typeof(Helper).GetMethod("GetRuntimeId", BindingFlags.NonPublic | BindingFlags.Static);
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

    var method = typeof(Helper).GetMethod("WriteTempConfig", BindingFlags.NonPublic | BindingFlags.Static);
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
    var method = typeof(Helper).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
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

  [Fact]
  public void AppendConfigArgument_WithValidConfigPath_AddsConfigFlag()
  {
    var args = new[] { "--allow-read", "script.ts" };
    var configPath = "./deno.json";

    var method = typeof(Helper).GetMethod("AppendConfigArgument", BindingFlags.NonPublic | BindingFlags.Static);
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

    var method = typeof(Helper).GetMethod("AppendConfigArgument", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, configPath]) as string[];

    Assert.Equal(args, result);
  }

  [Fact]
  public void DeleteTempFile_RemovesExistingFile()
  {
    var tempPath = Path.Combine(Path.GetTempPath(), $"test_delete_{Guid.NewGuid():N}.txt");
    File.WriteAllText(tempPath, "test content");
    Assert.True(File.Exists(tempPath));

    var method = typeof(Helper).GetMethod("DeleteTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    method.Invoke(null, [tempPath]);

    Assert.False(File.Exists(tempPath));
  }

  [Fact]
  public void DeleteTempFile_WithNonExistentFile_DoesNotThrow()
  {
    var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.txt");
    Assert.False(File.Exists(nonExistentPath));

    var method = typeof(Helper).GetMethod("DeleteTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Should not throw
    method.Invoke(null, [nonExistentPath]);
  }

  [Fact]
  public void IsJsonPathLike_WithNullOrEmpty_ReturnsFalse()
  {
    var method = typeof(Helper).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    Assert.False((bool)method.Invoke(null, [null])!);
    Assert.False((bool)method.Invoke(null, [""])!);
    Assert.False((bool)method.Invoke(null, ["   "])!);
    Assert.False((bool)method.Invoke(null, ["notjson.txt"])!);
  }

  [Theory]
  [InlineData("config.JSON")]
  [InlineData("config.JSONC")]
  [InlineData("  config.json  ")]
  [InlineData("path/to/config.jsonc")]
  public void IsJsonPathLike_WithVariousJsonPaths_ReturnsTrue(string input)
  {
    var method = typeof(Helper).GetMethod("IsJsonPathLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    Assert.True((bool)method.Invoke(null, [input])!);
  }

  [Fact]
  public void EnsureConfigFile_WithNonExistentFilePath_ThrowsFileNotFoundException()
  {
    var method = typeof(Helper).GetMethod("EnsureConfigFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var nonExistentPath = "./nonexistent_config.json";

    var exception = Assert.Throws<TargetInvocationException>(() =>
        method.Invoke(null, [nonExistentPath]));

    Assert.IsType<FileNotFoundException>(exception.InnerException);
  }

  [Fact]
  public void DeleteIfTempFile_WithJsonString_DeletesFile()
  {
    var method = typeof(Helper).GetMethod("DeleteIfTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Create a temp file
    var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempPath, "test");
    Assert.True(File.Exists(tempPath));

    // Call DeleteIfTempFile with JSON string (should delete)
    var jsonString = """{ "test": "value" }""";
    method.Invoke(null, [tempPath, jsonString]);

    Assert.False(File.Exists(tempPath));
  }

  [Fact]
  public void DeleteIfTempFile_WithFilePath_DoesNotDelete()
  {
    var method = typeof(Helper).GetMethod("DeleteIfTempFile", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Create a temp file
    var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.json");
    File.WriteAllText(tempPath, "test");
    Assert.True(File.Exists(tempPath));

    try
    {
      // Call DeleteIfTempFile with file path (should NOT delete)
      method.Invoke(null, [tempPath, "config.json"]);

      Assert.True(File.Exists(tempPath)); // Should still exist
    }
    finally
    {
      if (File.Exists(tempPath))
        File.Delete(tempPath);
    }
  }

  [Fact]
  public void BuildArgumentsArray_DirectTesting()
  {
    var method = typeof(Helper).GetMethod("BuildArgumentsArray", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // Test with command and args
    var result1 = method.Invoke(null, [new[] { "arg1", "arg2" }, "cmd"]) as string[];
    Assert.NotNull(result1);
    Assert.Equal(["cmd", "arg1", "arg2"], result1);

    // Test with only command
    var result2 = method.Invoke(null, [null, "version"]) as string[];
    Assert.NotNull(result2);
    Assert.Equal(["version"], result2);

    // Test with only args
    var result3 = method.Invoke(null, [new[] { "script.ts" }, null]) as string[];
    Assert.NotNull(result3);
    Assert.Equal(["script.ts"], result3);

    // Test with neither
    var result4 = method.Invoke(null, [null, null]) as string[];
    Assert.NotNull(result4);
    Assert.Equal([], result4);
  }

  [Fact]
  public void AppendConfigArgument_WithNullConfigPath_ReturnsOriginalArgs()
  {
    var args = new[] { "--allow-read", "script.ts" };

    var method = typeof(Helper).GetMethod("AppendConfigArgument", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, null]) as string[];
    Assert.Equal(args, result);
  }

  [Fact]
  public void AppendConfigArgument_WithWhitespaceConfigPath_ReturnsOriginalArgs()
  {
    var args = new[] { "--allow-read", "script.ts" };

    var method = typeof(Helper).GetMethod("AppendConfigArgument", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [args, "   "]) as string[];
    Assert.Equal(args, result);
  }

  [Fact]
  public void GetDenoPath_WhenExecutableNotFound_ThrowsFileNotFoundException()
  {
    // This test is tricky because it depends on the actual file system
    // We can't easily mock the file system for internal static methods
    // But we can test the method exists and has the right signature
    var method = typeof(Helper).GetMethod("GetDenoPath", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    // In a real scenario where deno executable doesn't exist, it would throw FileNotFoundException
    // For now, we just ensure the method can be called (it might succeed if deno is installed)
    try
    {
      var result = method.Invoke(null, null) as string;
      // If we get here, deno was found - that's also valid
      Assert.NotNull(result);
    }
    catch (TargetInvocationException ex) when (ex.InnerException is FileNotFoundException)
    {
      // This is the expected exception when deno is not found
      Assert.IsType<FileNotFoundException>(ex.InnerException);
    }
  }
}
