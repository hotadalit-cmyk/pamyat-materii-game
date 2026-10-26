Задача №4 — система эволюции (v0.8). Та же песочница. Игрок хочет: не случайные «1 из 3», а ДЕРЕВО
прокачки, где он сам выбирает, что развивать; улучшения стоят ресурсов, которые он копит в хранилище;
у героя — интерактивные показатели (с расшифровкой, откуда что взялось).

## Ветка и правила
`git checkout zai/world-v05 && git checkout -b zai/evolution-v08`. ТОЛЬКО НОВЫЕ ФАЙЛЫ в
`Assets/Game/Scripts/Domain/Run/` (namespace PM.Domain.Run) и `tools/dotnet-tests/Tests/Evo*Tests.cs`.
Существующие файлы НЕ меняй (RunState и др. у меня ушли далеко вперёд — интеграцию сделаю сам).
Без LINQ, без System.Random, детерминизм, без .meta, без push. Коммить по ходу.
Можно использовать: MutationSet, MutationCatalog, SpeciesCatalog, SoulMeta (SoulStat, Level, BonusDashCharges,
DashCostMult, StartMassBonus), SizeRule — как они есть в твоей ветке.

## API (строго; Unity-слой пишется под него параллельно)

```csharp
namespace PM.Domain.Run {

// ---------- Ресурсы и хранилище ----------
public enum ResourceKind { Organic = 0, Chitin = 1, Slime = 2, Crystal = 3, Ember = 4, Psyche = 5 }
public static class ResourceInfo {
    public const int Count = 6;
    public static string Name(ResourceKind k);          // «Органика», «Хитин», «Слизь», «Кристалл», «Искра», «Психея»
    public static string Description(ResourceKind k);   // 1 предложение: откуда берётся и на что идёт
    public static uint Color(ResourceKind k);           // 0xRRGGBB: зелёный, янтарный, голубой, циан, оранжевый, фиолетовый
}
public struct CostItem { public ResourceKind Kind; public double Amount; }
public sealed class Cost {
    public CostItem[] Items;
    public static Cost Of(params CostItem[] items);
    public override string ToString();                   // «12 Органика · 4 Хитин»
}
public sealed class ResourceStore {
    public const double BaseCapacity = 40.0;             // на каждый вид
    public double Capacity { get; set; }
    public double Get(ResourceKind k);
    public double Add(ResourceKind k, double amount);    // возвращает реально добавленное (с учётом вместимости)
    public bool CanPay(Cost c);
    public bool Pay(Cost c);                             // всё или ничего
    public double[] ToArray(); public static ResourceStore FromArray(double[] a, double capacity);
}
public static class YieldTable {
    // Что даёт поглощённое существо (пропорционально массе). spore → Organic 0.6·m;
    // mite/beetle → Chitin 0.8·m + Organic 0.4·m; jelly/leech/bubble_jelly → Slime 0.9·m + Organic 0.2·m;
    // crystal_mite/shard_crab → Crystal 0.8·m; ember_beetle/salamander → Ember 0.8·m + Chitin 0.3·m;
    // glowworm/wisp → Psyche 0.5·m + Organic 0.3·m; spikeback → Chitin 1.0·m; soul_slime → Psyche 1.2·m;
    // warden → по 15 каждого. Неизвестный id → Organic 0.5·m.
    public static CostItem[] For(string speciesId, double mass);
}

// ---------- Дерево эволюции ----------
public enum EvoBranch { Body = 0, Motion = 1, Hunt = 2, Senses = 3, Division = 4 }
// Тело (масса, защита, регенерация), Движение (рывок), Охота (урон, поглощение), Чувства (свет, притяжение, эхо),
// Деление (сколько фрагментов, порог деления, хранилище)
public enum EvoEffectKind { Mutation = 0, MaxDash = 1, MaxFragments = 2, StorageCap = 3, LightFlat = 4,
    MassGainMult = 5, YieldMult = 6, SplitMinMass = 7, DashSpeedMult = 8, RegenFlat = 9, DamageTakenMult = 10, PullFlat = 11 }
public struct EvoEffect { public EvoEffectKind Kind; public string MutationId; public double Value; }
public sealed class EvoNode {
    public string Id, Name, Description;    // описание — что даёт, по-русски, ≤ 90 символов
    public EvoBranch Branch;
    public int Tier;                        // 1..4 (строка в дереве)
    public int Column;                      // 0..2 (позиция в строке для отрисовки)
    public Cost Cost;
    public string[] Requires;               // id узлов-предков (все нужны)
    public string ExclusiveGroup;           // null или id группы: в группе можно взять только ОДИН узел (развилка!)
    public string RequiredGene;             // null или speciesId: узел виден/доступен, только если вид поглощён
    public EvoEffect[] Effects;
}
public static class EvolutionTree {
    public static EvoNode[] All();          // ≥ 30 узлов: по 6–7 в каждой из 5 ветвей, тиры 1–4
    public static EvoNode Get(string id);
    public static EvoNode[] Branch(EvoBranch b);
    public static string BranchName(EvoBranch b);  // «Тело», «Движение», «Охота», «Чувства», «Деление»
}
// Требования к дереву:
// - в каждой ветви ≥ 1 развилка (ExclusiveGroup) на тирах 2–3 — выбор с последствиями
//   (пример Охота: «Шипы» ИЛИ «Вампиризм»; Движение: «Второй рывок» ИЛИ «Кристальный таран»);
// - узлы-гены для всех мутаций видов: m_light(mite), m_shell(beetle), m_eyes(glowworm), m_spikes(spikeback),
//   m_sticky(jelly), m_crystal(crystal_mite), m_hunger(soul_slime), m_heatproof(ember_beetle), m_vampire(salamander),
//   m_echo(bubble_jelly), m_ricochet(shard_crab) — Effects = {Mutation, id}, RequiredGene = вид;
// - базовые без гена: m_dash2, m_magnet, m_regrow, m_toxic + статовые узлы (MaxDash, LightFlat, MassGainMult и т.д.);
// - ветвь Деление: базово можно делиться на 2; узлы «Тройня» (+1 MaxFragments), «Рой» (+2), «Колония» (+3, тир 4),
//   «Лёгкое деление» (SplitMinMass −0.3), «Кладовая» (StorageCap +20), «Бездонная кладовая» (+40, тир 4), «Добытчик» (YieldMult +0.25);
// - цена растёт с тиром (тир1 ≈ 8–12 ед., тир4 ≈ 40–60 ед. в сумме) и тематична ветви (Тело — Органика/Хитин,
//   Движение — Слизь/Органика, Охота — Хитин/Искра, Чувства — Психея/Кристалл, Деление — Слизь/Органика/Психея);
// - нет циклов в Requires, все Requires существуют и стоят на меньшем тире, у каждого узла хотя бы 1 эффект.

public enum UnlockBlock { None = 0, AlreadyUnlocked = 1, MissingPrereq = 2, ExclusiveTaken = 3, GeneUnknown = 4, NotEnoughResources = 5 }
public sealed class EvolutionState {
    public HashSet<string> Unlocked { get; }
    public int FreeTokens;                                   // святилище даёт жетон бесплатного узла
    public UnlockBlock CanUnlock(string id, ResourceStore store, ICollection<string> knownSpecies);
    public UnlockBlock CanUnlockFree(string id, ICollection<string> knownSpecies);   // всё, кроме ресурсов; нужен жетон
    public bool TryUnlock(string id, ResourceStore store, ICollection<string> knownSpecies, MutationSet mutations);
    public bool TryUnlockFree(string id, ICollection<string> knownSpecies, MutationSet mutations); // тратит жетон
    // при открытии: эффекты Mutation применяются к mutations (mutations.Add), прочие суммируются
    public double Sum(EvoEffectKind k);                      // сумма Value по открытым узлам
    public string[] ToArray(); public static EvolutionState FromArray(string[] ids);
    public static string BlockText(UnlockBlock b);           // «Нужен предыдущий узел», «Выбран другой путь», «Поглотите: Жук» …
}

// ---------- Показатели героя ----------
public struct StatLine {
    public string Key;          // "mass", "fragments", "dash", "dash_speed", "damage", "defense", "pull", "light",
                                // "regen", "absorb", "dash_cost", "yield", "storage", "mass_gain"
    public string Name;         // «Масса», «Фрагменты», …
    public string Value;        // отформатировано: «7.4», «2 / 4», «×1.50», «−30%»
    public double Raw;
    public string Tooltip;      // 1 предложение — что это значит в игре
    public string[] Sources;    // разбор: «База 1», «Второй рывок +1», «Душа: Энергия ур.2 +1», «Шипы ×1.5»
}
public static class StatSheet {
    public static List<StatLine> Build(double mass, int fragments, int dashCharges, MutationSet m,
                                       EvolutionState e, SoulMeta meta);
    // ≥ 14 строк (ключи выше). Значения согласованы с MutationSet и EvoEffect:
    // dash max = m.DashCharges + meta.BonusDashCharges + e.Sum(MaxDash); fragments max = 2 + e.Sum(MaxFragments);
    // light = m.LightRadius + e.Sum(LightFlat); damage = m.DamageDealtMult; defense = m.DamageTakenMult × (1 + e.Sum(DamageTakenMult));
    // regen = m.RegenPerSec + e.Sum(RegenFlat); storage = BaseCapacity + e.Sum(StorageCap); yield = 1 + e.Sum(YieldMult) и т.д.
}
}
```

