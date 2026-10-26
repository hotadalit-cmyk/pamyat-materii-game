using System.Collections.Generic;
using PM.Domain.Core;
namespace PM.Domain.Run {

/// <summary>Запись о немезиде: ИИ-душа, убившая игрока, и её счёт.</summary>
public sealed class NemesisRecord {
    public string SoulName;
    public int Level;
    public int KillsOfPlayer;
    public bool Defeated;
    public string StolenMutation;
}

/// <summary>Реестр немезид (мета-прогресс). Без LINQ.</summary>
public sealed class NemesisRegistry {
    public const double SpawnChance = 0.35;

    private static readonly string[] SoulNames = { "Искра", "Осколок", "Эхо", "Тлен", "Капля", "Шёпот" };

    public List<NemesisRecord> Records = new List<NemesisRecord>();

    /// <summary>Имя ИИ-души из канонического набора; предпочитает ещё не занятые.</summary>
    public string PickSoulName(PmRandom rng) {
        if (rng == null) return SoulNames[0];
        var unused = new List<string>(SoulNames.Length);
        for (int i = 0; i < SoulNames.Length; i++)
            if (Find(SoulNames[i]) == null) unused.Add(SoulNames[i]);
        string[] pool = unused.Count > 0 ? unused.ToArray() : SoulNames;
        return pool[rng.NextInt(pool.Length)];
    }

    /// <summary>Душа убила игрока: Level++, KillsOfPlayer++, крадёт случайную мутацию игрока.</summary>
    public void OnPlayerKilledBy(string soulName, string[] playerMutations, PmRandom rng) {
        if (string.IsNullOrEmpty(soulName)) return;
        var r = Find(soulName);
        if (r == null) {
            r = new NemesisRecord { SoulName = soulName, Level = 0, KillsOfPlayer = 0 };
            Records.Add(r);
        }
        r.Level++;
        r.KillsOfPlayer++;
        r.StolenMutation = StealMutation(playerMutations, rng);
    }

    /// <summary>Игрок поглотил душу: Defeated = true.</summary>
    public void OnSoulAbsorbed(string soulName) {
        var r = Find(soulName);
        if (r != null) r.Defeated = true;
    }

    /// <summary>Живой немезида с шансом 35% (не вызывается для комнаты 0 глубины 1).</summary>
    public NemesisRecord PickForRoom(int depth, PmRandom rng) {
        if (rng == null) return null;
        var alive = new List<NemesisRecord>(Records.Count);
        for (int i = 0; i < Records.Count; i++)
            if (!Records[i].Defeated) alive.Add(Records[i]);
        if (alive.Count == 0) return null;
        if (rng.NextDouble01() >= SpawnChance) return null;
        return alive[rng.NextInt(alive.Count)];
    }

    /// <summary>Масса немезиды: playerMass × (1.1 + 0.15 × Level).</summary>
    public static double NemesisMass(NemesisRecord r, double playerMass) {
        int level = r != null ? r.Level : 0;
        return playerMass * (1.1 + 0.15 * level);
    }

    /// <summary>Запись немезиды по имени; null, если такой души нет. Для мести и возврата мутации.</summary>
    public NemesisRecord FindRecord(string soulName) => Find(soulName);

    /// <summary>
    /// Эссенция мести: немезида убивала вас KillsOfPlayer раз — за её поглощение вы получаете
    /// 5 × KillsOfPlayer эссенции (награда за реванш). 0, если душа вас не убивала.
    /// </summary>
    public static double VengeanceEssence(NemesisRecord r) {
        if (r == null || r.KillsOfPlayer <= 0) return 0.0;
        return 5.0 * r.KillsOfPlayer;
    }

    /// <summary>Мутация, украденная немезидой у игрока. При мести можно вернуть её (один стак на забег).</summary>
    public static string ReclaimableMutation(NemesisRecord r) {
        if (r == null || string.IsNullOrEmpty(r.StolenMutation)) return null;
        return r.StolenMutation;
    }

    private NemesisRecord Find(string soulName) {
        if (soulName == null) return null;
        for (int i = 0; i < Records.Count; i++)
            if (Records[i].SoulName == soulName) return Records[i];
        return null;
    }

    private static string StealMutation(string[] playerMutations, PmRandom rng) {
        if (playerMutations == null || playerMutations.Length == 0 || rng == null) return null;
        for (int attempt = 0; attempt < 4; attempt++) {
            string pick = playerMutations[rng.NextInt(playerMutations.Length)];
            if (!string.IsNullOrEmpty(pick)) return pick;
        }
        return null;
    }
}
}
