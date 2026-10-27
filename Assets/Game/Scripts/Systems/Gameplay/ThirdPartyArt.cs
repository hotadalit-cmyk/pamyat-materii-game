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
    // v0.15: имя может быть цепочкой «a|b|c» — берётся первый найденный; символ | — запасной вариант.
    // AiArt-иконки ищутся в AiArt/Icons/, старые — в Icons/.
    private static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string name) {
        if (string.IsNullOrEmpty(name)) return null;
        if (_icons.TryGetValue(name, out var s)) return s;
        int bar = name.IndexOf('|');
        if (bar >= 0) {
            s = Icon(name.Substring(0, bar)) ?? Icon(name.Substring(bar + 1));
            _icons[name] = s;
            return s;
        }
        var tex = Resources.Load<Texture2D>("Icons/" + name);
        if (tex == null) tex = Resources.Load<Texture2D>("AiArt/Icons/" + name); // v0.15: новый арт
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
        // v0.10: подобраны под портреты энциклопедии (членистоногие — краб, светлячок — летун, души — призрак)
        // v0.15: пересажены на новый зоомир (Spider/Wasp/Snake/Slime/Bat) + 4 новых вида + тела стражей
        { "mite", "Spider" }, { "beetle", "Crab" }, { "glowworm", "Wasp" }, { "spikeback", "Cactus" },
        { "jelly", "Q_Slime" }, { "crystal_mite", "Cyclops" }, { "soul_slime", "Slime" }, { "ember_beetle", "Demon" },
        { "salamander", "Q_Dragon" }, { "leech", "Snake" }, { "bubble_jelly", "Q_Slime" }, { "shard_crab", "Crab" },
        { "wisp", "Bat" }, { "warden", "Cthulhu" }, { "nemesis", "GreenDemon" }, { "hero", "Q_Slime" },
        { "cave_rat", "Rat" }, { "piranha", "Piranha" }, { "bomb_toad", "Frog" }, { "deep_shark", "Shark" },
        { "gd_spore_mother", "Mushroom" }, { "gd_forge", "LavaGolem" }, { "gd_abyss_choir", "Octopus" },
        { "gd_grinder", "Cyclops" }, { "gd_pulsar", "Heart" },
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
    /// diameter — диаметр тела (мир. ед.); модель вписывается в ~2.1× этого диаметра.
    /// </summary>
    /// <summary>Облик вида: цвет как на портрете и сила окраски (0 — родная текстура модели, 1 — сплошной цвет).</summary>
    private static readonly Dictionary<string, (Color c, float k)> Look = new Dictionary<string, (Color, float)> {
        { "mite", (new Color(1f, 0.7f, 0.25f), 0.55f) },          // янтарный пугливый клещ
        { "beetle", (new Color(0.62f, 0.4f, 0.22f), 0.6f) },      // бронзовый жук
        { "glowworm", (new Color(0.78f, 1f, 0.3f), 0.5f) },       // жёлто-зелёный светлячок
        { "spikeback", (new Color(1f, 0.5f, 0.2f), 0.45f) },      // рыжий шипастик
        { "jelly", (new Color(0.95f, 0.5f, 0.9f), 0.75f) },       // розово-фиолетовая медуза
        { "crystal_mite", (new Color(0.3f, 0.95f, 0.9f), 0.55f) },// бирюзовые кристаллы
        { "soul_slime", (new Color(0.65f, 0.45f, 1f), 0.6f) },    // фиолетовый призрак-двойник
        { "ember_beetle", (new Color(1f, 0.45f, 0.15f), 0.4f) },  // раскалённый лоб
        { "salamander", (new Color(1f, 0.35f, 0.15f), 0.6f) },    // огненная охотница
        { "leech", (new Color(0.35f, 0.1f, 0.15f), 0.8f) },       // тёмная пиявка
        { "bubble_jelly", (new Color(0.45f, 0.75f, 1f), 0.75f) }, // голубая пузыремедуза
        { "shard_crab", (new Color(0.62f, 0.9f, 1f), 0.55f) },    // стеклянные осколки
        { "wisp", (new Color(0.4f, 0.7f, 1f), 0.65f) },           // голубой огонёк
        { "warden", (new Color(0.6f, 0.25f, 0.2f), 0.35f) },      // каменно-багровый страж
        { "hero", (new Color(0.3f, 0.78f, 1f), 0.85f) },          // наша капля — как на заставке
        // v0.15: новые виды
        { "cave_rat", (new Color(0.55f, 0.5f, 0.45f), 0.5f) },    // пепельная крыса
        { "piranha", (new Color(0.75f, 0.25f, 0.3f), 0.55f) },    // кровавая пиранья
        { "bomb_toad", (new Color(0.5f, 0.6f, 0.25f), 0.5f) },    // болотная жаба
        { "deep_shark", (new Color(0.35f, 0.5f, 0.62f), 0.55f) }, // стальная акула
        // v0.15: тела стражей
        { "gd_spore_mother", (new Color(0.45f, 0.85f, 0.42f), 0.5f) }, // грибная зелень
        { "gd_forge", (new Color(1f, 0.5f, 0.18f), 0.55f) },           // раскалённый оранжевый
        { "gd_abyss_choir", (new Color(0.45f, 0.52f, 0.95f), 0.55f) },// бездонный сине-фиолетовый
        { "gd_grinder", (new Color(0.72f, 0.85f, 1f), 0.6f) },        // кристальный тинт
        { "gd_pulsar", (new Color(0.9f, 0.22f, 0.2f), 0.65f) },       // багровый пульс
    };

    /// <summary>Посадить модель вида с его обликом из таблицы (для души-немезиды — свой цвет).</summary>
    public static MonsterModel AttachLook(Transform creature, string speciesId, float diameter, Color nemesisTint = default, float fit = 2.1f) {
        if (Look.TryGetValue(speciesId, out var l)) return Attach(creature, speciesId, diameter, l.c, true, l.k, fit);
        return Attach(creature, speciesId, diameter, nemesisTint, nemesisTint.a > 0f, 0.55f, fit);
    }

    public static MonsterModel Attach(Transform creature, string speciesId, float diameter, Color tint, bool tinted,
                                      float tintStrength = 0.55f, float fit = 2.1f) {
        if (!MonsterFor.TryGetValue(speciesId, out var model)) return null;
        var prefab = Prefab(model);
        if (prefab == null) return null;
        var tilt = new GameObject("model_tilt").transform;
        tilt.SetParent(creature, false);
        tilt.localPosition = new Vector3(0f, -diameter * 0.18f, -0.6f);   // ближе к камере, чем спрайты пола
        tilt.localRotation = Quaternion.Euler(-58f, 0f, 0f);                // 3/4 сверху, как в мобильных RPG
        var yaw = new GameObject("model_yaw").transform;
        yaw.SetParent(tilt, false);
        // габариты считаем у живого экземпляра в начале координат: у неоткрытого префаба в сборке bounds пустые
        var inst = Object.Instantiate(prefab);
        inst.transform.position = Vector3.zero;
        inst.name = model;
        if (!_footprint.ContainsKey(model)) {
            var bb = new Bounds(); bool f0 = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) {
                if (f0) { bb = r.bounds; f0 = false; } else bb.Encapsulate(r.bounds);
            }
            _footprint[model] = Mathf.Max(0.01f, Mathf.Max(bb.size.x, bb.size.z));
        }
        inst.transform.SetParent(yaw, false);
        var mat = Resources.Load<Material>("Monsters/Materials/" + model);
        foreach (var r in inst.GetComponentsInChildren<Renderer>()) {
            if (mat != null) { var arr = r.sharedMaterials; for (int i = 0; i < arr.Length; i++) arr[i] = mat; r.sharedMaterials = arr; }
            if (tinted) foreach (var m in r.materials) m.color = Color.Lerp(m.color, tint, tintStrength);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; // рамка скин-меша в см — без этого телефон отсекает модель
        }
        float fp = _footprint[model];
        // масштаб — на своём узле: клипы FBX анимируют корень модели (позиция/масштаб) и перезаписали бы его
        float scale = diameter * fit / fp;
        yaw.localScale = new Vector3(scale, scale, scale);
        var animator = inst.GetComponentInChildren<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        var mm = tilt.gameObject.AddComponent<MonsterModel>();
        mm.Yaw = yaw;
        mm.Animator = animator;
        if (!_clips.TryGetValue(model, out var clips)) {
            clips = Resources.LoadAll<AnimationClip>("Monsters/" + model);
            _clips[model] = clips;
        }
        mm.Loop = PickClip(clips, "Walk", "Flying", "Running", "Idle");
        mm.IdleClip = PickClip(clips, "Idle", "Flying", "Walk");
        mm.HitClip = PickClip(clips, "HitRecieve", "Hit");
        mm.PlayLoop(true);
        return mm;
    }

    private static readonly Dictionary<string, AnimationClip[]> _clips = new Dictionary<string, AnimationClip[]>();

    private static AnimationClip PickClip(AnimationClip[] clips, params string[] keys) {
        if (clips == null) return null;
        foreach (var k in keys)
            foreach (var c in clips)
                if (c != null && c.name.Contains(k) && !c.name.StartsWith("__preview")) return c;
        return null;
    }
}

