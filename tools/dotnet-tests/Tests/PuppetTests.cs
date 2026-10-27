using PM.Domain.Core;
using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {
/// <summary>v0.13: «Марионетка» — чистый мозг автопилота и узлы puppet/puppet_master.</summary>
public static class PuppetTests {

    private static PuppetSense Sense(double x, double y, double mass, bool food = false,
                                     bool shimmer = false, bool threatens = false) {
        return new PuppetSense { Pos = new Vec2(x, y), Mass = mass, IsFood = food,
            IsShimmering = shimmer, Threatens = threatens };
    }

    private static PuppetInput Base(double x = 0, double y = 0, double mass = 5.0) {
        return new PuppetInput {
            SelfPos = new Vec2(x, y), SelfMass = mass, SelfVel = Vec2.Zero,
            DashCharges = 0, MaxDashCharges = 2, AbsorbRatio = 1.0,
            Creatures = new PuppetSense[0], Zones = new PuppetZone[0],
            Carry = 0, CarryCapacity = 0, LeaderPos = Vec2.Zero, Leash = 0,
        };
    }

    // ---------- бегство ----------

    public static void FleesFromThreatCloserThanFour() {
        var inp = Base();
        inp.Creatures = new[] { Sense(2.5, 0, 20, threatens: true) };
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Flee, (int)d.Intent, "intent flee");
        True(d.Move.X < 0.0, "runs away (west)");
        // на границе 4 угроза уже не пугает
        var far = Base();
        far.Creatures = new[] { Sense(4.5, 0, 20, threatens: true) };
        Eq((int)PuppetIntent.Graze, (int)PuppetBrain.Think(far).Intent, "beyond 4 — calm");
        // несколько угроз — сумма отталкиваний, всё ещё бегство
        var two = Base();
        two.Creatures = new[] { Sense(1, 0, 20, threatens: true), Sense(0, 1, 20, threatens: true) };
        var d2 = PuppetBrain.Think(two);
        Eq((int)PuppetIntent.Flee, (int)d2.Intent, "two threats — still flee");
        True(d2.Move.X < 0 && d2.Move.Y < 0, "away from both");
    }

    public static void SmallEdibleIsNotAThreat() {
        var inp = Base(mass: 10.0);
        inp.Creatures = new[] { Sense(1.0, 0, 3.0) };   // мелкий жук: съедобен, не угрожает
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Hunt, (int)d.Intent, "hunts instead of fleeing");
        // крупный хищник рядом — уже угроза
        var inp2 = Base(mass: 3.0);
        inp2.Creatures = new[] { Sense(1.0, 0, 3.0, threatens: true) };  // равный по массе — ранит
        var d2 = PuppetBrain.Think(inp2);
        Eq((int)PuppetIntent.Flee, (int)d2.Intent, "equal foe threatens");
    }

    // ---------- выбор цели ----------

    public static void PicksMostProfitableTarget() {
        var inp = Base(mass: 10.0);
        // дальний крупный (10/8 = 1.25) против ближнего мелкого (3/3 = 1.0): выгоднее богатый
        inp.Creatures = new[] { Sense(-8, 0, 10), Sense(3, 0, 3) };
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Hunt, (int)d.Intent, "hunt");
        True(d.Move.X < -0.9, "toward the richer target (west)");
        // теперь богатый слишком далеко (10/12 = 0.83 против 3/2 = 1.5) — выбираем ближнего
        var inp2 = Base(mass: 10.0);
        inp2.Creatures = new[] { Sense(-12, 0, 10), Sense(2, 0, 3) };
        var d2 = PuppetBrain.Think(inp2);
        Eq((int)PuppetIntent.Hunt, (int)d2.Intent, "still hunt");
        True(d2.Move.X > 0.9, "switches to the cheaper near target (east)");
        Eq(1.0, d2.Move.Length, "normalized");
    }

    public static void ShimmeringPreferredOverEqual() {
        var inp = Base();
        // равные по выгоде цели (масса/дистанция = 1), одна мерцает
        inp.Creatures = new[] { Sense(0, 4, 4), Sense(4, 0, 4, shimmer: true) };
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Hunt, (int)d.Intent, "hunt");
        True(d.Move.X > 0.7 && d.Move.Y > -0.1, "chooses shimmering (east)");
    }

    public static void DashOnlyInBandAndWithReserve() {
        var inp = Base();
        inp.DashCharges = 2; inp.MaxDashCharges = 2;
        inp.Creatures = new[] { Sense(4, 0, 3) };       // 4 ед. — внутри окна 2–6
        var d = PuppetBrain.Think(inp);
        True(d.Dash, "dash with reserve");
        Eq(1.0, d.DashDir.X, "dash toward target");
        // один заряд — копим на побег, рывка нет
        inp.DashCharges = 1;
        var d2 = PuppetBrain.Think(inp);
        True(!d2.Dash, "no dash with single charge");
        // заряды есть, но цель слишком близко/далеко
        inp.DashCharges = 2;
        inp.Creatures = new[] { Sense(1.2, 0, 3) };
        True(!PuppetBrain.Think(inp).Dash, "no dash closer than 2");
        inp.Creatures = new[] { Sense(7, 0, 3) };
        True(!PuppetBrain.Think(inp).Dash, "no dash farther than 6");
        // три заряда: после рывка останется два — можно
        inp.Creatures = new[] { Sense(4, 0, 3) };
        inp.DashCharges = 3;
        True(PuppetBrain.Think(inp).Dash, "dash with 3 charges");
    }

    // ---------- обход зон ----------

    public static void AvoidsDangerZones() {
        var inp = Base(x: 1, y: 0);
        inp.Zones = new[] { new PuppetZone { Pos = Vec2.Zero, Radius = 2.0 } };
        inp.Creatures = new[] { Sense(-1, 0, 2) };      // съедобная цель прямо в зоне
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Avoid, (int)d.Intent, "avoid overrides hunt");
        True(d.Move.X > 0.5, "moves away from zone center");
        // вне зоны с запасом — снова охота
        var outp = Base(x: 4.5, y: 0);
        outp.Zones = new[] { new PuppetZone { Pos = Vec2.Zero, Radius = 2.0 } };
        outp.Creatures = new[] { Sense(1, 0, 2) };
        Eq((int)PuppetIntent.Hunt, (int)PuppetBrain.Think(outp).Intent, "outside zone — hunt");
    }

    // ---------- возврат клона ----------

    public static void CloneReturnsWhenLadenOrAtLeashEdge() {
        var inp = Base(x: 3, y: 0);
        inp.Carry = 10.0; inp.CarryCapacity = 15.0;     // 66% > 60%
        inp.LeaderPos = Vec2.Zero; inp.Leash = 20.0;
        inp.Creatures = new[] { Sense(3.5, 0.5, 2) };   // рядом добыча — но ноша важнее
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Return, (int)d.Intent, "laden clone returns");
        True(d.Move.X < -0.9, "toward leader");
        // ноша мала, но клон у края связи
        var edge = Base(x: 18, y: 0);
        edge.Carry = 1.0; edge.CarryCapacity = 15.0;
        edge.LeaderPos = Vec2.Zero; edge.Leash = 20.0;  // 18 > 20×0.85 = 17
        var d2 = PuppetBrain.Think(edge);
        Eq((int)PuppetIntent.Return, (int)d2.Intent, "at leash edge returns");
        // спокойный клон продолжает охоту
        var free = Base(x: 3, y: 0);
        free.Carry = 2.0; free.CarryCapacity = 15.0;
        free.LeaderPos = Vec2.Zero; free.Leash = 20.0;
        free.Creatures = new[] { Sense(3.5, 0, 2) };
        Eq((int)PuppetIntent.Hunt, (int)PuppetBrain.Think(free).Intent, "free clone hunts");
        // герой (без ноши и связи) никогда не «возвращается»
        var hero = Base();
        hero.Carry = 99; hero.CarryCapacity = 0;
        Eq((int)PuppetIntent.Graze, (int)PuppetBrain.Think(hero).Intent, "hero ignores carry");
    }

    // ---------- выпас ----------

    public static void GrazesOnSporesWhenNoHunt() {
        var inp = Base();
        inp.Creatures = new[] { Sense(0, 3, 0.2, food: true) };
        var d = PuppetBrain.Think(inp);
        Eq((int)PuppetIntent.Graze, (int)d.Intent, "graze intent");
        True(d.Move.Y > 0.9, "drifts toward the spore");
        // совсем пусто — стоит по инерции
        var empty = Base();
        empty.SelfVel = new Vec2(1, 0);
        var d2 = PuppetBrain.Think(empty);
        Eq((int)PuppetIntent.Graze, (int)d2.Intent, "still graze");
        True(d2.Move.Length < 1e-9 || d2.Move.Length <= 0.31, "idle drift");
    }

    public static void DeterministicForSameInput() {
        var inp = Base();
        inp.DashCharges = 2;
        inp.Creatures = new[] { Sense(3, 2, 4, shimmer: true), Sense(-2, 5, 9, threatens: true),
            Sense(1, 1, 0.3, food: true) };
        inp.Zones = new[] { new PuppetZone { Pos = new Vec2(2, 2), Radius = 1.5 } };
        var a = PuppetBrain.Think(inp);
        var b = PuppetBrain.Think(inp);
        Eq(a.Move.X, b.Move.X, "same move x");
        Eq(a.Move.Y, b.Move.Y, "same move y");
        Eq(a.Dash, b.Dash, "same dash");
        Eq((int)a.Intent, (int)b.Intent, "same intent");
    }

    // ---------- узлы дерева ----------

    private static RunState Unlocked(params string[] nodes) {
        var rs = new RunState(new SoulMeta(), 61UL, true);
        foreach (var n in nodes) { rs.Evolution.FreeTokens++; True(rs.TryEvolve(n, true), "unlock " + n); }
        return rs;
    }

    public static void PuppetNodes_RequirementsAndAbilities() {
        var puppet = EvolutionTree.Get("puppet");
        True(puppet != null, "puppet node exists");
        Eq((int)EvoBranch.Clones, (int)puppet.Branch, "in Clones branch");
        Eq(1, puppet.Requires.Length, "single prereq");
        Eq("clone_instinct", puppet.Requires[0], "requires Инстинкт клона");
        Eq(1, puppet.Effects.Length, "one effect");
        Eq("puppet", puppet.Effects[0].MutationId, "ability key puppet");

        var master = EvolutionTree.Get("puppet_master");
        True(master != null, "puppet_master node exists");
        Eq((int)EvoBranch.Clones, (int)master.Branch, "in Clones branch");
        Eq(2, master.Requires.Length, "two prereqs");
        True(System.Array.IndexOf(master.Requires, "puppet") >= 0, "needs Марионетка");
        True(System.Array.IndexOf(master.Requires, "clone_soul") >= 0, "needs Перенос души");
        Eq("puppet_master", master.Effects[0].MutationId, "ability key puppet_master");

        // блокировки: без инстинкта марионетка недоступна
        var rs = Unlocked("div_split_basics");
        rs.Evolution.FreeTokens++;
        True(!rs.TryEvolve("puppet", true), "puppet locked without instinct");
        True(!rs.HasAbility("puppet"), "no ability yet");
        // инстинкт → марионетка
        rs = Unlocked("div_split_basics", "clone_instinct", "puppet");
        True(rs.HasAbility("puppet"), "puppet ability on");
        // кукловод требует обе ветки
        rs.Evolution.FreeTokens++;
        True(!rs.TryEvolve("puppet_master", true), "master needs clone_soul");
        rs = Unlocked("div_split_basics", "clone_leash", "clone_soul", "clone_instinct", "puppet",
            "puppet_master");
        True(rs.HasAbility("puppet_master"), "master ability on");
        // у всех клоновых узлов корень — «Деление» (новые тоже)
        foreach (var id in new[] { "puppet", "puppet_master" }) {
            var cur = EvolutionTree.Get(id); int safety = 8;
            while (cur.Requires.Length > 0 && safety-- > 0) cur = EvolutionTree.Get(cur.Requires[0]);
            Eq("div_split_basics", cur.Id, id + " roots at Деление");
        }
    }
}
}
