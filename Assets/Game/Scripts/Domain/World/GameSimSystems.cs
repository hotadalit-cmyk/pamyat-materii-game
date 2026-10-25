using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Fields;
using PM.Domain.Absorption;
using PM.Domain.Genetics;
using PM.Domain.Metamorphosis;
using PM.Domain.Soul;
using PM.Domain.AI;

namespace PM.Domain.World {
/// <summary>Часть GameSim: биогравитация, питание, захват, пищеварение, гены, формы, ИИ.</summary>
public sealed partial class GameSim {
    // ---------- Биогравитация и питание ----------
    /// <summary>Ускорение для гранулы (Unity применит силу; Headless двигает сам).</summary>
    public Vec2 PelletPull(FoodPellet p) {
        return BiogravityPull.ComputeAcceleration(Player.Body.Total, p.Mass,
            Player.Pos - p.Pos, (Player.Pos - p.Pos).Length, new BiogravityPull.Config());
    }
    private void BiogravityAndFood(double dt) {
        for (int i = Food.Count - 1; i >= 0; i--) {
            var p = Food[i];
            if (p.Consumed) continue;
            if (Mode == SimMode.Headless) {
                var acc = PelletPull(p);
                p.Pos = p.Pos + acc * dt * dt; // x = x0 + a*dt² (скорость гранулы не моделируем)
            }
            TryConsumePelletByProximity(p);
        }
    }
    private void TryConsumePelletByProximity(FoodPellet p) {
        double d = (p.Pos - Player.Pos).Length;
        if (d > Player.Radius + 0.15) return;
        TryConsumePellet(p.Id);
    }
    /// <summary>Поглощение гранулы — ровно один раз (инвариант дублирования, QA-2).</summary>
    public bool TryConsumePellet(int pelletId) {
        var p = FindPellet(pelletId);
        if (p == null || p.Consumed) return false;
        p.Consumed = true;
        Player.Body.AddWater(p.Mass * 0.6);
        Player.Body.AddReserve(p.Mass * 0.4);
        Stock.Add(0, p.Mass * 0.2, 0, 0);
        if (!_firstDiscoverDone) {
            _firstDiscoverDone = true;
            SoulGrowth.Apply(Soul, SoulGrowth.ActFirstDiscover, SoulGrowth.BaseReward);
        }
        Player.LastSignificantAt = Clock.WorldTime;
        RecordEvent("pellet_consumed", p.Id.ToString());
        return true;
    }

    // ---------- Захват (T09) ----------
    public BeetlePrey CaptureTarget {
        get {
            for (int i = 0; i < Beetles.Count; i++) {
                var b = Beetles[i];
                if (b.Phase == PreyPhase.Weakened && !b.Consumed) return b;
            }
            return null;
        }
    }
    private void UpdateCapture(double dt) {
        var target = CaptureTarget;
        if (target == null) {
            if (Capture.Active) Capture.Tick(false, 0, 0, 1, dt, ref _unusedEnergy);
            return;
        }
        if (!Capture.Active && CaptureRequested) {
            Capture.Start(target.Id);
            CaptureRequested = false;
        }
        if (!Capture.Active) return;
        if (Capture.TargetId != target.Id) { Capture.Stop(); return; }
        double dist = (target.Pos - Player.Pos).Length;
        bool inRange = dist <= Cfg.CaptureRadius + Player.Radius;
        double strength = Player.Organs.CaptureStrength *
                          Unlocked.SumEffect(GeneCatalogRef, "capture_strength") * 2.0;
        double energy = Soul.CurrentEnergy;
        bool done = Capture.Tick(inRange, strength / target.Resistance, target.Health01,
                                target.Mass / 2.0, dt, ref energy);
        Soul.CurrentEnergy = energy;
        if (done) {
            target.Phase = PreyPhase.Internalized;
            var yield = new DigestionYield {
                Water = 0.6, Organic = 0.9, Chitin = 1.4, Mineral = 0.4,
                GeneFragmentId = PM.Domain.Genetics.GeneCatalog.GeneChitin
            };
            bool added = Chamber.TryAdd(target.Id, MaterialClass.Chitin, target.Mass,
                                        seconds: 8.0, yield);
            if (!added) target.Phase = PreyPhase.Weakened; // нет места — добыча снова снаружи
            else {
                Player.Body.AddCargo(target.Mass); // груз физически переносится телом
                RecordEvent("beetle_internalized", target.Id.ToString());
                SoulGrowth.Apply(Soul, SoulGrowth.ActCapture, SoulGrowth.RewardFor(CountOf(SoulGrowth.ActCapture)));
                Player.LastSignificantAt = Clock.WorldTime;
            }
        }
    }
    public bool CaptureRequested;

    // ---------- Пищеварение и гены ----------
    private void HarvestChamber() {
        var ready = Chamber.HarvestReadyItems();
        for (int i = 0; i < ready.Count; i++) {
            var it = ready[i];
            Player.Body.RemoveCargo(it.CargoMass); // груз уходит из тела с выдачей (баланс)
            var y = it.Yield;
            Player.Body.AddWater(y.Water);
            Stock.Add(y.Water, y.Organic, y.Chitin, y.Mineral);
            if (y.GeneFragmentId != 0) {
                string source = "source_beetle_" + y.GeneFragmentId;
                if (Genome.TryExtract(y.GeneFragmentId, source)) {
                    Soul.LearnGene(source);
                    RecordEvent("gene_extracted", y.GeneFragmentId.ToString());
                }
            }
            RecordEvent("digestion_complete", $"w{y.Water:0.##} o{y.Organic:0.##} c{y.Chitin:0.##}");
        }
    }

