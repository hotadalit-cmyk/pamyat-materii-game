# Задача для Z.ai: модуль забега PM.Domain.Run (v0.4)

Ты — senior C#-разработчик. Работаешь в своей песочнице. Отвечай по-русски, кратко.

## Репозиторий
`git clone https://github.com/hotadalit-cmyk/pamyat-materii-game` (ветка `main`).
Создай ветку `zai/run-module`. **Только добавляй новые файлы**, существующие файлы НЕ меняй
(параллельно другой разработчик меняет Unity-слой; любая правка существующих файлов = конфликт).
Не делай push. Соблюдай AGENTS.md.

## Контекст
Игра — мобильный roguelite: слизень (бильярдное управление «оттянул-отпустил») в тёмной пещере
из комнат. Одна жизнь = один забег 5–10 минут. Тело = билд забега (масса + мутации).
Душа = мета-прогресс между забегами. ИИ-души становятся немезидами. Полная концепция: см. ниже.

Логика Domain — чистый C# 9 без UnityEngine. Тесты: `tools/dotnet-tests` (.NET 8 harness,
`cd tools/dotnet-tests && dotnet run` → `PASSED N/N`). Тесты — публичные статические методы
без параметров в `public static class XxxTests` (namespace PMHarness), проверки через
`using static PMHarness.Check;` (`True`, `Eq` — смотри TestKit/TestKit.cs и Tests/SoulTests.cs).
Используй `PM.Domain.Core` (`Vec2`, `PmRandom`, `MathUtil`) и `PM.Persistence.MiniJson`.

## Где создавать
- Код: `Assets/Game/Scripts/Domain/Run/*.cs`, namespace `PM.Domain.Run`.
- Тесты: `tools/dotnet-tests/Tests/Run*Tests.cs`.
- Файлы `.meta` НЕ создавай. Без LINQ в горячих путях. Без `System.Random` — только `PmRandom`.
- Всё детерминировано от seed.

## API — СТРОГО эти имена и сигнатуры (под них параллельно пишется Unity-слой)

