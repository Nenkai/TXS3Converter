using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;

using Syroot.BinaryData.Core;
using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

using Pfim;
using Pfim.dds;

namespace GTTools.Formats
{
    public class TXS3
    {
        public const string MAGIC = "TXS3";
        public const string MAGIC_LE = "3SXT";

        public string OriginalFilePath { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int PitchOrLinearSize;
        public int Mipmap;
        public uint Stride { get; set; }

        public TXS3Flags Flags;

        public ImageFormat Format;
        private byte[] _ddsData;

        public static TXS3 ParseFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("File does not exist");

            byte[] file = File.ReadAllBytes(path);

            // Every format is read as BE.
            var sr = new SpanReader(file);

            string magic = sr.ReadStringRaw(4);
            if (sr.Length < 4)
                throw new InvalidDataException("File is too small to be a TXS3 image.");

            /*
             * magic != MAGIC_LE: false
             * !magic.Equals(MAGIC_LE, StringComparison.Ordinal): false
             * magic: "3SXT"
             * MAGIC_LE: "3SXT"
             * 
             * Happens in Release, THE FUCK?
            */

            if (!magic.Equals(MAGIC) && !magic.Equals(MAGIC_LE))
                throw new InvalidDataException("Not a valid TXS3 image file.");

            sr.Endian = magic == MAGIC_LE ? Endian.Little : Endian.Big;

            var endOffset = sr.ReadInt32(); 
            if (endOffset > sr.Length)
                Console.WriteLine($"Warning: Provided file is above bundled size length ({endOffset} > {sr.Length}). Might break.");
                

            var tex = new TXS3();

            sr.Position += 4; // Original Position, if bundled

            uint originalFilePathOffset = sr.ReadUInt32();
            if (originalFilePathOffset != 0 && sr.Length - originalFilePathOffset >= 3)
            {
                sr.Position = (int)originalFilePathOffset + 2; // Two empty bytes
                tex.OriginalFilePath = sr.ReadString1();
                if (!string.IsNullOrEmpty(tex.OriginalFilePath))
                    Console.WriteLine($"Original file path found: {tex.OriginalFilePath}");
                sr.Position = 0x10;

            }

            sr.Position += 4; // Nothing
            // ushort unk always 1
            sr.ReadUInt16(); // Image Count, expect 1
            sr.ReadUInt16(); // Unk, expect 1
            int imageDataOffset = sr.ReadInt32(); // Offset for the image data
            sr.ReadUInt32(); // Also an offset

            sr.Position = imageDataOffset;
            // 00 00 1A 00 always
            // 00 00 00 00 00 00 01
            // Byte, image format again
            // Byte, always 2A?
            // 00 03 03 03 always
            // 80 07 (32775) always
            // 80 00 00 00 always
            // AA E4 02 02 20 00 always
            // ushort Image Width
            // ushort Image Height
            // 00 00 00 00 00 00
            // 18 40 00 10
            // ushort flags?
            // Empty until 0x86
            // ushort always 1

            sr.Position = 0x6A;
            tex.Stride = sr.ReadUInt16();

            sr.Position = 0x88;
            var imageSize = sr.ReadInt32();
            sr.Position++; // Always 2?
            var type = sr.ReadByte();

            if (type == 0x85)
                tex.Format = ImageFormat.DXT10_MORTON;
            else if (type == 0x86 || type == 0xA6)
                tex.Format = ImageFormat.DXT1;
            else if (type == 0x87 || type == 0xA7)
                tex.Format = ImageFormat.DXT3;
            else if (type == 0x88 || type == 0xA8)
                tex.Format = ImageFormat.DXT5;
            else if (type == 0xA5)
                tex.Format = ImageFormat.DXT10;

            tex.Mipmap = sr.ReadByte() - 1;
            sr.Position++;
            tex.Width = sr.ReadInt16();
            tex.Height = sr.ReadInt16();

            // Image data starts at 0x100
            sr.Position = 0x100;
            tex._ddsData = tex.CreateDDSData(sr.ReadBytes(imageSize));

            return tex;
        }

