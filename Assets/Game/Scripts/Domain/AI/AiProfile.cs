using PM.Domain.Core;
namespace PM.Domain.AI {
/// <summary>Контекст решения ИИ. Никаких скрытых путей к ресурсам: только наблюдаемые факты.</summary>
public struct AiContext {
    public Vec2 SelfPos;
    public double SelfMass;
    public double SelfWater01;      // заполненность водой 0..1
    public Vec2 NearestFoodDir;     // направление к ближайшей пище (Zero — нет)
    public double NearestFoodDist;
    public Vec2 PlayerPos;
    public double PlayerMass;
    public bool PlayerWeakenedWater; // у игрока мало воды (окно для помощи/конфликта)
    public long Tick;
}
/// <summary>
/// Профиль ИИ-носителя души. Альянс-склонный и соперник (SOUL_WORLD, T15/T16).
/// Решение — ActorCommand в общий конвейер, тот же, что у игрока (D-15).
/// </summary>
public sealed class AiProfile {
    public string Id;
    public bool Ally;
    public double HungerThreshold = 0.35;
    public readonly AiMemory Memory = new AiMemory();
    public int ActorId; // для записи событий памяти

    public ActorCommand Decide(AiContext ctx) {
        // 1) Недавняя атака игрока → бегство (память меняет решение, QA-9).
        if (Memory.Recently(MemoryEventKind.AttackedBy, 0, ctx.Tick, 40)) {
            var away = ctx.SelfPos - ctx.PlayerPos;
            if (!away.Approx(Vec2.Zero, 1e-6))
                return Cmd(CommandKind.Flee, away.Normalized(), target: 0);
        }
        bool playerThreatensMass = !Ally && ctx.SelfMass > ctx.PlayerMass * 1.2;
        bool playerStronger = ctx.PlayerMass > ctx.SelfMass * 1.4;
        if (!Ally && playerThreatensMass && !playerStronger)
            return Cmd(CommandKind.Confront, (ctx.PlayerPos - ctx.SelfPos).Normalized(), target: 0);
        // 2) Голод → поиск пищи.
        if (ctx.SelfWater01 < HungerThreshold && !ctx.NearestFoodDir.Approx(Vec2.Zero, 1e-6))
            return Cmd(CommandKind.SeekFood, ctx.NearestFoodDir.Normalized());
        // 3) Социальная развилка.
        if (Ally && ctx.PlayerWeakenedWater && Memory.Trust >= 0) {
            var to = ctx.PlayerPos - ctx.SelfPos;
            return Cmd(CommandKind.Assist, to.Approx(Vec2.Zero, 1e-6) ? Vec2.Zero : to.Normalized(), target: 0);
        }
        if (!Ally && ctx.PlayerWeakenedWater && !playerStronger && Memory.Trust < 0)
            return Cmd(CommandKind.Confront, (ctx.PlayerPos - ctx.SelfPos).Normalized(), target: 0);
        return Cmd(CommandKind.Wait, Vec2.Zero, duration: 0.5);
    }
    private static ActorCommand Cmd(CommandKind kind, Vec2 dir, int target = -1, double duration = 0) {
        return new ActorCommand { Kind = kind, Direction = dir, TargetActorId = target, Duration = duration };
    }
}
}
