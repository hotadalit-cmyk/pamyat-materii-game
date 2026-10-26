using System.IO;
using UnityEditor;
using UnityEngine;

namespace PM.EditorTools {
/// <summary>
/// Импорт сторонних моделей (Assets/ThirdParty/Resources/Monsters): анимации — Legacy (играются по имени
/// без контроллеров), все клипы зациклены; для моделей с внешней текстурой — материал Standard.
/// </summary>
public sealed class ThirdPartyModelImport : AssetPostprocessor {
    private const string MonsterDir = "Assets/ThirdParty/Resources/Monsters";

    void OnPreprocessModel() {
        if (!assetPath.Replace('\\', '/').StartsWith(MonsterDir)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Legacy;
        mi.importAnimation = true;
        mi.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations;
        mi.globalScale = 1f;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    void OnPostprocessModel(GameObject root) {
        if (!assetPath.Replace('\\', '/').StartsWith(MonsterDir)) return;
        var anim = root.GetComponent<Animation>();
        if (anim == null) return;
        foreach (AnimationState st in anim) st.clip.wrapMode = WrapMode.Loop;
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
            string texPath = dir + "/" + name + "_Texture.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null) {
                string matPath = dir + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, matPath); }
                mat.mainTexture = tex;
                mat.SetFloat("_Glossiness", 0.15f);
                EditorUtility.SetDirty(mat);
            }
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            report.Append(name).Append(" size=").Append(bounds.size.ToString("F2"));
            report.Append(" mats=").Append(go.GetComponentInChildren<Renderer>() != null ? go.GetComponentInChildren<Renderer>().sharedMaterials.Length : 0);
            report.Append(" clips:");
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) report.Append(' ').Append(c.name);
            report.Append('\n');
        }
        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString());
    }
}
}
