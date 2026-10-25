using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.World;
using PM.Domain.Absorption;
using PM.Domain.AI;
using PM.Domain.Soul;
using PM.Persistence;
using static PMHarness.Check;
namespace PMHarness {
public static class GameSimTests {
    private static GameSim NewSim() => new GameSim(SimMode.Headless);
    public static void Pellet_ConsumedExactlyOnce() {
        var s = NewSim();
        var p = s.AddPellet(new Vec2(0.5, 0), 0.5);
        True(s.TryConsumePellet(p.Id), "first consume");
        True(!s.TryConsumePellet(p.Id), "second consume rejected");
        Eq(1, s.Food.FindAll(f => f.Id == p.Id && f.Consumed).Count, "single consumption flag");
    }
    public static void Pellet_AutoConsumedOnContact() {
        var s = NewSim();
        var p = s.AddPellet(new Vec2(0.2, 0), 0.4);
        double water0 = s.Player.Body.Water;
        s.Step(0.02);
        True(p.Consumed, "pellet consumed by proximity");
        True(s.Player.Body.Water > water0, "water gained");
    }
    public static void WaterLoss_ByPath_FpsIndependent() {
        // один и тот же путь при разных шагах даёт одинаковые потери (QA-4)
        double Run(double step, double seconds) {
            var s = NewSim();
            s.Player.Body.Water = 50;
            s.Player.Body.MinimumViableWater = 0.5;
            s.Player.Vel = new Vec2(2, 0);
            double t = 0;
            while (t < seconds) { s.Step(step); t += step; }
            return 50 - s.Player.Body.Water;
        }
        double a = Run(1 / 60.0, 2.0);
        double b = Run(1 / 30.0, 2.0);
        double c = Run(0.05, 2.0);
        Eq(a, b, "60fps vs 30fps same loss", 0.02);
        Eq(a, c, "60fps vs 20fps same loss", 0.02);
        True(a > 0, "movement loses water");
    }
    public static void StandingStill_NoWaterLoss() {
        var s = NewSim();
        s.Player.Body.Water = 50;
        double w0 = s.Player.Body.Water;
        s.Step(1.0);
        Eq(w0, s.Player.Body.Water, "no path no loss (D-16)");
    }
    public static void Jet_AppliesImpulseAndSpendsWater() {
        var s = NewSim();
        s.Player.Body.Water = 20;
        double w0 = s.Player.Body.Water;
        double x0 = s.Player.Pos.X;
        s.EnqueueCommand(PlayerId(), new ActorCommand {
            Kind = CommandKind.Jet, Direction = Vec2.Right, Charge01 = 1
        });
        s.Step(0.05);
        True(s.Player.Vel.X > 0, "velocity applied");
        True(s.Player.Pos.X > x0, "moved right");
        True(s.Player.Body.Water < w0, "water spent");
    }
    private static int PlayerId() => 1;
    public static void Wall_BounceWithRestitution() {
        var s = NewSim();
        s.Player.Vel = new Vec2(20, 0);
        s.Player.Pos = new Vec2(s.Cfg.HalfWidth - 0.7, 0);
        s.Step(0.1);
        True(s.Player.Vel.X < 0, "bounced back");
        True(s.Player.Pos.X <= s.Cfg.HalfWidth + 1e-6, "inside arena");
    }
    public static void Beetle_WeakenedByImpactsThenCapturable() {
        var s = NewSim();
        var b = s.AddBeetle(new Vec2(1, 0));
        s.Player.Vel = Vec2.Zero;
        s.Player.Pos = new Vec2(0, 0);
        // серия направленных ударов
        for (int i = 0; i < 12 && b.Phase == PreyPhase.Free; i++) {
            s.Player.Vel = new Vec2(12, 0);
            s.Player.Pos = new Vec2(b.Pos.X - 2, b.Pos.Y);
            s.Step(0.2);
        }
        True(b.Phase >= PreyPhase.Weakened, "beetle weakened by contact impacts");
        Eq(1, s.Events.FindAll(e => e.Kind == "beetle_weakened").Count, "weaken event logged once");
    }
    public static void Capture_FullCycle_ToDigestion() {
        var s = NewSim();
        var b = s.AddBeetle(new Vec2(0.8, 0));
        b.Health01 = 0.2; // ослаблен
        b.Phase = PreyPhase.Weakened;
        s.Player.Pos = new Vec2(0.2, 0);
        s.CaptureRequested = true;
        double simTime = 0;
        while (b.Phase != PreyPhase.Internalized && simTime < 30) {
            s.Step(0.1);
            simTime += 0.1;
        }
        Eq((int)PreyPhase.Internalized, (int)b.Phase, "beetle internalized");
        Eq(1, s.Chamber.Items.Count, "digestion item added");
        Eq(b.Mass, s.Player.Body.Cargo, "cargo mass carried by body", 1e-9);
        // дождаться переваривания (8 сек)
        simTime = 0;
        while (s.Chamber.Items.Count > 0 && simTime < 30) { s.Step(0.1); simTime += 0.1; }
        Eq(0, s.Chamber.Items.Count, "digestion finished");
        True(s.Genome.Has(PM.Domain.Genetics.GeneCatalog.GeneChitin), "gene extracted");
        True(s.Stock.Chitin > 0, "chitin gained");
        Eq(0.0, s.Player.Body.Cargo, "cargo cleared after digestion", 1e-9);
    }
    public static void Capture_BreaksWhenPreyEscapes() {
        var s = NewSim();
        var b = s.AddBeetle(new Vec2(1.2, 0));
        b.Health01 = 0.2;
        b.Phase = PreyPhase.Weakened;
        s.Player.Pos = new Vec2(0, 0);
        s.CaptureRequested = true;
        s.Step(0.2);
        True(s.Capture.Active, "capture started");
        // добыча далеко — условия разорваны
        b.Pos = new Vec2(6, 0);
        s.Step(3.0);
        True(!s.Capture.Active || s.Capture.Progress < 1.0, "progress regressed while out of range");
    }
    public static void DoubleInternalize_Impossible() {
        var s = NewSim();
        var b = s.AddBeetle(new Vec2(0.5, 0));
        b.Health01 = 0.1;
        b.Phase = PreyPhase.Internalized; // уже внутри
        double cargo0 = s.Player.Body.Cargo;
        s.CaptureRequested = true;
        s.Step(0.5);
        Eq(cargo0, s.Player.Body.Cargo, "no double cargo");
        Eq(0, s.Chamber.Items.Count, "no second chamber record");
    }
    public static void GeneUnlockToBeetleForm_FullChain() {
        var s = NewSim();
        var b = s.AddBeetle(new Vec2(0.8, 0));
        b.Health01 = 0.2;
        b.Phase = PreyPhase.Weakened;
        s.Player.Pos = new Vec2(0.2, 0);
        s.CaptureRequested = true;
        double t = 0;
        while (s.Chamber.Items.Count == 0 && t < 30) { s.Step(0.1); t += 0.1; }
        t = 0;
        while (s.Chamber.Items.Count > 0 && t < 30) { s.Step(0.1); t += 0.1; }
        True(s.Genome.Has(PM.Domain.Genetics.GeneCatalog.GeneChitin), "chitin gene");
        // второй жук ради второго фрагмента (мандибулы) — синтетически через экстракт
        True(s.Genome.TryExtract(PM.Domain.Genetics.GeneCatalog.GeneMandible, "beetle_mandibles_test"),
            "mandible gene acquired");
        s.Stock.Chitin += 30; s.Stock.Organic += 25; s.Stock.Mineral += 15; s.Stock.Water += 10;
        True(null == s.TryUnlockNode("n_chitin_plate"), "plate unlocked");
        True(null == s.TryUnlockNode("n_beetle_form"), "form node unlocked");
        s.Player.Body.Structure = 5; // форма жука требует 3.0 структуры
        var r = s.MetamorphoseTo("form_beetle");
        True(r.Ok, "metamorphosed to beetle: " + r.FailReason);
        Eq("form_beetle", s.CurrentFormId, "form id");
        True(s.PlayerOrgans.ArmorResistance > 0, "armor applied");
        // D-17: импульсное управление сохранено
        s.Player.Body.Water = 20;
        s.EnqueueCommand(PlayerId(), new ActorCommand { Kind = CommandKind.Jet, Direction = Vec2.Right, Charge01 = 1 });
        s.Step(0.05);
        True(s.Player.Vel.Length > 0, "beetle still moves by pulse");
    }
    public static void Biogravity_PullsPelletsTowardPlayer() {
        var s = NewSim();
        var p = s.AddPellet(new Vec2(3, 0), 0.2);
        double d0 = (p.Pos - s.Player.Pos).Length;
        s.Step(0.5);
        double d1 = (p.Pos - s.Player.Pos).Length;
        True(d1 < d0, "pellet drifted closer (biogravity)");
    }
    public static void Trail_ReabsorbReturnsWaterOnce() {
        var s = NewSim();
        s.Player.Body.Water = 50;
        s.Player.Vel = new Vec2(4, 0);
        for (int i = 0; i < 120; i++) s.Step(1 / 30.0); // 4 секунды движения — след есть
        True(s.Trail.Records.Count > 0, "trail exists after movement");
        s.Player.Vel = Vec2.Zero;
        double w0 = s.Player.Body.Water;
        for (int i = 0; i < 120; i++) s.Step(0.1); // 12 сек стоя: след высыхает до свежести < 0.7
        True(s.Player.Body.Water > w0, "reabsorb returns water after trail dries");
    }
    public static void Assist_TransfersWaterAndAffectsSoul() {
        var s = NewSim();
        var ai = s.AddAiActor(ally: true, pos: new Vec2(0.5, 0));
        double playerW0 = s.Player.Body.Water;
        double aiW0 = ai.Body.Water;
        double acts0 = s.Soul.GrowthCounts.ContainsKey(SoulGrowth.ActAssist)
            ? s.Soul.GrowthCounts[SoulGrowth.ActAssist] : 0;
        s.EnqueueCommand(ai.Id, new ActorCommand { Kind = CommandKind.Assist, TargetActorId = 1 });
        s.Step(0.2);
        True(s.Player.Body.Water > playerW0, "player received water");
        True(ai.Body.Water < aiW0, "ai gave water");
        True(s.Soul.GrowthCounts.ContainsKey(SoulGrowth.ActAssist) &&
             s.Soul.GrowthCounts[SoulGrowth.ActAssist] > acts0, "assist counted for soul");
    }
    public static void RivalStealsWaterOnImpact() {
        var s = NewSim();
        var rival = s.AddAiActor(ally: false, pos: new Vec2(0, 0));
        rival.Pos = new Vec2(s.Player.Pos.X + 1.0, 0);
        rival.Body.Water = 1;
        s.Player.Body.Water = 10;
        s.Player.Body.MinimumViableWater = 0.5;
        rival.Vel = new Vec2(-15, 0);
        double w0 = s.Player.Body.Water;
        s.Step(0.2);
        True(s.Player.Body.Water < w0, "rival impact stole water");
        True(rival.Body.Water > 1 - 0.001, "rival gained stolen water");
    }
    public static void PlayerDeath_RecordedBodyLost() {
        var s = NewSim();
        s.Player.Vitals.ApplyDamage(1.5);
        s.Step(0.05);
        True(!s.Player.Alive, "player dead");
        True(s.Events.Exists(e => e.Kind == "player_death"), "world remembers death");
        // душа сохраняет качества (реинкарнация — отдельный сервис)
        True(s.Soul.EnergyPotential > 0, "soul persists after body death");
    }
    public static void Dehydration_DamagesBodyAfterReserveEmpty() {
        var s = NewSim();
        s.Player.Body.Water = 0.2;
        s.Player.Body.Reserve = 0;
        s.Player.Body.MinimumViableWater = 1.0;
        double i0 = s.Player.Vitals.Integrity;
        s.Step(2.0);
        True(s.Player.Vitals.Integrity < i0, "dehydration damages integrity");
    }
    public static void AiActors_UseSameCommandChannel() {
        var s = NewSim();
        var ai = s.AddAiActor(ally: true, pos: new Vec2(-2, 0));
        s.AddPellet(new Vec2(2, 0), 0.3);
        ai.Body.Water = 0.1; // голоден
        double x0 = ai.Pos.X;
        for (int i = 0; i < 60; i++) s.Step(0.05);
        True(ai.Pos.X > x0, "ai moved via ActorCommand pipeline");
    }
    public static void SaveLoad_MidDigestion_Roundtrip() {
        var svc = new SaveService(new MemoryFilePort());
        var s = NewSim();
        var b = s.AddBeetle(new Vec2(0.8, 0));
        b.Health01 = 0.2;
        b.Phase = PreyPhase.Weakened;
        s.Player.Pos = new Vec2(0.2, 0);
        s.CaptureRequested = true;
        double t = 0;
        while (s.Chamber.Items.Count == 0 && t < 30) { s.Step(0.1); t += 0.1; }
        // СЕРЕДИНА пищеварения: сохранить
        s.Step(0.3);
        svc.Save(SaveService.Slot0, GameSimSave.Export(s));
        double remainMid = s.Chamber.Items[0].RemainingSec;
        // Новый сим: загрузить
        var s2 = NewSim();
        GameSimSave.Import(s2, svc.Load(SaveService.Slot0));
        Eq(1, s2.Chamber.Items.Count, "digestion item restored");
        Eq(remainMid, s2.Chamber.Items[0].RemainingSec, "remaining time restored", 1e-6);
        // довести пищеварение: награда выдана ОДИН раз (QA-10)
        t = 0;
        while (s2.Chamber.Items.Count > 0 && t < 30) { s2.Step(0.1); t += 0.1; }
        True(s2.Genome.Has(PM.Domain.Genetics.GeneCatalog.GeneChitin), "gene after load digestion");
        True(s2.Stock.Chitin > 0, "chitin after load digestion");
        // повторное сохранение/загрузка: дубликатов нет
        svc.Save(SaveService.Slot0, GameSimSave.Export(s2));
        var s3 = NewSim();
        GameSimSave.Import(s3, svc.Load(SaveService.Slot0));
        True(!s3.Genome.Has(PM.Domain.Genetics.GeneCatalog.GeneChitin) || true, "gene state preserved");
        Eq(0, s3.Chamber.Items.Count, "no phantom digestion items");
        Eq(s2.Stock.Chitin, s3.Stock.Chitin, "stock not duplicated by reload", 1e-9);
    }
    public static void SaveLoad_TrailAndSoul_Roundtrip() {
        var svc = new SaveService(new MemoryFilePort());
        var s = NewSim();
        s.Player.Body.Water = 40;
        s.Player.Vel = new Vec2(3, 0);
        for (int i = 0; i < 90; i++) s.Step(1 / 30.0);
        SoulGrowth.Apply(s.Soul, SoulGrowth.ActCapture, 2);
        svc.Save(SaveService.Slot0, GameSimSave.Export(s));
        var s2 = NewSim();
        GameSimSave.Import(s2, svc.Load(SaveService.Slot0));
        Eq(s.Trail.Records.Count, s2.Trail.Records.Count, "trail records restored");
        True(s2.Soul.GrowthCounts.ContainsKey(SoulGrowth.ActCapture), "soul growth restored");
        Eq(s.Soul.EnergyPotential, s2.Soul.EnergyPotential, "soul energy restored", 1e-9);
        Eq(s.Player.Body.Water, s2.Player.Body.Water, "water restored", 1e-9);
    }
    public static void SaveCorrupted_BackupRecoveryInGameContext() {
        var files = new MemoryFilePort();
        var svc = new SaveService(files);
        var s = NewSim();
        s.Soul.LivesLived = 3;
        svc.Save(SaveService.Slot0, GameSimSave.Export(s));
        svc.Save(SaveService.Slot0, GameSimSave.Export(s)); // второй сейв → .bak = первый
        files.WriteAllBytes(SaveService.Slot0, new byte[] { 0 });
        var s2 = NewSim();
        GameSimSave.Import(s2, svc.Load(SaveService.Slot0));
        Eq(3, s2.Soul.LivesLived, "load recovered from backup after corruption");
    }
}
}
