using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIRCUS_MES
{
    static class Extensions
    {
        public static byte[] ReadCString(this BinaryReader @this, bool nullTerminated = false)
        {
            var buffer = new List<byte>();

            for (byte value = @this.ReadByte(); value != 0; value = @this.ReadByte())
            {
                buffer.Add(value);
            }

            if (nullTerminated)
            {
                buffer.Add(0);
            }

            return buffer.ToArray();
        }
    }
}
