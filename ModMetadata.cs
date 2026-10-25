using SPTarkov.Server.Core.Models.Spt.Mod;
using System.Collections.Generic;
using Version = SemanticVersioning.Version;
using Range = SemanticVersioning.Range;

#nullable enable
namespace BUMA_Revamped;

public record ModMetadata : IModMetadata
{
  public string ModGuid { get; init; } = "com.papershredder432.BumaRevamped2";

  public string Name { get; init; } = "BUMA Revamped";

  public string Author { get; init; } = "papershredder432";

  public List<string>? Contributors { get; init; } = new()
  {
    "MusicManiac",
    "Mattdokn_",
    "egbog",
    "wozzy"
  };

  public Version Version { get; init; } = new("2.0.2");

  public Range SptVersion { get; init; } = new("~4.1.0");

  public List<string>? Incompatibilities { get; init; }

  public Dictionary<string, Range>? ModDependencies { get; init; }

  public string? Url { get; init; }

  public bool HasPrepatcher { get; init; } = false;

  public string License { get; init; } = "GNU GPLv3";
}
