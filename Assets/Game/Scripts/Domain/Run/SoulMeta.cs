using System;
using System.Collections.Generic;
using PM.Domain.Core;
using PM.Persistence;
namespace PM.Domain.Run {

/// <summary>Характеристика души: энергия / выносливость / живучесть.</summary>
public enum SoulStat { Energy = 0, Endurance = 1, Vitality = 2 }

/// <summary>
/// Мета-прогресс между забегами. Сериализация через PM.Persistence.MiniJson;
/// FromJson устойчив к мусору и пустой строке (возвращает дефолтную душу).
/// </summary>
public sealed class SoulMeta {
    public const int MaxLevel = 5;
    private static readonly double[] LevelCosts = { 20, 35, 55, 80, 110 };

    public double Essence;
    public int Runs, BestDepth;
    public HashSet<string> KnownGenes = new HashSet<string>();   // гены, хоть раз поглощённые
    public HashSet<string> SeenSpecies = new HashSet<string>();  // виды, хоть раз поглощённые
    public HashSet<string> Codex = new HashSet<string>();        // v0.9: открытые записи энциклопедии (sp:/bi:/res:)
    public NemesisRegistry Nemeses = new NemesisRegistry();
    // v0.13: наследие прошлой жизни — снимок при смерти; сгорает при старте жизни без применения
    public LegacyPack Legacy;
    /// <summary>Сколько раз наследие было применено (за все жизни).</summary>
    public int LegacyUses;

    private readonly int[] _levels = new int[3];

    public int Level(SoulStat s) => _levels[(int)s];

    /// <summary>Стоимость апгрейда с текущего уровня: 20, 35, 55, 80, 110.</summary>
    public static double UpgradeCost(int currentLevel) {
        if (currentLevel < 0 || currentLevel >= MaxLevel) return double.MaxValue;
        return LevelCosts[currentLevel];
    }

    /// <summary>Списывает эссенцию и поднимает уровень; false — если нет эссенции или уровень максимальный.</summary>
    public bool TryUpgrade(SoulStat s) {
        int idx = (int)s;
        int lvl = _levels[idx];
        if (lvl >= MaxLevel) return false;
        double cost = UpgradeCost(lvl);
        if (Essence < cost) return false;
        Essence -= cost;
        _levels[idx] = lvl + 1;
        return true;
    }

    /// <summary>Energy: +1 заряд рывка на уровнях 2 и 4.</summary>
    public int BonusDashCharges {
        get {
            int e = _levels[(int)SoulStat.Energy];
            return (e >= 2 ? 1 : 0) + (e >= 4 ? 1 : 0);
        }
    }

    /// <summary>Endurance: стоимость рывка ×(1 − 0.08 × lvl).</summary>
    public double DashCostMult => 1.0 - 0.08 * _levels[(int)SoulStat.Endurance];

    /// <summary>Vitality: +0.6 массы к стартовой.</summary>
    public double StartMassBonus => 0.6 * _levels[(int)SoulStat.Vitality];

    /// <summary>
    /// Применить итог жизни: Essence += EssenceEarned, Runs++, BestDepth; KnownGenes пополняется
    /// из Mutations оттиска. Списка видов в LifeImprint нет — SeenPopulation обновлять нечем.
    /// </summary>
    public void ApplyLife(LifeImprint imprint) {
        if (imprint == null) return;
        Essence += imprint.EssenceEarned;
        Runs++;
        if (imprint.DepthReached > BestDepth) BestDepth = imprint.DepthReached;
        if (imprint.Mutations != null) {
            for (int i = 0; i < imprint.Mutations.Length; i++) {
                string gene = imprint.Mutations[i];
                if (!string.IsNullOrEmpty(gene)) KnownGenes.Add(gene);
            }
        }
    }

    /// <summary>
    /// v0.13: забрать наследие перед стартом новой жизни. Возвращает снимок и стирает его:
    /// наследие сгорает, если жизнь начата без применения (или применяется этим же вызовом в Unity).
    /// </summary>
    public LegacyPack TakeLegacy() { var p = Legacy; Legacy = null; return p; }