```csharp
namespace PM.Domain.Run {

public enum CreatureBehavior { Drift = 0, Flee = 1, Chase = 2, Charger = 3, Food = 4 }

public sealed class SpeciesDef {
    public string Id; public string Name;      // русское имя для UI
    public double BaseMass;                    // типичная масса
    public CreatureBehavior Behavior;
    public string GeneMutationId;              // мутация, которую даёт вид (может быть null)
    public int MinDepth, MaxDepth;             // глубины появления
    public int ColorIndex;                     // 0 еда,1 добыча,2 опасность,3 душа,4 кристалл
}
public static class SpeciesCatalog {
    public static SpeciesDef[] All();          // >= 7 видов, ниже
    public static SpeciesDef Get(string id);   // null если нет
}
// Виды (минимум): spore (Food, 0.3, без гена, 1-5), mite (Flee, 0.8, "m_light", 1-3),
// beetle (Drift, 2.5, "m_shell", 1-4), glowworm (Flee, 1.5, "m_eyes", 2-5),
// spikeback (Charger, 4.0, "m_spikes", 2-5), jelly (Drift, 3.0, "m_sticky", 3-5),
// crystal_mite (Chase, 6.0, "m_crystal", 4-5), soul_slime (Chase, ~ масса игрока, "m_hunger", 1-5).

public sealed class MutationDef { public string Id, Name, Description; public int MaxStacks; }
public static class MutationCatalog {
    public static MutationDef[] All();         // >= 10, ниже
    public static MutationDef Get(string id);
}
// m_spikes  Шипы: урон, наносимый ударом ×1.5 за стак (макс 2)
// m_shell   Панцирь: получаемый урон ×0.7 за стак (макс 2)
// m_sticky  Липкость: упругость отскока −0.25 за стак (макс 2), база 0.8, минимум 0.2
// m_dash2   Второй рывок: +1 заряд рывка (макс 2)
// m_magnet  Магнит: радиус притяжения ×1.5 за стак (макс 2)
// m_eyes    Глаза: радиус света +2.0 за стак (макс 3), база 3.5
// m_toxic   Ядовитый след: след наносит 0.4 массы/сек врагам (макс 1)
// m_light   Лёгкость: стоимость рывка ×0.6 за стак (макс 2)
// m_regrow  Регенерация: +0.05×масса в минуту… проще: +0.02 массы/сек за стак (макс 2)
// m_hunger  Голод: порог поглощения 1.25 → 1.10 (макс 1)
// m_crystal Кристалл: +25% массы мгновенно при выборе, но скорость рывка ×0.85 (макс 1)

public sealed class MutationSet {
    public int Stacks(string id);
    public bool CanAdd(string id);
    public void Add(string id);                // игнор, если нельзя
    public double DamageDealtMult { get; }     // spikes
    public double DamageTakenMult { get; }     // shell
    public double Bounciness { get; }          // sticky
    public int DashCharges { get; }            // 1 + dash2 (+ бонус души добавляется в RunState)
    public double PullRadiusMult { get; }      // magnet
    public double LightRadius { get; }         // eyes
    public double TrailDamagePerSec { get; }   // toxic
    public double DashCostMult { get; }        // light
    public double RegenPerSec { get; }         // regrow
    public double AbsorbRatio { get; }         // hunger
    public double DashSpeedMult { get; }       // crystal
    public string[] ToArray(); public static MutationSet FromArray(string[] ids);
}

public struct ContactResult {
    public int Absorbed;       // 0 нет; 1 — A поглотил B; 2 — B поглотил A
    public double MassLossA, MassLossB;   // потери массы (>=0), если не поглощение
}
public static class SizeRule {
    public const double MinViableMass = 1.0;   // ниже — смерть игрока
    public static double RadiusForMass(double mass);   // 0.35*sqrt(max(mass,0.05))
    // Поглощение: если massA >= massB * A.AbsorbRatio → A поглощает B (и наоборот).
    // Иначе удар: потеря B = k * relSpeed * massA/(massA+massB) * A.DamageDealtMult * B.DamageTakenMult,
    // k = 0.12; аналогично для A. Скорость < 1.0 → потерь нет (касание).
    public static ContactResult Resolve(double massA, MutationSet mutA, double massB, MutationSet mutB, double relSpeed);
    // Капли: потерянная масса разлетается каплями по ~0.25 (не больше 8 штук)
    public static double[] SplitIntoDroplets(double lostMass);
    public static double DashCost(double mass, MutationSet m, double soulCostMult); // max(0.04, 0.025*mass)*m.DashCostMult*soulCostMult
}

public struct SpawnSpec { public string SpeciesId; public Vec2 Pos; public double Mass; public bool IsNemesis; public string SoulName; }
public sealed class RoomPlan {
    public int Depth, RoomIndex;
    public double HalfWidth, HalfHeight;       // размеры арены (9.0 x 5.0 базово, растут с глубиной до 12x7)
    public System.Collections.Generic.List<SpawnSpec> Spawns;
    public double GoalMass;                    // сколько массы надо поглотить в комнате, чтобы открыть выход
    public Vec2 ExitPos;                       // у края арены, напротив старта
    public Vec2 PlayerStart;
    public double Darkness01;                  // 0.55 на глубине 1 → 0.9 на глубине 5
}
public static class RoomGenerator {
    // Спавны не ближе 2.0 к старту игрока и не ближе (r1+r2+0.3) друг к другу; внутри арены с отступом.
    // На комнату: 6-12 спор, 3-6 существ по глубине; масса существ растёт с глубиной (×(1+0.25*(depth-1))).
    // Цель комнаты ≈ 40% суммарной массы поглощаемого содержимого, не меньше 2.
    public static RoomPlan Generate(int depth, int roomIndex, ulong seed, NemesisRegistry nemeses);
}

public sealed class MutationOffer { public string[] Options; } // ровно 3 разных id (или меньше, если пул мал)

public sealed class LifeImprint {
    public int DepthReached, RoomsCleared, Absorbed, NewSpecies;
    public double PeakMass, BestHit, EssenceEarned;
    public string KilledBy;                    // speciesId или имя души, null если вышел/победил
    public string[] Mutations;
}

public sealed class RunState {
    public const int RoomsPerDepth = 3; public const int MaxDepth = 5;
    public int Depth { get; } public int RoomIndex { get; }
    public double Mass { get; } public double PeakMass { get; }
    public MutationSet Mutations { get; }
    public RoomPlan Room { get; }
    public double RoomAbsorbed { get; }        // поглощено в текущей комнате
    public bool ExitOpen { get; }              // RoomAbsorbed >= Room.GoalMass
    public bool IsDead { get; }                // Mass < SizeRule.MinViableMass
    public bool IsVictory { get; }             // прошёл последнюю комнату MaxDepth
    public MutationOffer PendingOffer { get; } // не null → игра ждёт выбора
    public int DashCharges { get; }            // текущие заряды; восстанавливаются 1 за 1.2 с
    public int MaxDashCharges { get; }

    public RunState(SoulMeta meta, ulong seed);         // стартовая масса = 4.0 + meta бонус
    public bool TryDash();                              // тратит заряд и SizeRule.DashCost массы; false если нет заряда
    public void OnAbsorb(string speciesId, double mass);// +масса, учёт видов/генов, счётчики, проверка порога роста
    public void OnMassLost(double amount, string sourceId); // −масса; при смерти запоминает KilledBy
    public void OnHit(double dealtMassLoss);            // для BestHit
    public void Tick(double dt);                        // регенерация, восстановление зарядов
    public void ChooseMutation(string id);              // применяет, снимает PendingOffer (m_crystal даёт +25% массы)
    public bool TryEnterExit();                         // если ExitOpen: следующая комната/глубина (новый RoomPlan), true
    public LifeImprint Finish();                        // итог жизни + эссенция; повторный вызов возвращает тот же объект
}
// Пороги роста: предложение мутации при пересечении PeakMass 6, 9, 13, 18, 24, 31, 40...(×~1.3).
// Пул предложения: базовый (m_dash2, m_magnet, m_light, m_regrow) ∪ meta.KnownGenes ∪ гены видов,
// поглощённых в этом забеге; исключить недоступные (MaxStacks). Seeded.
// Эссенция: 10×(DepthReached−1) + 3×RoomsCleared + 0.5×Absorbed(не споры) + 5×NewSpecies(впервые за всё время)
//          + 25 за победу + 15 за поглощение немезиды. Смерть сама ничего не даёт (правило D-19).

public enum SoulStat { Energy = 0, Endurance = 1, Vitality = 2 }
public sealed class SoulMeta {
    public double Essence; public int Runs, BestDepth;
    public int Level(SoulStat s); public const int MaxLevel = 5;
    public static double UpgradeCost(int currentLevel);   // 20, 35, 55, 80, 110
    public bool TryUpgrade(SoulStat s);                   // списывает эссенцию
    public System.Collections.Generic.HashSet<string> KnownGenes;    // гены, хоть раз поглощённые
    public System.Collections.Generic.HashSet<string> SeenSpecies;
    public NemesisRegistry Nemeses;
    // производные:
    public int BonusDashCharges { get; }        // Energy: +1 на уровнях 2 и 4
    public double DashCostMult { get; }         // Endurance: ×(1 − 0.08×lvl)
    public double StartMassBonus { get; }       // Vitality: +0.6×lvl
    public void ApplyLife(LifeImprint imprint); // Essence += EssenceEarned, Runs++, BestDepth, SeenSpecies, KnownGenes
    public string ToJson(); public static SoulMeta FromJson(string json); // через PM.Persistence.MiniJson, устойчиво к мусору/пустой строке
}

public sealed class NemesisRecord { public string SoulName; public int Level; public int KillsOfPlayer; public bool Defeated; public string StolenMutation; }
public sealed class NemesisRegistry {
    public System.Collections.Generic.List<NemesisRecord> Records;
    // Имена ИИ-душ: Искра, Осколок, Эхо, Тлен, Капля, Шёпот
    public string PickSoulName(PmRandom rng);
    public void OnPlayerKilledBy(string soulName, string[] playerMutations, PmRandom rng); // Level++, KillsOfPlayer++, крадёт случайную мутацию игрока
    public void OnSoulAbsorbed(string soulName);           // Defeated = true
    public NemesisRecord PickForRoom(int depth, PmRandom rng); // живой немезида с шансом 35% (не в комнате 0 глубины 1)
    public static double NemesisMass(NemesisRecord r, double playerMass); // playerMass×(1.1+0.15×Level)
}

public struct BrainInput { public Vec2 SelfPos; public double SelfMass; public CreatureBehavior Behavior; public Vec2 PlayerPos; public double PlayerMass; public double Time; }
public struct BrainOutput { public bool Impulse; public Vec2 Direction; public double Strength01; }
public sealed class CreatureBrain {
    public CreatureBrain(ulong seed);
    // Не чаще одного импульса в 0.9–2.2 с (случайно). Drift — случайное направление 0.3;
    // Flee — от игрока, если он ближе 4 и тяжелее; Chase — к игроку, если ближе 6 и сам тяжелее
    // (иначе как Flee); Charger — пауза 2 с, затем сильный рывок 1.0 к игроку, если ближе 7; Food — никогда.
    public BrainOutput Think(BrainInput input);
}
}
```

