using UnityEngine;
using PM.Domain;
using PM.Domain.Core;
using PM.Domain.World;

namespace PM.Systems.Sim {
/// <summary>
/// Физический адаптер (ARCHITECTURE «Systems использует Domain через адаптер»):
/// Unity ведёт Rigidbody2D; Domain аккумулирует экономику. Единая точка порядка шага.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class SimRunner : MonoBehaviour {
    public static SimRunner Instance { get; private set; }
    public GameSim Sim { get; private set; }
    public Rigidbody2D PlayerRb;
    public System.Collections.Generic.List<Rigidbody2D> AiRbs = new System.Collections.Generic.List<Rigidbody2D>();
    public System.Collections.Generic.List<Rigidbody2D> BeetleRbs = new System.Collections.Generic.List<Rigidbody2D>();
    public System.Collections.Generic.List<Rigidbody2D> PelletRbs = new System.Collections.Generic.List<Rigidbody2D>();

    private readonly System.Collections.Generic.Dictionary<Rigidbody2D, Vector2> _lastPositions =
        new System.Collections.Generic.Dictionary<Rigidbody2D, Vector2>();
    private PmRandom _wander;

    public void Construct(GameSim sim) {
        Sim = sim;
        _wander = new PmRandom(20261025);
        Instance = this;
    }

    void FixedUpdate() {
        if (Sim == null) return;
        float dt = Time.fixedDeltaTime;
        ApplyPendingImpulses();
        SyncKinematics();
        BeetleWander(dt);
        PelletsUpdate(dt);
        AutoCaptureRequest();
        Sim.Step(dt);
        GuardSpeeds();
    }

    private void ApplyPendingImpulses() {
        // рывки игрока и ИИ планируются в Domain (JetPlanner), применяются сюда как Δv
        var q = Sim.PlayerPendingImpulses;
        while (q.Count > 0) {
            var imp = q.Dequeue();
            if (PlayerRb != null) PlayerRb.linearVelocity += new Vector2((float)imp.X, (float)imp.Y);
        }
        for (int i = 0; i < Sim.AiActors.Count && i < AiRbs.Count; i++) {
            while (Sim.TryDrainAiImpulse(Sim.AiActors[i].Id, out var imp)) {
                if (AiRbs[i] != null) AiRbs[i].linearVelocity += new Vector2((float)imp.X, (float)imp.Y);
            }
        }
    }

    private void SyncKinematics() {
        if (PlayerRb != null) {
            double path = PathDelta(PlayerRb);
            Sim.SyncPlayerKinematics(ToVec(PlayerRb.position), ToVec(PlayerRb.linearVelocity), path);
        }
        for (int i = 0; i < Sim.AiActors.Count && i < AiRbs.Count; i++) {
            var rb = AiRbs[i];
            if (rb == null) continue;
            double path = PathDelta(rb);
            Sim.SyncAiKinematics(Sim.AiActors[i].Id, ToVec(rb.position), ToVec(rb.linearVelocity), path);
        }
    }

    private double PathDelta(Rigidbody2D rb) {
        Vector2 last;
        if (!_lastPositions.TryGetValue(rb, out last)) { _lastPositions[rb] = rb.position; return 0; }
        double d = Vector2.Distance(rb.position, last);
        _lastPositions[rb] = rb.position;
        return d;
    }

    private void BeetleWander(float dt) {
        for (int i = 0; i < Sim.Beetles.Count && i < BeetleRbs.Count; i++) {
            var rb = BeetleRbs[i];
            var b = Sim.Beetles[i];
            if (rb == null || b.Consumed || b.Phase >= PreyPhase.Internalized) continue;
            // лёгкое блуждание жука
            var dir = new Vector2((float)_wander.Range(-1, 1), (float)_wander.Range(-1, 1));
            rb.AddForce(dir * (2.2f * rb.mass), ForceMode2D.Force);
            Sim.SyncBeetleKinematics(b.Id, ToVec(rb.position));
        }
    }

    private void PelletsUpdate(float dt) {
        for (int i = 0; i < Sim.Food.Count && i < PelletRbs.Count; i++) {
            var p = Sim.Food[i];
            var rb = PelletRbs[i];
            if (rb == null) continue;
            if (p.Consumed) { if (rb.gameObject.activeSelf) rb.gameObject.SetActive(false); continue; }
            var acc = Sim.PelletPull(p); // биогравитация из Domain
            rb.AddForce(new Vector2((float)acc.X, (float)acc.Y) * rb.mass, ForceMode2D.Force);
            // поглощение по дистанции — ровно один раз (валидация в Domain)
            double d = (ToVec(rb.position) - Sim.Player.Pos).Length;
            if (d <= Sim.Player.Radius + 0.15) Sim.TryConsumePellet(p.Id);
        }
    }

    private void AutoCaptureRequest() {
        var t = Sim.CaptureTarget;
        if (t == null) return;
        double d = (t.Pos - Sim.Player.Pos).Length;
        if (d <= Sim.Cfg.CaptureRadius + Sim.Player.Radius) Sim.CaptureRequested = true;
    }

    private void GuardSpeeds() {
        float max = (float)Sim.PlayerOrgans.MaxSpeed * 1.6f;
        if (PlayerRb != null && PlayerRb.linearVelocity.magnitude > max)
            PlayerRb.linearVelocity = PlayerRb.linearVelocity.normalized * max;
    }

    public static Vec2 ToVec(Vector2 v) => new Vec2(v.x, v.y);
}
}
