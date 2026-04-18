namespace AseToolkit;

// ─────────────────────────────────────────────────────────────
//  AseColor — immutable record representing a single ASE swatch
// ─────────────────────────────────────────────────────────────

public record AseColor
{
    public string Name { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;   // "RGB ", "CMYK", "LAB ", "Gray"
    public float[] Values { get; init; } = Array.Empty<float>();
    public int ColorType { get; init; }                    // 0 = Global, 1 = Spot, 2 = Normal

    // ── Computed RGB properties ──────────────────────────────
    public byte R => ToRgb().r;
    public byte G => ToRgb().g;
    public byte B => ToRgb().b;
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    public (byte r, byte g, byte b) ToRgb() => Model switch
    {
        "RGB " => ColorConvert.RgbFromRgb(Values),
        "CMYK" => ColorConvert.RgbFromCmyk(Values),
        "LAB " => ColorConvert.RgbFromLab(Values),
        "Gray" => ColorConvert.RgbFromGray(Values),
        _ => throw new NotSupportedException($"Unknown color model: '{Model}'")
    };

    public override string ToString() => $"{Name} [{Model.Trim()}] {Hex}";
}
