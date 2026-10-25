using System.Collections.Generic;
using UnityEngine;
namespace PM.Systems.Gameplay {
/// <summary>
/// Процедурные звуки без ассетов (T19-минимум; DEMO_SCOPE п.10 «настройки звука»):
/// короткие синтезированные сигналы на ключевые события. Громкость — AudioListener,
/// сохраняется в PlayerPrefs и меняется из паузы (кнопки «Звук − / +»).
/// </summary>
public sealed class ProceduralSfx : MonoBehaviour {
    public static ProceduralSfx Instance { get; private set; }
    public const string PrefKey = "pm_volume";
    private const int Rate = 22050;
    private AudioSource _src;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

    void Awake() {
        Instance = this;
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        Build("jet",     0.16f, t => Mathf.Lerp(260f, 130f, t));
        Build("eat",     0.06f, t => 640f * (1f - t * 0.3f));
        Build("weaken",  0.12f, t => 190f * (1f - t * 0.4f));
        Build("capture", 0.25f, t => Mathf.Lerp(330f, 392f, t));
        Build("unlock",  0.30f, t => t < 1f / 3f ? 523f : (t < 2f / 3f ? 659f : 784f));
        Build("hit",     0.09f, t => 120f * (1f - t));
        Build("death",   0.50f, t => Mathf.Lerp(220f, 110f, t));
        Build("assist",  0.15f, t => Mathf.Lerp(440f, 554f, t));
        AudioListener.volume = CurrentVolume();
    }

    private void Build(string key, float dur, System.Func<float, float> freq) {
        int n = Mathf.Max(1, (int)(Rate * dur));
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n;
            phase += 2f * Mathf.PI * freq(t) / Rate;
            float env = 1f - t; // линейное затухание
            data[i] = Mathf.Sin(phase) * env * 0.55f;
        }
        var clip = AudioClip.Create("sfx_" + key, n, 1, Rate, false);
        clip.SetData(data, 0);
        _clips[key] = clip;
    }

    public void Play(string key) {
        if (_clips.TryGetValue(key, out var c) && _src != null) _src.PlayOneShot(c);
    }

    public static float CurrentVolume() => PlayerPrefs.GetFloat(PrefKey, 0.8f);

    public static void ChangeVolume(float delta) {
        float v = Mathf.Clamp(CurrentVolume() + delta, 0f, 1f);
        PlayerPrefs.SetFloat(PrefKey, v);
        PlayerPrefs.Save();
        AudioListener.volume = v;
    }
}
}
