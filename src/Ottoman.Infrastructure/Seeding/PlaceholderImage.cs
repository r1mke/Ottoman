using System.IO.Compression;
using System.Text;

namespace Ottoman.Infrastructure.Seeding;

/// <summary>
/// Makes a plain but distinct picture (a gradient with a diagonal band, colour chosen from the name) so a fresh demo
/// database has images everywhere without shipping binary files. Pure and deterministic; no imaging library needed.
/// </summary>
public static class PlaceholderImage
{
    public const int Size = 480;

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Create(string label)
    {
        var hue = Hue(label);
        var top = Hsv(hue, 0.55, 0.88);
        var bottom = Hsv((hue + 28) % 360, 0.70, 0.46);
        var band = Hsv(hue, 0.25, 1.0);

        var raw = new byte[Size * (1 + Size * 3)];
        var i = 0;
        for (var y = 0; y < Size; y++)
        {
            raw[i++] = 0; // PNG filter: none
            for (var x = 0; x < Size; x++)
            {
                var t = y / (double)(Size - 1);
                var (r, g, b) = (Lerp(top.R, bottom.R, t), Lerp(top.G, bottom.G, t), Lerp(top.B, bottom.B, t));

                // A soft diagonal band across the picture.
                var distance = Math.Abs((x + y) - Size) / (double)Size;
                if (distance < 0.09)
                {
                    var strength = (1 - distance / 0.09) * 0.45;
                    r = Lerp(r, band.R, strength);
                    g = Lerp(g, band.G, strength);
                    b = Lerp(b, band.B, strength);
                }
                raw[i++] = (byte)r;
                raw[i++] = (byte)g;
                raw[i++] = (byte)b;
            }
        }
        return Encode(raw);
    }

    private static int Hue(string label)
    {
        var hash = 17;
        foreach (var c in label) hash = unchecked(hash * 31 + c);
        return Math.Abs(hash % 360);
    }

    private static int Lerp(int a, int b, double t) => (int)Math.Round(a + (b - a) * Math.Clamp(t, 0, 1));

    private static (int R, int G, int B) Hsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var h = hue / 60.0;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        var m = value - c;
        return ((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    private static byte[] Encode(byte[] rawRows)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteInt(header, 0, Size);
        WriteInt(header, 4, Size);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: RGB
        Chunk(output, "IHDR", header);

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(rawRows);
            Chunk(output, "IDAT", compressed.ToArray());
        }

        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);

        var typed = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(typed, 0);
        data.CopyTo(typed, 4);
        output.Write(typed);

        var crc = new byte[4];
        WriteInt(crc, 0, unchecked((int)Crc(typed)));
        output.Write(crc);
    }

    private static void WriteInt(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static uint Crc(byte[] bytes)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in bytes) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
