using System.Collections.Generic;
using UnityEngine;

namespace PM.Systems.Gameplay {
/// <summary>
/// Сторонние ассеты (Assets/ThirdParty, см. LICENSES.md): шрифты, кнопки, иконки, звуки, музыка, 3D-монстры.
/// Всё грузится из Resources лениво и с откатом: нет файла — игра работает на процедурной графике.
/// </summary>
public static class ThirdPartyArt {
    // ---------- Шрифты ----------
    private static Font _body, _title;
    private static bool _fontsTried;
    public static Font Body { get { LoadFonts(); return _body; } }
    public static Font Title { get { LoadFonts(); return _title ?? _body; } }
    private static void LoadFonts() {
        if (_fontsTried) return;
        _fontsTried = true;
        _body = Resources.Load<Font>("Fonts/Rubik");
        _title = Resources.Load<Font>("Fonts/RussoOne");
    }

    // ---------- Кнопка с объёмом (Kenney, тонируется цветом кнопки) ----------
    private static Sprite _button;
    private static bool _buttonTried;
    public static Sprite Button {
        get {
            if (_buttonTried) return _button;
            _buttonTried = true;
            var tex = Resources.Load<Texture2D>("UI/button_rectangle_depth_gradient");
            if (tex != null)
                _button = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(18, 14, 18, 12), false);
            return _button;
        }
    }