    public string ToJson() {
        var w = new MiniJson.Writer();
        w.BeginObject();
        w.Key("essence").Value(Essence);
        w.Key("runs").Value((long)Runs);
        w.Key("bestDepth").Value((long)BestDepth);
        w.Key("levels").BeginArray();
        for (int i = 0; i < 3; i++) w.Value((long)_levels[i]);
        w.EndArray();
        w.Key("knownGenes").BeginArray();
        foreach (string g in KnownGenes) w.Value(g ?? "");
        w.EndArray();
        w.Key("seenSpecies").BeginArray();
        foreach (string s in SeenSpecies) w.Value(s ?? "");
        w.EndArray();
        w.Key("codex").BeginArray();
        foreach (string c in Codex) w.Value(c ?? "");
        w.EndArray();
        w.Key("nemeses").BeginArray();
        if (Nemeses != null) {
            for (int i = 0; i < Nemeses.Records.Count; i++) {
                var rec = Nemeses.Records[i];
                w.BeginObject();
                w.Key("name").Value(rec.SoulName ?? "");
                w.Key("level").Value((long)rec.Level);
                w.Key("kills").Value((long)rec.KillsOfPlayer);
                w.Key("defeated").Value(rec.Defeated);
                if (string.IsNullOrEmpty(rec.StolenMutation)) w.Key("stolen").NullValue();
                else w.Key("stolen").Value(rec.StolenMutation);
                w.EndObject();
            }
        }
        w.EndArray();
        w.Key("legacyUses").Value((long)LegacyUses);
        if (Legacy != null) {
            w.Key("legacy").BeginObject();
            w.Key("mass").Value(Legacy.StartMassBonus);
            w.Key("res").BeginArray();
            for (int i = 0; i < Legacy.Resources.Length; i++) w.Value(Legacy.Resources[i]);
            w.EndArray();
            w.Key("nodes").BeginArray();
            if (Legacy.Nodes != null)
                for (int i = 0; i < Legacy.Nodes.Length; i++) w.Value(Legacy.Nodes[i] ?? "");
            w.EndArray();
            w.EndObject();
        }
        w.EndObject();
        return w.ToString();
    }

    /// <summary>Из JSON; мусор/пустая строка/null → дефолтная душа без исключений.</summary>
    public static SoulMeta FromJson(string json) {
        var meta = new SoulMeta();
        if (string.IsNullOrEmpty(json)) return meta;
        object root;
        try { root = MiniJson.Parse(json); } catch { return meta; }
        if (!(root is Dictionary<string, object> dict)) return meta;
        meta.Essence = AsDouble(At(dict, "essence"), 0.0);
        meta.Runs = AsInt(At(dict, "runs"), 0);
        meta.BestDepth = AsInt(At(dict, "bestDepth"), 0);
        if (At(dict, "levels") is List<object> levels) {
            for (int i = 0; i < 3 && i < levels.Count; i++) {
                int lvl = AsInt(levels[i], 0);
                if (lvl < 0) lvl = 0;
                if (lvl > MaxLevel) lvl = MaxLevel;
                meta._levels[i] = lvl;
            }
        }
        meta.KnownGenes = AsSet(At(dict, "knownGenes"));
        meta.SeenSpecies = AsSet(At(dict, "seenSpecies"));
        meta.Codex = AsSet(At(dict, "codex"));
        meta.LegacyUses = AsInt(At(dict, "legacyUses"), 0);
        if (At(dict, "legacy") is Dictionary<string, object> lg) {
            var pack = new LegacyPack();
            double mass = AsDouble(At(lg, "mass"), 0.0);
            if (double.IsNaN(mass) || double.IsInfinity(mass) || mass < 0.0) mass = 0.0;
            if (mass > LegacyPack.MassCap) mass = LegacyPack.MassCap;
            pack.StartMassBonus = mass;
            if (At(lg, "res") is List<object> res)
                for (int i = 0; i < res.Count && i < pack.Resources.Length; i++) {
                    double v = AsDouble(res[i], 0.0);
                    pack.Resources[i] = double.IsNaN(v) || double.IsInfinity(v) || v < 0.0 ? 0.0 : v;
                }
            if (At(lg, "nodes") is List<object> nodes) {
                var nl = new List<string>();
                for (int i = 0; i < nodes.Count; i++) {
                    string s = nodes[i] as string;
                    if (string.IsNullOrEmpty(s) || s == LegacyPack.ExcludedNode) continue;
                    if (EvolutionTree.Get(s) != null) nl.Add(s);   // только существующие узлы
                }
                pack.Nodes = nl.ToArray();
            }
            meta.Legacy = pack;
        }
        if (At(dict, "nemeses") is List<object> records) {
            meta.Nemeses = new NemesisRegistry();
            for (int i = 0; i < records.Count; i++) {
                if (!(records[i] is Dictionary<string, object> rd)) continue;
                var rec = new NemesisRecord {
                    SoulName = AsString(At(rd, "name")),
                    Level = AsInt(At(rd, "level"), 0),
                    KillsOfPlayer = AsInt(At(rd, "kills"), 0),
                    Defeated = AsBool(At(rd, "defeated")),
                    StolenMutation = AsString(At(rd, "stolen")),
                };
                if (!string.IsNullOrEmpty(rec.SoulName)) meta.Nemeses.Records.Add(rec);
            }
        }
        return meta;
    }

    private static object At(Dictionary<string, object> dict, string key) {
        dict.TryGetValue(key, out object v);
        return v;
    }
    private static double AsDouble(object v, double def) => v is double d ? d : def;
    private static int AsInt(object v, int def) {
        double d = AsDouble(v, def);
        if (double.IsNaN(d) || double.IsInfinity(d)) return def;
        if (d < 0) d = 0;
        return (int)d;
    }
    private static bool AsBool(object v) => v is bool b && b;
    private static string AsString(object v) => v as string;
    private static HashSet<string> AsSet(object v) {
        var set = new HashSet<string>();
        if (v is List<object> list)
            for (int i = 0; i < list.Count; i++) {
                string s = list[i] as string;
                if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
        return set;
    }
}
}
