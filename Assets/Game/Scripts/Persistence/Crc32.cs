namespace PM.Persistence {
/// <summary>CRC32 (IEEE 802.3) — контрольная сумма сохранений без внешних зависимостей.</summary>
public static class Crc32 {
    private static readonly uint[] Table = BuildTable();
    private static uint[] BuildTable() {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    public static uint Compute(byte[] data) {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < data.Length; i++)
            crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
    public static string Hex(uint crc) => crc.ToString("x8");
    public static string HexOf(byte[] data) => Hex(Compute(data));
}
}
