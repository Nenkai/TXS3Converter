using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Syroot.BinaryData.Memory;

namespace GTTools.Formats.Entities
{
    public class MDL3Mesh
    {
        private byte Flag1;
        private byte Flag2;
        public ushort MeshIndex { get; set; }
        public ushort UnkIndex { get; set; }
        private ushort null1;

        private uint VertexCount { get; set; }
        public uint VertexOffset { get; set; }
        public Vertex[] Verts { get; set; }

        private uint null3;
        public uint FacesDataLength { get; set; }
        public uint FacesOffset { get; set; }

        public Face[] Faces { get; set; }

        public uint null5;
        public uint null6;

        private byte Flag3;
        private byte Flag4;
        public ushort Unk;
        private uint UnkOffset; // Offset that points to a list of floats
        private uint UnkOffset2;

        public static MDL3Mesh FromStream(ref SpanReader sr)
        {
            var meshInfo = new MDL3Mesh();
            meshInfo.Flag1 = sr.ReadByte();   //unk flag 1
            meshInfo.Flag2 = sr.ReadByte();   //unk flag 2
            meshInfo.MeshIndex = sr.ReadUInt16();
            meshInfo.UnkIndex = sr.ReadUInt16(); 
            meshInfo.null1 = sr.ReadUInt16(); 
            meshInfo.VertexCount = sr.ReadUInt32(); 
            meshInfo.VertexOffset = sr.ReadUInt32(); 

            if (meshInfo.VertexCount > 0)
            {
                meshInfo.Verts = new Vertex[meshInfo.VertexCount];
                int curPos = sr.Position;
                sr.Position = (int)meshInfo.VertexOffset;
                for (int i = 0; i < meshInfo.VertexCount; i++)
                    meshInfo.Verts[i] = MemoryMarshal.Read<Vertex>(sr.ReadBytes(20));
                sr.Position = curPos;
            }

            meshInfo.null3 = sr.ReadUInt32(); 
            meshInfo.FacesDataLength = sr.ReadUInt32(); 
            meshInfo.FacesOffset = sr.ReadUInt32(); 
            if (meshInfo.FacesDataLength > 0)
            {
                meshInfo.Faces = new Face[meshInfo.FacesDataLength / 3];
                int curPos = sr.Position;
                sr.Position = (int)meshInfo.FacesOffset;
                for (int i = 0; i < meshInfo.FacesDataLength / 3; i++)
                    meshInfo.Faces[i] = MemoryMarshal.Read<Face>(sr.ReadBytes(6));
                sr.Position = curPos;
            }

            meshInfo.null5 = sr.ReadUInt32(); 
            meshInfo.null6 = sr.ReadUInt32();
            meshInfo.Flag3 = sr.ReadByte();   //unk flag 3
            meshInfo.Flag4 = sr.ReadByte();   //unk flag 4
            meshInfo.Unk = sr.ReadUInt16();
            meshInfo.UnkOffset = sr.ReadUInt32();
            meshInfo.UnkOffset2 = sr.ReadUInt32();

            return meshInfo;
        }

        public struct Vertex
        {
            float X;
            float Y;
            float Z;
            int pad;
            int unkFlags;
        }

        public struct Face
        {
            ushort X;
            ushort Y;
            ushort Z;
        }
    }
}