    // ---------- Иконки game-icons.net (белые на прозрачном — тонируются) ----------
    private static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string name) {
        if (string.IsNullOrEmpty(name)) return null;
        if (_icons.TryGetValue(name, out var s)) return s;
        var tex = Resources.Load<Texture2D>("Icons/" + name);
        s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
        _icons[name] = s;
        return s;
    }

    // ---------- Звуки (Kenney) поверх процедурных ----------
    private static readonly Dictionary<string, AudioClip[]> _sfx = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, string[]> SfxMap = new Dictionary<string, string[]> {
        { "jet", new[] { "cloth1", "cloth2", "cloth3", "cloth4" } },
        { "capture", new[] { "chop" } },
        { "hit", new[] { "metalPot1", "metalPot2", "metalPot3" } },
        { "weaken", new[] { "knifeSlice", "knifeSlice2" } },
        { "unlock", new[] { "metalLatch" } },
        { "death", new[] { "doorClose_4" } },
        { "assist", new[] { "creak1", "creak2" } },
        { "click", new[] { "click1", "click2", "click3" } },
        { "coins", new[] { "handleCoins", "handleCoins2" } },
    };
    public static AudioClip Sfx(string key) {
        if (!_sfx.TryGetValue(key, out var clips)) {
            clips = null;
            if (SfxMap.TryGetValue(key, out var names)) {
                var list = new List<AudioClip>();
                foreach (var n in names) { var c = Resources.Load<AudioClip>("Audio/" + n); if (c != null) list.Add(c); }
                if (list.Count > 0) clips = list.ToArray();
            }
            _sfx[key] = clips;
        }
        return clips != null ? clips[Random.Range(0, clips.Length)] : null;
    }

    public static AudioClip Music(string name) => Resources.Load<AudioClip>("Music/" + name);

    // ---------- 3D-монстры (Quaternius) ----------
    private static readonly Dictionary<string, string> MonsterFor = new Dictionary<string, string> {
        { "mite", "Bee" }, { "beetle", "Crab" }, { "glowworm", "Ghost" }, { "spikeback", "Cactus" },
        { "jelly", "Q_Slime" }, { "crystal_mite", "Cyclops" }, { "soul_slime", "Alien" }, { "ember_beetle", "Demon" },
        { "salamander", "Q_Dragon" }, { "leech", "Skull" }, { "bubble_jelly", "Q_Slime" }, { "shard_crab", "Crab" },
        { "wisp", "Ghost" }, { "warden", "Cthulhu" }, { "nemesis", "GreenDemon" },
    };
    private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, float> _footprint = new Dictionary<string, float>();

    public static bool HasModel(string speciesId) => MonsterFor.ContainsKey(speciesId) && Prefab(MonsterFor[speciesId]) != null;

    private static GameObject Prefab(string model) {
        if (_prefabs.TryGetValue(model, out var p)) return p;
        p = Resources.Load<GameObject>("Monsters/" + model);
        _prefabs[model] = p;
        return p;
    }

    /// <summary>
    /// Посадить 3D-модель на существо. Вид 3/4 сверху (наклон к камере), модель «смотрит» по движению.
    /// diameter — диаметр тела (мир. ед.); модель вписывается в ~1.5× этого диаметра.
    /// </summary>
    public static MonsterModel Attach(Transform creature, string speciesId, float diameter, Color tint, bool tinted) {
        if (!MonsterFor.TryGetValue(speciesId, out var model)) return null;
        var prefab = Prefab(model);
        if (prefab == null) return null;
        var tilt = new GameObject("model_tilt").transform;
        tilt.SetParent(creature, false);
        tilt.localPosition = new Vector3(0f, -diameter * 0.18f, -0.6f);   // ближе к камере, чем спрайты пола
        tilt.localRotation = Quaternion.Euler(-58f, 0f, 0f);                // 3/4 сверху, как в мобильных RPG
        var yaw = new GameObject("model_yaw").transform;
        yaw.SetParent(tilt, false);
        var inst = Object.Instantiate(prefab, yaw, false);
        inst.name = model;
        var mat = Resources.Load<Material>("Monsters/Materials/" + model);
        foreach (var r in inst.GetComponentsInChildren<Renderer>()) {
            if (mat != null) { var arr = r.sharedMaterials; for (int i = 0; i < arr.Length; i++) arr[i] = mat; r.sharedMaterials = arr; }
            if (tinted) foreach (var m in r.materials) m.color = Color.Lerp(m.color, tint, 0.55f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
        }
        // масштаб по «следу» модели на полу (наибольшая из ширины и глубины)
        if (!_footprint.TryGetValue(model, out float fp)) {
            var b = new Bounds(); bool first = true;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>()) {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); // мировые габариты ассета
            }
            fp = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            _footprint[model] = fp;
        }
        float scale = diameter * 1.5f / fp;
        inst.transform.localScale = inst.transform.localScale * scale;
        var anim = inst.GetComponentInChildren<Animation>();
        var mm = tilt.gameObject.AddComponent<MonsterModel>();
        mm.Yaw = yaw;
        mm.Anim = anim;
        if (anim != null) {
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            mm.Loop = PickClip(anim, "Walk", "Flying", "Running", "Idle");
            mm.IdleClip = PickClip(anim, "Idle", "Flying", "Walk");
            mm.HitClip = PickClip(anim, "HitRecieve", "Hit");
            if (mm.Loop != null) anim.Play(mm.Loop);
        }
        return mm;
    }

    private static string PickClip(Animation anim, params string[] keys) {
        foreach (var k in keys)
            foreach (AnimationState st in anim)
                if (st.name.Contains(k)) return st.name;
        return null;
    }
}

/// <summary>Живая модель существа: поворот по направлению движения, ходьба/покой, вздрагивание при ударе.</summary>
public sealed class MonsterModel : MonoBehaviour {
    public Transform Yaw;
    public Animation Anim;
    public string Loop, IdleClip, HitClip;
    private float _yaw = 180f;     // по умолчанию — лицом к игроку (вниз по экрану)
    private Rigidbody2D _rb;
    private bool _moving = true;

    void Start() { _rb = GetComponentInParent<Rigidbody2D>(); }

    void Update() {
        if (Yaw == null) return;
        Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;
        float sp = v.magnitude;
        if (sp > 0.3f) {
            float target = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
            _yaw = Mathf.LerpAngle(_yaw, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        Yaw.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        if (Anim == null) return;
        bool moving = sp > 0.4f;
        if (moving != _moving && !Anim.IsPlaying(HitClip ?? "")) {
            _moving = moving;
            string clip = moving ? Loop : (IdleClip ?? Loop);
            if (clip != null) Anim.CrossFade(clip, 0.2f);
        }
    }

    public void Flinch() {
        if (Anim == null || HitClip == null) return;
        Anim.CrossFade(HitClip, 0.05f);
        Anim.CrossFadeQueued(_moving ? Loop : (IdleClip ?? Loop), 0.2f);
    }
}
}
