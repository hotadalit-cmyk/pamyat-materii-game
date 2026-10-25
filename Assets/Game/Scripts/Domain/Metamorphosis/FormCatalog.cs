using System.Collections.Generic;
using PM.Domain.Core;
using PM.Domain.Body;
using PM.Domain.Genetics;
using PM.Domain.Movement;
namespace PM.Domain.Metamorphosis {
/// <summary>Определение формы (полная метаморфоза). Меняет профиль физики и органов.</summary>
public sealed class FormDef {
    public string Id;
    public string Title;
    public double MinStructure;      // минимальная структура для удержания формы
    public ComponentCost Cost;       // вложение компонентов (возврат 80% при отмене)
    public OrganStats Organs;
    public double RadiusBonus;       // изменение радиуса коллайдера (масштаб)
}
/// <summary>Каталог форм демо: чистая слизь, хитиновая модификация, форма жука (D-17).</summary>
public static class FormCatalog {
    public static FormDef Slime() => new FormDef {
        Id = "form_slime", Title = "Чистая слизь", MinStructure = 0.5,
        Cost = ComponentCost.Of(0, 0, 0, 0), RadiusBonus = 0,
        Organs = OrganStats.BaseSlime()
    };
    public static FormDef ChitinSlime() => new FormDef {
        Id = "form_chitin_slime", Title = "Хитиновая модификация", MinStructure = 1.5,
        Cost = ComponentCost.Of(0, 2, 5, 0), RadiusBonus = 0.05,
        Organs = WithResist(OrganStats.BaseSlime(), 0.15)
    };
    public static FormDef Beetle() => new FormDef {
        Id = "form_beetle", Title = "Форма жука", MinStructure = 3.0,
        Cost = ComponentCost.Of(2, 8, 10, 4), RadiusBonus = 0.25,
        // D-17: бильярдное импульсное управление сохраняется; жук тяжелее и бронированнее
        Organs = WithResist(OrganStats.BaseSlime(), 0.30, speedScale: 0.85, powerScale: 1.15)
    };
    private static OrganStats WithResist(OrganStats o, double resist,
                                         double speedScale = 1.0, double powerScale = 1.0) {
        o.ArmorResistance += resist;
        o.MaxSpeed *= speedScale;
        o.MinSpeed *= speedScale;
        o.ImpulsePower *= powerScale;
        return o;
    }
    public static FormDef Find(string formId) {
        if (formId == "form_chitin_slime") return ChitinSlime();
        if (formId == "form_beetle") return Beetle();
        return Slime();
    }
}
/// <summary>
/// Сервис метаморфозы: исключает «создание массы из воздуха» — компоненты списываются,
/// структура тела должна покрывать MinStructure; откат возвращает 80% вложений (потери зафиксированы).
/// </summary>
public static class MetamorphosisService {
    public const double RevertRefundFactor = 0.8;
    public sealed class Result {
        public bool Ok; public string FailReason;
        public FormDef Applied; public double RefundedWater, RefundedOrganic, RefundedChitin, RefundedMineral;
    }
    public static Result Metamorphose(FormDef target, UnlockedNodes nodes, string requiredNodeId,
                                      ComponentsStock stock, BodyMassState body) {
        var r = new Result();
        if (target == null) { r.FailReason = "null_form"; return r; }
        if (target.Cost.Chitin + target.Cost.Organic + target.Cost.Mineral + target.Cost.Water > 0
            && !nodes.Has(requiredNodeId)) {
            r.FailReason = "form_node_not_unlocked"; return r;
        }
        if (body.Structure < target.MinStructure) { r.FailReason = "structure_too_low"; return r; }
        if (!stock.TrySpend(target.Cost)) { r.FailReason = "components_missing"; return r; }
        r.Ok = true; r.Applied = target;
        return r;
    }

    /// <summary>Возврат к чистой слизи с частичным возвратом вложений (масса сохраняется).</summary>
    public static Result RevertToSlime(FormDef current, ComponentsStock stock) {
        var r = new Result();
        if (current == null || current.Id == "form_slime") { r.FailReason = "already_slime"; return r; }
        double f = RevertRefundFactor;
        double w = current.Cost.Water * f, o = current.Cost.Organic * f,
                c = current.Cost.Chitin * f, m = current.Cost.Mineral * f;
        stock.Add(w, o, c, m);
        r.Ok = true; r.Applied = FormCatalog.Slime();
        r.RefundedWater = w; r.RefundedOrganic = o; r.RefundedChitin = c; r.RefundedMineral = m;
        return r;
    }
}
}
