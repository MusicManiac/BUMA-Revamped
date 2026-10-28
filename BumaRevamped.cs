using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotType = SPTarkov.Server.Core.Models.Eft.Common.Tables.BotType;

#nullable enable
namespace BUMA_Revamped;

[Injectable(InjectionType.Transient, 400050)]
public class BumaRevamped(
  BotTable botTable,
  ISptLogger<BumaRevamped> logger,
  ModHelper modHelper) : IOnLoad
{
  public Task OnLoadAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    Dictionary<string, BotType?> types = botTable.Types;
    ModConfig? config;
    try
    {
      config = modHelper.GetJsonDataFromFile<ModConfig>(Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "config"), "config.jsonc");
    }
    catch (Exception ex)
    {
      logger.Error("[BUMA-Revamped] Could not load config, mod changes will not apply. Please run your config through a validator if all bot types and ammo types are correct.", ex);
      return Task.CompletedTask;
    }

    if (config == null)
    {
      logger.Error("[BUMA-Revamped] Config file could not be loaded or is empty.");
      return Task.CompletedTask;
    }

    foreach (KeyValuePair<string, BotType?> keyValuePair1 in types)
    {
      if (keyValuePair1.Value != null && config.BotsToReplaceAmmoFor.Contains(keyValuePair1.Key))
      {
        foreach (KeyValuePair<string, Dictionary<MongoId, double>> keyValuePair2 in keyValuePair1.Value.BotInventory.Ammo.Where<KeyValuePair<string, Dictionary<MongoId, double>>>((Func<KeyValuePair<string, Dictionary<MongoId, double>>, bool>) (ammo => config.Ammo.ContainsKey(ammo.Key))))
          keyValuePair1.Value.BotInventory.Ammo[keyValuePair2.Key] = config.Ammo[keyValuePair2.Key];
      }
    }
    logger.Info($"[BUMA-Revamped] Changed the ammo pool on {config.BotsToReplaceAmmoFor.Count} bots. Enjoy getting your armor shredded, or just dying.");
    return Task.CompletedTask;
  }
}
