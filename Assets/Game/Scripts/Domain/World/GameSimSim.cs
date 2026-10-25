using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Movement;
using PM.Domain.Damage;
using PM.Domain.Absorption;
using PM.Domain.Soul;
using PM.Domain.AI;

namespace PM.Domain.World {
/// <summary>Часть GameSim: команды, интеграция, экономика воды, след, душа, ИИ.</summary>
public sealed partial class GameSim {
    private void ExecuteCommands(double dt) {
        // Игрок (и любой актёр) — через один канал ActorCommand (D-15).
        if (_commands.TryGetValue(Player.Id, out var pq)) {
            while (pq.Count > 0) {
                var cmd = pq.Dequeue();
                if (cmd.Kind == CommandKind.Jet) ExecuteJet(Player, cmd, _playerPendingImpulses);
            }
        }
        for (int i = 0; i < AiActors.Count; i++) {
            var a = AiActors[i];
            if (!_commands.TryGetValue(a.Id, out var q)) continue;
            while (q.Count > 0) {
                var cmd = q.Dequeue();
                switch (cmd.Kind) {
                    case CommandKind.Jet: ExecuteJet(a, cmd, _aiPendingImpulses[a.Id]); break;
                    case CommandKind.SeekFood: SteerTowards(a, cmd.Direction, dt); break;
                    case CommandKind.Flee: SteerTowards(a, cmd.Direction, dt, 1.1); break;
                    case CommandKind.Confront: SteerTowards(a, cmd.Direction, dt, 1.05); break;
                    case CommandKind.Assist: DoAssist(a, cmd); break;
                }
            }
        }
    }
    private void ExecuteJet(SimActor actor, ActorCommand cmd, Queue<Vec2> pending) {
        var gesture = new PullChargeResult { Valid = true, Direction = cmd.Direction, Charge01 = cmd.Charge01 };
        double energy = actor.IsPlayer ? Soul.CurrentEnergy : 5.0; // ИИ-души: условный запас
        var plan = JetPlanner.Plan(actor.Body, actor.Organs, gesture, energy, _moveCfg);
        if (!plan.Valid) return;
        pending.Enqueue(plan.Impulse);
        actor.Vel += plan.Impulse; // зеркало скорости (Unity применит то же самое к rb)
        if (actor.IsPlayer) {
            Soul.CurrentEnergy = MathUtil.Max(0, Soul.CurrentEnergy - plan.EnergySpent);
            if (plan.DehydrationRisk) actor.Vitals.ApplyDamage(0.01); // риск при нехватке воды
        }
    }
    private void SteerTowards(SimActor actor, Vec2 dir, double dt, double speedScale = 1.0) {
        if (dir.Approx(Vec2.Zero, 1e-6)) return;
        // Мягкое руление вместо мгновенной скорости (просто ИИ, тот же физический мир).
        var acc = dir.Normalized() * 6.0 * speedScale;
        actor.Vel += acc * dt;
        var cap = actor.Organs.MaxSpeed * speedScale;
        if (actor.Vel.Length > cap) actor.Vel = actor.Vel.Normalized() * cap;
        if (Mode == SimMode.UnityDriven) _aiPendingImpulses[actor.Id].Enqueue(acc * dt);
    }
    private void DoAssist(SimActor ai, ActorCommand cmd) {
        var player = Player;
        double d = (player.Pos - ai.Pos).Length;
        if (d > Cfg.CaptureRadius * 1.5) return; // не дошёл — помощи нет
        double give = MathUtil.Min(0.3, ai.Body.Water * 0.5);
        if (give <= 0.01) return;
        ai.Body.SpendWater(give);
        player.Body.AddWater(give); // сохранение суммарной массы (SOUL_WORLD «Помощь»)
        RecordEvent("assist_water", $"ai_{ai.Id} -> player {give:0.###}");
        SoulGrowth.Apply(Soul, SoulGrowth.ActAssist, SoulGrowth.RewardFor(CountOf(SoulGrowth.ActAssist)));
        Player.LastSignificantAt = Clock.WorldTime;
    }

