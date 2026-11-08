using System.IO.Compression;
using System.Text;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Tiny but complete pictures for upload tests: the server refuses files that only start like a picture.</summary>
public static class TestImages
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A valid 2x2 RGB PNG; different seeds give different bytes, the same seed the same bytes.</summary>
    public static byte[] Png(int seed)
    {
        var s = BitConverter.GetBytes(seed);
        // Two rows, each: filter byte 0 + two RGB pixels.
        byte[] raw = [0, s[0], s[1], s[2], s[3], 1, 2, 0, 3, 4, 5, 6, 7, 8];

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);

        using var png = new MemoryStream();
        png.Write(Signature);
        Chunk(png, "IHDR", [0, 0, 0, 2, 0, 0, 0, 2, 8, 2, 0, 0, 0]);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream to, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        to.Write(BigEndian((uint)data.Length));
        to.Write(typeBytes);
        to.Write(data);
        to.Write(BigEndian(Crc([.. typeBytes, .. data])));
    }

    private static byte[] BigEndian(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    private static uint Crc(byte[] bytes)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            c ^= b;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFFu;
    }
}