## Тесты (минимум 45, все зелёные, существующие 450 — тоже)
Покрой: правило размера (поглощение в обе стороны, AbsorbRatio с m_hunger, нет потерь при
скорости <1, шипы/панцирь), капли (сумма = потеря, ≤8), DashCost, стаки мутаций и MaxStacks,
все производные MutationSet, генерация комнаты (детерминизм по seed, дистанции, внутри арены,
цель ≥2, рост сложности с глубиной, немезида не в первой комнате), RunState (рывки и заряды,
смерть ниже порога, порог роста → предложение 3 разных, выбор, m_crystal, выход и переход
комнат/глубин, победа на 5-й, Finish идемпотентен, эссенция по формуле, смерть не даёт
эссенции сама), SoulMeta (стоимости, уровни, производные, ApplyLife, JSON туда-обратно и мусор),
немезиды (кража мутации, уровень, масса, Defeated), CreatureBrain (кулдаун, каждое поведение).

## Сдача
1. `dotnet run` в tools/dotnet-tests → `PASSED N/N`, покажи последнюю строку.
2. Коммит(ы) в ветку `zai/run-module`.
3. Положи в download/: `pm-run-module.bundle` (`git bundle create ... main..zai/run-module` плюс
   полная история допустима), `pm-run-module.zip` (только новые файлы) и `SHA256SUMS.txt`.
4. Отчёт ≤ 15 строк: список файлов, число тестов, отклонения от API (если были — почему).