    // ---------- Интеграция и столкновения (только Headless) ----------
    private void IntegrateAndCollide(double dt) {
        Integrate(Player, dt);
        for (int i = 0; i < AiActors.Count; i++) Integrate(AiActors[i], dt);
        for (int i = 0; i < Beetles.Count; i++) IntegrateBeetle(Beetles[i], dt);
        CollideActorsWithWorld(Player, dt);
        for (int i = 0; i < AiActors.Count; i++) CollideActorsWithWorld(AiActors[i], dt);
        ActorImpacts(dt);
    }
    private void Integrate(SimActor a, double dt) {
        double drag = 1.0 - System.Math.Min(0.95, Cfg.LinearDragPerSec * dt);
        a.Vel = a.Vel * drag;
        var delta = a.Vel * dt;
        a.Pos = a.Pos + delta;
        a.PathLenAccum += delta.Length;
    }
    private void IntegrateBeetle(BeetlePrey b, double dt) {
        double drag = 1.0 - System.Math.Min(0.95, Cfg.LinearDragPerSec * dt);
        b.Vel = b.Vel * drag;
        b.Pos = b.Pos + b.Vel * dt;
        ClampToArena(ref b.Pos, ref b.Vel, b.Radius);
    }
    private void CollideActorsWithWorld(SimActor a, double dt) {
        ClampToArena(ref a.Pos, ref a.Vel, a.Radius);
    }
    private void ClampToArena(ref Vec2 pos, ref Vec2 vel, double r) {
        double hw = Cfg.HalfWidth - r, hh = Cfg.HalfHeight - r;
        if (pos.X < -hw) { pos = new Vec2(-hw, pos.Y); vel = new Vec2(-vel.X * Cfg.Restitution, vel.Y); }
        if (pos.X > hw) { pos = new Vec2(hw, pos.Y); vel = new Vec2(-vel.X * Cfg.Restitution, vel.Y); }
        if (pos.Y < -hh) { pos = new Vec2(pos.X, -hh); vel = new Vec2(vel.X, -vel.Y * Cfg.Restitution); }
        if (pos.Y > hh) { pos = new Vec2(pos.X, hh); vel = new Vec2(vel.X, -vel.Y * Cfg.Restitution); }
    }
    private void ActorImpacts(double dt) {
        ContactPair(Player, null);
        for (int i = 0; i < AiActors.Count; i++) ContactPair(Player, AiActors[i]);
        for (int i = 0; i < AiActors.Count; i++)
            for (int j = i + 1; j < AiActors.Count; j++) ContactPair(AiActors[i], AiActors[j]);
    }
    private void ContactPair(SimActor a, SimActor b) {
        // актор против актора и актор против жука
        if (b != null) {
            double d = (a.Pos - b.Pos).Length;
            double minD = a.Radius + b.Radius;
            if (d < minD) {
                var n = d < 1e-6 ? Vec2.Up : (a.Pos - b.Pos) / d;
                double relSpeed = System.Math.Abs(Vec2.Dot(a.Vel - b.Vel, n));
                ResolveOverlap(ref a.Pos, ref b.Pos, n, minD, d);
                Bounce(a, b, n);
                if (relSpeed > 0.01) {
                    RegisterImpact(new ImpactEvent { AttackerId = a.Id, TargetId = b.Id,
                        RelativeNormalSpeed = relSpeed, AttackerMass = a.Body.Total, TargetMass = b.Body.Total,
                        HitZoneMultiplier = 1.0, Penetration = 1.0, TargetResistance = 1.0 + b.Organs.ArmorResistance * 2.0 });
                    RegisterImpact(new ImpactEvent { AttackerId = b.Id, TargetId = a.Id,
                        RelativeNormalSpeed = relSpeed, AttackerMass = b.Body.Total, TargetMass = a.Body.Total,
                        HitZoneMultiplier = 1.0, Penetration = 1.0, TargetResistance = 1.0 + a.Organs.ArmorResistance * 2.0 });
                }
            }
            return;
        }
        // игрок/ИИ против жуков
        for (int k = 0; k < Beetles.Count; k++) {
            var bt = Beetles[k];
            if (bt.Consumed || bt.Phase >= PreyPhase.Internalized) continue;
            double d = (a.Pos - bt.Pos).Length;
            double minD = a.Radius + bt.Radius;
            if (d < minD) {
                var n = d < 1e-6 ? Vec2.Up : (a.Pos - bt.Pos) / d;
                double relSpeed = System.Math.Abs(Vec2.Dot(a.Vel - bt.Vel, n));
                // расталкивание
                bt.Pos = bt.Pos - n * (minD - d) * 0.5;
                a.Pos = a.Pos + n * (minD - d) * 0.5;
                // упругий отскок по нормали
                double vn = Vec2.Dot(a.Vel - bt.Vel, n);
                if (vn > 0) {
                    a.Vel = a.Vel - n * (vn * (1 + Cfg.Restitution) * 0.5);
                    bt.Vel = bt.Vel + n * (vn * (1 + Cfg.Restitution) * 0.5);
                }
                if (relSpeed > 0.5) {
                    // удар и встречный (жуку и игроку) — RegisterImpact сам дедуплицирует
                    var ev = new ImpactEvent {
                        AttackerId = a.Id, TargetId = bt.Id,
                        RelativeNormalSpeed = relSpeed,
                        AttackerMass = a.Body.Total, TargetMass = bt.Mass,
                        HitZoneMultiplier = 1.0, Penetration = 1.0, TargetResistance = 1.0
                    };
                    RegisterImpact(ev);
                    var rev = new ImpactEvent {
                        AttackerId = bt.Id, TargetId = a.Id,
                        RelativeNormalSpeed = relSpeed,
                        AttackerMass = bt.Mass, TargetMass = a.Body.Total,
                        HitZoneMultiplier = 1.0, Penetration = 1.0, TargetResistance = 1.0 + a.Organs.ArmorResistance * 2.0
                    };
                    RegisterImpact(rev);
                }
            }
        }
    }
    private void ResolveOverlap(ref Vec2 pa, ref Vec2 pb, Vec2 n, double minD, double d) {
        double push = (minD - d) * 0.5 + 1e-4;
        pa = pa + n * push;
        pb = pb - n * push;
    }
    private void Bounce(SimActor a, SimActor b, Vec2 n) {
        double vn = Vec2.Dot(a.Vel - b.Vel, n);
        if (vn <= 0) return;
        a.Vel = a.Vel - n * (vn * (1 + Cfg.Restitution) * 0.5);
        b.Vel = b.Vel + n * (vn * (1 + Cfg.Restitution) * 0.5);
    }
    private void ApplyActorDamage(SimActor from, SimActor to, double relSpeed) {
        double dmg = ImpactDamage.ComputeDamage(from.Body.Total, to.Body.Total, relSpeed,
            1.0, 1.0, 1.0 + to.Organs.ArmorResistance * 2.0, k: 0.02, cap: 0.25);
        to.Vitals.ApplyDamage(dmg);
    }
    private void ApplyBeetleDamage(BeetlePrey beetle, double attackerMass, double relSpeed) {
        double dmg = ImpactDamage.ComputeDamage(attackerMass, beetle.Mass, relSpeed,
            1.0, 1.0, 1.0, k: 0.06, cap: 0.4);
        beetle.Health01 = MathUtil.Clamp01(beetle.Health01 - dmg);
        if (beetle.Health01 <= Cfg.WeakenThreshold && beetle.Phase == PreyPhase.Free) {
            beetle.Phase = PreyPhase.Weakened;
            RecordEvent("beetle_weakened", beetle.Id.ToString());
        }
    }

