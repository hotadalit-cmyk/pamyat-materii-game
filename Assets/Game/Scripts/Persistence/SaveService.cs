using System;
namespace PM.Persistence {
/// <summary>Порт в памяти — для тестов harness (без файловой системы).</summary>
public sealed class MemoryFilePort : ISaveFilePort {
    private readonly System.Collections.Generic.Dictionary<string, byte[]> _files =
        new System.Collections.Generic.Dictionary<string, byte[]>();
    public bool Exists(string fileName) => _files.ContainsKey(fileName);
    public byte[] ReadAllBytes(string fileName) =>
        _files.TryGetValue(fileName, out var b) ? b : throw new System.IO.FileNotFoundException(fileName);
    public void WriteAllBytes(string fileName, byte[] data) { _files[fileName] = data; }
    public void Delete(string fileName) { _files.Remove(fileName); }
    public void Move(string fromName, string toName) {
        _files[toName] = _files[fromName];
        _files.Remove(fromName);
    }
}
/// <summary>
/// Сервис сохранений: JSON + CRC32 в обёртке {"crc":"xxxxxxxx","data":{...}},
/// атомарная запись temp→rename, резервная копия .bak перед перезаписью,
/// два слота. Повреждение → SaveCorruptedException (не молча новая кампания, QA-10).
/// </summary>
public sealed class SaveService {
    public sealed class SaveCorruptedException : Exception {
        public string Slot;
        public SaveCorruptedException(string slot) : base("save_corrupted:" + slot) { Slot = slot; }
    }
    public const string Slot0 = "pm_slot0.sav";
    public const string Slot1 = "pm_slot1.sav";
    private const string TempSuffix = ".tmp";
    private const string BakSuffix = ".bak";
    private readonly ISaveFilePort _files;
    public SaveService(ISaveFilePort files) { _files = files ?? new MemoryFilePort(); }

    public string[] SlotNames => new[] { Slot0, Slot1 };

    /// <summary>Сериализация с контрольной суммой.</summary>
    public static string WrapWithChecksum(SaveData data) {
        string json = SaveCodec.Serialize(data);
        uint crc = Crc32.Compute(System.Text.Encoding.UTF8.GetBytes(json));
        var w = new MiniJson.Writer();
        w.BeginObject();
        w.Key("crc").Value(Crc32.Hex(crc));
        w.Key("data").Value(json); // data — строка с вложенным JSON
        w.EndObject();
        return w.ToString();
    }
    public static SaveData UnwrapWithChecksum(string wrapped) {
        var root = MiniJson.Parse(wrapped) as System.Collections.Generic.Dictionary<string, object>;
        if (root == null || !root.TryGetValue("crc", out var crcO) || !root.TryGetValue("data", out var dataO))
            throw new MiniJson.MiniJsonException("missing_checksum");
        string expected = crcO as string;
        string payload = dataO as string;
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(expected))
            throw new MiniJson.MiniJsonException("missing_payload");
        uint actual = Crc32.Compute(System.Text.Encoding.UTF8.GetBytes(payload));
        if (Crc32.Hex(actual) != expected) throw new MiniJson.MiniJsonException("crc_mismatch");
        return SaveCodec.Deserialize(payload);
    }
    /// <summary>Атомарная запись: .bak ← старый, temp → основной.</summary>
    public void Save(string slot, SaveData data) {
        string wrapped = WrapWithChecksum(data);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(wrapped);
        if (_files.Exists(slot)) {
            // освежаем бэкап только при валидном текущем (не распространяем порчу)
            if (IsValid(_files.ReadAllBytes(slot))) _files.WriteAllBytes(slot + BakSuffix, _files.ReadAllBytes(slot));
        }
        string tmp = slot + TempSuffix;
        _files.WriteAllBytes(tmp, bytes);
        if (_files.Exists(slot)) _files.Delete(slot);
        _files.Move(tmp, slot);
    }
    private static bool IsValid(byte[] raw) {
        try {
            UnwrapWithChecksum(System.Text.Encoding.UTF8.GetString(raw));
            return true;
        } catch { return false; }
    }
    /// <summary>Загрузка: основной → при повреждении .bak → иначе явная ошибка.</summary>
    public SaveData Load(string slot) {
        if (_files.Exists(slot)) {
            try {
                return UnwrapWithChecksum(System.Text.Encoding.UTF8.GetString(_files.ReadAllBytes(slot)));
            } catch (Exception inner) {
                if (_files.Exists(slot + BakSuffix)) {
                    try {
                        return UnwrapWithChecksum(System.Text.Encoding.UTF8.GetString(_files.ReadAllBytes(slot + BakSuffix)));
                    } catch { /* бэкап тоже бит — падаем наружу */ }
                }
                throw new SaveCorruptedException(slot);
            }
        }
        throw new System.IO.FileNotFoundException(slot);
    }
    public bool HasSave(string slot) => _files.Exists(slot);
    public void DeleteSlot(string slot) {
        _files.Delete(slot);
        _files.Delete(slot + BakSuffix);
    }
}
}
