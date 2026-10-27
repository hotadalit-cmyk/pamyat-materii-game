using System.Collections.Generic;

namespace PM.Domain.Run {

/// <summary>Одна особенность глубины в окне знакомства: значок, заголовок, пояснение.</summary>
public struct DepthFeature {
    public string Icon;   // ключ значка (Icons/…)
    public string Title;
    public string Text;
}

/// <summary>
/// v0.16: знакомство с новой глубиной — эпиграф, три особенности (местность, опасность, приметы),
/// обитатели, страж и совет. Тексты описывают то, что реально делают WorldGenerator и HazardRules.
/// </summary>
public sealed class DepthIntroInfo {
    public int Depth;
    public BiomeDef Biome;
    public string Epigraph;
    public DepthFeature[] Features;
    public string[] Inhabitants;      // id видов биома (кроме спор — они везде)
    public GuardianDef Guardian;
    public string Tip;
}

public static class DepthIntro {
    public static DepthIntroInfo For(int depth) {
        var b = BiomeCatalog.ForDepth(depth);
        var info = new DepthIntroInfo {
            Depth = b.Depth, Biome = b, Guardian = GuardianCatalog.ForDepth(b.Depth),
        };
        var list = new List<string>();
        foreach (var id in b.SpeciesPool) if (id != "spore" && !list.Contains(id)) list.Add(id);
        info.Inhabitants = list.ToArray();
        switch (b.Id) {
            case BiomeId.Fungal:
                info.Epigraph = "Тёплая темнота пахнет грибницей. Здесь ты впервые почувствуешь голод — и тех, кто голоднее тебя.";
                info.Features = new[] {
                    F("lorc_mushroom-gills", "Грибные поляны", "Упругие грибы отбрасывают при ударе — отскочи от них, чтобы резко сменить курс."),
                    F("lorc_leaf-swirl", "Просторные пещеры", "Широкие залы и мягкий свет: лучшее место, чтобы освоить рывок."),
                    F("lorc_drop", "Лёгкая добыча", "Споры дрейфуют сами по себе — еда на первые минуты жизни."),
                };
                info.Tip = "Ешь меньших, обходи больших: враг вдвое крупнее проглотит тебя целиком.";
                break;
            case BiomeId.Warm:
                info.Epigraph = "Камень здесь дышит жаром. Капля сохнет на глазах — каждая секунда стоит массы.";
                info.Features = new[] {
                    F("lorc_heat-haze", "Испарение", "Тело теряет массу каждую секунду. Не стой на месте — двигайся от еды к еде."),
                    F("lorc_fire-ray", "Лавовые реки", "Лава жжёт. Переходи по каменным мостикам — река никогда не перекрыта целиком."),
                    F("lorc_sun-radiations", "Гейзеры и шипы", "Раскалённые жерла и колючки ранят при касании — обходи их на скорости."),
                };
                info.Tip = "Держи запас массы: на глубине 2 медлительность убивает вернее любого хищника.";
                break;
            case BiomeId.Ponds:
                info.Epigraph = "Острова среди чёрной воды. Течения несут всех — хищников, добычу и тебя.";
                info.Features = new[] {
                    F("lorc_divert", "Течения", "Полосы воды сносят героя и существ. Плыви по течению — сбережёшь рывки."),
                    F("lorc_bubbles", "Острова и липкие лужи", "Суша — островками; вязкие лужи замедляют. Выбирай путь заранее."),
                    F("lorc_claw-slashes", "Стаи и сюрпризы", "Пираньи охотятся стаей, жаба-бомба взрывается, а в глубине кружит акула."),
                };
                info.Tip = "Не врывайся в стаю: бери пираний по одной с края.";
                break;
            case BiomeId.Crystal:
                info.Epigraph = "Коридоры из поющих кристаллов. Каждый удар отзывается звоном — и болью.";
                info.Features = new[] {
                    F("lorc_crystal-growth", "Кристальные лабиринты", "Узкие коридоры из колонн: рывок легко увести в стену."),
                    F("lorc_spiked-shell", "Острые грани", "Шипы и осколки ранят при касании. Короткие точные рывки — безопаснее длинных."),
                    F("lorc_heat-haze", "Тонкий воздух", "Масса понемногу испаряется — не задерживайся в тупиках."),
                };
                info.Tip = "Рвись от колонны к колонне короткими рывками: длинный рывок в узком коридоре кончается шипами.";
                break;
            default:
                info.Epigraph = "Стены пульсируют. Пещера жива — и она знает, что ты пришёл.";
                info.Features = new[] {
                    F("lorc_heart-organ", "Кольцо вокруг логова", "Пещера сомкнулась в кольцо. В центре — логово последнего стража."),
                    F("lorc_biohazard", "Пульсирующие жилы", "Жилы бьют раз в несколько секунд. Засветились — уходи: до удара меньше секунды."),
                    F("lorc_semi-closed-eye", "Почти полная тьма", "Света почти нет. Ищи свечение добычи и сверяйся с миникартой."),
                };
                info.Tip = "Набери массу, прежде чем будить стража: в бою её не восполнить.";
                break;
        }
        return info;
    }

    private static DepthFeature F(string icon, string title, string text) =>
        new DepthFeature { Icon = icon, Title = title, Text = text };
}
}
