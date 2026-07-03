using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DenoHost.Core;

namespace DenoHost.Tests;

/// <summary>
/// Validates that the generated command options classes stay in sync with the installed Deno binary
/// and with the Deno permission schema.
/// When this test fails, run `deno task generate` in tools/gen-commands/ and commit the result.
/// </summary>
public class DenoCommandsSchemaTests
{
  // Commands whose generated options include permission flags (must match `hasPermissions: true`
  // entries in tools/gen-commands/generate.ts's COMMANDS list).
  private static readonly HashSet<string> PermissionCommands = ["run", "eval", "test", "bench", "compile", "serve"];

  private static readonly HttpClient HttpClient = new();

  private static readonly string SnapshotPath = FindSnapshotFile();

  [Fact]
  public async Task GeneratedOptions_MatchCurrentDenoJsonReference()
  {
    var snapshot = LoadSnapshot();
    var current = RunDenoJsonReference();

    var permissionFlags = await FetchPermissionFlags(TestContext.Current.CancellationToken);
    foreach (var command in PermissionCommands)
    {
      if (!current.TryGetValue(command, out var flags)) continue;
      flags.UnionWith(permissionFlags);
    }

    var diffs = new List<string>();

    foreach (var (command, snapshotFlags) in snapshot)
    {
      if (!current.TryGetValue(command, out var currentFlags))
      {
        diffs.Add($"deno {command}: subcommand no longer exists in `deno json_reference`.");
        continue;
      }

      var added = currentFlags.Except(snapshotFlags).ToList();
      var removed = snapshotFlags.Except(currentFlags).ToList();

      foreach (var flag in added)
        diffs.Add($"deno {command}: new flag '--{flag}' — add to {ToPascalCase(command)}Options if relevant.");

      foreach (var flag in removed)
        diffs.Add($"deno {command}: flag '--{flag}' no longer exists — remove from {ToPascalCase(command)}Options.");
    }

    Assert.True(
      diffs.Count == 0,
      $"Generated command options are out of sync with the current Deno binary.\n" +
      $"Run `deno task generate` in tools/gen-commands/ and commit the result.\n\n" +
      string.Join('\n', diffs));
  }

  private static Dictionary<string, HashSet<string>> LoadSnapshot()
  {
    var json = File.ReadAllText(SnapshotPath);
    var root = JsonNode.Parse(json)!;
    var commands = root["commands"]!.AsObject();

    return commands.ToDictionary(
      kvp => kvp.Key,
      kvp => kvp.Value!.AsArray().Select(v => v!.GetValue<string>()).ToHashSet());
  }

  private static Dictionary<string, HashSet<string>> RunDenoJsonReference()
  {
    var denoPath = Helper.GetDenoPath();

    var psi = new ProcessStartInfo
    {
      FileName = denoPath,
      ArgumentList = { "json_reference" },
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true,
      StandardOutputEncoding = System.Text.Encoding.UTF8,
    };

    using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start deno process.");
    var output = proc.StandardOutput.ReadToEnd();

    if (!proc.WaitForExit(TimeSpan.FromSeconds(30)))
    {
      proc.Kill(entireProcessTree: true);
      throw new TimeoutException("deno json_reference did not exit within the expected time.");
    }

    if (proc.ExitCode != 0)
      throw new InvalidOperationException($"deno json_reference exited with code {proc.ExitCode}.");

    var skipFlags = new HashSet<string> { "config", "no-config", "help", "inspect", "inspect-brk", "inspect-wait", "inspect-publish-uid", "tunnel" };

    var root = JsonNode.Parse(output)!;
    var subcommands = root["subcommands"]!.AsArray();

    return subcommands
      .Where(s => s!["name"] is not null && s["args"] is not null)
      .ToDictionary(
        s => s!["name"]!.GetValue<string>(),
        s => s!["args"]!.AsArray()
          .Where(a => a!["long"] is not null && !skipFlags.Contains(a["long"]!.GetValue<string>()))
          .Where(a => a!["usage"] is not null && a["usage"]!.GetValue<string>().StartsWith('-'))
          .Select(a => a!["long"]!.GetValue<string>())
          .ToHashSet());
  }

  private static async Task<HashSet<string>> FetchPermissionFlags(CancellationToken cancellationToken)
  {
    var version = await GetDenoVersion(cancellationToken);
    var schemaUrl = $"https://raw.githubusercontent.com/denoland/deno/v{version}/cli/schemas/config-file.v1.json";
    var schemaJson = await HttpClient.GetStringAsync(schemaUrl, cancellationToken);
    using var schema = JsonDocument.Parse(schemaJson);

    if (!schema.RootElement.TryGetProperty("$defs", out var defs) ||
        !defs.TryGetProperty("permissionSet", out var permissionSet) ||
        !permissionSet.TryGetProperty("properties", out var permissionSetProps))
      throw new InvalidOperationException("Deno config schema is missing $defs.permissionSet.properties");

    var flags = new HashSet<string> { "allow-all", "no-prompt" };

    foreach (var prop in permissionSetProps.EnumerateObject())
    {
      if (prop.Name == "all") continue;

      flags.Add($"allow-{prop.Name}");
      flags.Add($"deny-{prop.Name}");
      if (FindRef(prop.Value).Contains("allowDenyIgnore"))
        flags.Add($"ignore-{prop.Name}");
    }

    if (flags.Count == 2)
      throw new InvalidOperationException("Deno config schema did not expose any permission types");

    return flags;
  }

  private static string FindRef(JsonElement obj)
  {
    if (obj.ValueKind != JsonValueKind.Object) return "";
    if (obj.TryGetProperty("$ref", out var refProp)) return refProp.GetString() ?? "";
    if (obj.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
      return string.Join('|', anyOf.EnumerateArray().Select(FindRef));
    if (obj.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array)
      return string.Join('|', oneOf.EnumerateArray().Select(FindRef));
    return "";
  }

  private static async Task<string> GetDenoVersion(CancellationToken cancellationToken)
  {
    var denoPath = Helper.GetDenoPath();
    var psi = new ProcessStartInfo
    {
      FileName = denoPath,
      ArgumentList = { "--version" },
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start deno process.");
    var output = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
    await proc.WaitForExitAsync(cancellationToken);

    var firstLine = output.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("deno ")) ??
      throw new InvalidOperationException($"Could not read Deno version. Output: {output}");
    var version = firstLine.Trim().Split(' ')[1];

    var plusIndex = version.IndexOf('+');
    return plusIndex > 0 ? version[..plusIndex] : version;
  }

  private static string FindSnapshotFile()
  {
    // Walk up from the test binary location to find the solution root, then locate the snapshot.
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && dir.GetFiles("*.sln").Length == 0)
      dir = dir.Parent;

    if (dir == null)
      throw new DirectoryNotFoundException("Solution root not found.");

    var path = Path.Combine(dir.FullName, "tools", "gen-commands", "deno_reference.snapshot.json");
    if (!File.Exists(path))
      throw new FileNotFoundException($"Snapshot not found at {path}. Run `deno task generate` in tools/gen-commands/.");

    return path;
  }

  private static string ToPascalCase(string s) =>
    string.Concat(s.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
