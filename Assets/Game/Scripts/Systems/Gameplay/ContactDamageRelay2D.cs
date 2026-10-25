using UnityEngine;
using PM.Systems.Sim;

namespace PM.Systems.Gameplay {
/// <summary>
/// Контактный урон из физики Unity (QA-3): скорость вдоль нормали, массы — в Domain,
/// антидребезг и каскад-защита внутри Domain.DamageDebouncer. Одна коллизия — один отчёт.
/// </summary>
public sealed class ContactDamageRelay2D : MonoBehaviour {
    private PM.Domain.Damage.DamageDebouncer _localGuard = new PM.Domain.Damage.DamageDebouncer(0.05f);

    private void OnCollisionEnter2D(Collision2D collision) {
        var runner = SimRunner.Instance;
        if (runner == null || runner.Sim == null) return;
        var self = GetComponent<ISimEntity>();
        var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<ISimEntity>() : null;
        if (self == null || other == null) return; // стены не наносят урон в демо
        if (!_localGuard.TryRegister(self.SimId, other.SimId, Time.unscaledTime)) return;
        var n = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector2.up;
        float relNormal = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, -n));
        if (relNormal < 0.5f) return;
        runner.Sim.RegisterImpact(new PM.Domain.Damage.ImpactEvent {
            AttackerId = self.SimId,
            TargetId = other.SimId,
            RelativeNormalSpeed = relNormal,
            AttackerMass = MassOf(self.SimId, runner),
            TargetMass = MassOf(other.SimId, runner),
            HitZoneMultiplier = 1.0,
            Penetration = 1.0,
            TargetResistance = 1.0
        });
    }

    private static double MassOf(int simId, SimRunner runner) {
        var sim = runner.Sim;
        if (simId == sim.Player.Id) return sim.Player.Body.Total;
        var ai = sim.FindAi(simId);
        if (ai != null) return ai.Body.Total;
        var b = sim.FindBeetle(simId);
        if (b != null) return b.Mass;
        return 1.0;
    }
}
}
