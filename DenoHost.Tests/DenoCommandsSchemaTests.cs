using System.Diagnostics;
using System.Text.Json.Nodes;
using DenoHost.Core;

namespace DenoHost.Tests;

/// <summary>
/// Validates that the generated command options classes stay in sync with the installed Deno binary.
/// When this test fails, run `deno task generate` in tools/gen-commands/ and commit the result.
/// </summary>
public class DenoCommandsSchemaTests
{
  private static readonly string SnapshotPath = FindSnapshotFile();

  [Fact]
  public void GeneratedOptions_MatchCurrentDenoJsonReference()
  {
    var snapshot = LoadSnapshot();
    var current = RunDenoJsonReference();

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
    proc.WaitForExit();

    if (proc.ExitCode != 0)
      throw new InvalidOperationException($"deno json_reference exited with code {proc.ExitCode}.");

    var skipFlags = new HashSet<string> { "config", "no-config", "help", "inspect", "inspect-brk", "inspect-wait", "inspect-publish-uid", "tunnel" };

    var root = JsonNode.Parse(output)!;
    var subcommands = root["subcommands"]!.AsArray();

    return subcommands
      .Where(s => s!["name"] is not null)
      .ToDictionary(
        s => s!["name"]!.GetValue<string>(),
        s => s!["args"]!.AsArray()
          .Where(a => a!["long"] is not null && !skipFlags.Contains(a["long"]!.GetValue<string>()))
          .Where(a => a!["usage"]!.GetValue<string>().StartsWith('-'))
          .Select(a => a!["long"]!.GetValue<string>())
          .ToHashSet());
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
