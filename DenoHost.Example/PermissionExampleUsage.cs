using DenoHost.Core.Config;

namespace DenoHost.Example;

/// <summary>
/// Demonstrates usage of the new permission DTOs.
/// </summary>
public class PermissionExampleUsage
{
  public static void DemonstratePermissionUsage()
  {
    // Create a DenoConfig with typed permission settings
    var config = new DenoConfig
    {
      Name = "my-app",
      Version = "1.0.0",

      // Configure compile permissions using the new DTO
      Compile = new CompileConfig
      {
        Permissions = PermissionNameOrSet.FromSet(new PermissionSet
        {
          All = false,
          Read = AllowDenyPermissionConfigValue.FromArray("./data", "./config"),
          Write = AllowDenyPermissionConfigValue.FromBoolean(false),
          Net = AllowDenyPermissionConfigValue.FromObject(new AllowDenyPermissionConfig
          {
            Allow = PermissionConfigValue.FromArray("api.example.com", "cdn.example.com"),
            Deny = PermissionConfigValue.FromArray("malicious.site.com")
          })
        })
      },

      // Configure test permissions using a named permission set
      Test = new TestConfig
      {
        Include = ["**/*_test.ts", "tests/**/*.ts"],
        Exclude = ["vendor/**/*.ts"],
        Permissions = PermissionNameOrSet.FromName("test-permissions")
      },

      // Configure benchmark permissions with simple boolean permission
      Bench = new BenchConfig
      {
        Include = ["bench/**/*.ts"],
        Permissions = PermissionNameOrSet.FromSet(new PermissionSet
        {
          Read = AllowDenyPermissionConfigValue.FromBoolean(true),
          Write = AllowDenyPermissionConfigValue.FromBoolean(false)
        })
      }
    };

    // Serialize to JSON to show the result
    var json = config.ToJson();
    Console.WriteLine("Generated Deno configuration:");
    Console.WriteLine(json);

    // Demonstrate type safety
    if (config.Compile?.Permissions?.IsPermissionSet == true)
    {
      var permissionSet = config.Compile.Permissions.PermissionSet;
      Console.WriteLine($"\nCompile permissions configured with all={permissionSet?.All}");

      if (permissionSet?.Read?.IsSimple == true && permissionSet.Read.SimpleValue?.IsArray == true)
      {
        Console.WriteLine($"Read permissions allow: {string.Join(", ", permissionSet.Read.SimpleValue.ArrayValue ?? [])}");
      }
    }

    if (config.Test?.Permissions?.IsPermissionName == true)
    {
      Console.WriteLine($"Test uses named permission set: {config.Test.Permissions.PermissionName}");
    }
  }
}
