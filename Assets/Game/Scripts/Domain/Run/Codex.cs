// PM.Domain.Run — энциклопедия мира (v0.9). C# 9, без LINQ.
// Записи собираются из каталогов игры (виды, области, ресурсы) + литературные тексты (lore, JSON).
// Запись открывается, когда игрок впервые встретил её в игре; ключи хранятся в SoulMeta.Codex.
using System.Collections.Generic;
using PM.Persistence;

namespace PM.Domain.Run {
    public enum CodexKind { Species = 0, Biome = 1, Resource = 2 }

    public sealed class CodexEntry {
        public string Key;          // sp:mite, bi:fungal, res:Slime
        public CodexKind Kind;
        public string Id;
        public string Name;
        public string Epithet;      // короткий эпитет под именем
        public string Lore;         // художественный текст
        public string[] Facts;      // «Где живёт», «Что даёт», «Ген» — из каталогов
        public string Tip;          // как с ним справиться (Bestiary)
        public string ArtKey;       // AiArt/<ArtKey>
    }

    public static class Codex {
        public static string World = "Под землёй, где не бывает солнца, материя помнит всё, что когда-либо поглотила.";
        public static string Hero = "Капля живой слизи. Каждая жизнь коротка, но память о ней остаётся.";
        private static readonly Dictionary<string, string> _lore = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _epithet = new Dictionary<string, string>();

        public static string SpeciesKey(string id) => "sp:" + id;
        public static string BiomeKey(BiomeId id) => "bi:" + BiomeArtId(id);
        public static string ResourceKey(ResourceKind k) => "res:" + k;
        public static string BiomeArtId(BiomeId id) => id.ToString().ToLowerInvariant();

        /// <summary>
        /// Тексты из lore JSON: {world, hero, species:{id:{title,lore}}, biomes:{id:{lore}}, resources:{Kind:text}}.
        /// Повреждённый JSON не ломает игру — остаются тексты по умолчанию. Возвращает число загруженных текстов.
        /// </summary>
        public static int LoadLore(string json) {
            _lore.Clear(); _epithet.Clear();
            if (string.IsNullOrEmpty(json)) return 0;
            object root;
            try { root = MiniJson.Parse(json); } catch (System.Exception) { return 0; }
            if (!(root is Dictionary<string, object> d)) return 0;
            int n = 0;
            if (d.TryGetValue("world", out var w) && w is string ws) { World = ws; n++; }
            if (d.TryGetValue("hero", out var h) && h is string hs) { Hero = hs; n++; }
            if (d.TryGetValue("species", out var sp) && sp is Dictionary<string, object> spd)
                foreach (var kv in spd)
                    if (kv.Value is Dictionary<string, object> e) {
                        if (e.TryGetValue("lore", out var l) && l is string ls) { _lore["sp:" + kv.Key] = ls; n++; }
                        if (e.TryGetValue("title", out var t) && t is string ts) _epithet["sp:" + kv.Key] = ts;
                    }
            if (d.TryGetValue("biomes", out var bi) && bi is Dictionary<string, object> bid)
                foreach (var kv in bid)
                    if (kv.Value is Dictionary<string, object> e && e.TryGetValue("lore", out var l) && l is string ls) { _lore["bi:" + kv.Key] = ls; n++; }
            if (d.TryGetValue("resources", out var rs) && rs is Dictionary<string, object> rsd)
                foreach (var kv in rsd)
                    if (kv.Value is string ls) { _lore["res:" + kv.Key] = ls; n++; }
            return n;
        }

        public static List<CodexEntry> All(CodexKind kind) {
            var l = new List<CodexEntry>();
            if (kind == CodexKind.Species) foreach (var s in SpeciesCatalog.All()) l.Add(ForSpecies(s.Id));
            else if (kind == CodexKind.Biome) foreach (var b in BiomeCatalog.All()) l.Add(ForBiome(b));
            else for (int i = 0; i < ResourceInfo.Count; i++) l.Add(ForResource((ResourceKind)i));
            return l;
        }

