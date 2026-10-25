using System;
using System.Collections.Generic;
using System.Reflection;
namespace PMHarness {
/// <summary>Минимальный тестовый раннер без внешних зависимостей.</summary>
public static class Check {
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new List<string>();
    public static int Passed => _passed;
    public static int Failed => _failed;
    public static IReadOnlyList<string> Failures => _failures;
    public static void True(bool cond, string msg) {
        if (cond) { _passed++; return; }
        _failed++;
        _failures.Add(msg);
        Console.WriteLine("  FAIL: " + msg);
    }
    public static void Eq(double expected, double actual, string msg, double eps = 1e-4) {
        True(Math.Abs(expected - actual) <= eps,
            $"{msg} (expected {expected:0.####}, got {actual:0.####})");
    }
    public static void Eq(int expected, int actual, string msg) =>
        True(expected == actual, $"{msg} (expected {expected}, got {actual})");
    public static void Eq(string expected, string actual, string msg) =>
        True(expected == actual, $"{msg} (expected '{expected}', got '{actual}')");
    public static void Eq(object expected, object actual, string msg) =>
        True(Equals(expected, actual), $"{msg} (expected {expected}, got {actual})");
    public static void Reset() { _passed = 0; _failed = 0; _failures.Clear(); }
}
public static class Runner {
    public static int Main() {
        Check.Reset();
        var types = typeof(Runner).Assembly.GetTypes();
        int suites = 0;
        foreach (var t in types) {
            if (!t.Name.EndsWith("Tests")) continue;
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
            bool hasTests = false;
            foreach (var m in methods)
                if (m.ReturnType == typeof(void) && m.GetParameters().Length == 0 &&
                    !m.IsSpecialName) hasTests = true;
            if (!hasTests) continue;
            suites++;
            Console.WriteLine($"[{t.Name}]");
            foreach (var m in methods) {
                if (m.ReturnType != typeof(void) || m.GetParameters().Length != 0 || m.IsSpecialName)
                    continue;
                try {
                    m.Invoke(null, null);
                } catch (Exception ex) {
                    Check.True(false, $"{t.Name}.{m.Name} threw: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }
        Console.WriteLine($"Suites: {suites}");
        Console.WriteLine($"PASSED {Check.Passed}/{Check.Passed + Check.Failed}");
        return Check.Failed == 0 ? 0 : 1;
    }
}
}
