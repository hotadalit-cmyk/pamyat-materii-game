using System.Collections.Generic;
using UnityEngine;
using PM.Domain.Run;
using PM.Systems.Gameplay;

namespace PM.Systems.Run {
/// <summary>Препятствие или зона комнаты (v0.5). Твёрдые — с коллайдером, зоны — проверяются по дистанции.</summary>
public sealed class RunObstacle : MonoBehaviour {
    public ObstacleKind Kind;
    public float Radius;
    public bool Solid;
    public bool Shattered;
    public SpriteRenderer Main, Glow;
}

/// <summary>
/// Строит арену в цветах биома и препятствия. Только вид и коллайдеры — правила в Domain.Run
/// (ObstacleRules). Всё процедурно: круги, кольца, свечение; «стиль» биома — палитра и акценты.
/// </summary>
public static class RunWorldBuilder {
    public static Color Hex(uint rgb, float a = 1f) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

    private static readonly Dictionary<ObstacleKind, PhysicsMaterial2D> _mats = new Dictionary<ObstacleKind, PhysicsMaterial2D>();
    private static PhysicsMaterial2D MatFor(ObstacleKind k) {
        if (!_mats.TryGetValue(k, out var m)) {
            m = new PhysicsMaterial2D("obstacle_" + k) { bounciness = (float)ObstacleRules.Bounciness(k), friction = 0f };
            _mats[k] = m;
        }
        return m;
    }

    public static void BuildArena(Transform root, RoomPlan plan, BiomeDef biome, bool elite, PhysicsMaterial2D wallMat,
                                  float wallThickness) {
        float hw = (float)plan.HalfWidth, hh = (float)plan.HalfHeight;
        Color floor = Hex(biome.FloorColor), wall = Hex(biome.WallColor), glowC = Hex(biome.GlowColor);
        if (elite) floor = Color.Lerp(floor, new Color(0.35f, 0.08f, 0.1f), 0.35f);
        SpriteUtil.AddSprite(root, SpriteUtil.GlowSprite, hw * 3.4f, hh * 3.8f, glowC * 0.9f, -1).name = "bg_glow";
        SpriteUtil.AddSprite(root, SpriteUtil.SoftRectSprite, hw * 2f + 0.2f, hh * 2f + 0.2f, floor, 0).name = "floor";
        // «пятна» пола — немного фактуры без текстур
        var rnd = new System.Random(plan.Depth * 1000 + plan.RoomIndex);
        for (int i = 0; i < 14; i++) {
            float x = (float)(rnd.NextDouble() * 2 - 1) * hw * 0.9f, y = (float)(rnd.NextDouble() * 2 - 1) * hh * 0.9f;
            float s = 0.8f + (float)rnd.NextDouble() * 2.4f;
            var c = Color.Lerp(floor, glowC, 0.25f); c.a = 0.18f;
            var spot = SpriteUtil.AddSprite(root, SpriteUtil.GlowSprite, s, s * 0.8f, c, 0);
            spot.transform.position = new Vector3(x, y, 0);
        }
        MakeWall(root, new Vector2(0, hh + wallThickness * 0.5f), new Vector2(hw * 2f + wallThickness * 2f, wallThickness), wall, wallMat);
        MakeWall(root, new Vector2(0, -hh - wallThickness * 0.5f), new Vector2(hw * 2f + wallThickness * 2f, wallThickness), wall, wallMat);
        MakeWall(root, new Vector2(-hw - wallThickness * 0.5f, 0), new Vector2(wallThickness, hh * 2f), wall, wallMat);
        MakeWall(root, new Vector2(hw + wallThickness * 0.5f, 0), new Vector2(wallThickness, hh * 2f), wall, wallMat);
        // светящаяся кромка по периметру — акцент биома
        var edge = Hex(biome.AccentColor, 0.18f);
        AddEdge(root, new Vector2(0, hh - 0.08f), new Vector2(hw * 2f - 0.3f, 0.08f), edge);
        AddEdge(root, new Vector2(0, -hh + 0.08f), new Vector2(hw * 2f - 0.3f, 0.08f), edge);
        AddEdge(root, new Vector2(-hw + 0.08f, 0), new Vector2(0.08f, hh * 2f - 0.3f), edge);
        AddEdge(root, new Vector2(hw - 0.08f, 0), new Vector2(0.08f, hh * 2f - 0.3f), edge);
    }

    private static void MakeWall(Transform root, Vector2 pos, Vector2 size, Color color, PhysicsMaterial2D mat) {
        var go = new GameObject("wall");
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.sharedMaterial = mat;
        SpriteUtil.AddSprite(go.transform, SpriteUtil.SoftRectSprite, size.x, size.y, color, 1);
    }

    private static void AddEdge(Transform root, Vector2 pos, Vector2 size, Color c) {
        var sr = SpriteUtil.AddSprite(root, SpriteUtil.CircleSprite, size.x, size.y, c, 1);
        sr.transform.position = pos;
    }

