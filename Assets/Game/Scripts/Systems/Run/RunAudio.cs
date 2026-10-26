using System.Collections.Generic;
using UnityEngine;
using PM.Domain.Run;
using PM.Systems.Gameplay;

namespace PM.Systems.Run {
/// <summary>
/// Атмосфера (v0.9). Четыре слоя:
/// 1) музыка — своя для каждой области, плавная смена через перекрёстное затухание;
/// 2) «дыхание» пещеры — синтезированная петля шума и гула со своим аккордом у каждой области;
/// 3) случайные звуки вокруг — капли, споры, треск жара, пузыри, звон кристаллов, далёкий гул — в стерео;
/// 4) тревога — сердцебиение и биение низких частот, нарастает, когда рядом тот, кто может вас съесть.
/// Музыка и тревога — на ползунке «Музыка», остальное — на общей громкости.
/// </summary>
public sealed class RunAudio : MonoBehaviour {
    private const int Rate = ProceduralSfx.Rate;
    private const float Fade = 3f;

    private AudioSource _musA, _musB, _bed, _tension;
    private bool _aActive = true;
    private string _track;
    private float _trackPitch = 1f;
    private BiomeId _biome = (BiomeId)(-1);
    private readonly Dictionary<BiomeId, AudioClip> _beds = new Dictionary<BiomeId, AudioClip>();
    private float _danger, _dangerTarget, _bossFloor;
    private float _ambT = 2f, _chirpT = 4f;
    private System.Random _rnd = new System.Random(11);

    public static RunAudio Create(Transform parent) {
        var go = new GameObject("run_audio");
        go.transform.SetParent(parent, false);
        var a = go.AddComponent<RunAudio>();
        a._musA = a.Src(true); a._musB = a.Src(true); a._bed = a.Src(true); a._tension = a.Src(true);
        a._tension.clip = Synth.Clip("tension", Synth.Normalize(TensionLoop(), 0.9f));
        a._tension.volume = 0f;
        a._tension.Play();
        return a;
    }

    private AudioSource Src(bool loop) {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop; s.playOnAwake = false; s.volume = 0f;
        return s;
    }

    /// <summary>Новая область (или комната босса): сменить музыку и фон.</summary>
    public void SetBiome(BiomeId biome, bool boss) {
        string track; float pitch;
        switch (biome) {
            case BiomeId.Fungal: track = "cavern"; pitch = 1f; break;
            case BiomeId.Warm: track = "cavern"; pitch = 0.9f; break;      // тот же мотив, ниже и тяжелее
            case BiomeId.Ponds: track = "dungeon"; pitch = 1f; break;
            case BiomeId.Crystal: track = "dungeon"; pitch = 1.08f; break; // выше, «стекляннее»
            default: track = "dungeon"; pitch = 0.82f; break;              // Сердце — глухо и медленно
        }
        if (boss) pitch *= 0.9f;
        _bossFloor = boss ? 0.45f : 0f;
        if (track != _track || Mathf.Abs(pitch - _trackPitch) > 0.01f) {
            var clip = ThirdPartyArt.Music(track);
            if (clip != null) {
                var next = _aActive ? _musB : _musA;
                next.clip = clip; next.pitch = pitch; next.volume = 0f;
                next.time = Random.Range(0f, clip.length * 0.5f);
                next.Play();
                _aActive = !_aActive;
                _track = track; _trackPitch = pitch;
            }
        }
        if (biome != _biome) {
            _biome = biome;
            if (!_beds.TryGetValue(biome, out var bed)) { bed = Synth.Clip("bed_" + biome, Synth.Normalize(Bed(biome), 0.9f)); _beds[biome] = bed; }
            _bed.clip = bed; _bed.volume = 0f; _bed.Play();
        }
    }

    /// <summary>danger01 — близость опасного существа; creature — позиция случайного соседа для стрекотания (или null).</summary>
    public void Tick(Vector2 listener, float danger01, Vector2? creature, bool paused) {
        _dangerTarget = Mathf.Max(Mathf.Clamp01(danger01), _bossFloor);
        float dt = Time.unscaledDeltaTime;
        _danger = Mathf.MoveTowards(_danger, _dangerTarget, dt * (_dangerTarget > _danger ? 1.5f : 0.4f));
        float music = GameSettings.MusicVolume;
        var cur = _aActive ? _musA : _musB;
        var old = _aActive ? _musB : _musA;
        float musTarget = music * 0.5f * (1f - 0.4f * _danger) * (paused ? 0.6f : 1f);
        cur.volume = Mathf.MoveTowards(cur.volume, musTarget, dt / Fade);
        old.volume = Mathf.MoveTowards(old.volume, 0f, dt / Fade);
        if (old.volume <= 0f && old.isPlaying) old.Stop();
        _bed.volume = Mathf.MoveTowards(_bed.volume, 0.55f, dt / Fade);
        _tension.volume = music * 0.85f * _danger * _danger;
        if (paused) return;
        _ambT -= dt;
        if (_ambT <= 0f) { _ambT = 1.4f + (float)_rnd.NextDouble() * 3.2f; Ambient(); }
        _chirpT -= dt;
        if (_chirpT <= 0f && creature.HasValue) {
            _chirpT = 2.5f + (float)_rnd.NextDouble() * 4f;
            Vector2 d = creature.Value - listener;
            float vol = Mathf.Clamp01(1f - d.magnitude / 13f) * 0.6f;
            if (vol > 0.05f) ProceduralSfx.Instance?.Play("chirp", vol, d.x / 9f, 0.8f + (float)_rnd.NextDouble() * 0.5f);
        }
    }

