using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Pfim;
namespace TXS3Converter
{
    public class TXS3
    {
        public const string MAGIC = "TXS3";
        public int width;
        public int height;
        public int pitchOrLinearSize;
        public int mipmap;
        public ImageFormat format;
        private byte[] imgData;

        private byte[] _ddsData;

        public static TXS3 FromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("File does not exist");

            using (FileStream fs = File.Open(path, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 4 || Encoding.ASCII.GetString(br.ReadBytes(4)) != MAGIC)
                    throw new InvalidDataException("Not a valid TXS3 image file.");

                var size = br.ReadBEInt32();
                if (size != fs.Length)
                    Console.WriteLine($"Warning: Hardcoded file size does not match actual file size ({size} != {fs.Length}). Might break.");

                br.BaseStream.Seek(4, SeekOrigin.Current); // ?
                br.BaseStream.Seek(4, SeekOrigin.Current); // Real Size

                br.BaseStream.Position = 0x88;
                var imageSize = br.ReadBEInt32();
                br.BaseStream.Position++;
                var type = br.ReadByte();

                var tex = new TXS3();

                if (type == 168)
                    tex.format = ImageFormat.DXT5;
                else if (type == 166)
                    tex.format = ImageFormat.DXT1;
                else if (type == 133)
                    tex.format = ImageFormat.Norm;

                tex.mipmap = br.ReadByte() - 1;
                br.BaseStream.Position++;
                tex.width = br.ReadBEInt16();
                tex.height = br.ReadBEInt16();

                // Skip to 0x0100 as the rest is void
                br.BaseStream.Position = 0x100;
                tex.imgData = br.ReadBytes(imageSize);
                tex._ddsData = tex.CreateDDSData();

                return tex;
            }
        }

        public void SaveAsPng(string path)
        {
            var dds = Dds.Create(_ddsData, new PfimConfig());

            if (dds.Format == Pfim.ImageFormat.Rgb24)
                Save<Rgb24>(dds, path);
            else if (dds.Format == Pfim.ImageFormat.Rgba32)
                Save<Rgba32>(dds, path);
            else
            {
                Console.WriteLine($"Invalid format to save..? {dds.Format}");
            }
        }

        private void Save<T>(Dds dds, string path) where T : struct, IPixel<T>
        {
            using (var i = Image.LoadPixelData<T>(dds.Data.Reverse().ToArray(), dds.Width, dds.Height))
            {
                i.Mutate(p => p.Flip(FlipMode.Horizontal));
                i.Save(path);
            }

        }

        public enum ImageFormat
        {
            DXT1,
            Norm,
            DXT5
        }

        private byte[] CreateDDSData()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                try
                {
                    bw.Write(new char[] { 'D', 'D', 'S', ' ' });
                    bw.Write(124);
                    bw.Write(0x1007); // Flags
                    bw.Write(height);
                    bw.Write(width);

                    switch (format)
                    {
                        case ImageFormat.DXT1:
                            bw.Write(height * width / 2);
                            break;
                        case ImageFormat.DXT5:
                            bw.Write(height * width);
                            break;
                        default:
                            bw.Write(0);
                            break;
                    }

                    bw.Write(0);
                    bw.Write(mipmap);
                    bw.Write(new byte[44]); // reserved
                    bw.Write(32); // Struct size

                    switch (format)
                    {
                        case ImageFormat.DXT1:
                        case ImageFormat.DXT5:
                            bw.Write(4);
                            bw.Write(format.ToString().ToCharArray()); // FourCC
                            bw.Write(0); // RGBBitCount
                            bw.Write(0);
                            bw.Write(0);
                            bw.Write(0);
                            bw.Write(0);
                            break;
                        default:
                            bw.Write(65);
                            bw.Write(0);
                            bw.Write(0x20);
                            bw.Write(0x0000FF00u);
                            bw.Write(0x00FF0000u);
                            bw.Write(0xFF000000u);
                            bw.Write(0x000000FFu);
                            break;
                    }

                    bw.Write(0x1000); // dwCaps, 0x1000 = required
                    bw.Write(0); // dwCaps2
                    bw.Write(new byte[12]);
                    bw.Write(imgData);

                    bw.BaseStream.Position = 0;

                }
                catch (Exception e)
                {
                    return null;
                }
                return ms.ToArray();
            }
        }
    }
}
