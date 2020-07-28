using System;
using System.Collections.Generic;
using System.Text;

using Syroot.BinaryData.Memory;

namespace GTTools.Formats.Entities
{
    /// <summary>
    /// Flexible Vertex Format
    /// https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dfvf
    /// </summary>
    public class MDL3FVF
    {
        public byte[] data;
        public static MDL3FVF FromStream(ref SpanReader sr)
        {
            MDL3FVF fvf = new MDL3FVF();

            
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadByte();
            sr.ReadByte();
            sr.ReadByte();
            sr.ReadByte();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();
            sr.ReadUInt32();

            /*
            fs.Seek(fvfTableEnum[0], SeekOrigin.Begin);

            fvfTableEnum.Add((uint)fs.Position);

            fvfTable.Add(fvfTableEnum);*/
            return fvf;
        }
    }
}
