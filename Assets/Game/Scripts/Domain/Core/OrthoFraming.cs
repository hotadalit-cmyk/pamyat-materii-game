namespace PM.Domain.Core {
/// <summary>
/// Взятие ортографической камеры (T01): гарантированно видно проектную ширину 17.78
/// и высоту 10 на любом аспекте; на широких экранах добавляем высоту.
/// Чистая математика — покрыта harness-тестами.
/// </summary>
public static class OrthoFraming {
    public const double DesignWidth = 17.78;  // 16:9 арена
    public const double DesignHeight = 10.0;
    /// <summary>Ортографический half-size (size = высота) для данного аспекта (w/h).</summary>
    public static double ComputeOrthoSize(double aspect,
                                          double designWidth = DesignWidth,
                                          double designHeight = DesignHeight) {
        if (aspect <= 0) aspect = 16.0 / 9.0;
        double byHeight = designHeight / 2.0;
        double byWidth = (designWidth / aspect) / 2.0;
        return MathUtil.Max(byHeight, byWidth);
    }
}
}