    public static RunObstacle BuildObstacle(Transform root, ObstacleSpec spec, BiomeDef biome) {
        var go = new GameObject("obstacle_" + spec.Kind);
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3((float)spec.Pos.X, (float)spec.Pos.Y, 0);
        var o = go.AddComponent<RunObstacle>();
        o.Kind = spec.Kind;
        o.Radius = (float)spec.Radius;
        o.Solid = ObstacleRules.IsSolid(spec.Kind);
        float d = o.Radius * 2f;
        Color accent = Hex(biome.AccentColor), wall = Hex(biome.WallColor);
        switch (spec.Kind) {
            case ObstacleKind.Pillar:
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d, d, Color.Lerp(wall, Color.white, 0.12f), 3);
                SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, d * 1.04f, d * 1.04f, Color.Lerp(wall, accent, 0.35f), 3);
                SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.55f, d * 0.55f, wall * 0.8f, 3);
                break;
            case ObstacleKind.Mushroom: {
                var cap = new Color(0.95f, 0.45f, 0.75f);
                o.Glow = SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, d * 2.2f, d * 2.2f, new Color(cap.r, cap.g, cap.b, 0.35f), DarknessMask.Order + 3);
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d, d, cap, 3);
                for (int i = 0; i < 5; i++) {
                    float a = i * Mathf.PI * 2f / 5f + 0.4f;
                    var dot = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.16f, d * 0.16f, new Color(1f, 0.92f, 0.95f), 4);
                    dot.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * o.Radius * 0.55f;
                }
                break;
            }
            case ObstacleKind.Crystal: {
                var c = new Color(0.55f, 0.92f, 1f);
                o.Glow = SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, d * 2.4f, d * 2.4f, new Color(c.r, c.g, c.b, 0.4f), DarknessMask.Order + 3);
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d, d, new Color(c.r, c.g, c.b, 0.85f), 3);
                o.Main.transform.localRotation = Quaternion.Euler(0, 0, 45);
                o.Main.transform.localScale = new Vector3(d * 0.8f, d, 1);
                SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.3f, d * 0.3f, new Color(1, 1, 1, 0.8f), 4)
                    .transform.localPosition = new Vector3(-0.15f, 0.2f, 0) * o.Radius;
                break;
            }
            case ObstacleKind.Thorns: {
                var dark = new Color(0.32f, 0.08f, 0.1f);
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.8f, d * 0.8f, dark, 3);
                int spikes = 10;
                for (int i = 0; i < spikes; i++) {
                    float a = i * Mathf.PI * 2f / spikes;
                    var sp = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.16f, d * 0.42f, new Color(0.78f, 0.2f, 0.22f), 3);
                    sp.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * o.Radius * 0.82f;
                    sp.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
                }
                o.Glow = SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, d * 1.6f, d * 1.6f, new Color(0.9f, 0.2f, 0.2f, 0.18f), DarknessMask.Order + 2);
                break;
            }
            case ObstacleKind.Vent:
                o.Glow = SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, d * 1.3f, d * 1.3f, new Color(1f, 0.5f, 0.15f, 0.45f), DarknessMask.Order + 2);
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, d, d, new Color(1f, 0.55f, 0.2f, 0.5f), 2);
                SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.22f, d * 0.22f, new Color(0.15f, 0.05f, 0.02f), 2);
                break;
            case ObstacleKind.Pool:
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d, d, new Color(0.2f, 0.55f, 0.95f, 0.35f), 1);
                o.Glow = SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, d * 1.4f, d * 1.4f, new Color(0.3f, 0.65f, 1f, 0.3f), DarknessMask.Order + 2);
                SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, d, d, new Color(0.5f, 0.8f, 1f, 0.35f), 1);
                break;
            case ObstacleKind.Sticky:
                o.Main = SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d, d, new Color(0.45f, 0.75f, 0.25f, 0.38f), 1);
                for (int i = 0; i < 6; i++) {
                    float a = i * 1.1f;
                    SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, d * 0.18f, d * 0.18f, new Color(0.6f, 0.9f, 0.35f, 0.45f), 1)
                        .transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * o.Radius * 0.6f * ((i % 2) + 0.5f);
                }
                break;
        }
        if (o.Solid) {
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = o.Radius;
            col.sharedMaterial = MatFor(spec.Kind);
        }
        return o;
    }

    /// <summary>Алтарь святилища / родник: светящийся круг, к которому надо прикоснуться.</summary>
    public static SpriteRenderer BuildAltar(Transform root, Vector2 pos, bool spring, bool relic = false) {
        var go = new GameObject(relic ? "relic" : spring ? "spring" : "shrine");
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        Color c = relic ? new Color(1f, 0.82f, 0.35f) : spring ? new Color(0.35f, 0.75f, 1f) : new Color(0.75f, 0.55f, 1f);
        SpriteUtil.AddSprite(go.transform, SpriteUtil.GlowSprite, 3.4f, 3.4f, new Color(c.r, c.g, c.b, 0.45f), DarknessMask.Order + 4);
        var ring = SpriteUtil.AddSprite(go.transform, SpriteUtil.RingSprite, 1.4f, 1.4f, c, DarknessMask.Order + 5);
        SpriteUtil.AddSprite(go.transform, SpriteUtil.CircleSprite, 0.6f, 0.6f, Color.Lerp(c, Color.white, 0.5f), DarknessMask.Order + 5);
        return ring;
    }
}
}
