using UnityEngine;
using UnityEditor;

namespace PM.EditorTools {
/// <summary>PM/Dev: переключение Game View между 16:9 и 20:9 (T01-проверка).</summary>
public static class DevMenu {
    private const string Aspect16x9 = "16:9";
    private const string Aspect20x9 = "20:9";

    [MenuItem("PM/Dev/Game View 16:9", priority = 10)]
    public static void Set16x9() => SetAspect(16, 9, Aspect16x9);

    [MenuItem("PM/Dev/Game View 20:9", priority = 11)]
    public static void Set20x9() => SetAspect(20, 9, Aspect20x9);

    private static void SetAspect(int w, int h, string label) {
        //Рефлексия-подход: внутренний API GameViewSizes менялся между версиями Unity,
        //при неудаче даём понятную ручную инструкцию (T01-возврат).
        try {
            var assembly = typeof(UnityEditor.EditorWindow).Assembly;
            var type = assembly.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(type);
            var sizesProp = type.GetProperty("customCount",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _ = sizesProp;
            var method = type.GetMethod("SizeSelectionCallback",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null) {
                method.Invoke(gv, new object[] { w, h });
                Debug.Log($"PM/Dev: Game View → {label}");
                return;
            }
            throw new System.MissingMethodException("SizeSelectionCallback");
        } catch (System.Exception e) {
            Debug.LogWarning(
                $"PM/Dev: автоматическое переключение не сработало ({e.Message}).\n" +
                "Ручной путь: Game View → выпадающий аспект → + → добавить " + label +
                " (например 1600×900 и 1600×720) → выбрать.");
        }
    }
}
}
