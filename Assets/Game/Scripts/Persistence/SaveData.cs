using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.World;
namespace PM.Persistence {
/// <summary>
/// Модель сохранения v1 (тело, душа, мир, следы, пищеварение, гены, ИИ, события).
/// Сериализация явная, порядок полей детерминирован (ARCHITECTURE «Сохранения»).
/// </summary>
public sealed class SaveData {
    public const int SchemaVersion = 1;
    public int Schema = SchemaVersion;
    public long Tick;
    public double WorldTime;

    // игрок
    public double PX, PY, VX, VY;
    public double Structure, Water, Reserve, Cargo, MinWater;
    public double Integrity;
    public string FormId;
    public double OrganPower, OrganMinSpeed, OrganMaxSpeed, OrganRetention,
                  OrganConductivity, OrganArmor, OrganCapture;
    // душа
    public double SoulEnergy, SoulEnergyPot, SoulEndurance, SoulMagic, SoulKarma;
    public int LivesLived;
    public string[] KnowledgeGenes;
    public long LastSignificantAt;
    public Dictionary<string, int> GrowthCounts = new Dictionary<string, int>();
    // статистика текущей жизни (v1 расширение: отсутствующие поля в старых файлах = 0)
    public int LifePellets, LifeBeetles, LifeNodes, LifeAssists;
    // ресурсы и гены
    public double StockWater, StockOrganic, StockChitin, StockMineral;
    public string[] UnlockedNodes;
    public int[] FragIds; public string[] FragSources; public bool[] FragUsed;
    // пищеварение
    public int[] DigPreyId; public int[] DigClass; public double[] DigCargo, DigRemain, DigTotal;
    public double[] YWater, YOrganic, YChitin, YMineral; public int[] YGene;
    // захват
    public bool CaptureActive; public int CaptureTarget; public double CaptureProgress;
    // мир
    public double[] PelletX, PelletY, PelletMass; public bool[] PelletConsumed; public int[] PelletIds;
    public int[] BeetleIds; public double[] BeetleX, BeetleY, BeetleHealth, BeetleMass;
    public int[] BeetlePhase;
    public int[] TrailIds; public double[] TrailX, TrailY, TrailMass, TrailFresh;
    // ИИ
    public int[] AiIds; public bool[] AiAlly; public double[] AiX, AiY, AiStr, AiWater, AiReserve;
    public double[] AiTrust; public int[] AiEventKind; public int[] AiEventActor; public long[] AiEventTick;
    public int[] AiEventOwner;
    // события мира
    public string[] EventKind, EventDetail; public long[] EventTick;
}
}
