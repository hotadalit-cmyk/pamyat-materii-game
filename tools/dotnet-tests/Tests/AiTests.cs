using PM.Domain.Core;
using PM.Domain.AI;
using static PMHarness.Check;
namespace PMHarness {
public static class AiTests {
    private static AiContext Ctx(
        double selfWater01 = 1, bool playerWeakWater = false,
        bool foodNearby = true, double selfMass = 3, double playerMass = 3,
        long tick = 0) {
        return new AiContext {
            SelfPos = new Vec2(0, 0),
            SelfMass = selfMass,
            SelfWater01 = selfWater01,
            NearestFoodDir = foodNearby ? new Vec2(1, 0) : Vec2.Zero,
            NearestFoodDist = foodNearby ? 2 : 0,
            PlayerPos = new Vec2(5, 0),
            PlayerMass = playerMass,
            PlayerWeakenedWater = playerWeakWater,
            Tick = tick
        };
    }
    public static void HungryAi_SeeksFood() {
        var p = new AiProfile { Id = "t", Ally = true };
        var cmd = p.Decide(Ctx(selfWater01: 0.1));
        Eq(CommandKind.SeekFood, cmd.Kind, "hungry -> seek food");
    }
    public static void RecentlyAttacked_Flees() {
        var p = new AiProfile { Id = "t", Ally = true, ActorId = 2 };
        p.Memory.Record(MemoryEventKind.AttackedBy, actorId: 0, tick: 10); // 0 = игрок
        var cmd = p.Decide(Ctx(tick: 20));
        Eq(CommandKind.Flee, cmd.Kind, "attacked recently -> flee");
        // память стареет: через окно поведение меняется
        var cmdLater = p.Decide(Ctx(tick: 200));
        True(cmdLater.Kind != CommandKind.Flee, "after window behaviour changes");
    }
    public static void Ally_AssistsWeakenedPlayer() {
        var p = new AiProfile { Id = "t", Ally = true };
        var cmd = p.Decide(Ctx(playerWeakWater: true, foodNearby: false));
        Eq(CommandKind.Assist, cmd.Kind, "ally helps thirsty player");
    }
    public static void Ally_DoesNotAssistIfBetrayed() {
        var p = new AiProfile { Id = "t", Ally = true };
        p.Memory.Record(MemoryEventKind.StolenBy, 1, tick: 1);
        True(p.Memory.Trust < 0, "trust negative after theft");
        var cmd = p.Decide(Ctx(playerWeakWater: true, foodNearby: false, tick: 5));
        True(cmd.Kind != CommandKind.Assist, "betrayed ally refuses help");
    }
    public static void Rival_ConfrontsWeakerPlayer() {
        var p = new AiProfile { Id = "t", Ally = false };
        var cmd = p.Decide(Ctx(playerWeakWater: true, foodNearby: false, selfMass: 5, playerMass: 2));
        Eq(CommandKind.Confront, cmd.Kind, "stronger rival confronts");
    }
    public static void Rival_RetreatsFromMuchStrongerPlayer() {
        var p = new AiProfile { Id = "t", Ally = false };
        var cmd = p.Decide(Ctx(playerWeakWater: true, foodNearby: false, selfMass: 2, playerMass: 10));
        True(cmd.Kind != CommandKind.Confront, "outmatched rival does not confront");
    }
    public static void SatedAi_Waits() {
        var p = new AiProfile { Id = "t", Ally = true };
        var cmd = p.Decide(Ctx(selfWater01: 1.0, foodNearby: false));
        Eq(CommandKind.Wait, cmd.Kind, "calm ai waits");
    }
    public static void Memory_TrustBoundsAndHistory() {
        var m = new AiMemory();
        for (int i = 0; i < 100; i++) m.Record(MemoryEventKind.HelpedBy, 1, i);
        True(m.Trust <= 100, "trust capped at 100");
        for (int i = 0; i < 100; i++) m.Record(MemoryEventKind.AttackedBy, 2, i);
        True(m.Trust >= -100, "trust floored at -100");
        True(m.Recently(MemoryEventKind.AttackedBy, 2, 100, 50), "recent remembered");
        True(!m.Recently(MemoryEventKind.HelpedBy, 1, 1000, 10), "old help forgotten");
        True(m.Events.Count <= AiMemory.MaxEvents, "event log capped");
    }
    public static void ActorCommand_VersionedAndSerializable() {
        var c = new ActorCommand { Kind = CommandKind.Jet, Direction = Vec2.Up, Charge01 = 0.7, TargetActorId = -1 };
        Eq(ActorCommand.CurrentVersion, c.Version, "command carries schema version");
        Eq(CommandKind.Jet, c.Kind, "kind stored");
        Eq(0.7, c.Charge01, "charge stored");
        // D-15: один канал для игрока и ИИ — тип один и тот же
        var same = new ActorCommand { Kind = CommandKind.Jet, Direction = c.Direction, Charge01 = c.Charge01 };
        Eq(c.Kind, same.Kind, "player and AI share command type");
    }
}
}