        public static CodexEntry ForSpecies(string id) {
            var s = SpeciesCatalog.Get(id);
            string key = SpeciesKey(id);
            var facts = new List<string>();
            var homes = new List<string>();
            foreach (var b in BiomeCatalog.All())
                if (b.SpeciesPool != null && System.Array.IndexOf(b.SpeciesPool, id) >= 0) homes.Add(b.Name);
            if (homes.Count > 0) facts.Add("Где живёт: " + string.Join(", ", homes));
            facts.Add("Нрав: " + BehaviorText(s != null ? s.Behavior : CreatureBehavior.Drift));
            var y = YieldTable.For(id, 1.0);
            var res = new List<string>();
            for (int i = 0; i < y.Length; i++) res.Add(ResourceInfo.Name(y[i].Kind));
            if (res.Count > 0) facts.Add("Что даёт: " + string.Join(", ", res));
            if (s != null && !string.IsNullOrEmpty(s.GeneMutationId)) {
                var m = MutationCatalog.Get(s.GeneMutationId);
                facts.Add("Ген: " + (m != null ? m.Name : s.GeneMutationId) + " — открывает узел эволюции");
            }
            return new CodexEntry {
                Key = key, Kind = CodexKind.Species, Id = id, Name = s != null ? s.Name : id,
                Epithet = _epithet.TryGetValue(key, out var ep) ? ep : "",
                Lore = _lore.TryGetValue(key, out var lo) ? lo : "",
                Facts = facts.ToArray(), Tip = Bestiary.Describe(id), ArtKey = "portrait_" + id
            };
        }

        public static CodexEntry ForBiome(BiomeDef b) {
            string key = BiomeKey(b.Id);
            var facts = new List<string> { "Глубина " + b.Depth };
            if (b.SpeciesPool != null) {
                var names = new List<string>();
                foreach (var id in b.SpeciesPool) { var s = SpeciesCatalog.Get(id); if (s != null) names.Add(s.Name); }
                facts.Add("Обитатели: " + string.Join(", ", names));
            }
            return new CodexEntry {
                Key = key, Kind = CodexKind.Biome, Id = BiomeArtId(b.Id), Name = b.Name, Epithet = b.Subtitle,
                Lore = _lore.TryGetValue(key, out var lo) ? lo : "", Facts = facts.ToArray(), Tip = "",
                ArtKey = "biome_" + BiomeArtId(b.Id)
            };
        }

        public static CodexEntry ForResource(ResourceKind k) {
            string key = ResourceKey(k);
            var from = new List<string>();
            foreach (var s in SpeciesCatalog.All()) {
                var y = YieldTable.For(s.Id, 1.0);
                for (int i = 0; i < y.Length; i++) if (y[i].Kind == k) { from.Add(s.Name); break; }
            }
            return new CodexEntry {
                Key = key, Kind = CodexKind.Resource, Id = k.ToString(), Name = ResourceInfo.Name(k), Epithet = "Ресурс",
                Lore = _lore.TryGetValue(key, out var lo) ? lo : "",
                Facts = new[] { ResourceInfo.Description(k), "Добывается из: " + (from.Count > 0 ? string.Join(", ", from) : "—") },
                Tip = "", ArtKey = null
            };
        }

        /// <summary>Открыта ли запись: встречена в игре (или вид поглощён в прошлых жизнях — до появления энциклопедии).</summary>
        public static bool IsUnlocked(CodexEntry e, SoulMeta meta) {
            if (meta == null) return false;
            if (meta.Codex != null && meta.Codex.Contains(e.Key)) return true;
            return e.Kind == CodexKind.Species && meta.SeenSpecies != null && meta.SeenSpecies.Contains(e.Id);
        }

        /// <summary>Отметить запись открытой. true — впервые (стоит показать карточку).</summary>
        public static bool Unlock(SoulMeta meta, string key) {
            if (meta == null) return false;
            if (meta.Codex == null) meta.Codex = new HashSet<string>();
            return meta.Codex.Add(key);
        }

        public static int CountUnlocked(CodexKind kind, SoulMeta meta) {
            int n = 0;
            foreach (var e in All(kind)) if (IsUnlocked(e, meta)) n++;
            return n;
        }

        private static string BehaviorText(CreatureBehavior b) {
            switch (b) {
                case CreatureBehavior.Food: return "пища, дрейфует";
                case CreatureBehavior.Flee: return "пуглив, удирает";
                case CreatureBehavior.Chase: return "охотник, преследует";
                case CreatureBehavior.Charger: return "таранит с разгона";
                default: return "бродит без цели";
            }
        }
    }
}
