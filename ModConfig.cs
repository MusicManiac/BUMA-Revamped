using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;

namespace BUMA_Revamped;

public sealed class ModConfig
{
    public bool AutoDetectAmmo { get; set; } = true;

    public int PenetrationThreshold { get; set; } = 40;

    public Dictionary<int, List<double>> AutoDetectWeights { get; set; } = new()
    {
        [2] = [3, 7],
        [3] = [3, 4, 3],
        [4] = [2, 3, 3, 2],
        [5] = [2, 2, 2, 3, 1],
        [6] = [2, 2, 3, 1, 1, 1]
    };

    public List<string> BotsToReplaceAmmoFor { get; set; } = [];

    public Dictionary<string, Dictionary<MongoId, double>> Ammo { get; set; } = [];
}
