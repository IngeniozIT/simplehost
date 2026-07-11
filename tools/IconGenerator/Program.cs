using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Converts a source PNG into a multi-resolution Windows .ico.
//
//   IconGenerator <input.png> <output.ico>
//
// Each icon frame is stored as a PNG blob (the "PNG-compressed" icon form Windows Vista+ and the
// System.Drawing loader both understand), so no BMP/AND-mask packing is needed. Only decoding,
// resizing and PNG re-encoding come from ImageSharp; the .ico container is written here by hand.

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: IconGenerator <input.png> <output.ico>");
    return 1;
}

string inputPng = args[0];
string outputIco = args[1];

if (!File.Exists(inputPng))
{
    Console.Error.WriteLine($"IconGenerator: source image not found: {inputPng}");
    return 1;
}

// Standard Windows icon sizes, small to large. 256 is stored with the width/height byte set to 0,
// which the .ico format reads as 256.
int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };

var frames = new List<byte[]>(sizes.Length);
using (Image<Rgba32> source = Image.Load<Rgba32>(inputPng))
{
    foreach (int size in sizes)
    {
        using Image<Rgba32> resized = source.Clone(ctx => ctx.Resize(size, size));
        using var buffer = new MemoryStream();
        resized.Save(buffer, new PngEncoder());
        frames.Add(buffer.ToArray());
    }
}

string? outputDir = Path.GetDirectoryName(Path.GetFullPath(outputIco));
if (!string.IsNullOrEmpty(outputDir))
{
    Directory.CreateDirectory(outputDir);
}

using (var fs = File.Create(outputIco))
using (var writer = new BinaryWriter(fs))
{
    // ICONDIR header.
    writer.Write((ushort)0);            // reserved, must be 0
    writer.Write((ushort)1);            // image type: 1 == icon
    writer.Write((ushort)sizes.Length); // number of images

    // ICONDIRENTRY table. Image data follows the whole table.
    int dataOffset = 6 + (16 * sizes.Length);
    for (int i = 0; i < sizes.Length; i++)
    {
        int size = sizes[i];
        byte dimension = (byte)(size >= 256 ? 0 : size);
        writer.Write(dimension);                  // width  (0 == 256)
        writer.Write(dimension);                  // height (0 == 256)
        writer.Write((byte)0);                    // palette size (0 == no palette)
        writer.Write((byte)0);                    // reserved
        writer.Write((ushort)1);                  // color planes
        writer.Write((ushort)32);                 // bits per pixel
        writer.Write((uint)frames[i].Length);     // size of the image data
        writer.Write((uint)dataOffset);           // offset of the image data
        dataOffset += frames[i].Length;
    }

    foreach (byte[] frame in frames)
    {
        writer.Write(frame);
    }
}

Console.WriteLine($"IconGenerator: wrote {outputIco} ({sizes.Length} sizes) from {inputPng}");
return 0;
