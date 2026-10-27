using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.13: «Наследие прошлой жизни» — снимок, применение, раз за жизнь, сгорание, сериализация.</summary>
public static class LegacyTests {

    private static RunState Donor(SoulMeta meta) {
        var rs = new RunState(meta, 91UL, true);
        rs.Evolution.FreeTokens = 2;
        True(rs.TryEvolve("div_split_basics", true), "unlock split");
        True(rs.TryEvolve("clone_leash", true), "unlock leash");
        for (int i = 0; i < ResourceInfo.Count; i++) rs.Resources.Add((ResourceKind)i, 10.0);
        rs.Heal(30.0);                       // пик массы 4 + 30 = 34
        return rs;
    }

    public static void MakeLegacy_Snapshot() {
        var pack = Donor(new SoulMeta()).MakeLegacy();
        for (int i = 0; i < ResourceInfo.Count; i++)
            Eq(3.0, pack.Resources[i], "30% of resource " + i);
        bool hasSplit = false, hasLeash = false;
        for (int i = 0; i < pack.Nodes.Length; i++) {
            if (pack.Nodes[i] == "div_split_basics") hasSplit = true;
            if (pack.Nodes[i] == "clone_leash") hasLeash = true;
        }
        True(!hasSplit, "split node never inherited");
        True(hasLeash, "leash node listed");
        Eq(3.0, pack.StartMassBonus, "mass bonus capped at +3");
        // скромная жизнь: пик 4.0 → бонус 0.6, без потолка
        var small = new RunState(new SoulMeta(), 92UL, true).MakeLegacy();
        Eq(0.6, small.StartMassBonus, "+15% of peak 4.0");
        Eq(0, small.Nodes.Length, "no nodes unlocked — empty list");
    }

    public static void ApplyLegacy_RestoresAll() {
        var pack = Donor(new SoulMeta()).MakeLegacy();
        var meta = new SoulMeta();
        var rs = new RunState(meta, 77UL, true);
        Eq(4.0, rs.Mass, "fresh life mass");
        True(rs.ApplyLegacy(pack, "clone_leash"), "applied");
        Eq(3.0, rs.Resources.Get(ResourceKind.Organic), "30% resources back");
        Eq(RunState.BaseCloneLeash + 6.0, rs.CloneLeash, "node granted");
        Eq(7.0, rs.Mass, "start mass +3");
        Eq(3.0, rs.LegacyMassBonus, "bonus recorded");
        True(rs.LegacyApplied, "flag set");
        Eq(1, meta.LegacyUses, "counter in SoulMeta");
    }

    public static void ApplyLegacy_OncePerLife_AndValidated() {
        var pack = Donor(new SoulMeta()).MakeLegacy();
        var meta = new SoulMeta();
        var rs = new RunState(meta, 78UL, true);
        True(rs.ApplyLegacy(pack, "clone_leash"), "first apply ok");
        True(!rs.ApplyLegacy(pack, "clone_leash"), "second apply blocked");
        True(!rs.ApplyLegacy(pack, "div_split_basics"), "split excluded");
        True(!rs.ApplyLegacy(pack, "body_thick_skin"), "node not in snapshot");
        True(!rs.ApplyLegacy(pack, null), "null node blocked");
        True(!rs.ApplyLegacy(null, "clone_leash"), "null pack blocked");
        Eq(1, meta.LegacyUses, "counter unchanged by rejected calls");
        // мёртвая жизнь ничего не принимает
        var dead = new RunState(new SoulMeta(), 79UL, true);
        dead.OnMassLost(50.0, "test");
        True(dead.IsDead, "dead");
        True(!dead.ApplyLegacy(pack, "clone_leash"), "dead life refuses legacy");
    }

    public static void ApplyLegacy_ResourcesOnlyWhenNoNodes() {
        // игрок ничего не развил, кроме «Деления» — в снимке только ресурсы и масса
        var donor = new RunState(new SoulMeta(), 95UL, true);
        donor.Resources.Add(ResourceKind.Slime, 8.0);
        donor.Heal(10.0);                    // пик 14 → бонус 2.1
        var pack = donor.MakeLegacy();
        Eq(0, pack.Nodes.Length, "only split unlocked — no nodes");
        var meta = new SoulMeta();
        var rs = new RunState(meta, 96UL, true);
        True(rs.ApplyLegacy(pack, null), "null node is fine without node list");
        Eq(2.4, rs.Resources.Get(ResourceKind.Slime), "30% of slime");
        Eq(4.0 + 2.1, rs.Mass, "mass bonus applied");
        Eq(1, meta.LegacyUses, "counted");
        // узел без списка — отказ
        var rs2 = new RunState(new SoulMeta(), 97UL, true);
        True(!rs2.ApplyLegacy(pack, "body_mass"), "cannot pick a node when list is empty");
    }

    public static void Legacy_BurnsWhenNotUsed() {
        var meta = new SoulMeta {
            Legacy = new LegacyPack { StartMassBonus = 1.0, Nodes = new[] { "body_mass" } },
        };
        var taken = meta.TakeLegacy();
        True(taken != null, "pack returned");
        Eq(1.0, taken.StartMassBonus, "same pack");
        True(meta.Legacy == null, "burned from soul");
        // второй забор — уже пусто
        True(meta.TakeLegacy() == null, "nothing to take twice");
    }

    public static void SoulMeta_SerializesLegacy() {
        var meta = new SoulMeta {
            LegacyUses = 4,
            Legacy = new LegacyPack {
                StartMassBonus = 2.5,
                Resources = new[] { 1.5, 0.0, 2.25, 0.0, 0.0, 0.0 },
                Nodes = new[] { "body_mass", "puppet" },
            },
        };
        var back = SoulMeta.FromJson(meta.ToJson());
        True(back.Legacy != null, "legacy survives round-trip");
        Eq(2.5, back.Legacy.StartMassBonus, "mass bonus");
        Eq(1.5, back.Legacy.Resources[0], "organic");
        Eq(2.25, back.Legacy.Resources[2], "slime");
        Eq(2, back.Legacy.Nodes.Length, "nodes count");
        Eq("puppet", back.Legacy.Nodes[1], "node id kept");
        Eq(4, back.LegacyUses, "uses counter");
        // чужой/битый JSON не роняет разбор и чистит мусор
        var junk = SoulMeta.FromJson(
            "{\"legacy\":{\"mass\":9e9,\"res\":[1,\"x\",-3,null],\"nodes\":[\"nope\",\"div_split_basics\",\"body_mass\",42]},\"legacyUses\":-3}");
        True(junk.Legacy != null, "junk still makes a pack");
        Eq(3.0, junk.Legacy.StartMassBonus, "mass capped on load");
        Eq(1.0, junk.Legacy.Resources[0], "bad entries become 0");
        Eq(0.0, junk.Legacy.Resources[1], "string -> 0");
        Eq(0.0, junk.Legacy.Resources[2], "negative -> 0");
        Eq(1, junk.Legacy.Nodes.Length, "unknown/split nodes dropped");
        Eq("body_mass", junk.Legacy.Nodes[0], "valid node kept");
        Eq(0, junk.LegacyUses, "negative counter clamped");
        // без наследия — поле отсутствует
        var bare = SoulMeta.FromJson(new SoulMeta().ToJson());
        True(bare.Legacy == null, "no legacy by default");
        True(bare.TakeLegacy() == null, "take is safe");
    }
}
}
