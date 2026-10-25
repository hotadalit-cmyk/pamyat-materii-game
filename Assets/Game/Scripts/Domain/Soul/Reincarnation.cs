using PM.Domain.Body;
namespace PM.Domain.Soul {
/// <summary>
/// Реинкарнация (T17/SOUL_WORLD): тело теряется, душа и знания сохраняются.
/// Новое тело строится по потенциалу души; проводимость тела ограничивает резерв души
/// («древняя душа в новом слабом теле не применяет весь резерв сразу»).
/// Гены сохраняются как знания, но не как готовые органы.
/// </summary>
public static class Reincarnation {
    public sealed class Result {
        public BodyMassState Body;
        public double BodyConductivity;   // фактическая проводимость нового тела
        public double CarriedEnergy;      // сколько духовной энергии перешло (ограничено телом)
    }
    public static Result Reborn(SoulState soul, double baseStructure, double baseWater,
                                double bodyConductivityBase, double regionRichness) {
        var body = new BodyMassState(
            structure: baseStructure * (0.8 + 0.2 * regionRichness),
            water: baseWater * (0.9 + 0.1 * regionRichness));
        // Проводимость тела ограничивает доступ к потенциалу души.
        double conductivity = bodyConductivityBase * (1 + soul.Endurance * 0.05);
        double carried = System.Math.Min(soul.EnergyPotential, conductivity * 10);
        soul.CurrentEnergy = carried;
        soul.LivesLived++;
        return new Result { Body = body, BodyConductivity = conductivity, CarriedEnergy = carried };
    }
}
}
