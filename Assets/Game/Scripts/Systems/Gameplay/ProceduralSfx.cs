using System.Collections.Generic;
using UnityEngine;
namespace PM.Systems.Gameplay {
/// <summary>
/// Звуки (v0.9): синтезированная «мокрая» палитра слизи — чавканье, бульканье, всплески, колокольца — без ассетов.
/// Каждый звук играет из пула источников со своей панорамой и случайной высотой (±6%), чтобы не надоедал.
/// Громкость — AudioListener (общая), сохраняется в PlayerPrefs.
/// </summary>
public sealed class ProceduralSfx : MonoBehaviour {
    public static ProceduralSfx Instance { get; private set; }
    public const string PrefKey = "pm_volume";
    public const int Rate = 22050;
    private const int Voices = 10;
    private readonly AudioSource[] _pool = new AudioSource[Voices];
    private int _next;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> _last = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> Gain = new Dictionary<string, float> {
        { "jet", 0.75f }, { "eat", 0.55f }, { "capture", 0.9f }, { "weaken", 0.85f }, { "hit", 1f }, { "deny", 0.5f },
        { "unlock", 0.7f }, { "evolve", 0.75f }, { "portal", 0.9f }, { "altar", 0.7f }, { "death", 1f }, { "split", 0.75f },
        { "merge", 0.75f }, { "dissolve", 0.8f }, { "pickup", 0.55f }, { "deposit", 0.7f }, { "assist", 0.6f },
        { "crystal", 0.8f }, { "shock", 1f }, { "click", 0.6f },
    };

    void Awake() {
        Instance = this;
        for (int i = 0; i < Voices; i++) {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            _pool[i] = s;
        }
        var rnd = new System.Random(7);
        // игрок и бой
        Put("jet", Synth.Mix(Synth.Whoosh(rnd, 0.3f), Synth.Sweep(0.07f, 190f, 90f, 0.35f, 30f)));
        Put("eat", Synth.Echo(Synth.Mix(Synth.Sweep(0.08f, 380f, 1250f, 0.6f, 55f), Synth.Noise(rnd, 0.012f, 3000f, 0.15f)), 0.06f, 0.2f));
        Put("capture", Synth.Echo(Synth.Mix(Synth.Squelch(rnd, 0.24f, 1400f, 260f), Synth.Gulp(0.3f, 260f, 105f)), 0.09f, 0.25f));
        Put("weaken", Synth.Mix(Synth.Noise(rnd, 0.06f, 2200f, 0.55f), Synth.Sweep(0.14f, 160f, 70f, 0.7f, 22f)));
        Put("hit", Synth.Drive(Synth.Mix(Synth.Sweep(0.24f, 85f, 38f, 0.95f, 14f), Synth.Noise(rnd, 0.09f, 650f, 0.6f)), 1.8f));
        Put("deny", Synth.Concat(Synth.Sweep(0.06f, 230f, 220f, 0.35f, 40f), Synth.Silence(0.03f), Synth.Sweep(0.07f, 185f, 175f, 0.35f, 40f)));
        Put("split", Synth.Mix(Synth.Vibrato(0.2f, 170f, 540f, 18f, 0.5f), Synth.Delay(Synth.Sweep(0.05f, 700f, 1300f, 0.5f, 60f), 0.18f)));
        Put("merge", Synth.Echo(Synth.Mix(Synth.Sweep(0.22f, 560f, 190f, 0.55f, 16f), Synth.Squelch(rnd, 0.2f, 900f, 200f)), 0.08f, 0.2f));
        Put("dissolve", Synth.Mix(Synth.Fizz(rnd, 0.55f), Synth.Sweep(0.4f, 900f, 260f, 0.25f, 7f)));
        Put("death", Synth.Echo(Synth.Mix(Synth.Vibrato(1.5f, 220f, 48f, 5f, 0.8f), Synth.Noise(rnd, 1.2f, 300f, 0.35f)), 0.22f, 0.35f));
        Put("assist", Synth.Boing(0.3f));
        Put("shock", Synth.Rumble(rnd, 1.1f, 110f, 1f));
        // находки и прогресс
        Put("unlock", Synth.Echo(Synth.Bell(1.1f, 660f, 0.6f), 0.12f, 0.3f));
        Put("evolve", Synth.Echo(Synth.Arp(new[] { 523f, 659f, 784f, 1046f }, 0.09f, 0.9f), 0.14f, 0.3f));
        Put("portal", Synth.Echo(Synth.Mix(Synth.Swell(1.2f, 110f, 220f), Synth.Whoosh(rnd, 1.0f)), 0.18f, 0.3f));
        Put("altar", Synth.Echo(Synth.Chord(1.4f, new[] { 440f, 554f, 659f, 880f }, 0.35f), 0.16f, 0.35f));
        Put("pickup", Synth.Mix(Synth.Bell(0.3f, 1318f, 0.4f), Synth.Delay(Synth.Bell(0.3f, 1760f, 0.35f), 0.05f)));
        Put("deposit", Synth.Echo(Synth.Arp(new[] { 880f, 1175f, 1568f }, 0.06f, 0.5f), 0.1f, 0.25f));
        Put("crystal", Synth.Echo(Synth.Shatter(rnd, 0.6f), 0.1f, 0.3f));
        // атмосфера (их играет RunAudio)
        Put("drip", Synth.Echo(Synth.Echo(Synth.Sweep(0.09f, 950f, 1900f, 0.5f, 45f), 0.11f, 0.35f), 0.23f, 0.25f));
        Put("rumble", Synth.Rumble(rnd, 2.6f, 90f, 0.7f));
        Put("chirp", Synth.Trill(0.2f, 2700f, 32f));
        Put("puff", Synth.Echo(Synth.Noise(rnd, 0.35f, 800f, 0.5f, 0.08f), 0.12f, 0.25f));
        Put("crackle", Synth.Crackle(rnd, 0.7f));
        Put("bubble", Synth.Concat(Synth.Sweep(0.05f, 300f, 700f, 0.4f, 50f), Synth.Silence(0.06f), Synth.Sweep(0.05f, 350f, 820f, 0.35f, 50f), Synth.Silence(0.09f), Synth.Sweep(0.06f, 260f, 640f, 0.3f, 45f)));
        Put("glass", Synth.Echo(Synth.Bell(0.8f, 2350f, 0.35f), 0.15f, 0.35f));
        Put("thump", Synth.Concat(Synth.Sweep(0.14f, 70f, 45f, 0.9f, 20f), Synth.Silence(0.12f), Synth.Sweep(0.16f, 62f, 40f, 0.75f, 18f)));
        AudioListener.volume = CurrentVolume();
    }

