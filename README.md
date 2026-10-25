# Память Материи — игровой репозиторий

Top-down физическая action-RPG для Android (Unity 6.3 LTS / 6000.3.x).
Игрок — слизень: импульсное движение, вода, биогравитация, поглощение, гены, душа.

**Версия сборки:** v0.2.0-full (спринт полной сборки, 2026-10-25).
**Статусы проверки:** Domain-логика — TESTED (harness 421/421 PASS);
Unity-компиляция и устройство — NOT RUN (Unity в среде сборки недоступен, проверка за владельцем).

## Что внутри (T00–T17 в объёме демо-среза)

| Слой | Папка | Содержимое |
|---|---|---|
| Domain (чистый C#, без UnityEngine) | `Assets/Game/Scripts/Domain/` | Vec2/OrthoFraming, масса/вода (D-16), PullCharge/JetPlanner, контактный урон + антидребезг, биогравитация, влажный след, захват, пищеварение, гены (8 узлов), метаморфоза (D-17), душа/реинкарнация, ИИ-души + ActorCommand (D-15), оркестратор GameSim |
| Persistence | `Assets/Game/Scripts/Persistence/` | MiniJson (зависимостей нет), CRC32, SaveService: 2 слота, чек-сумма, temp→rename, .bak |
| Systems/UI (Unity) | `Assets/Game/Scripts/Systems/` | SimRunner (физический адаптер), PullGestureInput (guard UI), AimIndicator, FollowCamera, сущности, след-эмиттер (пул), HUD/гены/смерть/пауза, SafeAreaFitter |
| Editor | `Assets/Game/Editor/` | `PM/Setup/01..03`, `PM/Dev` (16:9 ↔ 20:9) |
| Тесты | `tools/dotnet-tests/` | .NET 8 harness: **421 тест** на все инварианты GDD/QA |
| | `Assets/Game/Tests/EditMode/` | Дымовые тесты Unity Test Runner |

## Быстрый старт (проверка владельцем)

1. **Unity Hub** → установить **Unity 6000.3.x (LTS)** с модулем **Android Build Support**
   (OpenJDK + Android SDK Tools включить).
2. Открыть проект (версия в `ProjectSettings/ProjectVersion.txt`: 6000.3.10f1 —
   при несовпадении патча Hub предложит открыть установленной: согласиться).
3. Дождаться импорта. Меню: **PM/Setup/01 Create Boot Scene** → сцена создастся кодом
   и добавится в Build Settings.
4. **PM/Setup/02 Apply Android Settings** → IL2CPP, ARM64, minSdk 24, Linear, landscape,
   пакет `com.pamyatmaterii.game`.
5. **PM/Setup/03 Verify Project Wiring** → убедиться, что bootstrap/camera/HUD на месте.
6. Play: оттянуть палец/мышь **против** направления движения → отпустить → рывок;
   гранулы притягиваются, жук темнеет от ударов → подойти → удержание → пищеварение →
   гены (кнопка «ГЕНЫ») → «Форма жука» → смерть (если целостность 0) → реинкарнация.
7. **PM/Dev/Game View 16:9 / 20:9** — проверить взятие камеры и safe area
   (если автопереключение не сработало — вручную, см. лог в Console).
8. Тесты Domain вне Unity: `cd tools/dotnet-tests && dotnet run` → `PASSED 421/421`.
9. Тесты Unity: Window → General → Test Runner → EditMode → Run All (5 дымовых).

## Коммиты и статусы

- Проверять `git status` чистым после импорта: Unity может добавить `ProjectSettings/*.asset`
  (стандартные при первой генерации) — закоммитить как "meta import" по чек-листу docs.
- Статусы задач — по `QA_ACCEPTANCE.md` (NOT STARTED / CODE COMPLETE / TESTED LOCAL /
  TESTED UNITY / TESTED DEVICE). Сейчас: Domain = TESTED LOCAL (harness); Unity-слой =
  CODE COMPLETE (TESTED UNITY — после шагов 3–9 владельцем).

## Ограничения демо-сборки (честно)

- Арт — процедурные круги палитры VISUAL_UI; анимации — scale/цвет.
- Органы после реинкарнации восстанавливаются из знаний мгновенно (в полной игре — время/компоненты).
- Имена ИИ-душ — плейсхолдеры (`ai_ally`, `ai_rival`), до утверждения владельцем.
- Яд/холод/тепло, магия, маскировка — вне демо (DEMO_SCOPE «не включаем»).
- Аудио/локализация/публикация — этапы T19–T20.

## Структура тестов (tools/dotnet-tests)

ПРАВИЛО harness: компилируются ТОЛЬКО источники без UnityEngine
(`Scripts/Domain/**`, `Scripts/Persistence/**`). Запуск:

```bash
cd tools/dotnet-tests
dotnet run          # сборка + прогон всех тестов
```
