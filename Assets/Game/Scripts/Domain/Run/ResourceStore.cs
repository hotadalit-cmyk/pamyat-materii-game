// PM.Domain.Run — ресурсы и хранилище (v0.8). C# 9, без LINQ.
namespace PM.Domain.Run {
    public enum ResourceKind { Organic = 0, Chitin = 1, Slime = 2, Crystal = 3, Ember = 4, Psyche = 5 }

    public static class ResourceInfo {
        public const int Count = 6;

        public static string Name(ResourceKind k) {
            switch (k) {
                case ResourceKind.Organic: return "Органика";
                case ResourceKind.Chitin:  return "Хитин";
                case ResourceKind.Slime:   return "Слизь";
                case ResourceKind.Crystal: return "Кристалл";
                case ResourceKind.Ember:   return "Искра";
                case ResourceKind.Psyche:  return "Психея";
            }
            return k.ToString();
        }

        public static string Description(ResourceKind k) {
            switch (k) {
                case ResourceKind.Organic: return "Биомасса спор и простых существ — топливо роста и базовых узлов тела.";
                case ResourceKind.Chitin:  return "Панцири жуков и иглоспинов — основа защиты и охотничьих узлов.";
                case ResourceKind.Slime:   return "Слизь медуз и пиявок — топливо движения и деления.";
                case ResourceKind.Crystal: return "Осколки кристаллических тварей — питание чувств и эхолокации.";
                case ResourceKind.Ember:   return "Искры жаровых жуков — прокачка рывка и жаростойкости.";
                case ResourceKind.Psyche:  return "Психея светляков и душ — редкий ресурс высших узлов чувств.";
            }
            return "";
        }

        public static uint Color(ResourceKind k) {
            switch (k) {
                case ResourceKind.Organic: return 0x4CAF50u; // зелёный
                case ResourceKind.Chitin:  return 0xFFB300u; // янтарный
                case ResourceKind.Slime:   return 0x4FC3F7u; // голубой
                case ResourceKind.Crystal: return 0x26C6DAu; // циан
                case ResourceKind.Ember:   return 0xFF7043u; // оранжевый
                case ResourceKind.Psyche:  return 0xAB47BCu; // фиолетовый
            }
            return 0xFFFFFFu;
        }
    }

    public struct CostItem {
        public ResourceKind Kind;
        public double Amount;
        public CostItem(ResourceKind k, double a) { Kind = k; Amount = a; }
    }

    /// <summary>v0.10: Результат алхимии — для UI-всплеска «3 Хитин → 1 Психея».</summary>
    public sealed class TransmuteResult {
        public ResourceKind From;
        public ResourceKind To;
    }

    public sealed class Cost {
        public CostItem[] Items;
        public Cost(CostItem[] items) { Items = items; }
        public static Cost Of(params CostItem[] items) { return new Cost(items); }

