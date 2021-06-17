using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Buffers.Binary;

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
    public class TextureSet3
    {
        public const string MAGIC = "TXS3";
        public const string MAGIC_LE = "3SXT";

        public List<Texture> Textures { get; set; } = new();

        public bool WriteNames { get; set; }

        public bool LittleEndian { get; set; }

        public TextureSet3()
        {

        }

        public bool AddFromStandardImage(string path, TXS3ImageFormat txs3Format)
        {
            IImageFormat i = Image.DetectFormat(path);
            if (i is null)
            {
                Console.WriteLine($"This file is not a regular image file. {path}");
                return false;
            }

            ConvertToDDS(path, txs3Format);

            string ddsFileName = Path.ChangeExtension(path, ".dds");
            if (!File.Exists(ddsFileName))
                return false;

            var dds = Pfim.Pfim.FromFile(ddsFileName);
            Texture texture = new Texture(txs3Format);
            texture.InitFromDDSImage(dds);

            byte[] ddsData = File.ReadAllBytes(ddsFileName).AsSpan(0x80).ToArray();

            // Do endian swap for 32bpp type
            if (txs3Format == TXS3ImageFormat.DXT10 || txs3Format == TXS3ImageFormat.DXT10_MORTON)
            {
                SpanReader sr = new SpanReader(ddsData);
                SpanWriter sw = new SpanWriter(ddsData);
                for (int j = 0; j < texture.Width * texture.Height; j++)
                {
                    int pixel = sr.ReadInt32();
                    sw.WriteInt32(BinaryPrimitives.ReverseEndianness(pixel));
                }
            }

            texture.SetDDSData(ddsData);
            File.Delete(ddsFileName);

            Textures.Add(texture);
            texture.ImageID = Textures.Count - 1;
            texture.Name = Path.GetFileNameWithoutExtension(path);

            return true;
        }

        public void ConvertToTXS(string fileName)
        {
            using var ms = new FileStream(fileName, FileMode.Create);
            using var bs = new BinaryStream(ms, ByteConverter.Big);

            if (LittleEndian)
                bs.ByteConverter = ByteConverter.Little;

            WriteToStream(bs);
        }

        public void WriteToStream(BinaryStream bs, int txsBasePos = 0)
        {
            if (!LittleEndian)
                bs.WriteString(MAGIC, StringCoding.Raw);
            else
                bs.WriteString(MAGIC_LE);

            bs.Position = txsBasePos + 0x14;
            bs.WriteInt16((short)Textures.Count); // Image Params Count;
            bs.WriteInt16((short)Textures.Count); // Image Info Count;
            bs.WriteInt32(txsBasePos + 0x40); // PGLTexture Offset (render params)

            int imageInfoOffset = txsBasePos + 0x40 + (0x44 * Textures.Count);
            bs.WriteInt32(imageInfoOffset);

            bs.WriteInt32(0x100); // Stub this for now - Image offset?

            // Write textures's render params
            bs.Position = txsBasePos + 0x40;
            foreach (var texture in Textures)
                texture.WriteMeta(bs);

            // Skip the texture info for now
            bs.Position = txsBasePos + imageInfoOffset + (0x20 * Textures.Count);

            int mainHeaderSize = (int)bs.Position;

            // Write texture names
            int lastNamePos = (int)bs.Position;
            for (int i = 0; i < Textures.Count; i++)
            {
                Texture texture = Textures[i];
                if (WriteNames && !string.IsNullOrEmpty(texture.Name))
                {
                    bs.WriteString(texture.Name, StringCoding.ZeroTerminated);

                    // Update name offset
                    bs.Position = txsBasePos + 0x40 + (0x44 * i);
                    bs.Position += 0x40; // Skip to name offset field
                    bs.WriteInt32(lastNamePos);
                }

                bs.Position = lastNamePos;
            }

            bs.Align(0x80, grow: true);

            // Actually write the textures now and their linked information
            for (int i = 0; i < Textures.Count; i++)
            {
                int imageOffset = (int)bs.Position;
                bs.WriteBytes(Textures[i].GetDDSData());
                int endImageOffset = (int)bs.Position;

                bs.Position = txsBasePos + imageInfoOffset + (0x20 * i);
                bs.WriteInt32(imageOffset);
                bs.WriteInt32(endImageOffset - imageOffset);
                bs.WriteByte(0);

                if (Textures[i].Format == TXS3ImageFormat.DXT1)
                    bs.WriteByte(0xA6);
                else if (Textures[i].Format == TXS3ImageFormat.DXT3)
                    bs.WriteByte(0xA7);
                else if (Textures[i].Format == TXS3ImageFormat.DXT5)
                    bs.WriteByte(0xA8);
                else if (Textures[i].Format == TXS3ImageFormat.DXT10)
                    bs.WriteByte(0xA5);

                bs.WriteByte((byte)(Textures[i].Mipmap + 1));
                bs.WriteByte(1);
                bs.WriteUInt16(Textures[i].Width);
                bs.WriteUInt16(Textures[i].Height);
                bs.WriteUInt16(1);
                bs.WriteUInt16(0);
                bs.Position += 12; // Pad
            }

            // Finish up main header
            bs.Position = 4;
            if (txsBasePos != 0)
            {
                bs.WriteInt32(0);
                bs.WriteInt32(txsBasePos);
                bs.WriteInt32(0);
            }
            else
            {
                bs.WriteInt32((int)bs.Length);
                bs.WriteInt32(0);
                bs.WriteInt32(mainHeaderSize);
            }

        }

        private static void ConvertToDDS(string fileName, TXS3ImageFormat imgFormat)
        {
            string arguments = fileName;
            if (imgFormat == TXS3ImageFormat.DXT1)
                arguments += " -f DXT1";
            else if (imgFormat == TXS3ImageFormat.DXT3)
                arguments += " -f DXT3";
            else if (imgFormat == TXS3ImageFormat.DXT5)
                arguments += " -f DXT5";
            else if (imgFormat == TXS3ImageFormat.DXT10)
                arguments += " -f R8G8B8A8_UNORM";

            arguments += " -y"      // Overwrite if it exists
                      + " -m 1"     // Don't care about extra mipmaps
                      + " -nologo"  // No copyright logo
                      + " -srgb";   // Auto correct gamma

            Process converter = Process.Start(Path.Combine(Directory.GetCurrentDirectory(), "texconv.exe"), arguments);
            converter.WaitForExit();
        }

        public void FromStream(Stream stream)
        {
            BinaryStream bs = new BinaryStream(stream);
            string magic = bs.ReadString(4);
            if (magic == "TXS3")
                bs.ByteConverter = ByteConverter.Big;
            else if (magic == "3SXT")
                bs.ByteConverter = ByteConverter.Little;
            else
                throw new InvalidDataException("Could not parse TXS3 from stream, not a valid TXS3 image file.");

            int fileSize = bs.ReadInt32();
            int basePos = bs.ReadInt32(); // Original Position, if bundled
            bs.Position += 4;
            bs.Position += 4; // Sometimes 1

            // TODO: Implement proper image count reading - right now we only care about the real present images
            short imageRenderParamsCount = bs.ReadInt16();
            short imageInfoCount = bs.ReadInt16();
            int imageRenderParamOffset = bs.ReadInt32();
            int imageInfoOffset = bs.ReadInt32();

            if (imageInfoCount > 0)
            {
                bs.Position = imageInfoOffset;
                for (int i = 0; i < imageInfoCount; i++)
                {
                    bs.Position = imageInfoOffset + (i * 0x20);
                    Texture texture = new Texture(TXS3ImageFormat.Unknown);

                    int imageOffset = bs.ReadInt32();
                    int imageSize = bs.ReadInt32();
                    byte flag = bs.Read1Byte();
                    byte imageFormat = bs.Read1Byte();

                    if (imageFormat == 0x85)
                        texture.Format = TXS3ImageFormat.DXT10_MORTON;
                    else if (imageFormat == 0x86 || imageFormat == 0xA6)
                        texture.Format = TXS3ImageFormat.DXT1;
                    else if (imageFormat == 0x87 || imageFormat == 0xA7)
                        texture.Format = TXS3ImageFormat.DXT3;
                    else if (imageFormat == 0x88 || imageFormat == 0xA8)
                        texture.Format = TXS3ImageFormat.DXT5;
                    else if (imageFormat == 0xA5)
                        texture.Format = TXS3ImageFormat.DXT10;

                    texture.Mipmap = bs.ReadByte() - 1;
                    bs.ReadByte(); // 1
                    texture.Width = bs.ReadUInt16();
                    texture.Height = bs.ReadUInt16();

                    // Got header, proceed to parse the image
                    bs.Position = imageOffset;

                    if (texture.Format == TXS3ImageFormat.DXT1)
                        texture.Pitch = (int)texture.Width * 2;
                    else
                        texture.Pitch = (int)texture.Width *4;

                    var ddsData = CreateDDSData(texture, bs.ReadBytes(imageSize));
                    texture.SetDDSData(ddsData);
                    Textures.Add(texture);
                }
            }

            if (imageRenderParamOffset > 0)
            {
                for (int i = 0; i < imageRenderParamsCount; i++)
                {
                    bs.Position = imageRenderParamOffset + (i * 0x44);
                    Texture texture = new Texture(TXS3ImageFormat.Unknown);

                    // TODO - Properly read it

                    bs.Position += 0x40;
                    int imageNameOffset = bs.ReadInt32();
                    if (imageNameOffset != 0)
                    {
                        bs.Position = imageNameOffset;
                        texture.Name = bs.ReadString(StringCoding.ZeroTerminated);
                    }
                }
            }
        }

        public void FromFile(string file)
        {
            using var fs = new FileStream(file, FileMode.Open);
            FromStream(fs);
        }

        private byte[] CreateDDSData(Texture texture, byte[] imageData)
        {
            using var ms = new MemoryStream();
            using var bs = new BinaryStream(ms);
            try
            {
                // https://gist.github.com/Scobalula/d9474f3fcf3d5a2ca596fceb64e16c98#file-directxtexutil-cs-L355
                bs.WriteString("DDS ", StringCoding.Raw);
                bs.WriteInt32(124);    // dwSize (Struct Size)
                bs.WriteUInt32((uint)(DDSHeaderFlags.TEXTURE)); // dwFlags
                bs.WriteInt32(texture.Height); // dwHeight

                // Dirty fix, some TXS3's in GTHD have 1920 as width, but its actually 2048. Stride is correct, so use it instead.
                int width;
                if (texture.Format == TXS3ImageFormat.DXT1)
                    width = texture.Pitch / 2;  // dwWidth
                else
                    width = texture.Pitch / 4;  // dwWidth
                bs.WriteInt32(width);

                switch (texture.Format)   // dwPitchOrLinearSize
                {
                    case TXS3ImageFormat.DXT1:
                        bs.WriteInt32(texture.Height * width / 2);
                        break;
                    case TXS3ImageFormat.DXT3:
                    case TXS3ImageFormat.DXT5:
                        bs.WriteInt32(texture.Height * width);
                        break;
                    default:
                        //bs.WriteInt32((width * 32 + 7) / 8);
                        bs.WriteInt32(0);
                        break;
                }

                bs.WriteInt32(0);    // Depth
                
                bs.WriteInt32(texture.Mipmap);
                bs.WriteBytes(new byte[44]); // reserved
                bs.WriteInt32(32); // DDSPixelFormat Header starts here - Struct Size

                switch (texture.Format)
                {
                    case TXS3ImageFormat.DXT1:
                    case TXS3ImageFormat.DXT3:
                    case TXS3ImageFormat.DXT5:
                        bs.WriteUInt32((uint)DDSPixelFormatFlags.DDPF_FOURCC); // Format Flags
                        bs.WriteString(texture.Format.ToString(), StringCoding.Raw); // FourCC
                        bs.WriteInt32(0); // RGBBitCount
                        bs.WriteInt32(0); // RBitMask
                        bs.WriteInt32(0); // GBitMask
                        bs.WriteInt32(0); // BBitMask
                        bs.WriteInt32(0); // ABitMask
                        break;
                    case TXS3ImageFormat.DXT10_MORTON:
                    case TXS3ImageFormat.DXT10:
                        bs.WriteUInt32((uint)(DDSPixelFormatFlags.DDPF_RGB | DDSPixelFormatFlags.DDPF_ALPHAPIXELS | DDSPixelFormatFlags.DDPF_FOURCC));           // Format Flags
                        bs.WriteInt32(0);            // FourCC
                        bs.WriteInt32(32);         // RGBBitCount

                        bs.WriteUInt32(0x00FF0000);  // RBitMask 32U, 16711680U, 65280U, 255U, 4278190080U
                        bs.WriteUInt32(0x0000FF00);  // GBitMask
                        bs.WriteUInt32(0x000000FF);  // BBitMask
                        bs.WriteUInt32(0xFF000000);  // ABitMask
                        break;
                }

                bs.WriteInt32(0x1000); // dwCaps, 0x1000 = required
                bs.WriteBytes(new byte[16]); // dwCaps1-4

                // Dirty convert because of endian swap..
                if (texture.Format == TXS3ImageFormat.DXT10 || texture.Format == TXS3ImageFormat.DXT10_MORTON)
                {
                    SpanReader sr = new SpanReader(imageData);
                    SpanWriter sw = new SpanWriter(imageData);
                    for (int i = 0; i < width * texture.Height; i++)
                    {
                        int val = sr.ReadInt32();
                        int rev = BinaryPrimitives.ReverseEndianness(val);
                        sw.WriteInt32(rev);
                    }
                }
                
                // Unswizzle
                if (texture.Format == TXS3ImageFormat.DXT10_MORTON)
                {
                    int bytesPerPix = 4;
                    int byteCount = (width * texture.Height) * 4;
                    byte[] newImageData = new byte[byteCount];

                    SpanReader sr = new SpanReader(imageData);
                    SpanWriter sw = new SpanWriter(newImageData);
                    Span<byte> pixBuffer = new byte[4];
                    for (int i = 0; i < width * texture.Height; i++)
                    {
                        int pixIndex = MortonReorder(i, width, texture.Height);
                        pixBuffer = sr.ReadBytes(4);
                        int destIndex = bytesPerPix * pixIndex;
                        sw.Position = destIndex;
                        sw.WriteBytes(pixBuffer);
                    }

                    imageData = newImageData;
                }

                /*
                if (texture.Format == TXS3ImageFormat.DXT10_MORTON || texture.Format == TXS3ImageFormat.DXT10)
                {
                    // DDS_HEADER_DXT10
                    bs.WriteInt32(87); // DXGI_FORMAT_B8G8R8A8_UNORM
                    bs.WriteInt32(3);  // DDS_DIMENSION_TEXTURE2D
                    bs.BaseStream.Seek(4, SeekOrigin.Current);  // miscFlag
                    bs.WriteInt32(1); // arraySize
                    bs.WriteInt32(0); // miscFlags2

                    
                }*/

                bs.Write(imageData);
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
