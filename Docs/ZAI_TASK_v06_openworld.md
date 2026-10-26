Задача №3 — открытый мир (v0.6). Та же песочница. Игрок сказал: поле слишком маленькое, нужны огромные
разнообразные подземелья с лабиринтами и открытым миром. Каждая глубина = ОДНО большое подземелье.

## Ветка и правила
`git checkout zai/world-v05 && git checkout -b zai/openworld-v06`. Не трогай `RoomGenerator.cs` и
`RunRoomTests.cs`. Существующий API не ломать (старые тесты зелёные). Без LINQ, только PmRandom,
детерминизм от seed, без .meta, без push. Коммить по ходу работы (после каждого крупного шага).

## API (строго; Unity-слой пишется под него параллельно)

```csharp
namespace PM.Domain.Run {

public enum RegionKind { Cavern = 0, Start = 1, Exit = 2, Maze = 3, Shrine = 4, Spring = 5, Elite = 6,
                         Lair = 7 /*логово немезиды*/, Secret = 8 /*тайник*/, BossLair = 9 }
public sealed class RegionInfo {
    public int Id; public RegionKind Kind; public string Title;  // «Лабиринт корней», «Гнездо огненных жуков», «Тайник»
    public Vec2 Center; public double Radius;                     // приблизительный радиус области
    public int MinX, MinY, MaxX, MaxY;                            // габариты в клетках
}
public enum AltarKind { Shrine = 0, Spring = 1, Relic = 2 }      // Relic — в центре лабиринта и в тайниках: мутация + 15 эссенции
public struct AltarSpec { public AltarKind Kind; public Vec2 Pos; public int RegionId; }
public struct WallRect { public int X, Y, W, H; }                 // прямоугольник стеновых клеток (для коллайдеров)

public sealed class WorldMap {
    public int Width, Height;                 // в клетках; клетка = 1 мировая единица
    public byte[] Cells;                      // y*Width+x; 0 = стена, 1 = пол
    public short[] RegionOf;                  // id региона клетки или -1 (тоннели)
    public bool IsFloor(int x, int y);        // false за границами
    public Vec2 CellCenter(int x, int y);     // мир: (x+0.5, y+0.5) — начало координат в левом нижнем углу
    public bool CellOf(Vec2 p, out int x, out int y);
    public Vec2 PlayerStart, ExitPos;         // ExitPos — «шахта вниз» в самом дальнем (по BFS) регионе Exit/BossLair
    public Vec2 BossPos;                      // только глубина 5: центр BossLair (иначе Zero)
    public List<RegionInfo> Regions;
    public List<AltarSpec> Altars;
    public List<SpawnSpec> Spawns;            // все существа и споры уровня (босс НЕ входит)
    public List<ObstacleSpec> Obstacles;      // препятствия/зоны биома — только в пещерных залах
    public List<WallRect> WallRects;          // жадное слияние ВСЕХ стеновых клеток, граничащих с полом в радиусе 2 (не вся толща)
    public double GoalMass;                   // ≈25% суммарной поглощаемой массы уровня (не меньше 6) — открывает шахту
}

public static class WorldGenerator {
    public static WorldMap Generate(int depth, ulong seed, NemesisRegistry nemeses);
}
```

### Алгоритм (обязательные свойства — покрыть тестами)
1. Размер: глубина 1 — 140×90, далее +15×10 за глубину (5 → 200×130). Рамка из стен толщиной ≥ 3.
2. Пещерные залы: 12–18 (растёт с глубиной), центры — Poisson-выборка (мин. дистанция 16), формы разные:
   круг/эллипс/«клякса» (сумма 2–4 кругов), радиусы 5–12. После — клеточный автомат (4–5 итераций)
   только в пещерных зонах для органичных краёв.
3. Связи: минимальное остовное дерево по залам + 25% дополнительных рёбер (петли!). Тоннели — извилистые
   (шум/случайное блуждание к цели), ширина 2–4 клетки, иногда расширения-«карманы».
4. Лабиринты: 2 на глубине 1, 3 на 2–3, 4 на 4–5. Прямоугольник ~24×18 клеток, идеальный лабиринт
   (recursive backtracker) с коридорами шириной 2 и стенами 1, 1–2 входа, соединены с ближайшим залом.
   В центре — алтарь Relic. Region Kind = Maze, Title из набора («Лабиринт корней», «Сотовый лабиринт», …).