## Тесты (≥ 45, все старые зелёные)
ResourceStore (вместимость, Add режет, Pay всё-или-ничего, ToArray/FromArray); Cost.ToString; YieldTable
(все виды каталога, пропорциональность массе, неизвестный id); EvolutionTree (≥30 узлов, 5 ветвей, тиры,
Requires валидны и без циклов, ≥1 развилка на ветвь, все 11 генных мутаций есть, у каждого узла эффект и цена,
цена растёт с тиром); EvolutionState (каждый UnlockBlock достижим и корректен, оплата списывает ресурсы,
развилка блокирует соседа, ген из knownSpecies, мутации применяются к MutationSet, Sum, FreeTokens и
TryUnlockFree, сериализация); StatSheet (все ключи, источники содержат вклад узлов и души, значения
совпадают с формулами).

## Сдача
`dotnet run` → последняя строка. Коммиты в `zai/evolution-v08`. Положи
`/home/z/my-project/download/pm-evolution-v08.bundle` (`git bundle create … zai/world-v05..zai/evolution-v08`)
и `sha256sum` — и сделай файл доступным для скачивания в панели файлов задачи. Затем выведи base64
частями по 20000 символов (B64_PART_BEGIN N / B64_PART_END N). Отчёт ≤ 15 строк.