    // ---------- Удары (единый путь: Headless и Unity) ----------
    public void RegisterImpact(ImpactEvent ev) {
        if (!_debouncer.TryRegister(ev.AttackerId, ev.TargetId, Clock.WorldTime)) return;
        if (ev.RelativeNormalSpeed < 0.5) return;
        TrackStreak(ev.RelativeNormalSpeed);
        var beetle = FindBeetle(ev.TargetId);
        if (beetle != null && !beetle.Consumed && beetle.Phase < PreyPhase.Internalized) {
            ApplyBeetleDamage(beetle, ev.AttackerMass, ev.RelativeNormalSpeed);
            return;
        }
        var targetAi = FindAi(ev.TargetId);
        var attackerAi = FindAi(ev.AttackerId);
        SimActor to = ev.TargetId == Player.Id ? Player : targetAi;
        SimActor from = ev.AttackerId == Player.Id ? Player : attackerAi;
        if (to == null || from == null || !to.Alive || !from.Alive) return;
        double dmgActor = ImpactDamage.ComputeDamage(from.Body.Total, to.Body.Total, ev.RelativeNormalSpeed,
            1.0, 1.0, 1.0 + to.Organs.ArmorResistance * 2.0, k: 0.02, cap: 0.25);
        to.Vitals.ApplyDamage(dmgActor);
        // соперник крадёт воду при результативном столкновении с игроком (T16) —
        // направление события не важно: пара «соперник × игрок»
        bool playerInvolved = from.IsPlayer || to.IsPlayer;
        var counterpart = from.IsPlayer ? to : from;
        var rivalProf = playerInvolved ? ProfileOf(counterpart.Id) : null;
        if (rivalProf != null && !rivalProf.Ally && Player.Body.Water > 0.2) {
            double stolen = Player.Body.SpendWater(0.2);
            counterpart.Body.AddWater(stolen * 0.9);
            if (stolen > 0.01) RecordEvent("water_stolen", $"ai_{counterpart.Id} from player");
        }
    }
    private void TrackStreak(double relSpeed) {
        double now = Clock.WorldTime;
        _impactStreak.Add(now);
        _impactStreak.RemoveAll(t => now - t > 4.0);
        if (_impactStreak.Count >= 3) {
            _impactStreak.Clear();
            SoulGrowth.Apply(Soul, SoulGrowth.ActComplexStreak, SoulGrowth.RewardFor(CountOf(SoulGrowth.ActComplexStreak)));
            Player.LastSignificantAt = Clock.WorldTime;
        }
    }

