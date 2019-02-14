using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace TXS3Converter
{
    public static class BigEndianHelper
    {
        public static short ReadBEInt16(this BinaryReader br)
        {
            byte[] b = br.ReadBytes(2);
            return (short)(b[1] + (b[0] << 8));
        }
        public static int ReadBEInt32(this BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            return b[3] + (b[2] << 8) + (b[1] << 16) + (b[0] << 24);
        }
        public static long ReadBEInt64(this BinaryReader br)
        {
            byte[] b = br.ReadBytes(8);
            return b[7] + (b[6] << 8) + (b[5] << 16) + (b[4] << 24) + (b[3] << 32) + (b[2] << 40) + (b[1] << 48) + (b[0] << 56);
        }

        /// <summary>Returns <c>true</c> if the Int32 read is not zero, otherwise, <c>false</c>.</summary>
        /// <returns><c>true</c> if the Int32 is not zero, otherwise, <c>false</c>.</returns>
        public static bool ReadBEInt32AsBool(this BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            if (b[0] == 0 || b[1] == 0 || b[2] == 0 || b[3] == 0)
                return false;
            else
                return true;
        }
    }
}
