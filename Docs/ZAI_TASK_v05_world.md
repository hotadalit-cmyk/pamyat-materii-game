Отличная работа над модулем. Задача №2 — расширение мира (v0.5). Та же песочница и репозиторий.

## Ветка и правила
- `git checkout zai/run-module && git checkout -b zai/world-v05`.
- Можно менять СВОИ файлы в `Assets/Game/Scripts/Domain/Run/` и `tools/dotnet-tests/Tests/Run*Tests.cs`,
  КРОМЕ `RoomGenerator.cs` и `RunRoomTests.cs` (их параллельно правит другой разработчик — не трогай).
  Новые файлы — сколько нужно. `.meta` не создавай. Без LINQ, только PmRandom, всё от seed. Push не делать.
- Существующий публичный API НЕ ломать (только добавлять). Все старые тесты — зелёные.

## Идея
Пещера из 5 биомов (по одному на глубину). В комнатах появляются препятствия (для бильярдных
рикошетов) и опасные зоны, типы комнат (элитная, святилище мутаций, родник), на дне — босс.
Цепочки поглощений за один рывок дают бонус. В тёплых биомах тело испаряется.

## API (строго эти имена; Unity-слой пишется под них параллельно)

```csharp
namespace PM.Domain.Run {

public enum BiomeId { Fungal = 1, Warm = 2, Ponds = 3, Crystal = 4, Heart = 5 }
public sealed class BiomeDef {
    public BiomeId Id; public int Depth;
    public string Name, Subtitle;               // «Грибной коридор», «Здесь всё начинается…»
    public uint FloorColor, WallColor, AccentColor, GlowColor; // 0xRRGGBB, тёмные стены, яркий акцент
    public double Darkness01;                   // 0.55 → 0.9 по глубине
    public double EvaporationPerSec;            // доля массы/сек: Fungal 0, Warm 0.004, Ponds 0, Crystal 0.002, Heart 0.003
    public string[] SpeciesPool;                // id видов, которые водятся в биоме (spore — всегда)
    public ObstacleKind[] ObstaclePool;         // какие препятствия встречаются
}
public static class BiomeCatalog { public static BiomeDef ForDepth(int depth); public static BiomeDef[] All(); }
// Fungal(1): pillar, mushroom(bumper)   · Warm(2): pillar, vent, thorns
// Ponds(3): pillar, pool, sticky        · Crystal(4): crystal, thorns, pillar   · Heart(5): pillar, thorns, vent, pool

public enum ObstacleKind { Pillar = 0, Mushroom = 1, Crystal = 2, Thorns = 3, Vent = 4, Pool = 5, Sticky = 6 }
// Pillar  — твёрдый круг, упругость 0.75.       Mushroom — твёрдый «бампер», упругость 1.35 (разгоняет).
// Crystal — твёрдый, упругость 1.1; удар со скоростью ≥ 6 раскалывает его (ShatterSpeed) → 3 капли по 0.4.
// Thorns  — твёрдый; касание отнимает массу.    Vent — зона (не твёрдая): толкает от центра + испаряет.
// Pool    — зона: регенерация массы, но вязко.  Sticky — зона: сильное торможение.
public struct ObstacleSpec { public ObstacleKind Kind; public Vec2 Pos; public double Radius; }
public static class ObstacleRules {
    public static bool IsSolid(ObstacleKind k);
    public static double Bounciness(ObstacleKind k);
    public const double ShatterSpeed = 6.0;
    public static double ThornLoss(double relSpeed, MutationSet m);   // 0.15 + 0.08*relSpeed, × m.HazardMult
    public static double ZoneMassPerSec(ObstacleKind k, double mass, MutationSet m); // Pool +0.06 (не выше стартовой+? — просто +), Vent −0.02*mass×HazardMult, иначе 0
    public static double ZoneDamping(ObstacleKind k);   // доп. linearDamping: Pool 2.5, Sticky 6, иначе 0
    public static double VentPush(double dist, double radius); // сила толчка от центра, 0 на краю, 14 в центре
}

public enum RoomKind { Normal = 0, Elite = 1, Shrine = 2, Spring = 3, Boss = 4 }
public sealed class RoomLayout {
    public RoomKind Kind; public BiomeDef Biome;
    public List<ObstacleSpec> Obstacles;
    public string Title, Hint;                  // «Святилище мутаций», «Коснитесь алтаря — выберите дар»
    public Vec2 AltarPos;                       // для Shrine/Spring (иначе Zero)
}
public static class LayoutGenerator {
    // Kind: depth 5 room 2 = Boss; room 0 глубины 1 = Normal; room 1 любой глубины: 25% Shrine, 20% Spring;
    // иначе Elite с шансом 10%+6%*depth, иначе Normal. Детерминировано (seed, depth, room).
    public static RoomKind KindFor(int depth, int roomIndex, ulong seed);
    // 3–7 препятствий из ObstaclePool биома (в Boss-комнате 4 колонны по углам, в Spring/Shrine 2–3).
    // Не ближе 1.6 к PlayerStart/ExitPos/AltarPos, не пересекаются со спавнами (r+0.3), внутри арены
    // с отступом 1.0, радиусы 0.5–1.3 (зоны 1.2–2.2). Должен оставаться проход: проверь, что точки
    // старта и выхода связаны (сетка 0.5 по твёрдым препятствиям, BFS) — иначе перегенерировать.
    public static RoomLayout Generate(RoomPlan plan, RoomKind kind, ulong seed);
}

// --- Виды и черты ---
// Добавь 6 видов в SpeciesCatalog (с биомами): ember_beetle (Warm, Charger, 3.5, ген "m_heatproof"),
// salamander (Warm, Chase, 3.0, "m_vampire"), leech (Ponds, Chase, 1.2, null), bubble_jelly (Ponds, Drift, 3.5,
// "m_echo"), shard_crab (Crystal, Drift, 5.0, "m_ricochet"), wisp (Heart, Flee, 2.0, "m_echo"); MinDepth/MaxDepth
// согласуй с биомом. EligibleSpecies в RoomGenerator НЕ трогай — пул биома используй через SpeciesTraits/Layout.
public struct SpeciesTraits {
    public double ArmorSpeed;    // удары медленнее — не ранят (shard_crab 4, crystal_mite 3, страж 6), иначе 0
    public bool SplitsOnHit;     // bubble_jelly: при уроне делится на 2 половины (если масса ≥ 1.5)
    public double LeechPerSec;   // leech: пока касается игрока, тянет массу 0.25/с
    public bool Glows;           // светится сквозь тьму (glowworm, wisp, crystal_mite, ember_beetle)
}
public static class SpeciesTraitsCatalog { public static SpeciesTraits Get(string speciesId); }

// --- Новые мутации (добавь в MutationCatalog и MutationSet) ---
// m_heatproof «Жаростойкость»: HazardMult ×0.4 (шипы, жар, испарение), макс 1
// m_vampire   «Вампиризм»: LifestealFrac 0.35 — доля нанесённой ударом потери возвращается вам, макс 1
// m_ricochet  «Рикошет»: RicochetBonus +0.25 за каждый отскок от стены/препятствия в текущем рывке (урон), макс 2 (+0.25 за стак)
// m_echo      «Эхолокация»: SenseRadius 9 — существа вне света видны как метки, макс 1
public double HazardMult { get; }   public double LifestealFrac { get; }
public double RicochetBonus { get; } public double SenseRadius { get; }  // 0, если нет мутации

// --- Цепочки ---
public sealed class ComboTracker {
    public int Chain { get; }                   // поглощений (не спор) с момента последнего рывка
    public double Multiplier { get; }           // 1 + 0.25*(Chain-1), максимум 3
    public void OnDash();                       // сброс цепочки
    public double OnAbsorb(bool isSpore, double mass); // возвращает БОНУС массы: mass*(Multiplier-1) для не-спор; споры цепочку не растят
    public void Tick(double dt);                // через 2.5 с без поглощений — сброс
}

// --- Босс ---
public enum BossPhase { Dormant = 0, Charge = 1, Brood = 2, Frenzy = 3, Exposed = 4 }
public struct BossInput { public Vec2 SelfPos; public Vec2 PlayerPos; public double PlayerMass; public double Dt; }
public struct BossOutput {
    public bool Impulse; public Vec2 Direction; public double Strength01;
    public int SpawnMinions;                     // сколько минёнов (вид "mite") выпустить сейчас
    public bool Shockwave;                       // кольцевая волна: отталкивает игрока, если ближе 5
}
public sealed class BossState {
    public const string SpeciesId = "warden";    // добавь вид «Страж глубины» (Charger, 30, ген null, depth 5, ColorIndex 2)
    public double Mass { get; } public double StartMass { get; }
    public BossPhase Phase { get; }
    public bool IsExposed { get; }               // масса < 30% стартовой → можно поглотить (при касании, если игрок ≥ 0.6×массы босса)
    public BossState(double startMass, ulong seed);
    public BossOutput Think(BossInput input);    // Dormant пока игрок дальше 7; Charge (>60%): рывок к игроку каждые 2.4 с;
                                                 // Brood (30–60%): рывки реже (3.2 с) + 3 минёна каждые 7 с; Frenzy (<45% и после волны): рывки 1.4 с,
                                                 // волна каждые 5 с. Exposed: не атакует, медленно убегает.
    public double ApplyHit(double relSpeed, double playerMass, MutationSet playerMut, int ricochets);
        // если relSpeed < 6 — 0 (броня). Иначе потеря = 0.35*relSpeed*playerMass/(playerMass+Mass)*DamageDealtMult*(1+RicochetBonus*ricochets).
}

// --- Интеграция в RunState (меняй RunState.cs) ---
// public RoomLayout Layout { get; }            — генерируется вместе с Room (KindFor + LayoutGenerator)
// public BossState Boss { get; }               — не null только в Boss-комнате; Spawns босс-комнаты: босс НЕ входит в Spawns
//                                                 (Unity создаёт его сам), спор 6; ExitOpen в Boss-комнате = босс поглощён.
// public ComboTracker Combo { get; }           — TryDash вызывает Combo.OnDash(); OnAbsorb добавляет бонус и возвращает его:
// public double OnAbsorbEx(string speciesId, double mass) — как OnAbsorb, но возвращает бонус цепочки (OnAbsorb оставь, пусть вызывает Ex)
// public void OnBossAbsorbed()                 — победа над стражем: +40 эссенции в итог, выход открыт
// public bool ShrineUsed { get; } public void UseAltar() — Shrine: ставит PendingOffer (3 мутации, независимо от порогов),
//                                                 Spring: +35% массы (не выше PeakMass) — один раз на комнату
// public void OnLifesteal(double dealt)        — +dealt*LifestealFrac массы
// Tick(dt): испарение Mass -= Mass*Biome.EvaporationPerSec*HazardMult*dt; m_regrow компенсирует; не убивать испарением ниже 1.05
//          (испарение не может само убить — только довести до 1.05).
// Эссенция: +4 за Elite-комнату, +40 за стража. Elite: массы существ ×1.6, цель ×1.3, после выхода — гарантированное предложение мутации.
// LifeImprint: добавь public string BiomeReached, public int BestCombo, public bool WardenSlain.

// --- Бестиарий (для меню) ---
public static class Bestiary {
    public static string Describe(string speciesId);   // 1–2 предложения: повадки и подсказка, как одолеть
}
}
```