    private void Put(string key, float[] d) => _clips[key] = Synth.Clip("sfx_" + key, Synth.Normalize(d, 0.9f));

    public AudioClip Clip(string key) => _clips.TryGetValue(key, out var c) ? c : null;

    public void Play(string key) => Play(key, 1f, 0f, 1f + Random.Range(-0.06f, 0.06f));

    /// <summary>Звук с громкостью, панорамой (−1..1) и высотой. Один и тот же звук не чаще раза в 40 мс.</summary>
    public void Play(string key, float volume, float pan, float pitch) {
        if (_last.TryGetValue(key, out var t) && Time.unscaledTime - t < 0.04f) return;
        _last[key] = Time.unscaledTime;
        AudioClip clip = key == "click" ? ThirdPartyArt.Sfx("click") : null;   // клики интерфейса — записанные (Kenney, CC0)
        if (clip == null && !_clips.TryGetValue(key, out clip)) return;
        var src = _pool[_next];
        _next = (_next + 1) % Voices;
        src.pitch = pitch;
        src.panStereo = Mathf.Clamp(pan, -1f, 1f);
        src.PlayOneShot(clip, volume * (Gain.TryGetValue(key, out var g) ? g : 0.8f));
    }

    public static float CurrentVolume() => PlayerPrefs.GetFloat(PrefKey, 0.8f);

    public static void ChangeVolume(float delta) {
        float v = Mathf.Clamp(CurrentVolume() + delta, 0f, 1f);
        PlayerPrefs.SetFloat(PrefKey, v);
        PlayerPrefs.Save();
        AudioListener.volume = v;
    }
}

/// <summary>Маленький синтезатор: генераторы и обработки над float[] (моно, 22050 Гц).</summary>
public static class Synth {
    private const int R = ProceduralSfx.Rate;
    private const float TwoPi = Mathf.PI * 2f;

    public static AudioClip Clip(string name, float[] d) {
        var c = AudioClip.Create(name, Mathf.Max(1, d.Length), 1, R, false);
        c.SetData(d, 0);
        return c;
    }

    // --- генераторы (возвращают клипы через Mix/Echo; сырые массивы — внутри) ---
    public static float[] Sweep(float dur, float f0, float f1, float amp, float decay) {
        int n = (int)(R * dur); var d = new float[n]; float ph = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            ph += TwoPi * Mathf.Lerp(f0, f1, t) / R;
            d[i] = Mathf.Sin(ph) * amp * Mathf.Exp(-s * decay) * Mathf.Min(1f, s * 400f);
        }
        return d;
    }

