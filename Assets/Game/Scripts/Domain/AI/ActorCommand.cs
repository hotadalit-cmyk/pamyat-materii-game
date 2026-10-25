using PM.Domain.Core;
namespace PM.Domain.AI {
/// <summary>Типы команд — ЕДИНЫЙ канал намерений игрока и ИИ (D-15/ARCHITECTURE).</summary>
public enum CommandKind {
    Jet = 0,       // рывок в направлении Direction с зарядом Charge01
    Wait = 1,      // бездействие Duration секунд
    SeekFood = 2,  // движение к ближайшей пище
    Assist = 3,    // помощь актёру TargetActorId (передача воды)
    Confront = 4,  // противостояние актёру TargetActorId
    Flee = 5       // бегство от актёра TargetActorId
}
/// <summary>
/// Сериализуемое намерение (заготовка под офлайн-сохранения и будущую сеть — D-15).
/// Все поля — примитивы: сериализация без зависимостей.
/// </summary>
public sealed class ActorCommand {
    public const int CurrentVersion = 1;
    public int Version = CurrentVersion;
    public CommandKind Kind;
    public Vec2 Direction;
    public double Charge01;
    public int TargetActorId;   // -1 = не указан
    public double Duration;
    public long IssuedTick;
}
}