        public static bool ToTXS3File(string path, ImageFormat imageFormat)
        {
            IImageFormat i = Image.DetectFormat(path);
            if (i is null)
            {
                Console.WriteLine($"This file is not a regular image file. {path}");
                return false;
            }

            ToDDS(path, imageFormat);

            string ddsFileName = Path.ChangeExtension(path, ".dds");
            if (!File.Exists(ddsFileName))
            {
                Console.WriteLine($"Failed to convert {path} to DDS during the image to TXS process.");
                return false;
            }


            var dds = Pfim.Pfim.FromFile(ddsFileName);
            byte[] data = File.ReadAllBytes(ddsFileName).AsSpan(0x80).ToArray();
            File.Delete(ddsFileName);

            using var ms = new FileStream(Path.ChangeExtension(path, ".img"), FileMode.Create);
            using var bs = new BinaryStream(ms, ByteConverter.Big);

            bs.WriteString(MAGIC, StringCoding.Raw);

            bs.Position = 0x100;
            bs.Write(data);

            bs.BaseStream.Position = 0x04;
            bs.WriteUInt32((uint)ms.Length);

            bs.BaseStream.Position = 0x0C;
            bs.WriteUInt32((uint)ms.Length - 1);

            bs.Position = 0x14;
            bs.WriteUInt16(1);
            bs.WriteUInt16(1);
            bs.WriteUInt32(0x40);
            bs.WriteUInt32(0x84);

            bs.Position = 0x40;
            bs.WriteUInt32(6656);
            bs.Position += 4;
            bs.WriteUInt16(1);

            if (imageFormat == ImageFormat.DXT1)
                bs.WriteByte(0xA6);
            else if (imageFormat == ImageFormat.DXT3)
                bs.WriteByte(0xA7);
            else if (imageFormat == ImageFormat.DXT5)
                bs.WriteByte(0xA8);
            bs.WriteByte(0x2A);
            bs.WriteByte(0);
            bs.WriteByte(3);
            bs.WriteByte(3);
            bs.WriteByte(3);
            bs.WriteBytes(new byte[] { 0x80, 0x07, 0x80, 0x00 });
            bs.WriteUInt32(43748);
            bs.WriteBytes(new byte[] { 0x02, 0x02, 0x20, 0x00 });
            bs.WriteUInt16((ushort)dds.Width);
            bs.WriteUInt16((ushort)dds.Height);
            bs.Position += 6;
            bs.WriteBytes(new byte[] { 0x18, 0x40, 0x00, 0x10 });

            // Write Stride, width * bytes per pixel
            if (imageFormat == ImageFormat.DXT1)
                bs.WriteUInt16((ushort)(dds.Width * 2u));
            else
                bs.WriteUInt16((ushort)(dds.Width * 4u));

            bs.ByteConverter = ByteConverter.Little;
            bs.Position = 0x86;
            bs.WriteUInt16(0x01);
            bs.ByteConverter = ByteConverter.Big;
            bs.WriteInt32(data.Length);
            bs.WriteByte(2);
            if (imageFormat == ImageFormat.DXT1)
                bs.WriteByte(0xA6);
            else if (imageFormat == ImageFormat.DXT3)
                bs.WriteByte(0xA7);
            else if (imageFormat == ImageFormat.DXT5)
                bs.WriteByte(0xA8);
            else if (imageFormat == ImageFormat.DXT10)
                bs.WriteByte(0xA5);

            bs.WriteByte((byte)(dds.MipMaps.Length + 1));
            bs.WriteByte(2);
            bs.WriteUInt16((ushort)dds.Width);
            bs.WriteUInt16((ushort)dds.Height);
            bs.WriteUInt16(1);

            return true;
        }

        private static void ToDDS(string fileName, ImageFormat imgFormat)
        {
            string arguments = fileName;
            if (imgFormat == ImageFormat.DXT1)
                arguments += " -f DXT1";
            else if (imgFormat == ImageFormat.DXT3)
                arguments += " -f DXT3";
            else if (imgFormat == ImageFormat.DXT5)
                arguments += " -f DXT5";
            else if (imgFormat == ImageFormat.DXT10)
                arguments += " -f R8G8B8A8_UNORM -dx10";

            arguments += " -y"      // Overwrite if it exists
                      + " -m 1"     // Don't care about extra mipmaps
                      + " -nologo"  // No copyright logo
                      + " -srgb";   // Auto correct gamma

            Process converter = Process.Start("texconv.exe", arguments);
            converter.WaitForExit();
        }

