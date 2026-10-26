using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunTraitsTests {

    public static void NewSpecies_Fields() {
        var ember = SpeciesCatalog.Get("ember_beetle");
        True(ember != null, "углежук найден");
        Eq(3.5, ember.BaseMass, "масса углежука 3.5");
        Eq(CreatureBehavior.Charger, ember.Behavior, "углежук — чарджер");
        Eq("m_heatproof", ember.GeneMutationId, "углежук даёт жаростойкость");
        Eq(2, ember.MinDepth, "углежук с глубины 2");
        Eq(2, ember.MaxDepth, "углежук до глубины 2 (тёплый биом)");

        var sala = SpeciesCatalog.Get("salamander");
        True(sala != null, "саламандра найдена");
        Eq(3.0, sala.BaseMass, "масса саламандры 3.0");
        Eq(CreatureBehavior.Chase, sala.Behavior, "саламандра гонится");
        Eq("m_vampire", sala.GeneMutationId, "саламандра даёт вампиризм");
        Eq(2, sala.MinDepth, "саламандра в тёплом биоме");

        var leech = SpeciesCatalog.Get("leech");
        True(leech != null, "пиявка найдена");
        Eq(1.2, leech.BaseMass, "масса пиявки 1.2");
        Eq(CreatureBehavior.Chase, leech.Behavior, "пиявка гонится");
        True(leech.GeneMutationId == null, "пиявка без гена");
        Eq(3, leech.MinDepth, "пиявка в затопленных залах");

        var bubble = SpeciesCatalog.Get("bubble_jelly");
        True(bubble != null, "пузыремедуза найдена");
        Eq(3.5, bubble.BaseMass, "масса пузыремедузы 3.5");
        Eq(CreatureBehavior.Drift, bubble.Behavior, "пузыремедуза дрейфует");
        Eq("m_echo", bubble.GeneMutationId, "пузыремедуза даёт эхолокацию");
        Eq(3, bubble.MinDepth, "пузыремедуза в затопленных залах");

        var shard = SpeciesCatalog.Get("shard_crab");
        True(shard != null, "осколочный краб найден");
        Eq(5.0, shard.BaseMass, "масса осколочного краба 5.0");
        Eq(CreatureBehavior.Drift, shard.Behavior, "осколочный краб дрейфует");
        Eq("m_ricochet", shard.GeneMutationId, "осколочный краб даёт рикошет");
        Eq(4, shard.MinDepth, "осколочный краб в кристальных россыпях");

        var wisp = SpeciesCatalog.Get("wisp");
        True(wisp != null, "огонёк найден");
        Eq(2.0, wisp.BaseMass, "масса огонька 2.0");
        Eq(CreatureBehavior.Flee, wisp.Behavior, "огонёк убегает");
        Eq("m_echo", wisp.GeneMutationId, "огонёк даёт эхолокацию");
        Eq(5, wisp.MinDepth, "огонёк в сердце пещеры");
        Eq(5, wisp.MaxDepth, "огонёк только в сердце пещеры");
    }

    public static void Warden_Definition() {
        var w = SpeciesCatalog.Get("warden");
        True(w != null, "страж глубины найден");
        Eq("Страж глубины", w.Name, "имя стража");
        Eq(30.0, w.BaseMass, "масса стража 30");
        Eq(CreatureBehavior.Charger, w.Behavior, "страж — чарджер");
        True(w.GeneMutationId == null, "страж без гена");
        Eq(5, w.MinDepth, "страж на глубине 5");
        Eq(5, w.MaxDepth, "страж только на глубине 5");
        Eq(2, w.ColorIndex, "страж — цвет опасности");
        Eq("warden", BossState.SpeciesId, "SpeciesId босса — warden");
    }

    public static void Traits_ArmorSpeed() {
        Eq(4.0, SpeciesTraitsCatalog.Get("shard_crab").ArmorSpeed, "осколочный краб: броня до скорости 4");
        Eq(3.0, SpeciesTraitsCatalog.Get("crystal_mite").ArmorSpeed, "кристоклещ: броня до скорости 3");
        Eq(6.0, SpeciesTraitsCatalog.Get("warden").ArmorSpeed, "страж: броня до скорости 6");
        Eq(0.0, SpeciesTraitsCatalog.Get("beetle").ArmorSpeed, "у жука брони нет");
        Eq(0.0, SpeciesTraitsCatalog.Get("spore").ArmorSpeed, "у споры брони нет");
        Eq(0.0, SpeciesTraitsCatalog.Get("no_such").ArmorSpeed, "неизвестный вид — без брони");
    }

    public static void Traits_SplitsOnHitOnlyBubbleJelly() {
        True(SpeciesTraitsCatalog.Get("bubble_jelly").SplitsOnHit, "пузыремедуза делится при уроне");
        True(!SpeciesTraitsCatalog.Get("jelly").SplitsOnHit, "обычная медуза не делится");
        True(!SpeciesTraitsCatalog.Get("spore").SplitsOnHit, "спора не делится");
        True(!SpeciesTraitsCatalog.Get("warden").SplitsOnHit, "страж не делится");
    }

    public static void Traits_LeechPerSec() {
        Eq(0.25, SpeciesTraitsCatalog.Get("leech").LeechPerSec, "пиявка тянет 0.25 массы/сек");
        Eq(0.0, SpeciesTraitsCatalog.Get("mite").LeechPerSec, "клещ не тянет массу");
        Eq(0.0, SpeciesTraitsCatalog.Get("soul_slime").LeechPerSec, "душеслизень не пиявка");
    }

    public static void Traits_GlowsExactlyFour() {
        True(SpeciesTraitsCatalog.Get("glowworm").Glows, "светлячок светится");
        True(SpeciesTraitsCatalog.Get("wisp").Glows, "огонёк светится");
        True(SpeciesTraitsCatalog.Get("crystal_mite").Glows, "кристоклещ светится");
        True(SpeciesTraitsCatalog.Get("ember_beetle").Glows, "углежук светится");
        True(!SpeciesTraitsCatalog.Get("beetle").Glows, "жук не светится");
        True(!SpeciesTraitsCatalog.Get("spikeback").Glows, "шипастик не светится");
        True(!SpeciesTraitsCatalog.Get("jelly").Glows, "медуза не светится");
        True(!SpeciesTraitsCatalog.Get("spore").Glows, "спора не светится");
    }

    public static void Bestiary_DescribeAllSpecies() {
        var all = SpeciesCatalog.All();
        for (int i = 0; i < all.Length; i++) {
            var text = Bestiary.Describe(all[i].Id);
            True(text != null && text.Length >= 30, "у вида " + all[i].Id + " есть заметка в бестиарии");
        }
        True(Bestiary.Describe("warden").StartsWith("Страж"), "заметка о страже рассказывает о нём");
        True(Bestiary.Describe("no_such_species") == null, "неизвестный вид — null");
        True(Bestiary.Describe(null) == null, "null — null");
    }
}
}