        public override string ToString() {
            if (Items == null || Items.Length == 0) return "—";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Items.Length; i++) {
                if (i > 0) sb.Append(" · ");
                sb.Append(Fmt(Items[i].Amount)).Append(" ").Append(ResourceInfo.Name(Items[i].Kind));
            }
            return sb.ToString();
        }
        private static string Fmt(double v) {
            if (System.Math.Abs(v - System.Math.Round(v)) < 1e-9)
                return ((int)System.Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public sealed class ResourceStore {
        public const double BaseCapacity = 40.0;
        private readonly double[] _a = new double[ResourceInfo.Count];
        public double Capacity { get; set; } = BaseCapacity;

        public double Get(ResourceKind k) { return _a[(int)k]; }

        public double Add(ResourceKind k, double amount) {
            if (amount <= 0) return 0;
            int idx = (int)k;
            double room = Capacity - _a[idx];
            if (room <= 0) return 0;
            double add = amount < room ? amount : room;
            _a[idx] += add;
            return add;
        }

        public bool CanPay(Cost c) {
            if (c == null || c.Items == null || c.Items.Length == 0) return true;
            for (int i = 0; i < c.Items.Length; i++) {
                if (_a[(int)c.Items[i].Kind] + 1e-9 < c.Items[i].Amount) return false;
            }
            return true;
        }

        public bool Pay(Cost c) {
            if (c == null || c.Items == null || c.Items.Length == 0) return true;
            if (!CanPay(c)) return false;
            for (int i = 0; i < c.Items.Length; i++) {
                _a[(int)c.Items[i].Kind] -= c.Items[i].Amount;
            }
            return true;
        }

        public double[] ToArray() {
            var copy = new double[ResourceInfo.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = _a[i];
            return copy;
        }

        public static ResourceStore FromArray(double[] a, double capacity) {
            var s = new ResourceStore();
            s.Capacity = capacity;
            if (a != null) {
                for (int i = 0; i < s._a.Length && i < a.Length; i++) {
                    double v = a[i];
                    if (v < 0) v = 0;
                    if (v > capacity) v = capacity;
                    s._a[i] = v;
                }
            }
            return s;
        }
    }

    public static class YieldTable {
        public static CostItem[] For(string speciesId, double mass) {
            if (mass < 0) mass = 0;
            var l = new System.Collections.Generic.List<CostItem>();
            switch (speciesId) {
                case "spore":   // v0.9: споры дают и слизь — деление доступно с первой глубины
                    l.Add(new CostItem(ResourceKind.Organic, 0.8 * mass));
                    l.Add(new CostItem(ResourceKind.Slime, 0.5 * mass));
                    break;
                case "mite":
                    l.Add(new CostItem(ResourceKind.Chitin, 0.8 * mass));
                    l.Add(new CostItem(ResourceKind.Slime, 0.6 * mass));
                    break;
                case "beetle":
                    l.Add(new CostItem(ResourceKind.Chitin, 0.8 * mass));
                    l.Add(new CostItem(ResourceKind.Organic, 0.4 * mass));
                    break;
                case "jelly":
                case "leech":
                case "bubble_jelly":
                    l.Add(new CostItem(ResourceKind.Slime, 0.9 * mass));
                    l.Add(new CostItem(ResourceKind.Organic, 0.2 * mass));
                    break;
                case "crystal_mite":
                case "shard_crab":
                    l.Add(new CostItem(ResourceKind.Crystal, 0.8 * mass));
                    break;
                case "ember_beetle":
                case "salamander":
                    l.Add(new CostItem(ResourceKind.Ember, 0.8 * mass));
                    l.Add(new CostItem(ResourceKind.Chitin, 0.3 * mass));
                    break;
                case "glowworm":
                case "wisp":
                    l.Add(new CostItem(ResourceKind.Psyche, 0.5 * mass));
                    l.Add(new CostItem(ResourceKind.Organic, 0.3 * mass));
                    break;
                case "spikeback":
                    l.Add(new CostItem(ResourceKind.Chitin, 1.0 * mass));
                    break;
                // v0.15: новые виды
                case "cave_rat":
                    l.Add(new CostItem(ResourceKind.Organic, 0.7 * mass));
                    l.Add(new CostItem(ResourceKind.Slime, 0.3 * mass));
                    break;
                case "piranha":
                    l.Add(new CostItem(ResourceKind.Organic, 0.6 * mass));
                    l.Add(new CostItem(ResourceKind.Slime, 0.5 * mass));
                    break;
                case "bomb_toad":
                    l.Add(new CostItem(ResourceKind.Organic, 0.8 * mass));
                    l.Add(new CostItem(ResourceKind.Slime, 0.4 * mass));
                    break;
                case "deep_shark":
                    l.Add(new CostItem(ResourceKind.Chitin, 0.6 * mass));
                    l.Add(new CostItem(ResourceKind.Organic, 0.5 * mass));
                    break;
                case "soul_slime":
                    l.Add(new CostItem(ResourceKind.Psyche, 1.2 * mass));
                    l.Add(new CostItem(ResourceKind.Slime, 0.4 * mass));
                    break;
                case "warden":
                    l.Add(new CostItem(ResourceKind.Organic, 15));
                    l.Add(new CostItem(ResourceKind.Chitin,  15));
                    l.Add(new CostItem(ResourceKind.Slime,   15));
                    l.Add(new CostItem(ResourceKind.Crystal, 15));
                    l.Add(new CostItem(ResourceKind.Ember,   15));
                    l.Add(new CostItem(ResourceKind.Psyche,  15));
                    break;
                default:
                    l.Add(new CostItem(ResourceKind.Organic, 0.5 * mass));
                    break;
            }
            return l.ToArray();
        }
    }
}
