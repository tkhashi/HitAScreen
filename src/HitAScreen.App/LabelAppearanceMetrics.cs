using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

internal static class LabelAppearanceMetrics
{
    public static LabelMetrics Calculate(LabelAppearanceSettings appearance, double labelScale)
    {
        var normalizedLabelScale = Math.Clamp(labelScale, 0.5, 3.0);
        var normalizedFont = Math.Clamp(appearance.FontSize, 8, 48) * normalizedLabelScale;
        var horizontalPadding = Math.Clamp(normalizedFont * 0.55, 4, 28);
        var verticalPadding = Math.Clamp(normalizedFont * 0.2, 2, 14);
        var minWidth = Math.Clamp(normalizedFont * 1.8, 14, 120);
        var minHeight = Math.Clamp(normalizedFont * 1.3, 14, 72);

        return new LabelMetrics(
            FontSize: Math.Max(6, normalizedFont),
            HorizontalPadding: horizontalPadding,
            VerticalPadding: verticalPadding,
            MinWidth: minWidth,
            MinHeight: minHeight);
    }
}

internal readonly record struct LabelMetrics(
    double FontSize,
    double HorizontalPadding,
    double VerticalPadding,
    double MinWidth,
    double MinHeight);
