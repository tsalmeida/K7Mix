namespace K7Mix.Core;

/// <summary>
/// Physical sizes for a compact-cassette jewel-case J-card and the cassette
/// labels, at actual size. The cassette shell follows IEC 60094-1. Artwork in
/// K7 Mix and RunningWild is cropped to the same face proportion.
/// </summary>
public static class JewelCaseLayout
{
    public const double CassetteWidthMm = 100.4;
    public const double CassetteHeightMm = 63.8;
    public const double CassetteThicknessMm = 12.0;
    public const double FaceAspect = CassetteWidthMm / CassetteHeightMm;

    public const double CaseWidthMm = 109.5;
    public const double CaseHeightMm = 69.8;
    public const double CaseDepthMm = 16.5;

    public const double FrontWidthMm = 100.0;
    public const double FaceHeightMm = 64.0;
    public const double SpineWidthMm = 12.5;
    public const double BackWidthMm = 70.0;
    public const double FlapMm = 10.0;
    public const double BleedMm = 3.0;
    public const double SafeMm = 3.0;

    public const double LabelWidthMm = 89.0;
    public const double LabelHeightMm = 43.0;

    public static double CardWidthMm => FrontWidthMm + SpineWidthMm + BackWidthMm;
    public static double CardHeightMm => FaceHeightMm + FlapMm * 2;

    public static string MeasurementSummary =>
        $"""
        Cassette shell (IEC 60094-1): {CassetteWidthMm} × {CassetteHeightMm} × {CassetteThicknessMm} mm.
        Jewel case (Norelco): {CaseWidthMm} × {CaseHeightMm} × {CaseDepthMm} mm.
        J-card face height {FaceHeightMm} mm; front {FrontWidthMm} mm; spine {SpineWidthMm} mm; back {BackWidthMm} mm;
        top and bottom flaps {FlapMm} mm; bleed {BleedMm} mm; safe area {SafeMm} mm inside folds and trim.
        Cassette paper label: {LabelWidthMm} × {LabelHeightMm} mm, one for each side.
        Front and back artwork is cropped to {FaceAspect:0.000}:1, the cassette face proportion.
        The cassette sticker (the paper label on the shell) is not the jewel-case insert.
        """;
}
