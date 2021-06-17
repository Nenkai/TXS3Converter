using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

namespace GTTools.Formats.Entities
{
    [DebuggerDisplay("Index: {MeshIndex}, {MeshParams}")]
    public class MDL3MeshInfo
    {
        public uint MeshIndex { get; private set; }
        public string MeshParams { get; private set; }

        private uint _strOffset;

        public static MDL3MeshInfo FromStream(BinaryStream sr)
        {
            MDL3MeshInfo meshInfo = new MDL3MeshInfo();
            meshInfo._strOffset = sr.ReadUInt32();

            int curPos = (int)sr.Position;
            sr.Position = (int)meshInfo._strOffset;
            meshInfo.MeshParams = sr.ReadString(StringCoding.ZeroTerminated);
            sr.Position = curPos;

            meshInfo.MeshIndex = sr.ReadUInt32();
            return meshInfo;
        }
    }
}