        public static List<TXS3> FromStream(ref SpanReader sr)
        {
            if (sr.ReadStringRaw(4) != MAGIC)
                throw new InvalidDataException("Could not parse TXS3 from stream, not a valid TXS3 image file.");

            List<TXS3> textures = new List<TXS3>();

            sr.Position += 4; // No size, it's bundled

            int basePos = sr.ReadInt32(); // Original Position, if bundled
            sr.Position += 4; // Real Size, not present here
            sr.Position += 4; // Nothing
            uint imageCount = sr.ReadUInt32(); // Bundled txs in files can have multiple images inside
            sr.Position += 4;
            uint listEntryOffsetBegin = sr.ReadUInt32();

            for (int i = 0; i < imageCount; i++)
            {
                TXS3 tex = new TXS3();
                sr.Position = (int)listEntryOffsetBegin + (i * 32);
                int imageOffset = sr.ReadInt32();

                int imageSize = sr.ReadInt32();
                sr.ReadByte();
                byte imageType = sr.ReadByte();

                if (imageType == 0x85)
                    tex.Format = ImageFormat.DXT10_MORTON;
                else if (imageType == 0x86 || imageType == 0xA6)
                    tex.Format = ImageFormat.DXT1;
                else if (imageType == 0x87 || imageType == 0xA7)
                    tex.Format = ImageFormat.DXT3;
                else if (imageType == 0x88 || imageType == 0xA8)
                    tex.Format = ImageFormat.DXT5;
                else if (imageType == 0xA5)
                    tex.Format = ImageFormat.DXT10;
                Console.WriteLine(imageType);

                tex.Mipmap = sr.ReadByte() - 1;
                sr.ReadByte(); // 1
                tex.Width = sr.ReadUInt16();
                tex.Height = sr.ReadUInt16();

                // Got header, proceed to parse the image
                sr.Position = imageOffset;
                tex._ddsData = tex.CreateDDSData(sr.ReadBytes(imageSize));

                textures.Add(tex);
            }

            sr.Position = basePos;
            return textures;
        }

        public void SaveAsPng(string path)
        {
            using var ms = new MemoryStream(_ddsData);
            var dds = Pfim.Pfim.FromStream(ms);
            var encoder = new PngEncoder();

            if (dds.Format == Pfim.ImageFormat.Rgb24)
                Save<Bgr24>(dds, path);
            else if (dds.Format == Pfim.ImageFormat.Rgba32)
                Save<Bgra32>(dds, path);
            else
            {
                Console.WriteLine($"Invalid format to save..? {dds.Format}");
                return;
            }
        }

        private void Save<T>(Pfim.IImage dds, string path) where T : struct, IPixel<T>
        {
            using var i = Image.LoadPixelData<T>(dds.Data, dds.Width, dds.Height);
            /*
            if (this.Flags == 0)
            {
                i.Mutate(p =>
                {
                    p.Flip(FlipMode.Vertical);
                });
            }
            */
            i.Save(path);
        }


