using UnityEngine;
using PM.Domain;
using PM.Domain.World;
using PM.Systems.Sim;

namespace PM.Systems.Gameplay {
/// <summary>Идентификатор сущности симуляции на объекте (для сопоставления коллизий).</summary>
public interface ISimEntity {
    int SimId { get; }
}
/// <summary>Типы игровых сущностей сцены.</summary>
public enum SimEntityKind { Player = 0, Ai = 1, Beetle = 2 }

/// <summary>Общий вид сущности: круг, цвет, категория. Создаётся программно (без ассетов).</summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class SimEntityView2D : MonoBehaviour, ISimEntity {
    public SimEntityKind Kind;
    public int SimId { get; set; }
    public SpriteRenderer Body;
    public Rigidbody2D Rb { get; private set; }

    void Awake() {
        Rb = GetComponent<Rigidbody2D>();
        Rb.gravityScale = 0f;
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public static SimEntityView2D Create(Transform parent, string name, SimEntityKind kind,
                                         int simId, float radius, Color color) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<SimEntityView2D>();
        view.Kind = kind;
        view.SimId = simId;
        var rb = go.GetComponent<Rigidbody2D>();
        rb.mass = Mathf.Max(0.5f, radius);
        rb.linearDamping = 0.9f;
        rb.angularDamping = 0f;
        var col = go.GetComponent<CircleCollider2D>();
        col.radius = radius;
        view.Body = SpriteUtil.AddCircle(go.transform, radius * 2f, color);
        return view;
    }
}
/// <summary>Процедурные спрайты (круг с мягким краем) — без внешних ассетов.</summary>
public static class SpriteUtil {
    public static Sprite CircleSprite {
        get {
            if (_circle == null) _circle = MakeCircle(128);
            return _circle;
        }
    }
    private static Sprite _circle;

    public static Sprite MakeCircle(int size) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f - 1f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float dx = x - size / 2f + 0.5f, dy = y - size / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01((r - d) / 3f + 1f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
    }

    public static SpriteRenderer AddCircle(Transform parent, float diameter, Color color) {
        var go = new GameObject("body");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSprite;
        sr.color = color;
        go.transform.localScale = new Vector3(diameter, diameter, 1f);
        sr.sortingOrder = 5;
        return sr;
    }
}
}
