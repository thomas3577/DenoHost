using System;

namespace DenoWrapper;

public class DenoExecuteBaseOptions
{
  public string? WorkingDirectory { get; set; }
}

public class DenoExecuteOptions : DenoExecuteBaseOptions
{
  public string Command { get; set; } = string.Empty;
  public string? ConfigOrPath { get; set; }
  public DenoConfig? Config { get; set; }
  public string[] Args { get; set; } = [];
}
