// PM.Domain.Run — дерево эволюции (v0.8). C# 9, без LINQ.
namespace PM.Domain.Run {
    public enum EvoBranch { Body = 0, Motion = 1, Hunt = 2, Senses = 3, Division = 4 }

    public enum EvoEffectKind {
        Mutation = 0, MaxDash = 1, MaxFragments = 2, StorageCap = 3, LightFlat = 4,
        MassGainMult = 5, YieldMult = 6, SplitMinMass = 7, DashSpeedMult = 8,
        RegenFlat = 9, DamageTakenMult = 10, PullFlat = 11, DamageDealtMult = 12
    }

    public struct EvoEffect {
        public EvoEffectKind Kind;
        public string MutationId;
        public double Value;
        public EvoEffect(EvoEffectKind k, double v = 0, string mutId = null) { Kind = k; Value = v; MutationId = mutId; }
    }

    public sealed class EvoNode {
        public string Id, Name, Description;
        public EvoBranch Branch;
        public int Tier;
        public int Column;
        public Cost Cost;
        public string[] Requires;
        public string ExclusiveGroup;
        public string RequiredGene;
        public EvoEffect[] Effects;
    }

    public static class EvolutionTree {
        private static readonly EvoNode[] _all = BuildAll();

        public static EvoNode[] All() {
            var c = new EvoNode[_all.Length];
            for (int i = 0; i < _all.Length; i++) c[i] = _all[i];
            return c;
        }

        public static EvoNode Get(string id) {
            for (int i = 0; i < _all.Length; i++)
                if (_all[i].Id == id) return _all[i];
            return null;
        }

        public static EvoNode[] Branch(EvoBranch b) {
            var l = new System.Collections.Generic.List<EvoNode>();
            for (int i = 0; i < _all.Length; i++)
                if (_all[i].Branch == b) l.Add(_all[i]);
            return l.ToArray();
        }

        public static string BranchName(EvoBranch b) {
            switch (b) {
                case EvoBranch.Body:     return "Тело";
                case EvoBranch.Motion:   return "Движение";
                case EvoBranch.Hunt:     return "Охота";
                case EvoBranch.Senses:   return "Чувства";
                case EvoBranch.Division: return "Деление";
            }
            return b.ToString();
        }

        private static EvoEffect Mut(string id) { return new EvoEffect(EvoEffectKind.Mutation, 0, id); }
        private static EvoEffect Eff(EvoEffectKind k, double v) { return new EvoEffect(k, v, null); }

        private static EvoNode N(string id, string name, string desc, EvoBranch b, int tier, int col,
                                  Cost cost, EvoEffect[] fx, string[] requires = null,
                                  string exclusive = null, string gene = null) {
            return new EvoNode {
                Id = id, Name = name, Description = desc, Branch = b, Tier = tier, Column = col,
                Cost = cost, Requires = requires ?? System.Array.Empty<string>(),
                ExclusiveGroup = exclusive, RequiredGene = gene, Effects = fx
            };
        }

