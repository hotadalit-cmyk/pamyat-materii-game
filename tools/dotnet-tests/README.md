# dotnet-tests — harness тестов Domain (D-20)

Чистая C# Domain-логика проверяется вне Unity: этот проект компилирует исходники из
`Assets/Game/Scripts/Domain/**` и `Assets/Game/Scripts/Persistence/**` напрямую.

**ПРАВИЛО: включать только файлы без UnityEngine.** Systems/Editor/UI не включать
(см. комментарий в `PM.Tests.csproj`).

Запуск:
```bash
cd tools/dotnet-tests
dotnet run
```

Структура:
- `TestKit/TestKit.cs` — минимальный раннер без внешних зависимостей
  (классы с суффиксом `Tests`, публичные статические void-методы).
- `Tests/*.cs` — 14 наборов: Core, BodyMass, Movement, Damage, Biogravity, Trail,
  Capture, Digestion, Gene, Metamorphosis, Soul, Ai, MiniJson/SaveService, GameSim.

Текущее состояние: **PASSED 421/421** (v0.2.0-full, 2026-10-25).
