using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using BotType = SPTarkov.Server.Core.Models.Eft.Common.Tables.BotType;

namespace BUMA_Revamped;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 10000)]
public sealed class BumaRevamped(
    BotTable botTable,
    TemplateTable templateTable,
    ISptLogger<BumaRevamped> logger,
    ModHelper modHelper) : IOnLoad
{
    private static readonly MongoId AmmoCategory = new("5485a8684bdc2da71d8b4567");
    private static Dictionary<string, Dictionary<MongoId, double>>? activePools;
    private static HashSet<string>? activeBotRoles;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ModConfig? config;
        try
        {
            string configPath = System.IO.Path.Combine(
                modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "config");
            config = modHelper.GetJsonDataFromFile<ModConfig>(configPath, "config.jsonc");
        }
        catch (Exception ex)
        {
            logger.Error("[BUMA-Revamped] Could not load config; ammo pools were not changed.", ex);
            return Task.CompletedTask;
        }

        if (config is null)
        {
            logger.Error("[BUMA-Revamped] Config file could not be loaded or is empty.");
            return Task.CompletedTask;
        }

        Dictionary<string, Dictionary<MongoId, double>> pools;
        if (config.AutoDetectAmmo)
        {
            if (!ValidateWeights(config))
            {
                return Task.CompletedTask;
            }

            pools = DiscoverAmmo(config, cancellationToken);
        }
        else
        {
            pools = config.Ammo ?? [];
        }

        int changedBots = 0;
        int changedCalibers = 0;
        HashSet<string> selectedBots = new(config.BotsToReplaceAmmoFor ?? [], StringComparer.OrdinalIgnoreCase);
        foreach ((string botName, BotType? bot) in botTable.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!selectedBots.Contains(botName) || bot is null)
            {
                continue;
            }

            int botCalibers = 0;
            foreach (string caliber in bot.BotInventory.Ammo.Keys.ToArray())
            {
                if (pools.TryGetValue(caliber, out Dictionary<MongoId, double>? pool) && pool.Count > 0)
                {
                    bot.BotInventory.Ammo[caliber] = new Dictionary<MongoId, double>(pool);
                    botCalibers++;
                }
            }

            if (botCalibers > 0)
            {
                changedBots++;
                changedCalibers += botCalibers;
            }
        }

        logger.Info($"[BUMA-Revamped] {(config.AutoDetectAmmo ? "Auto" : "Manual")} mode changed "
            + $"{changedCalibers} caliber pools across {changedBots} bots.");

        activePools = pools;
        activeBotRoles = selectedBots;
        PatchProgressiveBotSystem();
        return Task.CompletedTask;
    }

    private void PatchProgressiveBotSystem()
    {
        Type? helperType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ProgressiveBotSystem.Helpers.BotEquipmentHelper"))
            .FirstOrDefault(type => type is not null);
        if (helperType is null)
        {
            return;
        }

        try
        {
            MethodInfo? original = helperType.GetMethod(
                "GetAmmoByBotRole",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                [typeof(string), typeof(int)],
                null);
            MethodInfo? postfix = typeof(BumaRevamped).GetMethod(
                nameof(ReplaceProgressiveBotAmmo), BindingFlags.Static | BindingFlags.NonPublic);
            if (original is null || postfix is null)
            {
                logger.Error("[BUMA-Revamped] Progressive Bot System ammo lookup was not found.");
                return;
            }

            new Harmony("com.musicmaniac.bumarevamped.apbs").Patch(
                original, postfix: new HarmonyMethod(postfix));
            logger.Info("[BUMA-Revamped] Patched Progressive Bot System's per-bot ammo lookup.");
        }
        catch (Exception ex)
        {
            logger.Error("[BUMA-Revamped] Could not patch Progressive Bot System ammo lookup.", ex);
        }
    }

    private static void ReplaceProgressiveBotAmmo(
        string __0,
        ref Dictionary<string, Dictionary<MongoId, double>> __result)
    {
        if (__result is null || activePools is null || activeBotRoles is null
            || !activeBotRoles.Contains(__0))
        {
            return;
        }

        Dictionary<string, Dictionary<MongoId, double>> replacement =
            new(__result, StringComparer.OrdinalIgnoreCase);
        foreach ((string caliber, Dictionary<MongoId, double> pool) in activePools)
        {
            if (replacement.ContainsKey(caliber) && pool.Count > 0)
            {
                replacement[caliber] = new Dictionary<MongoId, double>(pool);
            }
        }

        __result = replacement;
    }

    private bool ValidateWeights(ModConfig config)
    {
        if (config.AutoDetectWeights is null)
        {
            logger.Error("[BUMA-Revamped] AutoDetectWeights is missing.");
            return false;
        }

        for (int count = 2; count <= 6; count++)
        {
            if (!config.AutoDetectWeights.TryGetValue(count, out List<double>? weights)
                || weights is null
                || weights.Count != count
                || weights.Any(weight => !double.IsFinite(weight) || weight <= 0))
            {
                logger.Error($"[BUMA-Revamped] AutoDetectWeights[{count}] must contain "
                    + $"{count} positive weights, ordered highest to lowest penetration.");
                return false;
            }
        }

        return true;
    }

    private Dictionary<string, Dictionary<MongoId, double>> DiscoverAmmo(
        ModConfig config, CancellationToken cancellationToken)
    {
        Dictionary<string, List<(MongoId Id, int Penetration)>> byCaliber =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (TemplateItem item in templateTable.Items.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(item.Type, "Item", StringComparison.OrdinalIgnoreCase)
                || item.Properties?.Caliber is not string caliber
                || string.IsNullOrWhiteSpace(caliber)
                || item.Properties.PenetrationPower is not int penetration
                || !IsAmmo(item))
            {
                continue;
            }

            if (!byCaliber.TryGetValue(caliber, out List<(MongoId Id, int Penetration)>? rounds))
            {
                rounds = [];
                byCaliber.Add(caliber, rounds);
            }

            rounds.Add((item.Id, penetration));
        }

        Dictionary<string, Dictionary<MongoId, double>> pools =
            new(StringComparer.OrdinalIgnoreCase);
        foreach ((string caliber, List<(MongoId Id, int Penetration)> rounds) in byCaliber)
        {
            List<(MongoId Id, int Penetration)> ranked = rounds
                .OrderByDescending(round => round.Penetration)
                .ThenBy(round => round.Id.ToString(), StringComparer.Ordinal)
                .ToList();
            List<(MongoId Id, int Penetration)> eligible = ranked
                .Where(round => round.Penetration > config.PenetrationThreshold)
                .Take(6)
                .ToList();

            if (eligible.Count <= 1)
            {
                pools[caliber] = new Dictionary<MongoId, double> { [ranked[0].Id] = 1 };
                continue;
            }

            List<double> weights = config.AutoDetectWeights[eligible.Count];
            Dictionary<MongoId, double> pool = new();
            for (int index = 0; index < eligible.Count; index++)
            {
                pool.Add(eligible[index].Id, weights[index]);
            }

            pools[caliber] = pool;
        }

        logger.Info($"[BUMA-Revamped] Auto-detected ammo pools for {pools.Count} calibers.");
        return pools;
    }

    private bool IsAmmo(TemplateItem item)
    {
        HashSet<MongoId> visited = [];
        MongoId parentId = item.Parent;
        while (visited.Add(parentId))
        {
            if (parentId == AmmoCategory)
            {
                return true;
            }

            if (!templateTable.Items.TryGetValue(parentId, out TemplateItem? parent))
            {
                break;
            }

            parentId = parent.Parent;
        }

        return false;
    }
}
