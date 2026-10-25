using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Absorption;
using PM.Domain.World;
namespace PM.Persistence {
/// <summary>Мост GameSim ↔ SaveData: экспорт/импорт полного состояния.</summary>
public static class GameSimSave {
    public static SaveData Export(GameSim sim) {
        var d = new SaveData();
        d.Schema = SaveData.SchemaVersion;
        d.Tick = sim.Clock.TickIndex;
        d.WorldTime = sim.Clock.WorldTime;
        var p = sim.Player;
        d.PX = p.Pos.X; d.PY = p.Pos.Y; d.VX = p.Vel.X; d.VY = p.Vel.Y;
        d.Structure = p.Body.Structure; d.Water = p.Body.Water;
        d.Reserve = p.Body.Reserve; d.Cargo = p.Body.Cargo;
        d.MinWater = p.Body.MinimumViableWater;
        d.Integrity = p.Vitals.Integrity;
        d.FormId = sim.CurrentFormId;
        d.OrganPower = sim.PlayerOrgans.ImpulsePower;
        d.OrganMinSpeed = sim.PlayerOrgans.MinSpeed;
        d.OrganMaxSpeed = sim.PlayerOrgans.MaxSpeed;
        d.OrganRetention = sim.PlayerOrgans.WaterRetention;
        d.OrganConductivity = sim.PlayerOrgans.Conductivity;
        d.OrganArmor = sim.PlayerOrgans.ArmorResistance;
        d.OrganCapture = sim.PlayerOrgans.CaptureStrength;
        d.SoulEnergy = sim.Soul.CurrentEnergy;
        d.SoulEnergyPot = sim.Soul.EnergyPotential;
        d.SoulEndurance = sim.Soul.Endurance;
        d.SoulMagic = sim.Soul.MagicReserve;
        d.SoulKarma = sim.Soul.Karma;
        d.LivesLived = sim.Soul.LivesLived;
        d.LastSignificantAt = (long)p.LastSignificantAt;
        var kg = new List<string>(sim.Soul.KnowledgeGenes);
        d.KnowledgeGenes = kg.ToArray();
        d.GrowthCounts = new Dictionary<string, int>(sim.Soul.GrowthCounts);
        d.StockWater = sim.Stock.Water; d.StockOrganic = sim.Stock.Organic;
        d.StockChitin = sim.Stock.Chitin; d.StockMineral = sim.Stock.Mineral;
        var nodes = new List<string>(sim.Unlocked.Ids);
        d.UnlockedNodes = nodes.ToArray();
        // genome
        var ids = new List<int>(); var srcs = new List<string>(); var used = new List<bool>();
        foreach (var f in sim.Genome.Fragments) { ids.Add(f.Id); srcs.Add(f.SourceKey); used.Add(f.ConsumedByNode); }
        d.FragIds = ids.ToArray(); d.FragSources = srcs.ToArray(); d.FragUsed = used.ToArray();
        // digestion
        var items = sim.Chamber.Items;
        var dP = new List<int>(); var dC = new List<int>(); var dCa = new List<double>();
        var dR = new List<double>(); var dT = new List<double>();
        var yW = new List<double>(); var yO = new List<double>(); var yC = new List<double>();
        var yM = new List<double>(); var yG = new List<int>();
        for (int i = 0; i < items.Count; i++) {
            var it = items[i];
            dP.Add(it.PreyId); dC.Add((int)it.Class); dCa.Add(it.CargoMass);
            dR.Add(it.RemainingSec); dT.Add(it.TotalSec);
            yW.Add(it.Yield.Water); yO.Add(it.Yield.Organic);
            yC.Add(it.Yield.Chitin); yM.Add(it.Yield.Mineral); yG.Add(it.Yield.GeneFragmentId);
        }
        d.DigPreyId = dP.ToArray(); d.DigClass = dC.ToArray(); d.DigCargo = dCa.ToArray();
        d.DigRemain = dR.ToArray(); d.DigTotal = dT.ToArray();
        d.YWater = yW.ToArray(); d.YOrganic = yO.ToArray(); d.YChitin = yC.ToArray();
        d.YMineral = yM.ToArray(); d.YGene = yG.ToArray();
        // capture
        d.CaptureActive = sim.Capture.Active;
        d.CaptureTarget = sim.Capture.TargetId;
        d.CaptureProgress = sim.Capture.Progress;
        // pellets
        var pIds = new List<int>(); var pX = new List<double>(); var pY = new List<double>();
        var pM = new List<double>(); var pU = new List<bool>();
        for (int i = 0; i < sim.Food.Count; i++) {
            var f = sim.Food[i];
            pIds.Add(f.Id); pX.Add(f.Pos.X); pY.Add(f.Pos.Y); pM.Add(f.Mass); pU.Add(f.Consumed);
        }
        d.PelletIds = pIds.ToArray(); d.PelletX = pX.ToArray(); d.PelletY = pY.ToArray();
        d.PelletMass = pM.ToArray(); d.PelletConsumed = pU.ToArray();
        // beetles
        var bIds = new List<int>(); var bX = new List<double>(); var bY = new List<double>();
        var bHp = new List<double>(); var bM = new List<double>(); var bPh = new List<int>();
        for (int i = 0; i < sim.Beetles.Count; i++) {
            var b = sim.Beetles[i];
            bIds.Add(b.Id); bX.Add(b.Pos.X); bY.Add(b.Pos.Y);
            bHp.Add(b.Health01); bM.Add(b.Mass); bPh.Add((int)b.Phase);
        }
        d.BeetleIds = bIds.ToArray(); d.BeetleX = bX.ToArray(); d.BeetleY = bY.ToArray();
        d.BeetleHealth = bHp.ToArray(); d.BeetleMass = bM.ToArray(); d.BeetlePhase = bPh.ToArray();
        // trail
        var tIds = new List<int>(); var tX = new List<double>(); var tY = new List<double>();
        var tM = new List<double>(); var tF = new List<double>();
        for (int i = 0; i < sim.Trail.Records.Count; i++) {
            var r = sim.Trail.Records[i];
            tIds.Add(r.Id); tX.Add(r.Pos.X); tY.Add(r.Pos.Y); tM.Add(r.Mass); tF.Add(r.Freshness);
        }
        d.TrailIds = tIds.ToArray(); d.TrailX = tX.ToArray(); d.TrailY = tY.ToArray();
        d.TrailMass = tM.ToArray(); d.TrailFresh = tF.ToArray();
        // ai
        var aIds = new List<int>(); var aAl = new List<bool>(); var aX = new List<double>();
        var aY = new List<double>(); var aS = new List<double>(); var aW = new List<double>();
        var aR = new List<double>(); var aT = new List<double>();
        var mOw = new List<int>(); var mKd = new List<int>(); var mAc = new List<int>();
        var mTk = new List<long>();
        for (int i = 0; i < sim.AiActors.Count; i++) {
            var a = sim.AiActors[i];
            aIds.Add(a.Id); aX.Add(a.Pos.X); aY.Add(a.Pos.Y);
            aS.Add(a.Body.Structure); aW.Add(a.Body.Water); aR.Add(a.Body.Reserve);
            var prof = sim.ProfileOf(a.Id);
            aAl.Add(prof != null && prof.Ally);
            aT.Add(prof != null ? prof.Memory.Trust : 0);
            if (prof != null) {
                var evs = prof.Memory.Events;
                for (int e = 0; e < evs.Count; e++) {
                    mOw.Add(a.Id); mKd.Add((int)evs[e].Kind);
                    mAc.Add(evs[e].ActorId); mTk.Add(evs[e].Tick);
                }
            }
        }
        d.AiIds = aIds.ToArray(); d.AiAlly = aAl.ToArray(); d.AiX = aX.ToArray();
        d.AiY = aY.ToArray(); d.AiStr = aS.ToArray(); d.AiWater = aW.ToArray();
        d.AiReserve = aR.ToArray(); d.AiTrust = aT.ToArray();
        d.AiEventOwner = mOw.ToArray(); d.AiEventKind = mKd.ToArray();
        d.AiEventActor = mAc.ToArray(); d.AiEventTick = mTk.ToArray();
        // events
        var eK = new List<string>(); var eD = new List<string>(); var eT = new List<long>();
        for (int i = 0; i < sim.Events.Count; i++) {
            eK.Add(sim.Events[i].Kind); eD.Add(sim.Events[i].Detail); eT.Add(sim.Events[i].Tick);
        }
        d.EventKind = eK.ToArray(); d.EventDetail = eD.ToArray(); d.EventTick = eT.ToArray();
        return d;
    }
    public static void Import(GameSim sim, SaveData d) {
        var p = sim.Player;
        p.Pos = new Vec2(d.PX, d.PY); p.Vel = new Vec2(d.VX, d.VY);
        p.Body.Structure = d.Structure; p.Body.Water = d.Water;
        p.Body.Reserve = d.Reserve; p.Body.Cargo = d.Cargo;
        p.Body.MinimumViableWater = d.MinWater;
        p.Vitals.Integrity = d.Integrity;
        sim.CurrentFormId = string.IsNullOrEmpty(d.FormId) ? "form_slime" : d.FormId;
        p.FormId = sim.CurrentFormId;
        var organs = p.Organs;
        organs.ImpulsePower = d.OrganPower; organs.MinSpeed = d.OrganMinSpeed;
        organs.MaxSpeed = d.OrganMaxSpeed; organs.WaterRetention = d.OrganRetention;
        organs.Conductivity = d.OrganConductivity; organs.ArmorResistance = d.OrganArmor;
        organs.CaptureStrength = d.OrganCapture;
        sim.PlayerOrgans = organs;
        sim.Soul.CurrentEnergy = d.SoulEnergy;
        sim.Soul.EnergyPotential = d.SoulEnergyPot;
        sim.Soul.Endurance = d.SoulEndurance;
        sim.Soul.MagicReserve = d.SoulMagic;
        sim.Soul.Karma = d.SoulKarma;
        sim.Soul.LivesLived = d.LivesLived;
        p.LastSignificantAt = d.LastSignificantAt;
        sim.Soul.KnowledgeGenes.Clear();
        if (d.KnowledgeGenes != null)
            for (int i = 0; i < d.KnowledgeGenes.Length; i++) sim.Soul.LearnGene(d.KnowledgeGenes[i]);
        sim.Soul.GrowthCounts.Clear();
        if (d.GrowthCounts != null)
            foreach (var kv in d.GrowthCounts) sim.Soul.GrowthCounts[kv.Key] = kv.Value;
        sim.Stock.Water = d.StockWater; sim.Stock.Organic = d.StockOrganic;
        sim.Stock.Chitin = d.StockChitin; sim.Stock.Mineral = d.StockMineral;
        if (d.UnlockedNodes != null)
            for (int i = 0; i < d.UnlockedNodes.Length; i++) sim.Unlocked.Add(d.UnlockedNodes[i]);
        if (d.FragIds != null)
            for (int i = 0; i < d.FragIds.Length; i++) {
                sim.Genome.TryExtract(d.FragIds[i], d.FragSources[i]);
                if (d.FragUsed[i]) sim.Genome.TryConsume(d.FragIds[i]);
            }
        if (d.DigPreyId != null)
            for (int i = 0; i < d.DigPreyId.Length; i++) {
                sim.Chamber.TryAdd(d.DigPreyId[i], (MaterialClass)d.DigClass[i], d.DigCargo[i],
                    d.DigTotal[i], new DigestionYield {
                        Water = d.YWater[i], Organic = d.YOrganic[i],
                        Chitin = d.YChitin[i], Mineral = d.YMineral[i],
                        GeneFragmentId = d.YGene[i]
                    }, startRemainingSec: d.DigRemain[i]);
            }
        if (d.CaptureActive && d.CaptureTarget > 0) {
            sim.Capture.Start(d.CaptureTarget);
            sim.Capture.RestoreProgress(d.CaptureProgress);
        }
        sim.Food.Clear();
        if (d.PelletIds != null)
            for (int i = 0; i < d.PelletIds.Length; i++)
                sim.Food.Add(new FoodPellet {
                    Id = d.PelletIds[i], Pos = new Vec2(d.PelletX[i], d.PelletY[i]),
                    Mass = d.PelletMass[i], Consumed = d.PelletConsumed[i]
                });
        sim.Beetles.Clear();
        if (d.BeetleIds != null)
            for (int i = 0; i < d.BeetleIds.Length; i++)
                sim.Beetles.Add(new BeetlePrey {
                    Id = d.BeetleIds[i], Pos = new Vec2(d.BeetleX[i], d.BeetleY[i]),
                    Health01 = d.BeetleHealth[i], Mass = d.BeetleMass[i],
                    Phase = (PreyPhase)d.BeetlePhase[i]
                });
        sim.Trail.ClearForImport();
        if (d.TrailIds != null)
            for (int i = 0; i < d.TrailIds.Length; i++)
                sim.Trail.AddRecordForImport(d.TrailIds[i],
                    new Vec2(d.TrailX[i], d.TrailY[i]), d.TrailMass[i], d.TrailFresh[i]);
        sim.AiActors.Clear();
        if (d.AiIds != null)
            for (int i = 0; i < d.AiIds.Length; i++) {
                var a = sim.AddAiActorWithId(d.AiIds[i], d.AiAlly[i], new Vec2(d.AiX[i], d.AiY[i]));
                a.Body.Structure = d.AiStr[i]; a.Body.Water = d.AiWater[i];
                a.Body.Reserve = d.AiReserve[i];
                var prof = sim.ProfileOf(a.Id);
                if (prof != null) prof.Memory.Trust = d.AiTrust[i];
            }
        if (d.AiEventOwner != null)
            for (int i = 0; i < d.AiEventOwner.Length; i++)
                sim.ProfileOf(d.AiEventOwner[i])?.Memory.Record(
                    (PM.Domain.AI.MemoryEventKind)d.AiEventKind[i],
                    d.AiEventActor[i], d.AiEventTick[i]);
        sim.Events.Clear();
        if (d.EventKind != null)
            for (int i = 0; i < d.EventKind.Length; i++)
                sim.RecordEvent(d.EventKind[i], d.EventDetail[i]);
    }
}
}
