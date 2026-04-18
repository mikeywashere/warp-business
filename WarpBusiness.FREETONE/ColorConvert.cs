namespace AseToolkit;

// ─────────────────────────────────────────────────────────────
//  ColorConvert — model-to-RGB conversion pipelines
// ─────────────────────────────────────────────────────────────

public static class ColorConvert
{
    // ── RGB passthrough ──────────────────────────────────────
    // ASE stores RGB as 3 floats in [0.0, 1.0]
    public static (byte r, byte g, byte b) RgbFromRgb(float[] v)
    {
        return (ClampToByte(v[0] * 255f),
                ClampToByte(v[1] * 255f),
                ClampToByte(v[2] * 255f));
    }

    // ── CMYK → RGB ──────────────────────────────────────────
    // ASE stores CMYK as 4 floats in [0.0, 1.0]
    //   R = 255 × (1 - C) × (1 - K)
    //   G = 255 × (1 - M) × (1 - K)
    //   B = 255 × (1 - Y) × (1 - K)
    //
    // Note: This is a mathematical conversion. For print-exact
    // results you would need ICC profile-based conversion.
    public static (byte r, byte g, byte b) RgbFromCmyk(float[] v)
    {
        float c = v[0], m = v[1], y = v[2], k = v[3];

        float r = 255f * (1f - c) * (1f - k);
        float g = 255f * (1f - m) * (1f - k);
        float b = 255f * (1f - y) * (1f - k);

        return (ClampToByte(r), ClampToByte(g), ClampToByte(b));
    }

    // ── CIE L*a*b* → RGB ────────────────────────────────────
    // Pipeline: L*a*b* → XYZ (D50) → XYZ (D65) via Bradford → linear sRGB → sRGB
    // D50 illuminant: Xn=0.9642, Yn=1.0, Zn=0.8251
    public static (byte r, byte g, byte b) RgbFromLab(float[] v)
    {
        float L = v[0], a = v[1], b = v[2];

        // Step 1: L*a*b* → XYZ (D50)
        const float Xn = 0.9642f, Yn = 1.0f, Zn = 0.8251f;
        const float delta = 6f / 29f;
        const float delta2 = delta * delta;

        float fy = (L + 16f) / 116f;
        float fx = a / 500f + fy;
        float fz = fy - b / 200f;

        float x = (fx > delta ? fx * fx * fx : (fx - 16f / 116f) * 3f * delta2) * Xn;
        float y = (fy > delta ? fy * fy * fy : (fy - 16f / 116f) * 3f * delta2) * Yn;
        float z = (fz > delta ? fz * fz * fz : (fz - 16f / 116f) * 3f * delta2) * Zn;

        // Step 2: XYZ D50 → D65 (Bradford chromatic adaptation)
        float xD65 = 1.0479f * x + 0.0229f * y - 0.0502f * z;
        float yD65 = -0.0097f * x + 1.0004f * y + 0.0215f * z;
        float zD65 = 0.0092f * x - 0.0151f * y + 0.7519f * z;

        // Step 3: XYZ D65 → Linear sRGB
        float rl = 3.2404542f * xD65 - 1.5371385f * yD65 - 0.4985314f * zD65;
        float gl = -0.9692660f * xD65 + 1.8760108f * yD65 + 0.0415560f * zD65;
        float bl = 0.0556434f * xD65 - 0.2040259f * yD65 + 1.0572252f * zD65;

        // Step 4: Gamma correction (linear → sRGB)
        return (ClampToByte(SrgbGamma(rl) * 255f),
                ClampToByte(SrgbGamma(gl) * 255f),
                ClampToByte(SrgbGamma(bl) * 255f));
    }

    // ── Gray → RGB ───────────────────────────────────────────
    // 1 float [0.0, 1.0] → equal R=G=B
    public static (byte r, byte g, byte b) RgbFromGray(float[] v)
    {
        byte gray = ClampToByte(v[0] * 255f);
        return (gray, gray, gray);
    }

    // ── Helpers ──────────────────────────────────────────────

    public static byte ClampToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private static float SrgbGamma(float linear)
    {
        linear = Math.Clamp(linear, 0f, 1f);
        return linear <= 0.0031308f
            ? 12.92f * linear
            : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }
}
