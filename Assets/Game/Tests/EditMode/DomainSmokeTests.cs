using NUnit.Framework;
using PM.Domain;
using PM.Domain.Core;
using PM.Domain.Body;
using PM.Domain.World;
using PM.Domain.Genetics;
using PM.Persistence;

namespace PM.Tests.EditMode {
/// <summary>
/// Дымовые тесты в Unity (Compile-time + базовые инварианты Domain внутри Unity-рантайма).
/// Полное покрытие — в tools/dotnet-tests (D-20).
/// </summary>
public class DomainSmokeTests {
    [Test]
    public void Domain_TypesAreUsableInsideUnity() {
        var v = new Vec2(3, 4);
        Assert.AreEqual(5.0, v.Length, 1e-9);
        var body = new BodyMassState(1, 5);
        Assert.AreEqual(6.0, body.Total, 1e-9);
    }
    [Test]
    public void Sim_HeadlessStep_Runs() {
        var sim = new GameSim(SimMode.Headless);
        sim.AddPellet(new Vec2(0.2, 0), 0.3);
        sim.Step(0.05);
        Assert.GreaterOrEqual(sim.Clock.WorldTime, 0.0);
    }
    [Test]
    public void SaveCodec_Roundtrip_Minimal() {
        var d = new SaveData { Schema = SaveData.SchemaVersion, Water = 2.5 };
        string json = SaveService.WrapWithChecksum(d);
        var back = SaveService.UnwrapWithChecksum(json);
        Assert.AreEqual(2.5, back.Water, 1e-9);
    }
    [Test]
    public void GeneCatalog_HasDemoNodes() {
        Assert.IsTrue(GeneCatalog.Demo().Length >= 8);
    }
    [Test]
    public void OrthoFraming_CoversWideScreens() {
        Assert.AreEqual(5.0, OrthoFraming.ComputeOrthoSize(20.0 / 9.0), 1e-9);
    }
}
}