    // ---------- Гены: разблокировка (UI вызывает напрямую) ----------
    public string TryUnlockNode(string nodeId) {
        for (int i = 0; i < GeneCatalogRef.Length; i++) {
            var n = GeneCatalogRef[i];
            if (n.Id != nodeId) continue;
            bool ok = GeneNetworkService.TryUnlock(n, Unlocked, Genome, Stock);
            if (!ok) return "cannot_unlock";
            ApplyNodeEffects(n);
            RecordEvent("node_unlocked", nodeId);
            return null;
        }
        return "unknown_node";
    }
    private void ApplyNodeEffects(GeneNode n) {
        switch (n.EffectId) {
            case "armor_resist": PlayerOrgans.ArmorResistance += n.EffectValue; break;
            case "water_retention": PlayerOrgans.WaterRetention += n.EffectValue; break;
            case "conductivity": PlayerOrgans.Conductivity += n.EffectValue; break;
            case "capture_strength": PlayerOrgans.CaptureStrength += n.EffectValue; break;
            case "sense_chem": /* сенсорный слой Systems подхватит флаг */ break;
            case "structure_cap": Player.Body.MinimumViableWater += 0; break;
            case "form_beetle": /* форма доступна через MetamorphoseTo */ break;
        }
    }
    /// <summary>Пересборка органов: база текущей формы + эффекты открытых узлов.
    /// Используется после метаморфозы и реинкарнации.</summary>
    public void ReapplyNodeEffects() {
        var def = Metamorphosis.FormCatalog.Find(CurrentFormId);
        PlayerOrgans = def.Organs;
        for (int i = 0; i < GeneCatalogRef.Length; i++)
            if (Unlocked.Has(GeneCatalogRef[i].Id)) ApplyNodeEffects(GeneCatalogRef[i]);
    }

    // ---------- Метаморфоза (T12) ----------
    public MetamorphosisService.Result CurrentFormResult;
    public string CurrentFormId = "form_slime";
    public MetamorphosisService.Result MetamorphoseTo(string formId) {
        FormDef def;
        string reqNode;
        if (formId == "form_chitin_slime") { def = FormCatalog.ChitinSlime(); reqNode = "n_chitin_plate"; }
        else if (formId == "form_beetle") { def = FormCatalog.Beetle(); reqNode = "n_beetle_form"; }
        else { def = FormCatalog.Slime(); reqNode = null; }
        var r = MetamorphosisService.Metamorphose(def, Unlocked, reqNode, Stock, Player.Body);
        CurrentFormResult = r;
        if (r.Ok) {
            CurrentFormId = def.Id;
            ReapplyNodeEffects(); // база формы + эффекты узлов
            Player.FormId = def.Id;
            SoulGrowth.Apply(Soul, SoulGrowth.ActMetamorphose, SoulGrowth.RewardFor(CountOf(SoulGrowth.ActMetamorphose)));
            Player.LastSignificantAt = Clock.WorldTime;
            RecordEvent("metamorphosed", def.Id);
        }
        return r;
    }
    public MetamorphosisService.Result RevertForm() {
        FormDef current = FormCatalog.Find(CurrentFormId);
        var r = MetamorphosisService.RevertToSlime(current, Stock);
        if (r.Ok) {
            CurrentFormId = "form_slime";
            PlayerOrgans = FormCatalog.Slime().Organs;
            Player.FormId = "form_slime";
            RecordEvent("form_reverted", "form_slime");
        }
        return r;
    }

    // ---------- ИИ (T15/T16) ----------
    private void AiThink(double dt) {
        _aiCadence += dt;
        if (_aiCadence < 0.5) return;
        _aiCadence = 0;
        for (int i = 0; i < AiActors.Count; i++) {
            var a = AiActors[i];
            if (!a.Alive) continue;
            var prof = ProfileOf(a.Id);
            if (prof == null) continue;
            var ctx = BuildAiContext(a);
            EnqueueCommand(a.Id, prof.Decide(ctx));
        }
    }
    public AiContext BuildAiContext(SimActor a) {
        var ctx = new AiContext {
            SelfPos = a.Pos, SelfMass = a.Body.Total,
            SelfWater01 = System.Math.Min(1, a.Body.Water / MathUtil.Max(0.1, a.Body.MinimumViableWater * 3)),
            PlayerPos = Player.Pos, PlayerMass = Player.Body.Total,
            PlayerWeakenedWater = Player.Body.Water < Player.Body.MinimumViableWater * 1.2,
            Tick = Clock.TickIndex
        };
        double best = double.MaxValue;
        for (int i = 0; i < Food.Count; i++) {
            var p = Food[i];
            if (p.Consumed) continue;
            double d = (p.Pos - a.Pos).Length;
            if (d < best) { best = d; ctx.NearestFoodDir = p.Pos - a.Pos; ctx.NearestFoodDist = d; }
        }
        return ctx;
    }
    private double _unusedEnergy = 0;

    // ---------- Память ИИ о событиях ----------
    public void NotifyAiOfAttack(int aiActorId) {
        var prof = ProfileOf(aiActorId);
        prof?.Memory.Record(MemoryEventKind.AttackedBy, Player.Id, Clock.TickIndex);
    }
    public void NotifyAiOfHelp(int aiActorId) {
        var prof = ProfileOf(aiActorId);
        prof?.Memory.Record(MemoryEventKind.HelpedBy, Player.Id, Clock.TickIndex);
    }
}
}