## Тесты (минимум 50 новых, все старые зелёные)
Биомы по глубинам, цвета/пулы не пустые; KindFor (Boss на 5/2, первая комната Normal, детерминизм,
частоты на 2000 seed в разумных пределах); LayoutGenerator (дистанции, внутри арены, без пересечений,
проход старт→выход существует, детерминизм); ObstacleRules; SpeciesTraits; новые мутации; ComboTracker
(рост, кап 3, сброс на рывке и по таймеру, споры не растят); BossState (фазы по массе, броня <6, Exposed,
миньоны в Brood, волна в Frenzy, кулдауны); RunState (Layout/Boss/Combo, испарение не убивает, Spring/Shrine
один раз, Elite, эссенция за стража, OnLifesteal, LifeImprint новые поля).

## Сдача
`dotnet run` → покажи последнюю строку. Коммиты в `zai/world-v05`. Потом выполни ровно:
`git bundle create /home/z/my-project/download/pm-world-v05.bundle zai/run-module..zai/world-v05 && sha256sum /home/z/my-project/download/pm-world-v05.bundle && echo B64_BEGIN && base64 -w0 /home/z/my-project/download/pm-world-v05.bundle | head -c 20000 && echo && echo B64_PART_END`
и затем, если файл больше, по 20000 символов дальше (`cut -c20001-40000` и т.д.), каждую часть между
`B64_PART_BEGIN N` и `B64_PART_END`. Отчёт ≤ 15 строк: файлы, число тестов, отклонения от API.