/// <summary>Живая модель существа: поворот по направлению движения, ходьба/покой, вздрагивание при ударе.</summary>
public sealed class MonsterModel : MonoBehaviour {
    public Transform Yaw;
    public Animator Animator;
    public AnimationClip Loop, IdleClip, HitClip;
    /// <summary>v0.15: пульс масштаба (Пульсар-сердце) — дышит в такт ударам.</summary>
    public bool ScalePulse;
    private Vector3 _baseScale = Vector3.one;
    private float _yaw = 180f;     // по умолчанию — лицом к игроку (вниз по экрану)
    private Rigidbody2D _rb;
    private bool _moving = true;
    private UnityEngine.Playables.PlayableGraph _graph;
    private float _hitUntil;

    void Start() {
        _rb = GetComponentInParent<Rigidbody2D>();
        if (Yaw != null) _baseScale = Yaw.localScale; // база для пульса (задаётся при посадке модели)
    }

    private void Play(AnimationClip clip) {
        if (clip == null || Animator == null) return;
        if (_graph.IsValid()) _graph.Destroy();
        UnityEngine.Playables.AnimationPlayableUtilities.PlayClip(Animator, clip, out _graph);
    }

    public void PlayLoop(bool moving) { _moving = moving; Play(moving ? Loop : (IdleClip ?? Loop)); }

    void Update() {
        if (Yaw == null) return;
        if (ScalePulse) {
            float ps = 1f + 0.055f * Mathf.Sin(Time.time * 2.6f);
            Yaw.localScale = new Vector3(_baseScale.x * ps, _baseScale.y * ps, _baseScale.z);
        }
        Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;
        float sp = v.magnitude;
        if (sp > 0.3f) {
            float target = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
            _yaw = Mathf.LerpAngle(_yaw, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        Yaw.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_hitUntil > 0f) {
            if (Time.time < _hitUntil) return;
            _hitUntil = 0f;
            PlayLoop(_moving);
        }
        bool moving = sp > 0.4f;
        if (moving != _moving) PlayLoop(moving);
    }

    public void Flinch() {
        if (HitClip == null) return;
        Play(HitClip);
        _hitUntil = Time.time + Mathf.Min(HitClip.length, 0.6f);
    }

    void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); }
}
}