        private static EvoNode[] BuildAll() {
            var l = new System.Collections.Generic.List<EvoNode>();

            // ===== BODY ===== (защита, масса, регенерация)
            l.Add(N("body_thick_skin", "Густая кожа", "Снижает получаемый урон на 15%.",
                EvoBranch.Body, 1, 0,
                Cost.Of(new CostItem(ResourceKind.Organic, 8), new CostItem(ResourceKind.Chitin, 2)),
                new[]{ Eff(EvoEffectKind.DamageTakenMult, -0.15) }));
            l.Add(N("body_mass", "Накопление массы", "+20% к приросту массы при поглощении.",
                EvoBranch.Body, 2, 0,
                Cost.Of(new CostItem(ResourceKind.Organic, 14), new CostItem(ResourceKind.Chitin, 6)),
                new[]{ Eff(EvoEffectKind.MassGainMult, 0.20) }, requires: new[]{ "body_thick_skin" }));
            l.Add(N("body_chitin_plating", "Хитиновый пласт", "Мутация m_shell: панцирь режет входящий урон.",
                EvoBranch.Body, 2, 1,
                Cost.Of(new CostItem(ResourceKind.Organic, 12), new CostItem(ResourceKind.Chitin, 8)),
                new[]{ Mut("m_shell") }, requires: new[]{ "body_thick_skin" }, gene: "beetle"));
            l.Add(N("body_heatproof", "Жаростойкость", "Мутация m_heatproof: снижение урона от жара и опасных зон.",
                EvoBranch.Body, 2, 2,
                Cost.Of(new CostItem(ResourceKind.Organic, 10), new CostItem(ResourceKind.Ember, 4)),
                new[]{ Mut("m_heatproof") }, requires: new[]{ "body_thick_skin" }, gene: "ember_beetle"));
            l.Add(N("body_regrow", "Регенерация", "Мутация m_regrow: масса восстанавливается со временем.",
                EvoBranch.Body, 3, 0,
                Cost.Of(new CostItem(ResourceKind.Organic, 18), new CostItem(ResourceKind.Chitin, 8)),
                new[]{ Mut("m_regrow") }, requires: new[]{ "body_mass" }));
            l.Add(N("body_bulwark", "Бастион", "Дополнительно снижает урон на 25% — путь танка.",
                EvoBranch.Body, 3, 1,
                Cost.Of(new CostItem(ResourceKind.Chitin, 22), new CostItem(ResourceKind.Organic, 10)),
                new[]{ Eff(EvoEffectKind.DamageTakenMult, -0.25) },
                requires: new[]{ "body_chitin_plating" }, exclusive: "body_t3"));
            l.Add(N("body_adrenaline", "Адреналин", "+30% к приросту массы — путь буйного роста.",
                EvoBranch.Body, 3, 2,
                Cost.Of(new CostItem(ResourceKind.Organic, 22), new CostItem(ResourceKind.Chitin, 8)),
                new[]{ Eff(EvoEffectKind.MassGainMult, 0.30) },
                requires: new[]{ "body_chitin_plating" }, exclusive: "body_t3"));
            l.Add(N("body_kevlar", "Кевларовая кожа", "Урон снижен ещё на 30% — вершина брони.",
                EvoBranch.Body, 4, 1,
                Cost.Of(new CostItem(ResourceKind.Chitin, 40), new CostItem(ResourceKind.Organic, 15)),
                new[]{ Eff(EvoEffectKind.DamageTakenMult, -0.30) }, requires: new[]{ "body_bulwark" }));

            // ===== MOTION ===== (рывок, скорость, таран)
            l.Add(N("motion_root", "Скользящий шаг", "Рывок становится на 10% быстрее.",
                EvoBranch.Motion, 1, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 6), new CostItem(ResourceKind.Organic, 4)),
                new[]{ Eff(EvoEffectKind.DashSpeedMult, 0.10) }));
            l.Add(N("motion_dash2", "Второй рывок", "Мутация m_dash2: +1 заряд рывка.",
                EvoBranch.Motion, 2, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 12), new CostItem(ResourceKind.Organic, 6)),
                new[]{ Mut("m_dash2") }, requires: new[]{ "motion_root" }, exclusive: "motion_t2"));
            l.Add(N("motion_crystal_charge", "Кристальный таран", "Мутация m_crystal: рывок тараном раскидывает врагов.",
                EvoBranch.Motion, 2, 1,
                Cost.Of(new CostItem(ResourceKind.Slime, 12), new CostItem(ResourceKind.Crystal, 4)),
                new[]{ Mut("m_crystal") }, requires: new[]{ "motion_root" }, exclusive: "motion_t2", gene: "crystal_mite"));
            l.Add(N("motion_sticky", "Липкая слизь", "Мутация m_sticky: вязкие столкновения держат врагов.",
                EvoBranch.Motion, 2, 2,
                Cost.Of(new CostItem(ResourceKind.Slime, 10), new CostItem(ResourceKind.Organic, 5)),
                new[]{ Mut("m_sticky") }, requires: new[]{ "motion_root" }, gene: "jelly"));
            l.Add(N("motion_ricochet", "Рикошет", "Мутация m_ricochet: рывок отскакивает от врага к врагу.",
                EvoBranch.Motion, 3, 1,
                Cost.Of(new CostItem(ResourceKind.Slime, 18), new CostItem(ResourceKind.Crystal, 8)),
                new[]{ Mut("m_ricochet") }, requires: new[]{ "motion_crystal_charge" }, gene: "shard_crab"));
            l.Add(N("motion_dash_speed", "Скорость рывка", "+20% к скорости рывка.",
                EvoBranch.Motion, 3, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 20), new CostItem(ResourceKind.Crystal, 6)),
                new[]{ Eff(EvoEffectKind.DashSpeedMult, 0.20) }, requires: new[]{ "motion_dash2" }));
            l.Add(N("motion_freerun", "Свободный бег", "+30% к скорости рывка — ты летишь.",
                EvoBranch.Motion, 4, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 40), new CostItem(ResourceKind.Crystal, 10)),
                new[]{ Eff(EvoEffectKind.DashSpeedMult, 0.30) }, requires: new[]{ "motion_dash_speed" }));

            // ===== HUNT ===== (урон, поглощение, вампиризм)
            l.Add(N("hunt_root", "Охотник", "+10% к приросту массы от жертв.",
                EvoBranch.Hunt, 1, 0,
                Cost.Of(new CostItem(ResourceKind.Chitin, 6), new CostItem(ResourceKind.Ember, 2)),
                new[]{ Eff(EvoEffectKind.MassGainMult, 0.10) }));
            l.Add(N("hunt_spikes", "Шипы", "Мутация m_spikes: урон при контакте с врагом.",
                EvoBranch.Hunt, 2, 0,
                Cost.Of(new CostItem(ResourceKind.Chitin, 12), new CostItem(ResourceKind.Ember, 4)),
                new[]{ Mut("m_spikes") }, requires: new[]{ "hunt_root" }, exclusive: "hunt_t2", gene: "spikeback"));
            l.Add(N("hunt_vampire", "Вампиризм", "Мутация m_vampire: похищение массы при нанесении урона.",
                EvoBranch.Hunt, 2, 1,
                Cost.Of(new CostItem(ResourceKind.Chitin, 12), new CostItem(ResourceKind.Ember, 5)),
                new[]{ Mut("m_vampire") }, requires: new[]{ "hunt_root" }, exclusive: "hunt_t2", gene: "salamander"));
            l.Add(N("hunt_hunger", "Голод", "Мутация m_hunger: больше массы от каждого поглощения.",
                EvoBranch.Hunt, 2, 2,
                Cost.Of(new CostItem(ResourceKind.Chitin, 10), new CostItem(ResourceKind.Psyche, 4)),
                new[]{ Mut("m_hunger") }, requires: new[]{ "hunt_root" }, gene: "soul_slime"));
            l.Add(N("hunt_toxic", "Токсичный след", "Мутация m_toxic: урон от следа за героем.",
                EvoBranch.Hunt, 3, 0,
                Cost.Of(new CostItem(ResourceKind.Chitin, 18), new CostItem(ResourceKind.Ember, 8)),
                new[]{ Mut("m_toxic") }, requires: new[]{ "hunt_spikes" }));
            l.Add(N("hunt_executioner", "Палач", "+25% к приросту массы — кровавая дорога наверх.",
                EvoBranch.Hunt, 3, 1,
                Cost.Of(new CostItem(ResourceKind.Chitin, 22), new CostItem(ResourceKind.Ember, 10)),
                new[]{ Eff(EvoEffectKind.MassGainMult, 0.25) }, requires: new[]{ "hunt_vampire" }));
            l.Add(N("hunt_apex", "Апекс хищник", "+30% к приросту массы — ты вершина цепи.",
                EvoBranch.Hunt, 4, 1,
                Cost.Of(new CostItem(ResourceKind.Chitin, 40), new CostItem(ResourceKind.Ember, 20)),
                new[]{ Eff(EvoEffectKind.MassGainMult, 0.30) }, requires: new[]{ "hunt_executioner" }));

            // ===== SENSES ===== (свет, притяжение, эхо)
            l.Add(N("senses_eyes", "Глаза", "Мутация m_eyes: +2 к радиусу света за стак.",
                EvoBranch.Senses, 1, 0,
                Cost.Of(new CostItem(ResourceKind.Psyche, 6), new CostItem(ResourceKind.Crystal, 4)),
                new[]{ Mut("m_eyes") }, gene: "glowworm"));
            l.Add(N("senses_light", "Биолюминесценция", "Мутация m_light: дешевле рывок, больше света.",
                EvoBranch.Senses, 2, 0,
                Cost.Of(new CostItem(ResourceKind.Psyche, 12), new CostItem(ResourceKind.Crystal, 6)),
                new[]{ Mut("m_light") }, requires: new[]{ "senses_eyes" }, exclusive: "senses_t2", gene: "mite"));
            l.Add(N("senses_echo", "Эхолокация", "Мутация m_echo: чувство врагов сквозь темноту.",
                EvoBranch.Senses, 2, 1,
                Cost.Of(new CostItem(ResourceKind.Psyche, 14), new CostItem(ResourceKind.Crystal, 4)),
                new[]{ Mut("m_echo") }, requires: new[]{ "senses_eyes" }, exclusive: "senses_t2", gene: "bubble_jelly"));
            l.Add(N("senses_magnet", "Притяжение", "Мутация m_magnet: добыча сама плывёт к тебе.",
                EvoBranch.Senses, 2, 2,
                Cost.Of(new CostItem(ResourceKind.Psyche, 10), new CostItem(ResourceKind.Crystal, 5)),
                new[]{ Mut("m_magnet") }, requires: new[]{ "senses_eyes" }));
            l.Add(N("senses_pull", "Массовое притяжение", "+2 к плоскому притяжению — цепляешь всех вокруг.",
                EvoBranch.Senses, 3, 2,
                Cost.Of(new CostItem(ResourceKind.Psyche, 18), new CostItem(ResourceKind.Crystal, 8)),
                new[]{ Eff(EvoEffectKind.PullFlat, 2.0) }, requires: new[]{ "senses_magnet" }));
            l.Add(N("senses_perception", "Чувствительность", "+2 к радиусу света — дальше видно.",
                EvoBranch.Senses, 3, 0,
                Cost.Of(new CostItem(ResourceKind.Psyche, 20), new CostItem(ResourceKind.Crystal, 6)),
                new[]{ Eff(EvoEffectKind.LightFlat, 2.0) }, requires: new[]{ "senses_light" }));
            l.Add(N("senses_omniscience", "Всеведение", "+4 к радиусу света — ты видишь всё.",
                EvoBranch.Senses, 4, 0,
                Cost.Of(new CostItem(ResourceKind.Psyche, 35), new CostItem(ResourceKind.Crystal, 15)),
                new[]{ Eff(EvoEffectKind.LightFlat, 4.0) }, requires: new[]{ "senses_pull" }));

            // ===== DIVISION ===== (фрагменты, деление, хранилище)
            l.Add(N("div_split_basics", "Деление", "−0.3 к порогу массы для деления.",
                EvoBranch.Division, 1, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 8), new CostItem(ResourceKind.Organic, 4)),
                new[]{ Eff(EvoEffectKind.SplitMinMass, -0.30) }));
            l.Add(N("div_triplet", "Тройня", "+1 к числу фрагментов при делении.",
                EvoBranch.Division, 2, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 12), new CostItem(ResourceKind.Organic, 6)),
                new[]{ Eff(EvoEffectKind.MaxFragments, 1.0) }, requires: new[]{ "div_split_basics" }));
            l.Add(N("div_storage", "Кладовая", "+20 к вместимости каждого ресурса в хранилище.",
                EvoBranch.Division, 2, 1,
                Cost.Of(new CostItem(ResourceKind.Slime, 10), new CostItem(ResourceKind.Organic, 6), new CostItem(ResourceKind.Psyche, 4)),
                new[]{ Eff(EvoEffectKind.StorageCap, 20.0) }, requires: new[]{ "div_split_basics" }));
            l.Add(N("div_swarm", "Рой", "+2 к числу фрагментов — путь стаи.",
                EvoBranch.Division, 3, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 20), new CostItem(ResourceKind.Organic, 10)),
                new[]{ Eff(EvoEffectKind.MaxFragments, 2.0) }, requires: new[]{ "div_triplet" }, exclusive: "div_t3"));
            l.Add(N("div_easy_split", "Лёгкое деление", "−0.3 к порогу деления — путь частых делений.",
                EvoBranch.Division, 3, 1,
                Cost.Of(new CostItem(ResourceKind.Slime, 16), new CostItem(ResourceKind.Psyche, 4)),
                new[]{ Eff(EvoEffectKind.SplitMinMass, -0.30) }, requires: new[]{ "div_triplet" }, exclusive: "div_t3"));
            l.Add(N("div_harvester", "Добытчик", "+25% к выпадаемым ресурсам с жертв.",
                EvoBranch.Division, 3, 2,
                Cost.Of(new CostItem(ResourceKind.Slime, 18), new CostItem(ResourceKind.Organic, 8)),
                new[]{ Eff(EvoEffectKind.YieldMult, 0.25) }, requires: new[]{ "div_storage" }));
            l.Add(N("div_colony", "Колония", "+3 к числу фрагментов — из тебя рой.",
                EvoBranch.Division, 4, 0,
                Cost.Of(new CostItem(ResourceKind.Slime, 40), new CostItem(ResourceKind.Organic, 20)),
                new[]{ Eff(EvoEffectKind.MaxFragments, 3.0) }, requires: new[]{ "div_swarm" }));
            l.Add(N("div_bottomless", "Бездонная кладовая", "+40 к вместимости хранилища.",
                EvoBranch.Division, 4, 1,
                Cost.Of(new CostItem(ResourceKind.Slime, 30), new CostItem(ResourceKind.Psyche, 15)),
                new[]{ Eff(EvoEffectKind.StorageCap, 40.0) }, requires: new[]{ "div_storage" }));

            return l.ToArray();
        }
    }
}