    private void Ambient() {
        string[] set;
        switch (_biome) {
            case BiomeId.Fungal: set = new[] { "drip", "drip", "puff", "puff", "rumble" }; break;
            case BiomeId.Warm: set = new[] { "crackle", "crackle", "drip", "rumble", "puff" }; break;
            case BiomeId.Ponds: set = new[] { "drip", "drip", "bubble", "bubble", "drip", "rumble" }; break;
            case BiomeId.Crystal: set = new[] { "glass", "glass", "drip", "drip", "rumble" }; break;
            default: set = new[] { "thump", "rumble", "drip", "crackle", "thump" }; break;
        }
        string key = set[_rnd.Next(set.Length)];
        float vol = key == "rumble" ? 0.7f : 0.25f + (float)_rnd.NextDouble() * 0.35f;
        float pan = (float)_rnd.NextDouble() * 1.6f - 0.8f;
        ProceduralSfx.Instance?.Play(key, vol, pan, 0.85f + (float)_rnd.NextDouble() * 0.3f);
    }

    // ---------- Синтез петель ----------
    private const float TwoPi = Mathf.PI * 2f;

    /// <summary>Фон области: тёмный шум пещеры + аккорд области с медленным «дыханием». 12 с, бесшовная петля.</summary>
    private static float[] Bed(BiomeId biome) {
        const float len = 12f, xf = 1.5f;
        int n = (int)(Rate * len), f = (int)(Rate * xf);
        var d = new float[n + f];
        var rnd = new System.Random(100 + (int)biome);
        float[] notes; float noiseCut = 260f, noiseAmp = 1f, shimmer = 0f, pulse = 0f, flow = 0f;
        switch (biome) {
            case BiomeId.Fungal: notes = new[] { 110f, 164.8f, 261.6f }; break;
            case BiomeId.Warm: notes = new[] { 55f, 82.4f, 116.5f }; noiseCut = 420f; noiseAmp = 1.2f; break;
            case BiomeId.Ponds: notes = new[] { 146.8f, 220f, 277.2f }; flow = 1f; break;
            case BiomeId.Crystal: notes = new[] { 164.8f, 246.9f }; shimmer = 1f; noiseCut = 200f; break;
            default: notes = new[] { 49f, 73.4f, 98f }; pulse = 1f; noiseAmp = 0.8f; break;
        }
        float y1 = 0, y2 = 0;
        for (int i = 0; i < d.Length; i++) {
            float s = i / (float)Rate;
            float cut = flow > 0 ? 280f + 380f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 0.09f * s)) : noiseCut;
            float a = 1f - Mathf.Exp(-TwoPi * cut / Rate);
            y1 += a * ((float)rnd.NextDouble() * 2f - 1f - y1);
            y2 += a * (y1 - y2);
            float v = y2 * 2.2f * noiseAmp * (0.75f + 0.25f * Mathf.Sin(TwoPi * 0.07f * s));
            for (int k = 0; k < notes.Length; k++)
                v += Mathf.Sin(TwoPi * notes[k] * s) * 0.09f * (0.55f + 0.45f * Mathf.Sin(TwoPi * (0.05f + 0.031f * k) * s + k));
            if (shimmer > 0) {
                float tr = 0.5f + 0.5f * Mathf.Sin(TwoPi * 0.23f * s);
                v += (Mathf.Sin(TwoPi * 1318.5f * s) + Mathf.Sin(TwoPi * 1975.5f * s) * 0.6f) * 0.018f * tr;
            }
            if (pulse > 0) {
                float ph = (s * 1.1f) % 1f;      // ~66 ударов в минуту, двойной удар
                float beat = Mathf.Exp(-ph * 22f) + 0.7f * Mathf.Exp(-Mathf.Max(0f, ph - 0.18f) * 22f) * (ph > 0.18f ? 1f : 0f);
                v += Mathf.Sin(TwoPi * 52f * s) * beat * 0.35f;
            }
            d[i] = v;
        }
        // бесшовность: хвост вплетается в начало
        var outp = new float[n];
        System.Array.Copy(d, outp, n);
        for (int i = 0; i < f; i++) {
            float k = (float)i / f;
            outp[i] = d[i] * k + d[n + i] * (1f - k);
        }
        return outp;
    }

    /// <summary>Тревога: сердцебиение и низкое биение двух близких частот, 4 с петля.</summary>
    private static float[] TensionLoop() {
        int n = Rate * 4;
        var d = new float[n];
        for (int i = 0; i < n; i++) {
            float s = i / (float)Rate;
            float ph = (s * 1.5f) % 1f;
            float beat = Mathf.Exp(-ph * 18f) + (ph > 0.2f ? 0.75f * Mathf.Exp(-(ph - 0.2f) * 18f) : 0f);
            float v = Mathf.Sin(TwoPi * 55f * s) * beat * 0.6f;
            v += (Mathf.Sin(TwoPi * 58f * s) + Mathf.Sin(TwoPi * 61.5f * s)) * 0.12f;
            v += Mathf.Sin(TwoPi * 1760f * s) * 0.012f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 6f * s));
            d[i] = v;
        }
        return d;
    }
}
}