    // ---------- Экономика воды (D-16) и метаболизм ----------
    private void WaterEconomy(double dt) {
        SpendByPath(Player, dt);
        for (int i = 0; i < AiActors.Count; i++) SpendByPath(AiActors[i], dt);
        HydrationTick(Player, dt);
        for (int i = 0; i < AiActors.Count; i++) HydrationTick(AiActors[i], dt);
    }
    private void SpendByPath(SimActor a, double dt) {
        double path = a.PathLenAccum;
        a.PathLenAccum = 0;
        if (path <= 0) return;
        double retention = MathUtil.Clamp01(a.Organs.WaterRetention);
        double loss = path * Cfg.SurfaceLossRate * (1 - retention);
        double spent = a.Body.SpendWater(loss);
        if (a.IsPlayer && spent > 0) Trail.Feed(a.Pos, spent);
    }
    private void HydrationTick(SimActor a, double dt) {
        // нехватка воды: сначала запасы (P-03), затем урон целостности — независимо от движения
        if (a.Body.IsDehydrated) {
            a.Body.TryConvertReserveToWater(0.05, Cfg.MetabolizeReserveRate);
            if (a.Body.IsDehydrated) a.Vitals.ApplyDamage(Body.BodyVitals.DehydrationDamagePerSecond * dt);
        }
        else if (a.Body.Water < a.Body.MinimumViableWater * 1.5) {
            a.Body.TryConvertReserveToWater(0.02 * dt, Cfg.MetabolizeReserveRate);
        }
    }
    private void TrailEconomy(double dt) {
        Trail.Evaporate(dt);
        // повторный сбор только: игрок почти неподвижен и след уже не свежий —
        // иначе свежий след мгновенно всасывается и цена движения обнуляется
        bool standing = Player.Vel.Length < 0.3;
        if (standing) {
            double taken = Trail.Reabsorb(Player.Pos, Cfg.ReabsorbRadius, capacity: 0.5, maxFreshness: 0.7);
            if (taken > 0) Player.Body.AddWater(taken); // возврат без дублирования (QA-2)
        }
    }
    private void SoulEconomy(double dt) {
        // регенерация духовной энергии (тело проводит по своей способности)
        double cap = System.Math.Min(Soul.EnergyPotential, PlayerOrgans.Conductivity * 10 + 5);
        Soul.CurrentEnergy = MathUtil.Min(cap, Soul.CurrentEnergy + Cfg.EnergyRegenPerSec * dt);
        // «пережил опасность»: целостность падала < 0.3 и восстановилась
        if (Player.Vitals.Integrity < 0.3) _dangerMarked = true;
        else if (_dangerMarked && Player.Vitals.Integrity > 0.5) {
            _dangerMarked = false;
            SoulGrowth.Apply(Soul, SoulGrowth.ActSurviveDanger, SoulGrowth.RewardFor(CountOf(SoulGrowth.ActSurviveDanger)));
            Player.LastSignificantAt = Clock.WorldTime;
        }
        SoulGrowth.DecayIfIdle(Soul, Clock.WorldTime - Player.LastSignificantAt);
        // смерть тела: душа продолжит (T17), мир фиксирует событие
        if (!Player.Vitals.IsAlive && Player.Alive) {
            Player.Alive = false;
            RecordEvent("player_death", $"life #{Soul.LivesLived + 1}");
        }
    }
    private int CountOf(string action) => Soul.GrowthCounts.TryGetValue(action, out var c) ? c : 0;
    public void RecordEvent(string kind, string detail) {
        Events.Add(new WorldEvent { Kind = kind, Tick = Clock.TickIndex, Detail = detail });
        if (Events.Count > 64) Events.RemoveAt(0);
    }
}
}
