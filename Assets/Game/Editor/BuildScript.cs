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
        var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, BuildOptions.None);
        Debug.Log("BuildScript: " + report.summary.result + ", " + output +
                  ", size=" + report.summary.totalSize + " bytes");
    }
}
}
