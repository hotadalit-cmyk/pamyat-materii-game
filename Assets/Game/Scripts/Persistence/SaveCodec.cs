using System;
using System.Collections.Generic;
using PM.Domain.World;
namespace PM.Persistence {
/// <summary>
/// Кодек сохранения: явная сериализация SaveData в JSON (детерминированный порядок полей)
/// и обратно. Читаемая структура: schema → player → soul → stock → nodes → genom →
/// digestion → capture → pellets → beetles → trail → ai → events.
/// </summary>
public static class SaveCodec {
    public static string Serialize(SaveData d) {
        var w = new MiniJson.Writer();
        w.BeginObject();
        w.Key("schema").Value((long)d.Schema);
        w.Key("tick").Value(d.Tick);
        w.Key("worldTime").Value(d.WorldTime);
        // --- player ---
        w.Key("px").Value(d.PX); w.Key("py").Value(d.PY);
        w.Key("vx").Value(d.VX); w.Key("vy").Value(d.VY);
        w.Key("structure").Value(d.Structure);
        w.Key("water").Value(d.Water);
        w.Key("reserve").Value(d.Reserve);
        w.Key("cargo").Value(d.Cargo);
        w.Key("minWater").Value(d.MinWater);
        w.Key("integrity").Value(d.Integrity);
        w.Key("form").Value(d.FormId ?? "form_slime");
        w.Key("oPower").Value(d.OrganPower);
        w.Key("oMinSpeed").Value(d.OrganMinSpeed);
        w.Key("oMaxSpeed").Value(d.OrganMaxSpeed);
        w.Key("oRetention").Value(d.OrganRetention);
        w.Key("oConductivity").Value(d.OrganConductivity);
        w.Key("oArmor").Value(d.OrganArmor);
        w.Key("oCapture").Value(d.OrganCapture);
        // --- soul ---
        w.Key("sEnergy").Value(d.SoulEnergy);
        w.Key("sEnergyPot").Value(d.SoulEnergyPot);
        w.Key("sEndurance").Value(d.SoulEndurance);
        w.Key("sMagic").Value(d.SoulMagic);
        w.Key("sKarma").Value(d.SoulKarma);
        w.Key("sLives").Value((long)d.LivesLived);
        w.Key("sLastSignificant").Value(d.LastSignificantAt);
        WriteStringArray(w, "sKnowledge", d.KnowledgeGenes);
        if (d.GrowthCounts != null && d.GrowthCounts.Count > 0) {
            w.Key("sGrowth").BeginObject();
            foreach (var kv in d.GrowthCounts) w.Key(kv.Key).Value((long)kv.Value);
            w.EndObject();
        }
        // --- stock ---
        w.Key("stWater").Value(d.StockWater);
        w.Key("stOrganic").Value(d.StockOrganic);
        w.Key("stChitin").Value(d.StockChitin);
        w.Key("stMineral").Value(d.StockMineral);
        WriteStringArray(w, "nodes", d.UnlockedNodes);
        // --- genome ---
        w.Key("genom").BeginArray();
        if (d.FragIds != null)
            for (int i = 0; i < d.FragIds.Length; i++) {
                w.BeginObject();
                w.Key("id").Value((long)d.FragIds[i]);
                w.Key("src").Value(d.FragSources[i] ?? "");
                w.Key("used").Value(d.FragUsed[i]);
                w.EndObject();
            }
        w.EndArray();
        // --- digestion ---
        w.Key("digestion").BeginArray();
        if (d.DigPreyId != null)
            for (int i = 0; i < d.DigPreyId.Length; i++) {
                w.BeginObject();
                w.Key("prey").Value((long)d.DigPreyId[i]);
                w.Key("cls").Value((long)d.DigClass[i]);
                w.Key("cargo").Value(d.DigCargo[i]);
                w.Key("remain").Value(d.DigRemain[i]);
                w.Key("total").Value(d.DigTotal[i]);
                w.Key("yWater").Value(d.YWater[i]);
                w.Key("yOrganic").Value(d.YOrganic[i]);
                w.Key("yChitin").Value(d.YChitin[i]);
                w.Key("yMineral").Value(d.YMineral[i]);
                w.Key("yGene").Value((long)d.YGene[i]);
                w.EndObject();
            }
        w.EndArray();
        // --- capture ---
        w.Key("capActive").Value(d.CaptureActive);
        w.Key("capTarget").Value((long)d.CaptureTarget);
        w.Key("capProgress").Value(d.CaptureProgress);
        // --- pellets ---
        w.Key("pellets").BeginArray();
        if (d.PelletIds != null)
            for (int i = 0; i < d.PelletIds.Length; i++) {
                w.BeginObject();
                w.Key("id").Value((long)d.PelletIds[i]);
                w.Key("x").Value(d.PelletX[i]);
                w.Key("y").Value(d.PelletY[i]);
                w.Key("m").Value(d.PelletMass[i]);
                w.Key("used").Value(d.PelletConsumed[i]);
                w.EndObject();
            }
        w.EndArray();
        // --- beetles ---
        w.Key("beetles").BeginArray();
        if (d.BeetleIds != null)
            for (int i = 0; i < d.BeetleIds.Length; i++) {
                w.BeginObject();
                w.Key("id").Value((long)d.BeetleIds[i]);
                w.Key("x").Value(d.BeetleX[i]);
                w.Key("y").Value(d.BeetleY[i]);
                w.Key("hp").Value(d.BeetleHealth[i]);
                w.Key("m").Value(d.BeetleMass[i]);
                w.Key("phase").Value((long)d.BeetlePhase[i]);
                w.EndObject();
            }
        w.EndArray();
        // --- trail ---
        w.Key("trail").BeginArray();
        if (d.TrailIds != null)
            for (int i = 0; i < d.TrailIds.Length; i++) {
                w.BeginObject();
                w.Key("id").Value((long)d.TrailIds[i]);
                w.Key("x").Value(d.TrailX[i]);
                w.Key("y").Value(d.TrailY[i]);
                w.Key("m").Value(d.TrailMass[i]);
                w.Key("f").Value(d.TrailFresh[i]);
                w.EndObject();
            }
        w.EndArray();
        // --- ai ---
        w.Key("ai").BeginArray();
        if (d.AiIds != null)
            for (int i = 0; i < d.AiIds.Length; i++) {
                w.BeginObject();
                w.Key("id").Value((long)d.AiIds[i]);
                w.Key("ally").Value(d.AiAlly[i]);
                w.Key("x").Value(d.AiX[i]);
                w.Key("y").Value(d.AiY[i]);
                w.Key("structure").Value(d.AiStr[i]);
                w.Key("water").Value(d.AiWater[i]);
                w.Key("reserve").Value(d.AiReserve[i]);
                w.Key("trust").Value(d.AiTrust[i]);
                w.EndObject();
            }
        w.EndArray();
        w.Key("aiMem").BeginArray();
        if (d.AiEventOwner != null)
            for (int i = 0; i < d.AiEventOwner.Length; i++) {
                w.BeginObject();
                w.Key("owner").Value((long)d.AiEventOwner[i]);
                w.Key("kind").Value((long)d.AiEventKind[i]);
                w.Key("actor").Value((long)d.AiEventActor[i]);
                w.Key("tick").Value(d.AiEventTick[i]);
                w.EndObject();
            }
        w.EndArray();
        // --- events ---
        w.Key("events").BeginArray();
        if (d.EventKind != null)
            for (int i = 0; i < d.EventKind.Length; i++) {
                w.BeginObject();
                w.Key("kind").Value(d.EventKind[i] ?? "");
                w.Key("tick").Value(d.EventTick[i]);
                w.Key("detail").Value(d.EventDetail[i] ?? "");
                w.EndObject();
            }
        w.EndArray();
        w.EndObject();
        return w.ToString();
    }
    private static void WriteStringArray(MiniJson.Writer w, string key, string[] arr) {
        w.Key(key).BeginArray();
        if (arr != null)
            for (int i = 0; i < arr.Length; i++) w.Value(arr[i] ?? "");
        w.EndArray();
    }
    // ================= Deserialize =================
    public static SaveData Deserialize(string json) {
        object root = MiniJson.Parse(json);
        var o = root as Dictionary<string, object>;
        if (o == null) throw new MiniJson.MiniJsonException("root_not_object");
        var d = new SaveData();
        d.Schema = (int)Num(o, "schema");
        if (d.Schema != SaveData.SchemaVersion) throw new MiniJson.MiniJsonException("schema_mismatch");
        d.Tick = (long)Num(o, "tick");
        d.WorldTime = Num(o, "worldTime");
        d.PX = Num(o, "px"); d.PY = Num(o, "py");
        d.VX = Num(o, "vx"); d.VY = Num(o, "vy");
        d.Structure = Num(o, "structure"); d.Water = Num(o, "water");
        d.Reserve = Num(o, "reserve"); d.Cargo = Num(o, "cargo");
        d.MinWater = Num(o, "minWater"); d.Integrity = Num(o, "integrity");
        d.FormId = Str(o, "form");
        d.OrganPower = Num(o, "oPower"); d.OrganMinSpeed = Num(o, "oMinSpeed");
        d.OrganMaxSpeed = Num(o, "oMaxSpeed"); d.OrganRetention = Num(o, "oRetention");
        d.OrganConductivity = Num(o, "oConductivity"); d.OrganArmor = Num(o, "oArmor");
        d.OrganCapture = Num(o, "oCapture");
        d.SoulEnergy = Num(o, "sEnergy"); d.SoulEnergyPot = Num(o, "sEnergyPot");
        d.SoulEndurance = Num(o, "sEndurance"); d.SoulMagic = Num(o, "sMagic");
        d.SoulKarma = Num(o, "sKarma"); d.LivesLived = (int)Num(o, "sLives");
        d.LastSignificantAt = (long)Num(o, "sLastSignificant");
        d.KnowledgeGenes = StrArray(o, "sKnowledge");
        d.GrowthCounts = new Dictionary<string, int>();
        if (o.TryGetValue("sGrowth", out var gv) && gv is Dictionary<string, object> gc) {
            foreach (var kv in gc) d.GrowthCounts[kv.Key] = (int)AsNum(kv.Value);
        }
        d.StockWater = Num(o, "stWater"); d.StockOrganic = Num(o, "stOrganic");
        d.StockChitin = Num(o, "stChitin"); d.StockMineral = Num(o, "stMineral");
        d.UnlockedNodes = StrArray(o, "nodes");
        var fragList = ObjArray(o, "genom");
        var fIds = new List<int>(); var fSrc = new List<string>(); var fUsed = new List<bool>();
        for (int i = 0; i < fragList.Count; i++) {
            fIds.Add((int)Num(fragList[i], "id"));
            fSrc.Add(Str(fragList[i], "src"));
            fUsed.Add(Num(fragList[i], "used") > 0.5);
        }
        d.FragIds = fIds.ToArray(); d.FragSources = fSrc.ToArray(); d.FragUsed = fUsed.ToArray();
        var digList = ObjArray(o, "digestion");
        var dP = new List<int>(); var dC = new List<int>(); var dCa = new List<double>();
        var dR = new List<double>(); var dT = new List<double>();
        var yW = new List<double>(); var yO = new List<double>(); var yC = new List<double>();
        var yM = new List<double>(); var yG = new List<int>();
        for (int i = 0; i < digList.Count; i++) {
            dP.Add((int)Num(digList[i], "prey")); dC.Add((int)Num(digList[i], "cls"));
            dCa.Add(Num(digList[i], "cargo")); dR.Add(Num(digList[i], "remain"));
            dT.Add(Num(digList[i], "total"));
            yW.Add(Num(digList[i], "yWater")); yO.Add(Num(digList[i], "yOrganic"));
            yC.Add(Num(digList[i], "yChitin")); yM.Add(Num(digList[i], "yMineral"));
            yG.Add((int)Num(digList[i], "yGene"));
        }
        d.DigPreyId = dP.ToArray(); d.DigClass = dC.ToArray(); d.DigCargo = dCa.ToArray();
        d.DigRemain = dR.ToArray(); d.DigTotal = dT.ToArray();
        d.YWater = yW.ToArray(); d.YOrganic = yO.ToArray(); d.YChitin = yC.ToArray();
        d.YMineral = yM.ToArray(); d.YGene = yG.ToArray();
        d.CaptureActive = Num(o, "capActive") > 0.5;
        d.CaptureTarget = (int)Num(o, "capTarget");
        d.CaptureProgress = Num(o, "capProgress");
        var pel = ObjArray(o, "pellets");
        var pIds = new List<int>(); var pX = new List<double>(); var pY = new List<double>();
        var pM = new List<double>(); var pU = new List<bool>();
        for (int i = 0; i < pel.Count; i++) {
            pIds.Add((int)Num(pel[i], "id")); pX.Add(Num(pel[i], "x")); pY.Add(Num(pel[i], "y"));
            pM.Add(Num(pel[i], "m")); pU.Add(Num(pel[i], "used") > 0.5);
        }
        d.PelletIds = pIds.ToArray(); d.PelletX = pX.ToArray(); d.PelletY = pY.ToArray();
        d.PelletMass = pM.ToArray(); d.PelletConsumed = pU.ToArray();
        var bee = ObjArray(o, "beetles");
        var bIds = new List<int>(); var bX = new List<double>(); var bY = new List<double>();
        var bHp = new List<double>(); var bM = new List<double>(); var bPh = new List<int>();
        for (int i = 0; i < bee.Count; i++) {
            bIds.Add((int)Num(bee[i], "id")); bX.Add(Num(bee[i], "x")); bY.Add(Num(bee[i], "y"));
            bHp.Add(Num(bee[i], "hp")); bM.Add(Num(bee[i], "m")); bPh.Add((int)Num(bee[i], "phase"));
        }
        d.BeetleIds = bIds.ToArray(); d.BeetleX = bX.ToArray(); d.BeetleY = bY.ToArray();
        d.BeetleHealth = bHp.ToArray(); d.BeetleMass = bM.ToArray(); d.BeetlePhase = bPh.ToArray();
        var trl = ObjArray(o, "trail");
        var tIds = new List<int>(); var tX = new List<double>(); var tY = new List<double>();
        var tM = new List<double>(); var tF = new List<double>();
        for (int i = 0; i < trl.Count; i++) {
            tIds.Add((int)Num(trl[i], "id")); tX.Add(Num(trl[i], "x")); tY.Add(Num(trl[i], "y"));
            tM.Add(Num(trl[i], "m")); tF.Add(Num(trl[i], "f"));
        }
        d.TrailIds = tIds.ToArray(); d.TrailX = tX.ToArray(); d.TrailY = tY.ToArray();
        d.TrailMass = tM.ToArray(); d.TrailFresh = tF.ToArray();
        var ai = ObjArray(o, "ai");
        var aIds = new List<int>(); var aAl = new List<bool>(); var aX = new List<double>();
        var aY = new List<double>(); var aS = new List<double>(); var aW = new List<double>();
        var aR = new List<double>(); var aT = new List<double>();
        for (int i = 0; i < ai.Count; i++) {
            aIds.Add((int)Num(ai[i], "id")); aAl.Add(Num(ai[i], "ally") > 0.5);
            aX.Add(Num(ai[i], "x")); aY.Add(Num(ai[i], "y"));
            aS.Add(Num(ai[i], "structure")); aW.Add(Num(ai[i], "water"));
            aR.Add(Num(ai[i], "reserve")); aT.Add(Num(ai[i], "trust"));
        }
        d.AiIds = aIds.ToArray(); d.AiAlly = aAl.ToArray(); d.AiX = aX.ToArray();
        d.AiY = aY.ToArray(); d.AiStr = aS.ToArray(); d.AiWater = aW.ToArray();
        d.AiReserve = aR.ToArray(); d.AiTrust = aT.ToArray();
        var mem = ObjArray(o, "aiMem");
        var mOw = new List<int>(); var mKd = new List<int>(); var mAc = new List<int>();
        var mTk = new List<long>();
        for (int i = 0; i < mem.Count; i++) {
            mOw.Add((int)Num(mem[i], "owner")); mKd.Add((int)Num(mem[i], "kind"));
            mAc.Add((int)Num(mem[i], "actor")); mTk.Add((long)Num(mem[i], "tick"));
        }
        d.AiEventOwner = mOw.ToArray(); d.AiEventKind = mKd.ToArray();
        d.AiEventActor = mAc.ToArray(); d.AiEventTick = mTk.ToArray();
        var evs = ObjArray(o, "events");
        var eK = new List<string>(); var eD = new List<string>(); var eT = new List<long>();
        for (int i = 0; i < evs.Count; i++) {
            eK.Add(Str(evs[i], "kind")); eD.Add(Str(evs[i], "detail"));
            eT.Add((long)Num(evs[i], "tick"));
        }
        d.EventKind = eK.ToArray(); d.EventDetail = eD.ToArray(); d.EventTick = eT.ToArray();
        return d;
    }
    private static List<Dictionary<string, object>> ObjArray(Dictionary<string, object> o, string key) {
        var res = new List<Dictionary<string, object>>();
        if (o.TryGetValue(key, out var v) && v is List<object> list)
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Dictionary<string, object> dict) res.Add(dict);
        return res;
    }
    private static double AsNum(object v) {
        if (v is double d) return d;
        if (v is long l) return l;
        if (v is bool b) return b ? 1 : 0;
        return 0;
    }
    private static double Num(Dictionary<string, object> o, string k) {
        return o != null && o.TryGetValue(k, out var v) ? AsNum(v) : 0;
    }
    private static string Str(Dictionary<string, object> o, string k) {
        return o != null && o.TryGetValue(k, out var v) ? v as string : null;
    }
    private static string[] StrArray(Dictionary<string, object> o, string key) {
        if (!o.TryGetValue(key, out var v) || !(v is List<object> list)) return new string[0];
        var res = new string[list.Count];
        for (int i = 0; i < list.Count; i++) res[i] = list[i] as string ?? "";
        return res;
    }
}
}
