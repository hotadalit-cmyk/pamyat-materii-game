using UnityEngine;
using UnityEditor;
using System.IO;

namespace PM.EditorTools {
/// <summary>Batch-сборка APK (шаг 8 чек-листа запуска).</summary>
public static class BuildScript {
    public static void BuildAndroidApk() {
        var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "build");
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "PamyatMaterii.apk");
        var scenes = EditorBuildSettings.scenes;
        // только 64-бит: ARMv7 удваивал нативный код (+~20 МБ), а все целевые телефоны — arm64
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, BuildOptions.None);
        // summary.totalSize — не размер APK (считает промежуточные данные), берём файл
        long size = File.Exists(output) ? new FileInfo(output).Length : 0;
        Debug.Log("BuildScript: " + report.summary.result + ", " + output + ", apk=" + size + " bytes");
    }
}
}
