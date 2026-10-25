using PM.Domain.Genetics;
using PM.Domain.Metamorphosis;
using PM.Domain.Body;
using PM.Domain.Movement;
using static PMHarness.Check;
namespace PMHarness {
public static class MetamorphosisTests {
    private static ComponentsStock Rich() =>
        new ComponentsStock { Water = 20, Organic = 20, Chitin = 20, Mineral = 20 };
    private static UnlockedNodes WithNode(string id) {
        var un = new UnlockedNodes();
        un.Add(id);
        return un;
    }
    public static void Form_RequiresNode() {
        var body = new BodyMassState(5, 5);
        var r = MetamorphosisService.Metamorphose(FormCatalog.Beetle(), new UnlockedNodes(),
            "n_beetle_form", Rich(), body);
        True(!r.Ok, "beetle form locked without node");
        Eq("form_node_not_unlocked", r.FailReason, "reason");
        var r2 = MetamorphosisService.Metamorphose(FormCatalog.Beetle(), WithNode("n_beetle_form"),
            "n_beetle_form", Rich(), body);
        True(r2.Ok, "beetle form with node");
    }
    public static void Form_RequiresStructure() {
        var weakBody = new BodyMassState(2.0, 5);
        var r = MetamorphosisService.Metamorphose(FormCatalog.Beetle(), WithNode("n_beetle_form"),
            "n_beetle_form", Rich(), weakBody);
        True(!r.Ok, "too little structure");
        Eq("structure_too_low", r.FailReason, "reason");
        // запас не списан — атомарность
        var s = Rich();
        MetamorphosisService.Metamorphose(FormCatalog.Beetle(), WithNode("n_beetle_form"),
            "n_beetle_form", s, weakBody);
        Eq(20.0, s.Chitin, "no components spent on failed metamorphosis");
    }
    public static void Form_SpendsComponents() {
        var s = Rich();
        var body = new BodyMassState(5, 5);
        MetamorphosisService.Metamorphose(FormCatalog.Beetle(), WithNode("n_beetle_form"),
            "n_beetle_form", s, body);
        Eq(20 - 2, s.Water, "water spent");
        Eq(20 - 8, s.Organic, "organic spent");
        Eq(20 - 10, s.Chitin, "chitin spent");
        Eq(20 - 4, s.Mineral, "mineral spent");
    }
    public static void Revert_Refunds80Percent() {
        var s = Rich();
        var body = new BodyMassState(5, 5);
        MetamorphosisService.Metamorphose(FormCatalog.Beetle(), WithNode("n_beetle_form"),
            "n_beetle_form", s, body);
        double chitinAfterForm = s.Chitin; // 10
        var r = MetamorphosisService.RevertToSlime(FormCatalog.Beetle(), s);
        True(r.Ok, "revert ok");
        Eq(chitinAfterForm + 10 * 0.8, s.Chitin, "80% chitin refunded", 1e-9);
        Eq(12 + 8 * 0.8, s.Organic, "80% organic refunded", 1e-9);
        // масса не появилась из воздуха: вода вернулась в компоненты, тело не менялось
        True(r.Applied.Id == "form_slime", "back to slime");
    }
    public static void Revert_SlimeIsNoOp() {
        var s = Rich();
        var r = MetamorphosisService.RevertToSlime(FormCatalog.Slime(), s);
        True(!r.Ok, "already slime");
        Eq("already_slime", r.FailReason, "reason");
    }
    public static void BeetleKeepsPulseControl() {
        // D-17: форма жука сохраняет импульсное управление — MinSpeed/MaxSpeed > 0
        var beetle = FormCatalog.Beetle().Organs;
        True(beetle.MinSpeed > 0 && beetle.MaxSpeed > beetle.MinSpeed, "pulse control preserved");
        True(beetle.ArmorResistance > OrganStats.BaseSlime().ArmorResistance, "beetle armored");
    }
}
}
