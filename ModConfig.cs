using SPTarkov.Server.Core.Models.Common;
using System.Collections.Generic;

#nullable enable
namespace BUMA_Revamped;

public class ModConfig
{
  public List<string> BotsToReplaceAmmoFor { get; set; } = new List<string>();

  public Dictionary<string, Dictionary<MongoId, double>> Ammo { get; set; } = new Dictionary<string, Dictionary<MongoId, double>>();
}
