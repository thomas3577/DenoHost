using System.Text.RegularExpressions;
using DenoHost.Core;

if (args.Length != 1)
{
  Console.Error.WriteLine("Usage: dotnet run -- <expected-deno-version>");
  return 2;
}

var expectedVersion = args[0].Trim().TrimStart('v', 'V');
if (!Regex.IsMatch(expectedVersion, "^[0-9]+\\.[0-9]+\\.[0-9]+$"))
{
  Console.Error.WriteLine($"Expected semantic version like 2.8.3, got '{args[0]}'.");
  return 2;
}

string versionOutput;
try
{
  versionOutput = await Deno.Execute<string>("--version");
}
catch (Exception ex)
{
  Console.Error.WriteLine($"Failed to execute Deno runtime from package: {ex.Message}");
  return 1;
}

if (string.IsNullOrWhiteSpace(versionOutput))
{
  Console.Error.WriteLine("Deno version output was empty.");
  return 1;
}

var match = Regex.Match(versionOutput, @"\bdeno\s+([0-9]+\.[0-9]+\.[0-9]+)\b", RegexOptions.IgnoreCase);
if (!match.Success)
{
  Console.Error.WriteLine("Could not parse Deno version from output.");
  Console.Error.WriteLine(versionOutput);
  return 1;
}

var actualVersion = match.Groups[1].Value;
if (!string.Equals(actualVersion, expectedVersion, StringComparison.Ordinal))
{
  Console.Error.WriteLine($"Deno version mismatch. Expected {expectedVersion}, got {actualVersion}.");
  Console.Error.WriteLine(versionOutput);
  return 1;
}

Console.WriteLine($"verify-ok: deno {actualVersion}");
return 0;
