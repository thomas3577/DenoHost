using System;

namespace DenoWrapper;

public class DenoExecuteOptions
{
  public string? WorkingDirectory { get; set; }
  public string Command { get; set; } = string.Empty;
  public bool ExpectResult { get; set; } = true;
  public string? ConfigOrPath { get; set; }
  public DenoConfig? Config { get; set; }
  public string[] Args { get; set; } = Array.Empty<string>();
}
