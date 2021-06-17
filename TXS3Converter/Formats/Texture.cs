using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData.Core;
using Syroot.BinaryData.Memory;
using Syroot.BinaryData;
using System.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;

using Pfim;
using Pfim.dds;

namespace GTTools.Formats
{
    public class Texture
    {
        public int ImageID { get; set; }
        public string Name { get; set; }

        public int Mipmap { get; set; }
        public TXS3ImageFormat Format { get; set; }

        public byte Dim { get; set; } = 2;
        public bool NoBorder { get; set; } = true;
        public bool CubeMap { get; set; } = false;
        public byte Location { get; set; } = 1;
        public byte ZFunc { get; set; } = 0;
        public byte Gamma { get; set; } = 0;
        public byte SignExt { get; set; } = 6;
        public byte WrapT { get; set; } = 3;
        public byte UseAniso { get; set; } = 0;
        public byte WrapS { get; set; } = 3;
        public bool Enable { get; set; } = true;
        public short LODMin { get; set; }
        public short LODMax { get; set; } = 3840;
        public byte MaxAniso { get; set; }
        public byte SignedRGBA { get; set; }
        public byte Mag { get; set; } = 2;
        public byte Min { get; set; } = 2;
        public byte Convultion { get; set; } = 1;
        public byte LODBias { get; set; }
        public int BorderColor { get; set; }


        public ushort Width { get; set; }
        public ushort Height { get; set; }

        public short Depth { get; set; } = 1;
        public int Pitch { get; set; }

        private byte[] _ddsData;

        public Texture(TXS3ImageFormat format)
        {
            Format = format;
        }

        public void SetDDSData(byte[] data)
            => _ddsData = data;

        public byte[] GetDDSData()
            => _ddsData;

        public void InitFromDDSImage(Pfim.IImage image)
        {
            Width = (ushort)image.Width;
            Height = (ushort)image.Height;
            Mipmap = image.MipMaps.Length;
            
            
            Pitch = Width * 4;
        }

        public void WriteMeta(BinaryStream bs)
        {
            bs.WriteInt32(6656); // head0
            bs.WriteInt32(0); // offset (runtime)
            bs.WriteByte(0); // pad0
            bs.WriteByte((byte)(Mipmap + 1));
            if (Format == TXS3ImageFormat.DXT1)
                bs.WriteByte(0xA6);
            else if (Format == TXS3ImageFormat.DXT3)
                bs.WriteByte(0xA7);
            else if (Format == TXS3ImageFormat.DXT5)
                bs.WriteByte(0xA8);
            else if (Format == TXS3ImageFormat.DXT10)
                bs.WriteByte(0xA5);

            byte bits = 0;
            bits |= (byte)((Dim & 0b_1111) << 4);
            bits |= (byte)(((NoBorder ? 1 : 0) & 1) << 3);
            bits |= (byte)(((CubeMap ? 1 : 0) & 1) << 2);
            bits |= (byte)(Location & 0b_11);
            bs.WriteByte(bits);

            int bits2 = 0;
            bits2 |= ((ZFunc & 0b_1_1111) << 27);
            bits2 |= ((Gamma & 0b_1111_1111) << 19);
            bits2 |= ((SignExt & 0b_1111) << 15);
            bits2 |= ((0 << 0b111) << 12);
            bits2 |= ((WrapT & 0b_1111) << 8);
            bits2 |= ((UseAniso & 0b_111) << 5);
            bits2 |= (WrapS & 0b_1_1111);
            bs.WriteInt32(bits2);

            int bits3 = 0;
            bits3 |= (((Enable ? 1 : 0) & 31) << 31);
            bits3 |= ((LODMin & 0b_1111_1111_1111) << 19);
            bits3 |= ((LODMax & 0b_1111_1111_1111) << 7);
            bits3 |= ((MaxAniso << 5) & 0b_111);
            // 4 bit pad
            bs.WriteInt32(bits3);
            bs.WriteInt32(43748); // remap

            int bits4 = 0;
            bits4 |= ((SignedRGBA & 0b1_1111) << 27);
            bits4 |= ((Mag & 0b_111) << 24);
            bits4 |= ((Min & 0b_1111_1111) << 16);
            bits4 |= ((Convultion & 0b_111) << 13);
            bits4 |= (LODBias & 0b_1111_1111_1111);
            bs.WriteInt32(bits4);

            bs.WriteUInt16(Width);
            bs.WriteUInt16(Height);
            bs.WriteInt32(BorderColor);
            bs.WriteInt32(6208); // head1 fixme

            int bits5 = 0;
            bits5 |= (int)((Depth & 0x1111_1111_1111) << 20);
            bits5 |= (Pitch & 0b1111_1111_1111_1111_1111);
            bs.WriteInt32(bits5); // head1 fixme

            bs.WriteInt32(0); // Reserved.. or not?
            bs.WriteInt32(0); // Same
            bs.WriteInt32(0);
            bs.WriteInt32(ImageID); // Image Id
            bs.WriteInt32(0);
            bs.WriteInt32(0); // Img name offset to write later if exists
        }

        public void WriteDataInfo()
        {

        }

        public void SaveAsPng(string dir)
        {
            using var ms = new MemoryStream(_ddsData);
            var dds = Pfim.Pfim.FromStream(ms);
            var encoder = new PngEncoder();

            string finalFileName = Path.Combine(dir, Name) + ".png";

            if (dds.Format == Pfim.ImageFormat.Rgb24)
                Save<Bgr24>(dds, finalFileName);
            else if (dds.Format == Pfim.ImageFormat.Rgba32)
                Save<Bgra32>(dds, finalFileName);
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

    }

    public enum TXS3ImageFormat
    {
        Unknown,
        DXT1,
        DXT3,
        DXT5,

        // RGBA Pretty Much
        DXT10,
        DXT10_MORTON,
    }
}
