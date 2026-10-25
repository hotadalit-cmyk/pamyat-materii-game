using PM.Domain.Core;
namespace PM.Domain.Body {
/// <summary>
/// Три+1 компонента массы (MATTER_PHYSICS v0.1): структура, вода, запасы, груз в камерах.
/// Инварианты: масса неотрицательна; вода не появляется из ничего (только через явные Add*);
/// расход при нехватке ограничивается доступным остатком (D-16).
/// </summary>
public sealed class BodyMassState {
    public double Structure;   // материя органов/основы
    public double Water;       // текущая вода
    public double Reserve;     // обработанные питательные запасы
    public double Cargo;       // непереваренная добыча в камерах
    public double MinimumViableWater = 1.0;

    public double Total => Structure + Water + Reserve + Cargo;
    public bool IsDehydrated => Water < MinimumViableWater;

    public BodyMassState(double structure, double water, double reserve = 0, double cargo = 0) {
        Structure = MathUtil.Max(0, structure);
        Water = MathUtil.Max(0, water);
        Reserve = MathUtil.Max(0, reserve);
        Cargo = MathUtil.Max(0, cargo);
    }

    /// <summary>Расход воды: возвращает фактически потраченное (не больше остатка).</summary>
    public double SpendWater(double amount) {
        double spent = MathUtil.Min(MathUtil.Max(0, amount), Water);
        Water -= spent;
        return spent;
    }
    public double SpendReserve(double amount) {
        double spent = MathUtil.Min(MathUtil.Max(0, amount), Reserve);
        Reserve -= spent;
        return spent;
    }
    public void AddWater(double amount) { Water += MathUtil.Max(0, amount); }
    public void AddReserve(double amount) { Reserve += MathUtil.Max(0, amount); }
    public void AddStructure(double amount) { Structure += MathUtil.Max(0, amount); }
    public void AddCargo(double amount) { Cargo += MathUtil.Max(0, amount); }
    /// <summary>Списание груза (после переваривания/вычитания) — не уходит в минус.</summary>
    public double RemoveCargo(double amount) {
        double taken = MathUtil.Min(MathUtil.Max(0, amount), Cargo);
        Cargo -= taken;
        return taken;
    }
    /// <summary>Метаболизм: запасы → вода (возвращение влаги из питания).</summary>
    public bool TryConvertReserveToWater(double reserveAmount, double efficiency) {
        double taken = SpendReserve(reserveAmount);
        if (taken <= 0) return false;
        AddWater(taken * MathUtil.Clamp01(efficiency));
        return true;
    }
    /// <summary>Рост: запасы → структура (стоимость воды не берём — учтена в эффективности).</summary>
    public bool TryConvertReserveToStructure(double reserveAmount, double efficiency) {
        double taken = SpendReserve(reserveAmount);
        if (taken <= 0) return false;
        AddStructure(taken * MathUtil.Clamp01(efficiency));
        return true;
    }
}
}