        private byte[] CreateDDSData(byte[] imageData)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            try
            {
                // https://gist.github.com/Scobalula/d9474f3fcf3d5a2ca596fceb64e16c98#file-directxtexutil-cs-L355
                bw.Write(new char[] { 'D', 'D', 'S', ' ' });
                bw.Write(124);    // dwSize (Struct Size)
                bw.Write((uint)(DDSHeaderFlags.TEXTURE)); // dwFlags
                bw.Write(Height); // dwHeight

                // Dirty fix, some TXS3's in GTHD have 1920 as width, but its actually 2048. Stride is correct, so use it instead.
                int width;
                if (Format == ImageFormat.DXT1)
                    width = (int)Stride / 2;  // dwWidth
                else
                    width = (int)Stride / 4;  // dwWidth
                bw.Write(width);

                switch (Format)   // dwPitchOrLinearSize
                {
                    case ImageFormat.DXT1:
                        bw.Write(Height * width / 2);
                        break;
                    case ImageFormat.DXT3:
                    case ImageFormat.DXT5:
                        bw.Write(Height * width);
                        break;
                    default:
                        bw.Write((width * 32 + 7) / 8);
                        break;
                }

                bw.Write(0);    // Depth
                bw.Write(Mipmap);
                bw.Write(new byte[44]); // reserved
                bw.Write(32); // DDSPixelFormat Header starts here - Struct Size

                switch (Format)
                {
                    case ImageFormat.DXT1:
                    case ImageFormat.DXT3:
                    case ImageFormat.DXT5:
                        bw.Write((uint)DDSPixelFormatFlags.DDPF_FOURCC); // Format Flags
                        bw.Write(Format.ToString().ToCharArray()); // FourCC
                        bw.Write(0); // RGBBitCount
                        bw.Write(0); // RBitMask
                        bw.Write(0); // GBitMask
                        bw.Write(0); // BBitMask
                        bw.Write(0); // ABitMask
                        break;
                    case ImageFormat.DXT10_MORTON:
                    case ImageFormat.DXT10:
                        bw.Write((uint)(DDSPixelFormatFlags.DDPF_FOURCC));           // Format Flags
                        bw.Write("DX10".ToCharArray());            // FourCC
                        bw.Write(0);         // RGBBitCount
                        bw.Write(0);  // RBitMask
                        bw.Write(0);  // GBitMask
                        bw.Write(0);  // BBitMask
                        bw.Write(0);  // ABitMask
                        break;
                }

                bw.Write(0x1000); // dwCaps, 0x1000 = required
                bw.Write(new byte[16]); // dwCaps1-4

                if (Format == ImageFormat.DXT10_MORTON || Format == ImageFormat.DXT10)
                {
                    // DDS_HEADER_DXT10
                    bw.Write(87); // DXGI_FORMAT_B8G8R8A8_UNORM
                    bw.Write(3);  // DDS_DIMENSION_TEXTURE2D
                    bw.BaseStream.Seek(4, SeekOrigin.Current);  // miscFlag
                    bw.Write(1); // arraySize
                    bw.Write(0); // miscFlags2

                    if (Format == ImageFormat.DXT10_MORTON)
                    {
                        int bytesPerPix = 4;
                        int byteCount = (width * Height) * 4;
                        byte[] newImageData = new byte[byteCount];

                        SpanReader sr = new SpanReader(imageData);
                        SpanWriter sw = new SpanWriter(newImageData);
                        Span<byte> pixBuffer = new byte[4];
                        for (int i = 0; i < width * Height; i++)
                        {
                            int pixIndex = MortonReorder(i, width, Height);
                            pixBuffer = sr.ReadBytes(4);
                            int destIndex = bytesPerPix * pixIndex;
                            sw.Position = destIndex;
                            sw.WriteBytes(pixBuffer);
                        }

                        imageData = newImageData;
                    }
                }

                bw.Write(imageData);
            }
            catch (Exception e)
            {
                return null;
            }

            return ms.ToArray();
        }


        private static int MortonReorder(int i, int width, int height)
        {
            int x = 1;
            int y = 1;

            int w = width;
            int h = height;

            int index = 0;
            int index2 = 0;

            while (w > 1 || h > 1)
            {
                if (w > 1)
                {
                    index += x * (i & 1);
                    i >>= 1;
                    x *= 2;
                    w >>= 1;
                }
                if (h > 1)
                {
                    index2 += y * (i & 1);
                    i >>= 1;
                    y *= 2;
                    h >>= 1;
                }
            }
            return index2 * width + index;
        }

        public enum ImageFormat
        {
            DXT1,
            DXT3,
            DXT5,

            // RGBA Pretty Much
            DXT10,
            DXT10_MORTON,
        }

        [Flags]
        public enum TXS3Flags
        {
            Unflip = 0x50,
        }

        /// <summary>
        /// DDS Header Flags
        /// </summary>
        [Flags]
        private enum DDSHeaderFlags : uint
        {
            TEXTURE = 0x00001007,  // DDSDCAPS | DDSDHEIGHT | DDSDWIDTH | DDSDPIXELFORMAT 
            MIPMAP = 0x00020000,  // DDSDMIPMAPCOUNT
            VOLUME = 0x00800000,  // DDSDDEPTH
            PITCH = 0x00000008,  // DDSDPITCH
            LINEARSIZE = 0x00080000,  // DDSDLINEARSIZE
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/direct3ddds/dds-pixelformat
        /// </summary>
        [Flags]
        private enum DDSPixelFormatFlags
        {
            DDPF_ALPHAPIXELS = 0x01,
            DDPF_ALPHA = 0x02,
            DDPF_FOURCC = 0x04,
            DDPF_RGB = 0x40,
            DDPF_YUV = 0x200,
            DDPF_LUMINANCE = 0x20000
        }


    }
}
