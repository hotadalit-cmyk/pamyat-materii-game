// PM.Domain.Run — прокачка показателей за ресурсы (v0.9). C# 9, без LINQ.
// Показатели качаются напрямую, уровень за уровнем; эволюция даёт к ним бонусы и открывает новые показатели.
using System.Collections.Generic;

namespace PM.Domain.Run {
    public enum TrainBlock { Ok = 0, Unknown = 1, Locked = 2, Maxed = 3, NoResources = 4 }

    /// <summary>Прокачиваемый показатель: эффект за уровень, цена первого уровня, кем открывается.</summary>
    public sealed class TrainDef {
        public string Key, Name, Description, UnitFormat;
        public EvoEffectKind Kind;
        public double PerLevel;
        public CostItem[] BaseCost;
        /// <summary>null — открыт с начала; иначе — любой из этих узлов эволюции.</summary>
        public string[] UnlockedBy;
        public int MaxLevel = StatTraining.MaxLevel;
    }

    public static class TrainCatalog {
        private static readonly TrainDef[] _all = {
            // открыты сразу
            Def("strength", "Сила", "Урон при ударе и рывке.", EvoEffectKind.DamageDealtMult, 0.08, "+{0:0}% урона", null,
                C(ResourceKind.Chitin, 3), C(ResourceKind.Organic, 3)),
            Def("toughness", "Стойкость", "Меньше теряете массы от ударов, шипов и жара.", EvoEffectKind.DamageTakenMult, -0.05, "−{0:0}% урона по вам", null,
                C(ResourceKind.Chitin, 5)),
            Def("agility", "Ловкость", "Рывок быстрее и дальше; ползание — тоже.", EvoEffectKind.DashSpeedMult, 0.05, "+{0:0}% скорости", null,
                C(ResourceKind.Slime, 5)),
            Def("appetite", "Аппетит", "Больше массы с каждого поглощения.", EvoEffectKind.MassGainMult, 0.06, "+{0:0}% массы", null,
                C(ResourceKind.Organic, 6)),
            // открываются эволюцией
            Def("vitality", "Живучесть", "Масса понемногу восстанавливается сама.", EvoEffectKind.RegenFlat, 0.015, "+{0:0.###} массы/с",
                new[] { "body_regrow", "body_thick_skin" }, C(ResourceKind.Organic, 4), C(ResourceKind.Slime, 2)),
            Def("sight", "Зрение", "Шире круг света в темноте пещеры.", EvoEffectKind.LightFlat, 0.35, "+{0:0.##} к свету",
                new[] { "senses_eyes", "senses_light" }, C(ResourceKind.Crystal, 4), C(ResourceKind.Psyche, 1)),
            Def("pull", "Притяжение", "Еда и споры тянутся к вам издалека.", EvoEffectKind.PullFlat, 0.25, "+{0:0.##} к притяжению",
                new[] { "senses_magnet", "senses_echo" }, C(ResourceKind.Crystal, 3), C(ResourceKind.Slime, 3)),
            Def("harvest", "Добыча", "Больше ресурсов с каждой жертвы.", EvoEffectKind.YieldMult, 0.06, "+{0:0}% ресурсов",
                new[] { "hunt_root", "div_storage" }, C(ResourceKind.Organic, 4), C(ResourceKind.Chitin, 3)),
            Def("division", "Дробление", "Можно делиться при меньшей массе.", EvoEffectKind.SplitMinMass, -0.04, "−{0:0.##} к порогу деления",
                new[] { "div_split_basics" }, C(ResourceKind.Slime, 6)),
        };

        private static CostItem C(ResourceKind k, double a) => new CostItem(k, a);

        private static TrainDef Def(string key, string name, string desc, EvoEffectKind kind, double per, string unit,
                                    string[] unlockedBy, params CostItem[] cost) =>
            new TrainDef { Key = key, Name = name, Description = desc, Kind = kind, PerLevel = per, UnitFormat = unit,
                           UnlockedBy = unlockedBy, BaseCost = cost };

        public static TrainDef[] All() { var c = new TrainDef[_all.Length]; System.Array.Copy(_all, c, c.Length); return c; }

        public static TrainDef Get(string key) {
            for (int i = 0; i < _all.Length; i++) if (_all[i].Key == key) return _all[i];
            return null;
        }

        /// <summary>Показатели, которые открывает узел эволюции (для подписи в дереве).</summary>
        public static List<TrainDef> UnlockedByNode(string nodeId) {
            var l = new List<TrainDef>();
            for (int i = 0; i < _all.Length; i++)
                if (_all[i].UnlockedBy != null && System.Array.IndexOf(_all[i].UnlockedBy, nodeId) >= 0) l.Add(_all[i]);
            return l;
        }
    }

    public sealed class StatTraining {
        public const int MaxLevel = 10;
        public const double CostGrowth = 1.22;  // +22% за уровень: 10-й уровень помещается в базовое хранилище (40)
        private readonly Dictionary<string, int> _lv = new Dictionary<string, int>();

        public int Level(string key) => _lv.TryGetValue(key, out var v) ? v : 0;

        /// <summary>Сумма эффекта всех прокачанных показателей данного вида.</summary>
        public double Sum(EvoEffectKind k) {
            double t = 0;
            foreach (var kv in _lv) {
                var d = TrainCatalog.Get(kv.Key);
                if (d != null && d.Kind == k) t += d.PerLevel * kv.Value;
            }
            return t;
        }

        /// <summary>Эффект показателя на уровне level — для подписи «сейчас / на следующем уровне».</summary>
        public static double Effect(TrainDef d, int level) => d.PerLevel * level;

        public static Cost CostFor(TrainDef d, int level) {
            var items = new CostItem[d.BaseCost.Length];
            double k = System.Math.Pow(CostGrowth, level);
            for (int i = 0; i < items.Length; i++)
                items[i] = new CostItem(d.BaseCost[i].Kind, System.Math.Ceiling(d.BaseCost[i].Amount * k));
            return new Cost(items);
        }

        public static bool IsUnlocked(TrainDef d, EvolutionState e) {
            if (d.UnlockedBy == null) return true;
            if (e == null) return false;
            for (int i = 0; i < d.UnlockedBy.Length; i++) if (e.Unlocked.Contains(d.UnlockedBy[i])) return true;
            return false;
        }

        public TrainBlock CanTrain(string key, ResourceStore store, EvolutionState e) {
            var d = TrainCatalog.Get(key);
            if (d == null) return TrainBlock.Unknown;
            if (!IsUnlocked(d, e)) return TrainBlock.Locked;
            int lv = Level(key);
            if (lv >= d.MaxLevel) return TrainBlock.Maxed;
            if (store == null || !store.CanPay(CostFor(d, lv))) return TrainBlock.NoResources;
            return TrainBlock.Ok;
        }

        public bool TryTrain(string key, ResourceStore store, EvolutionState e) {
            if (CanTrain(key, store, e) != TrainBlock.Ok) return false;
            var d = TrainCatalog.Get(key);
            if (!store.Pay(CostFor(d, Level(key)))) return false;
            _lv[key] = Level(key) + 1;
            return true;
        }

        public static string BlockText(TrainBlock b) {
            switch (b) {
                case TrainBlock.Locked: return "Откроется эволюцией";
                case TrainBlock.Maxed: return "Максимум";
                case TrainBlock.NoResources: return "Не хватает ресурсов";
                case TrainBlock.Unknown: return "Неизвестный показатель";
            }
            return "";
        }
    }
}
