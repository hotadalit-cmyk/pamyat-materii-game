using System;
using System.Collections.Generic;
using PM.Persistence;
using static PMHarness.Check;
namespace PMHarness {
public static class MiniJsonTests {
    public static void Writer_BasicTypes() {
        var w = new MiniJson.Writer();
        w.BeginObject();
        w.Key("i").Value(42L);
        w.Key("d").Value(0.5);
        w.Key("b").Value(true);
        w.Key("s").Value("hello");
        w.Key("arr").BeginArray();
        w.Value(1L);
        w.Value(2L);
        w.EndArray();
        w.Key("nil").NullValue();
        w.EndObject();
        var parsed = MiniJson.Parse(w.ToString()) as Dictionary<string, object>;
        True(parsed != null, "parses back");
        Eq(42.0, (double)parsed["i"], "int roundtrip");
        Eq(0.5, (double)parsed["d"], "double roundtrip");
        Eq(true, (bool)parsed["b"], "bool roundtrip");
        Eq("hello", (string)parsed["s"], "string roundtrip");
        var arr = parsed["arr"] as List<object>;
        Eq(2, arr.Count, "array length");
        Eq(1.0, (double)arr[0], "array item");
        True(parsed["nil"] == null, "null roundtrip");
    }
    public static void Writer_EscapesSpecials() {
        var w = new MiniJson.Writer();
        w.Value("quote\" back\\slash\nnewline\ttab\u0001ctrl");
        string json = w.ToString();
        var back = MiniJson.Parse(json) as string;
        Eq("quote\" back\\slash\nnewline\ttab\u0001ctrl", back, "specials roundtrip");
    }
    public static void Writer_UnicodeStringRoundtrip() {
        var w = new MiniJson.Writer();
        string ru = "Память материи — слизень 記憶";
        w.Value(ru);
        var back = MiniJson.Parse(w.ToString()) as string;
        Eq(ru, back, "unicode roundtrip");
    }
    public static void Writer_DoubleRoundtripPrecision() {
        var w = new MiniJson.Writer();
        double v = 0.1234567890123;
        w.Value(v);
        var back = (double)MiniJson.Parse(w.ToString());
        Eq(v, back, "R format precision", 1e-15);
    }
    public static void Parser_MalformedThrows() {
        Throws(() => MiniJson.Parse("{"), "unclosed object");
        Throws(() => MiniJson.Parse("{\"a\":}"), "missing value");
        Throws(() => MiniJson.Parse("[1,2"), "unclosed array");
        Throws(() => MiniJson.Parse("{\"a\":1,}"), "trailing comma object");
        Throws(() => MiniJson.Parse("nul"), "bad literal");
        Throws(() => MiniJson.Parse("\"abc\" extra"), "trailing data");
        Throws(() => MiniJson.Parse("1.2.3"), "bad number");
    }
    public static void Parser_NestedStructure() {
        string json = "{\"a\":{\"b\":[1,{\"c\":true}]}}";
        var root = MiniJson.Parse(json) as Dictionary<string, object>;
        var a = root["a"] as Dictionary<string, object>;
        var b = a["b"] as List<object>;
        var inner = b[1] as Dictionary<string, object>;
        Eq(true, (bool)inner["c"], "deep nesting");
    }
    private static void Throws(Action a, string msg) {
        try { a(); True(false, msg + " (no exception)"); }
        catch (MiniJson.MiniJsonException) { True(true, msg); }
    }
    public static void Crc32_KnownVectorAndStability() {
        // известный вектор CRC32: "123456789" -> cbf43926
        Eq("cbf43926", Crc32.HexOf(System.Text.Encoding.ASCII.GetBytes("123456789")), "CRC32 test vector");
        Eq(Crc32.HexOf(new byte[] { 1, 2, 3 }), Crc32.HexOf(new byte[] { 1, 2, 3 }), "deterministic");
        True(Crc32.HexOf(new byte[] { 1, 2, 3 }) != Crc32.HexOf(new byte[] { 1, 2, 4 }), "sensitive to data");
    }
    public static void SaveWrap_ChecksumCatchesCorruption() {
        var d = new SaveData { Schema = SaveData.SchemaVersion, Water = 5 };
        string wrapped = SaveService.WrapWithChecksum(d);
        var ok = SaveService.UnwrapWithChecksum(wrapped);
        Eq(5.0, ok.Water, "unwrapped ok");
        // порча: изменяем байт данных
        int pos = wrapped.IndexOf("\\\"water\\\":5");
        string corrupted = wrapped.Substring(0, pos) + "\\\"water\\\":9" + wrapped.Substring(pos + "\\\"water\\\":5".Length);
        bool threw = false;
        try { SaveService.UnwrapWithChecksum(corrupted); }
        catch (MiniJson.MiniJsonException) { threw = true; }
        True(threw, "crc mismatch detected");
    }
    public static void SaveService_TwoSlotsAndBackupRecovery() {
        var files = new MemoryFilePort();
        var svc = new SaveService(files);
        var d1 = new SaveData { Schema = SaveData.SchemaVersion, Water = 1.5, Tick = 10 };
        var d2 = new SaveData { Schema = SaveData.SchemaVersion, Water = 2.5, Tick = 20 };
        svc.Save(SaveService.Slot0, d1);
        svc.Save(SaveService.Slot1, d2);
        Eq(1.5, svc.Load(SaveService.Slot0).Water, "slot0 data");
        Eq(2.5, svc.Load(SaveService.Slot1).Water, "slot1 data");
        // повреждение основного → восстановление из .bak
        svc.Save(SaveService.Slot0, new SaveData { Schema = SaveData.SchemaVersion, Water = 9.9, Tick = 30 });
        var bakBytes = files.ReadAllBytes(SaveService.Slot0 + ".bak");
        Eq(1.5, SaveService.UnwrapWithChecksum(System.Text.Encoding.UTF8.GetString(bakBytes)).Water,
            "backup holds previous valid save");
        files.WriteAllBytes(SaveService.Slot0, new byte[] { 1, 2, 3 }); // порча основного
        Eq(1.5, svc.Load(SaveService.Slot0).Water, "previous valid save (1.5) recovered from .bak after corruption");
    }
    public static void SaveService_CorruptionExplicitFailure() {
        var files = new MemoryFilePort();
        var svc = new SaveService(files);
        svc.Save(SaveService.Slot0, new SaveData { Schema = SaveData.SchemaVersion, Water = 3 });
        files.WriteAllBytes(SaveService.Slot0 + ".bak", new byte[] { 9 }); // бэкап тоже бит
        files.WriteAllBytes(SaveService.Slot0, new byte[] { 1 });         // и основной бит
        bool threw = false;
        try { svc.Load(SaveService.Slot0); }
        catch (SaveService.SaveCorruptedException) { threw = true; }
        True(threw, "corruption reported explicitly (no silent new campaign)");
    }
    public static void SaveService_MissingSlotThrows() {
        var svc = new SaveService(new MemoryFilePort());
        bool threw = false;
        try { svc.Load(SaveService.Slot1); }
        catch (System.IO.FileNotFoundException) { threw = true; }
        True(threw, "missing save -> explicit error");
    }
}
}
