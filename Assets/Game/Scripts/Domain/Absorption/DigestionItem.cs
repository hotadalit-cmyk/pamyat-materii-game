namespace PM.Domain.Absorption {
/// <summary>Класс материала добычи (ABSORPTION_GENETICS «Итог»).</summary>
public enum MaterialClass { Water = 0, Organic = 1, Chitin = 2, Mineral = 3, Genetic = 4 }

/// <summary>Выдача по завершении переваривания — начисляется ОДИН раз (QA-5/10).</summary>
public struct DigestionYield {
    public double Water;
    public double Organic;
    public double Chitin;
    public double Mineral;
    public int GeneFragmentId; // 0 — генов нет
}
/// <summary>Запись добычи во внутренней среде. Сохраняется как данные, не как сущность сцены.</summary>
public sealed class DigestionItem {
    public int PreyId;
    public MaterialClass Class;
    public double CargoMass;
    public double RemainingSec;
    public double TotalSec;
    public DigestionYield Yield;
    public bool Yielded;
}
}
