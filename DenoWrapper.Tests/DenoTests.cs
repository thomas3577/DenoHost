using DenoWrapper.Core;
using System.Reflection;

namespace DenoWrapper.Tests;

public class DenoTests
{
  [Fact]
  public void IsJsonLike_ReturnsTrue_ForValidJsonObject()
  {
    var json = "{ \"foo\": 1 }";
    var method = typeof(Deno).GetMethod("IsJsonLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [json]);
    Assert.NotNull(result);
    Assert.True((bool)result);
  }

  [Fact]
  public void IsJsonLike_ReturnsFalse_ForNonJson()
  {
    var notJson = "foo";
    var method = typeof(Deno).GetMethod("IsJsonLike", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var result = method.Invoke(null, [notJson]);
    Assert.NotNull(result);
    Assert.False((bool)result);
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
  [Fact(Skip = "Requires deno.exe and test script")]
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
}
