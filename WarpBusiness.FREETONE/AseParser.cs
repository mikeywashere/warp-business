using System.Text;

namespace AseToolkit;

// ─────────────────────────────────────────────────────────────
//  AseParser — reads .ase (Adobe Swatch Exchange) binary files
// ─────────────────────────────────────────────────────────────
//
//  ASE Binary Format:
//  ┌──────────────────────────────────────────┐
//  │ Header                                   │
//  │   4 bytes  "ASEF"     (magic signature)  │
//  │   2 bytes  Major version (uint16 BE)     │
//  │   2 bytes  Minor version (uint16 BE)     │
//  │   4 bytes  Block count  (uint32 BE)      │
//  ├──────────────────────────────────────────┤
//  │ Block (repeated per color/group)         │
//  │   2 bytes  Block type (uint16 BE)        │
//  │            0x0001 = Color entry           │
//  │            0xC001 = Group start           │
//  │            0xC002 = Group end             │
//  │   4 bytes  Block length (uint32 BE)      │
//  │   2 bytes  Name length in chars (BE)     │
//  │   N bytes  Name (UTF-16 BE, null-term)   │
//  │   4 bytes  Color model ("RGB ", "CMYK",  │
//  │            "LAB ", "Gray")               │
//  │   N floats Color values (BE float32)     │
//  │            RGB  = 3 floats (0.0–1.0)     │
//  │            CMYK = 4 floats (0.0–1.0)     │
//  │            LAB  = 3 floats               │
//  │            Gray = 1 float  (0.0–1.0)     │
//  │   2 bytes  Color type                    │
//  │            0 = Global, 1 = Spot,         │
//  │            2 = Normal                    │
//  └──────────────────────────────────────────┘

public static class AseParser
{
    public static List<AseColor> Parse(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            filePath = "./FREETONE.ase";
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }


        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);
        var colors = new List<AseColor>();

        // ── Header ───────────────────────────────────────────
        var signature = new string(reader.ReadChars(4));
        if (signature != "ASEF")
            throw new InvalidDataException(
                $"Not a valid ASE file (expected 'ASEF', got '{signature}')");

        var versionMajor = ReadUInt16BE(reader);
        var versionMinor = ReadUInt16BE(reader);
        var blockCount = ReadUInt32BE(reader);
        var currentGroup = string.Empty;

        // ── Blocks ───────────────────────────────────────────
        for (uint i = 0; i < blockCount; i++)
        {
            var blockType = ReadUInt16BE(reader);
            var blockLength = (int)ReadUInt32BE(reader);
            var blockStart = stream.Position;

            switch (blockType)
            {
                case 0xC001: // Group start
                    currentGroup = ReadName(reader);
                    break;

                case 0xC002: // Group end
                    currentGroup = string.Empty;
                    break;

                case 0x0001: // Color entry
                    var name = ReadName(reader);
                    var model = new string(reader.ReadChars(4));

                    int floatCount = model switch
                    {
                        "RGB " => 3,
                        "CMYK" => 4,
                        "LAB " => 3,
                        "Gray" => 1,
                        _ => throw new NotSupportedException(
                                 $"Unsupported color model: '{model}'")
                    };

                    var values = new float[floatCount];
                    for (int j = 0; j < floatCount; j++)
                        values[j] = ReadFloatBE(reader);

                    var colorType = ReadUInt16BE(reader);

                    colors.Add(new AseColor
                    {
                        Name = name,
                        Group = currentGroup,
                        Model = model,
                        Values = values,
                        ColorType = colorType
                    });
                    break;
            }

            // Consume any remaining unread bytes in this block
            var consumed = (int)(stream.Position - blockStart);
            if (consumed < blockLength)
                reader.ReadBytes(blockLength - consumed);
        }

        return colors;
    }

    // ── Big-endian binary readers ────────────────────────────

    private static string ReadName(BinaryReader reader)
    {
        var nameLength = ReadUInt16BE(reader);
        var nameBytes = reader.ReadBytes(nameLength * 2);
        return Encoding.BigEndianUnicode
            .GetString(nameBytes)
            .TrimEnd('\0');
    }

    private static ushort ReadUInt16BE(BinaryReader r)
    {
        var bytes = r.ReadBytes(2);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    private static uint ReadUInt32BE(BinaryReader r)
    {
        var bytes = r.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static float ReadFloatBE(BinaryReader r)
    {
        var bytes = r.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }
}
