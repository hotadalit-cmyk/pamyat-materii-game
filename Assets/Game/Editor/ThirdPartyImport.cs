using System.IO;
using UnityEditor;
using UnityEngine;

namespace PM.EditorTools {
/// <summary>
/// Импорт сторонних моделей (Assets/ThirdParty/Resources/Monsters): анимация Generic (Legacy на устройстве
/// «схлопывал» скелет из Blender), ходьба/полёт/покой зациклены; для моделей с внешней текстурой — материал Standard.
/// </summary>
public sealed class ThirdPartyModelImport : AssetPostprocessor {
    private const string MonsterDir = "Assets/ThirdParty/Resources/Monsters";

    private bool IsMonster => assetPath.Replace('\\', '/').StartsWith(MonsterDir);

    void OnPreprocessModel() {
        if (!IsMonster) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.importAnimation = true;
        mi.globalScale = 1f;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    void OnPreprocessAnimation() {
        if (!IsMonster) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++) {
            string n = clips[i].name;
            // ходьба/полёт/покой — по кругу; удары и смерть — один раз
            clips[i].loopTime = n.Contains("Walk") || n.Contains("Flying") || n.Contains("Idle") || n.Contains("Running") || n.Contains("Dance");
        }
        mi.clipAnimations = clips;
    }
}

public static class ThirdPartyImport {
    [MenuItem("PM/Setup/06 Import Third-Party Assets", priority = 6)]
    public static void Import() {
        AssetDatabase.ImportAsset("Assets/ThirdParty", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        string dir = "Assets/ThirdParty/Resources/Monsters";
        Directory.CreateDirectory(dir + "/Materials");
        var report = new System.Text.StringBuilder("ThirdParty monsters:\n");
        foreach (var path in Directory.GetFiles(dir, "*.fbx")) {
            string p = path.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(p);
            // текстура рядом с моделью → свой материал (Standard), чтобы модель не была белой
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_Texture.png");
            if (tex != null) {
                string matPath = dir + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, matPath); }
                mat.mainTexture = tex;
                mat.SetFloat("_Glossiness", 0.15f);
                EditorUtility.SetDirty(mat);
            }
            report.Append(name).Append(" clips:");
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) report.Append(' ').Append(c.name).Append(c.isLooping ? "(loop)" : "");
            report.Append('\n');
        }
        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString());
    }
}
}
