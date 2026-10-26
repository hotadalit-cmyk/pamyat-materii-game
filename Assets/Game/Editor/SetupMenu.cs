using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PM.Systems.Bootstrap;
using PM.Systems.Sim;
using PM.Systems.Input;
using PM.Systems.CameraRig;
using PM.Systems.UI;

namespace PM.EditorTools {
/// <summary>
/// PM/Setup: автоматизация подготовки проекта (T00/T01). Сцена генерируется кодом —
/// никакого ручного YAML.
/// </summary>
public static class SetupMenu {
    [MenuItem("PM/Setup/01 Create Boot Scene", priority = 1)]
    public static void CreateBootScene() {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // камера
        var camGo = new GameObject("main_camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = GameBootstrap.CBackground;
        camGo.AddComponent<FollowCamera>();
        // событийная система для UI
        var esGo = new GameObject("event_system");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
        // бутстрап (создаст симуляцию, арену, сущности, системы)
        var bootGo = new GameObject("game_bootstrap");
        bootGo.AddComponent<GameBootstrap>();
        // HUD (канва + элементы кодом)
        HudBuilder.BuildIfMissing(bootGo.transform);
        // сохранение сцены
        System.IO.Directory.CreateDirectory("Assets/Game/Scenes");
        const string path = "Assets/Game/Scenes/Boot.unity";
        EditorSceneManager.SaveScene(scene, path);
        // в билд
        // идемпотентно: повторный запуск не плодит дубли Boot.unity
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("PM/Setup 01: Boot-сцена создана и добавлена в Build Settings: " + path);
    }

    /// <summary>
    /// v0.4: сцена забега (Docs/CONCEPT_v4.md). Содержит только камеру и RunGame — всё остальное
    /// строится в рантайме. Ставится первой в Build Settings; прототип Boot.unity остаётся вторым.
    /// </summary>
    [MenuItem("PM/Setup/05 Create Run Scene (v0.4)", priority = 5)]
    public static void CreateRunScene() {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("main_camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<FollowCamera>();
        var esGo = new GameObject("event_system");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
        new GameObject("run_game").AddComponent<PM.Systems.Run.RunGame>();
        System.IO.Directory.CreateDirectory("Assets/Game/Scenes");
        const string path = "Assets/Game/Scenes/Run.unity";
        EditorSceneManager.SaveScene(scene, path);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("PM/Setup 05: сцена забега создана и стоит первой в Build Settings: " + path);
    }

    [MenuItem("PM/Setup/02 Apply Android Settings", priority = 2)]
    public static void ApplyAndroidSettings() {
        // D-02/D-18: Android, landscape, IL2CPP, Linear, пакет com.pamyatmaterii.game
        PlayerSettings.companyName = "PamyatMaterii";
        PlayerSettings.productName = "Память Материи";
        var android = UnityEditor.Build.NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, "com.pamyatmaterii.game");
        // Unity 6.3: минимально поддерживаемый уровень — API 25 (Android 7.1)
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        Application.targetFrameRate = 60;
        PlayerSettings.bundleVersion = "0.3.1";
        AssetDatabase.SaveAssets();
        Debug.Log("PM/Setup 02: Android-настройки применены (IL2CPP, ARM64, minSdk 25, Linear, landscape, v0.3.1).");
    }

    [MenuItem("PM/Setup/03 Verify Project Wiring", priority = 3)]
    public static void VerifyWiring() {
        var boot = Object.FindFirstObjectByType<GameBootstrap>();
        if (boot == null) { Debug.LogWarning("GameBootstrap не найден в сцене. Запустите 01."); return; }
        var cam = Camera.main;
        bool camOk = cam != null && cam.GetComponent<FollowCamera>() != null;
        Debug.Log(string.Format(
            "PM/Setup 03: bootstrap={0}, camera+rig={1}, hud={2}. Проверьте Game View 16:9/20:9 (PM/Dev).",
            boot != null, camOk, Object.FindFirstObjectByType<HudPresenter>() != null));
    }

    [MenuItem("PM/Setup/04 Build APK", priority = 4)]
    public static void BuildApk() {
        Debug.Log("PM/Setup 04: сборка APK (build/PamyatMaterii.apk)...");
        BuildScript.BuildAndroidApk();
    }
}
}