5. Тайники: 2–3 маленьких зала (r 3–4), соединены одним узким тоннелем (ширина 2), внутри Relic или много спор.
6. Связность: все клетки пола достижимы из PlayerStart (заливка); недостижимое — засыпать.
7. Назначение регионов: Start — зал у края карты; Exit — самый дальний по BFS от старта зал (на глубине 5 —
   это BossLair, BossPos = центр, ExitPos — у дальнего края логова); 1 Shrine, 1 Spring (ещё по одному на
   глубинах 3+), 1–2 Elite (масса существ ×1.6), Lair — если есть живая немезида (NemesisRegistry.PickForRoom
   с шансом 100% на уровень, если запись есть) — немезида спавнится там с IsNemesis и SoulName.
   Остальные — Cavern. Титулы — по-русски, по биому (BiomeCatalog.ForDepth): «Грибная роща», «Огненная яма»…
8. Спавны: споры ≈ 1 на 22 клетки пола (не в стенах, не ближе 6 к старту); существа — в залах и тоннелях,
   плотность растёт с BFS-дистанцией от старта (у старта — мелкие mite/beetle, к шахте — опасные), виды —
   из BiomeDef.SpeciesPool + базовые подходящие по глубине (soul_slime ≤ 2 на уровень, не ближе 25 к старту).
   Массы ×(1 + 0.25*(depth-1)). Существа не пересекают стены (центр клетки пола с отступом радиуса).
9. Препятствия: 2–5 в каждом пещерном зале (не в лабиринтах/тоннелях), из ObstaclePool биома, не ближе 2 к
   алтарям/старту/шахте, твёрдые не перекрывают проход (проверка связности после размещения).
10. WallRects: жадное слияние прямоугольниками только «видимых» стен (в пределах 2 клеток от пола),
    чтобы коллайдеров было ≤ 1500.

## Интеграция в RunState (меняй RunState.cs)
- Новый конструктор `public RunState(SoulMeta meta, ulong seed, bool openWorld)`; старый (2 аргумента) — как
  был (комнатный режим, для тестов). `public bool OpenWorld { get; }`, `public WorldMap World { get; }`.
- В открытом мире: одна карта на глубину (`RoomIndex` всегда 0). `Room` строится из World (HalfWidth=Width/2,
  HalfHeight=Height/2, Spawns, ExitPos, PlayerStart, GoalMass, Darkness01 биома) — чтобы старый код работал.
  `Layout` = { Kind = Boss на глубине 5, иначе Normal; Biome; Obstacles = World.Obstacles; AltarPos = Zero }.
  `Boss` создаётся на глубине 5 (масса 30 + 6×число убитых стражей ранее? — нет, просто 30).
  `TryEnterExit()` при ExitOpen → следующая глубина (новая World), после 5-й — победа.
  ExitOpen = RoomAbsorbed ≥ GoalMass (на глубине 5 — страж поглощён).
- Алтари по индексу: `public bool IsAltarUsed(int index)`, `public void UseAltarAt(int index)`:
  Shrine → PendingOffer (3 мутации); Spring → +35% массы (≤ PeakMass); Relic → PendingOffer + 15 эссенции в итог.
- `public int RegionsDiscovered { get; }` и `public void OnRegionEntered(int regionId)` — первая встреча
  региона: +1 эссенция за каждые 3 открытых региона (исследование вознаграждается); LifeImprint получает
  `public int RegionsDiscovered`.
- Эссенция за глубину в открытом мире ×2 (уровни огромные): 20×(DepthReached−1).

## Тесты (≥ 40 новых; все старые зелёные)
Размеры по глубинам; рамка стен; детерминизм; связность всего пола; число залов/лабиринтов/тайников по
глубине; лабиринт — идеальный (дерево: в его области клеток-коридоров нет циклов на сетке 2×) и имеет Relic;
Start у края; Exit — дальний по BFS; BossLair/BossPos на 5; спавны только на полу и не у старта; градиент
опасности (средняя масса существ в дальней трети > в ближней); soul_slime ≤ 2; немезида в Lair;
препятствия только в залах и не рвут связность; WallRects покрывают все стены у пола и их ≤ 1500;
GoalMass ≥ 6; RunState открытого мира (Room/Layout/Boss, переходы глубин, победа, алтари по индексу,
регионы и эссенция, старый конструктор не изменился). Добавь тест производительности: Generate(5, …)
быстрее 400 мс.

## Сдача
`dotnet run` → последняя строка. Коммиты в `zai/openworld-v06`. Затем:
`git bundle create /home/z/my-project/download/pm-openworld-v06.bundle zai/world-v05..zai/openworld-v06 && sha256sum /home/z/my-project/download/pm-openworld-v06.bundle`
и вывод base64 частями по 20000 символов (B64_PART_BEGIN N / B64_PART_END N), как в прошлый раз.
Отчёт ≤ 15 строк.
