using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.16: окно знакомства с глубиной — полные данные для каждой из пяти глубин.</summary>
public static class DepthIntroTests {
    public static void EveryDepth_HasFullIntro() {
        for (int d = 1; d <= RunState.MaxDepth; d++) {
            var i = DepthIntro.For(d);
            Eq(d, i.Depth, d + ": depth");
            Eq(d, i.Biome.Depth, d + ": biome");
            Eq(d, i.Guardian.Depth, d + ": guardian of this depth");
            True(!string.IsNullOrEmpty(i.Epigraph), d + ": epigraph");
            Eq(3, i.Features.Length, d + ": three features");
            foreach (var f in i.Features)
                True(f.Icon.Length > 0 && f.Title.Length > 0 && f.Text.Length > 0, d + ": feature filled");
            True(!string.IsNullOrEmpty(i.Tip), d + ": tip");
            True(i.Inhabitants.Length >= 3 && i.Inhabitants.Length <= 10, d + ": inhabitants fit one row");
        }
    }

    public static void Inhabitants_ComeFromBiome_WithoutSpores() {
        for (int d = 1; d <= RunState.MaxDepth; d++) {
            var i = DepthIntro.For(d);
            foreach (var id in i.Inhabitants) {
                True(id != "spore", d + ": spores are everywhere, not listed");
                True(System.Array.IndexOf(i.Biome.SpeciesPool, id) >= 0, d + ": " + id + " lives in biome");
            }
        }
        True(System.Array.IndexOf(DepthIntro.For(1).Inhabitants, "cave_rat") >= 0, "new species shown");
    }

    public static void DepthOutOfRange_Clamped() {
        Eq(1, DepthIntro.For(0).Depth, "depth 0 → 1");
        Eq(RunState.MaxDepth, DepthIntro.For(99).Depth, "depth 99 → 5");
    }
}
}