    public static float[] Vibrato(float dur, float f0, float f1, float rate, float amp) {
        int n = (int)(R * dur); var d = new float[n]; float ph = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            float f = Mathf.Lerp(f0, f1, t) * (1f + 0.06f * Mathf.Sin(TwoPi * rate * s));
            ph += TwoPi * f / R;
            d[i] = Mathf.Sin(ph) * amp * Mathf.Min(1f, s * 60f) * (1f - t);
        }
        return d;
    }

    /// <summary>Шум через однополюсный фильтр нижних частот; attack — время нарастания.</summary>
    public static float[] Noise(System.Random rnd, float dur, float cutoff, float amp, float attack = 0.002f) {
        int n = (int)(R * dur); var d = new float[n]; float y = 0, a = 1f - Mathf.Exp(-TwoPi * cutoff / R);
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            y += a * ((float)rnd.NextDouble() * 2f - 1f - y);
            d[i] = y * amp * Mathf.Min(1f, s / attack) * (1f - t) * (1f - t) * 2.2f;
        }
        return d;
    }

    /// <summary>Чавканье: шум с фильтром, скользящим сверху вниз, и «мокрыми» провалами громкости.</summary>
    public static float[] Squelch(System.Random rnd, float dur, float fHi, float fLo) {
        int n = (int)(R * dur); var d = new float[n]; float y = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            float fc = Mathf.Lerp(fHi, fLo, t), a = 1f - Mathf.Exp(-TwoPi * fc / R);
            y += a * ((float)rnd.NextDouble() * 2f - 1f - y);
            float wob = 0.6f + 0.4f * Mathf.Sin(TwoPi * 23f * s);
            d[i] = y * 2.6f * wob * Mathf.Min(1f, s * 120f) * (1f - t);
        }
        return d;
    }

    public static float[] Gulp(float dur, float f0, float f1) {
        int n = (int)(R * dur); var d = new float[n]; float ph = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            float f = Mathf.Lerp(f0, f1, t * t) * (1f + 0.12f * Mathf.Sin(TwoPi * 11f * s));
            ph += TwoPi * f / R;
            d[i] = Mathf.Sin(ph) * 0.5f * Mathf.Sin(Mathf.PI * t);
        }
        return d;
    }

    public static float[] Whoosh(System.Random rnd, float dur) {
        int n = (int)(R * dur); var d = new float[n]; float y = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n;
            float fc = 300f + 2400f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)), a = 1f - Mathf.Exp(-TwoPi * fc / R);
            y += a * ((float)rnd.NextDouble() * 2f - 1f - y);
            d[i] = y * 0.9f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));
        }
        return d;
    }

    public static float[] Fizz(System.Random rnd, float dur) {
        int n = (int)(R * dur); var d = new float[n]; float prev = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, x = (float)rnd.NextDouble() * 2f - 1f;
            float hp = x - prev; prev = x;                       // грубый ФВЧ — шипение
            d[i] = hp * 0.35f * (1f - t) * (rnd.NextDouble() < 0.6 ? 1f : 0.3f);
        }
        return d;
    }

    public static float[] Rumble(System.Random rnd, float dur, float cutoff, float amp) {
        int n = (int)(R * dur); var d = new float[n]; float y1 = 0, y2 = 0, a = 1f - Mathf.Exp(-TwoPi * cutoff / R);
        for (int i = 0; i < n; i++) {
            float t = (float)i / n;
            y1 += a * ((float)rnd.NextDouble() * 2f - 1f - y1);
            y2 += a * (y1 - y2);
            d[i] = y2 * 9f * amp * Mathf.Sin(Mathf.PI * t);
        }
        return d;
    }

    public static float[] Bell(float dur, float f, float amp) {
        int n = (int)(R * dur); var d = new float[n];
        float[] ratio = { 1f, 2.0f, 2.76f, 4.07f }, gain = { 1f, 0.45f, 0.3f, 0.15f }, dec = { 4f, 6f, 9f, 13f };
        for (int i = 0; i < n; i++) {
            float s = i / (float)R, v = 0;
            for (int k = 0; k < 4; k++) v += Mathf.Sin(TwoPi * f * ratio[k] * s) * gain[k] * Mathf.Exp(-s * dec[k]);
            d[i] = v * amp * 0.55f * Mathf.Min(1f, s * 500f);
        }
        return d;
    }

    public static float[] Arp(float[] notes, float step, float tail) {
        float[] r = Silence(step * notes.Length + tail);
        for (int k = 0; k < notes.Length; k++) r = Mix(r, Delay(Bell(tail, notes[k], 0.45f), step * k));
        return r;
    }

    public static float[] Chord(float dur, float[] notes, float amp) {
        int n = (int)(R * dur); var d = new float[n];
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R, v = 0;
            for (int k = 0; k < notes.Length; k++) v += Mathf.Sin(TwoPi * notes[k] * s * (1f + 0.002f * k));
            d[i] = v / notes.Length * amp * 2f * Mathf.Min(1f, t * 5f) * (1f - t);
        }
        return d;
    }

    public static float[] Swell(float dur, float f0, float f1) {
        int n = (int)(R * dur); var d = new float[n]; float p1 = 0, p2 = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R, f = Mathf.Lerp(f0, f1, t * t);
            p1 += TwoPi * f / R; p2 += TwoPi * f * 1.5f / R;
            d[i] = (Mathf.Sin(p1) + 0.5f * Mathf.Sin(p2)) * 0.45f * Mathf.Sin(Mathf.PI * t) * (0.75f + 0.25f * Mathf.Sin(TwoPi * 7f * s));
        }
        return d;
    }

    public static float[] Boing(float dur) {
        int n = (int)(R * dur); var d = new float[n]; float ph = 0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            float f = 170f + 180f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 2.5f)) * Mathf.Exp(-t * 2f);
            ph += TwoPi * f / R;
            d[i] = Mathf.Sin(ph) * 0.6f * Mathf.Exp(-s * 9f) * Mathf.Min(1f, s * 300f);
        }
        return d;
    }

    public static float[] Shatter(System.Random rnd, float dur) {
        var r = Noise(rnd, 0.05f, 6000f, 0.4f);
        for (int k = 0; k < 7; k++) {
            float f = 1800f + (float)rnd.NextDouble() * 4200f;
            r = Mix(r, Delay(Bell(dur * 0.8f, f, 0.18f), (float)rnd.NextDouble() * 0.12f));
        }
        return r;
    }

    public static float[] Trill(float dur, float f, float rate) {
        int n = (int)(R * dur); var d = new float[n];
        for (int i = 0; i < n; i++) {
            float t = (float)i / n, s = i / (float)R;
            float am = Mathf.Max(0f, Mathf.Sin(TwoPi * rate * s));
            d[i] = Mathf.Sin(TwoPi * f * (1f + 0.04f * t) * s) * am * 0.3f * Mathf.Sin(Mathf.PI * t);
        }
        return d;
    }

    public static float[] Crackle(System.Random rnd, float dur) {
        var d = Silence(dur);
        int pops = 9 + rnd.Next(6);
        for (int k = 0; k < pops; k++) {
            int at = rnd.Next(d.Length - 200), len = 40 + rnd.Next(120);
            float amp = 0.2f + (float)rnd.NextDouble() * 0.4f;
            for (int i = 0; i < len; i++) d[at + i] += ((float)rnd.NextDouble() * 2f - 1f) * amp * (1f - (float)i / len);
        }
        return d;
    }

    public static float[] Silence(float dur) => new float[Mathf.Max(1, (int)(R * dur))];

    // --- обработки ---
    public static float[] Mix(float[] a, float[] b) {
        var d = new float[Mathf.Max(a.Length, b.Length)];
        for (int i = 0; i < a.Length; i++) d[i] += a[i];
        for (int i = 0; i < b.Length; i++) d[i] += b[i];
        return d;
    }

    public static float[] Delay(float[] a, float sec) {
        int off = (int)(R * sec); var d = new float[a.Length + off];
        System.Array.Copy(a, 0, d, off, a.Length);
        return d;
    }

    public static float[] Concat(params float[][] parts) {
        int n = 0; foreach (var p in parts) n += p.Length;
        var d = new float[n]; int o = 0;
        foreach (var p in parts) { System.Array.Copy(p, 0, d, o, p.Length); o += p.Length; }
        return d;
    }

    /// <summary>Эхо пещеры: две затухающие копии.</summary>
    public static float[] Echo(float[] a, float sec, float fb) => Mix(Mix(a, Scale(Delay(a, sec), fb)), Scale(Delay(a, sec * 2.1f), fb * fb));

    /// <summary>Пик — не выше peak: звуки разной природы звучат ровно, без щелчков перегруза.</summary>
    public static float[] Normalize(float[] a, float peak) {
        float m = 0; for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, Mathf.Abs(a[i]));
        return m > peak ? Scale(a, peak / m) : a;
    }

    public static float[] Scale(float[] a, float k) { var d = new float[a.Length]; for (int i = 0; i < a.Length; i++) d[i] = a[i] * k; return d; }

    /// <summary>Мягкое перегрузочное «уплотнение» — удар звучит тяжелее.</summary>
    public static float[] Drive(float[] a, float k) {
        var d = new float[a.Length]; float norm = 1f / (float)System.Math.Tanh(k);
        for (int i = 0; i < a.Length; i++) d[i] = (float)System.Math.Tanh(a[i] * k) * norm * 0.8f;
        return d;
    }
}
}
