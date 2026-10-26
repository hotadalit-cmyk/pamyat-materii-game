using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunNewMutationTests {

    public static void Catalog_NewMutationsPresent() {
        var heat = MutationCatalog.Get("m_heatproof");
        True(heat != null, "жаростойкость найдена");
        Eq("Жаростойкость", heat.Name, "имя жаростойкости");
        Eq(1, heat.MaxStacks, "жаростойкость: 1 стак");
        var vamp = MutationCatalog.Get("m_vampire");
        Eq("Вампиризм", vamp.Name, "имя вампиризма");
        Eq(1, vamp.MaxStacks, "вампиризм: 1 стак");
        var rico = MutationCatalog.Get("m_ricochet");
        Eq("Рикошет", rico.Name, "имя рикошета");
        Eq(2, rico.MaxStacks, "рикошет: 2 стака");
        var echo = MutationCatalog.Get("m_echo");
        Eq("Эхолокация", echo.Name, "имя эхолокации");
        Eq(1, echo.MaxStacks, "эхолокация: 1 стак");
        True(MutationCatalog.All().Length >= 15, "в каталоге минимум 15 мутаций");
        var all = MutationCatalog.All();
        for (int i = 0; i < all.Length; i++)
            True(!string.IsNullOrEmpty(all[i].Description), "описание задано: " + all[i].Id);
    }

    public static void Set_HazardMult() {
        Eq(1.0, new MutationSet().HazardMult, "без жаростойкости вред ×1");
        var m = new MutationSet();
        m.Add("m_heatproof");
        Eq(0.4, m.HazardMult, "жаростойкость: вред ×0.4");
        m.Add("m_heatproof"); // сверх максимума — игнор
        Eq(0.4, m.HazardMult, "второй стак жаростойкости не действует");
    }

    public static void Set_LifestealFrac() {
        Eq(0.0, new MutationSet().LifestealFrac, "без вампиризма возврата нет");
        var m = new MutationSet();
        m.Add("m_vampire");
        Eq(0.35, m.LifestealFrac, "вампиризм возвращает 35%");
        m.Add("m_vampire");
        Eq(0.35, m.LifestealFrac, "стаков вампиризма не больше одного");
    }

    public static void Set_RicochetBonus() {
        Eq(0.0, new MutationSet().RicochetBonus, "без рикошета бонуса нет");
        var m = new MutationSet();
        m.Add("m_ricochet");
        Eq(0.25, m.RicochetBonus, "один стак: +0.25 за отскок");
        m.Add("m_ricochet");
        Eq(0.5, m.RicochetBonus, "два стака: +0.5 за отскок");
        m.Add("m_ricochet"); // максимум 2
        Eq(0.5, m.RicochetBonus, "третий стак игнорируется");
    }

    public static void Set_SenseRadius() {
        Eq(0.0, new MutationSet().SenseRadius, "без эхолокации существа вне света не видны");
        var m = new MutationSet();
        m.Add("m_echo");
        Eq(9.0, m.SenseRadius, "эхолокация: радиус чувства 9");
    }

    public static void Set_MaxStacksRespected() {
        var once = new MutationSet();
        once.Add("m_heatproof");
        True(!once.CanAdd("m_heatproof"), "жаростойкость исчерпана после одного стака");
        var twice = new MutationSet();
        twice.Add("m_ricochet");
        twice.Add("m_ricochet");
        True(!twice.CanAdd("m_ricochet"), "рикошет исчерпан после двух стаков");
        True(twice.CanAdd("m_vampire"), "вампиризм ещё доступен");
    }
}
}
