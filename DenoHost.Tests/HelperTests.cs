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
}
