using System;
using System.Collections.Generic;
using System.Text;

namespace CS_FPGA_CLIENT
{
    class Utilities
    {

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static byte[] FlipByteArrayUInt32(byte[] ba)
        {
            byte[] result = new byte[ba.Length];
            for (int i = 0; i + 3 < ba.Length; i += 4)
            {
                result[i + 0] = ba[i + 3];
                result[i + 1] = ba[i + 2];
                result[i + 2] = ba[i + 1];
                result[i + 3] = ba[i + 0];
            }
            return result;
        }

        public static byte[] StringToByteArray(String hex)
        {
            int numChars = hex.Length;
            byte[] bytes = new byte[numChars / 2];
            for (int i = 0; i < numChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static byte[] StringToByteArrayReverse(String hex)
        {
            int numChars = hex.Length;
            byte[] bytes = new byte[numChars / 2];
            for (int i = 0; i < numChars; i += 2)
                bytes[(numChars / 2) - (i / 2) - 1] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static string hex(byte[] v, string delimiter = "")
        {
            int i;
            StringBuilder s = new StringBuilder();
            for (i = 0; i < v.Length; i++)
            {
                s.Append(String.Format("{0:X2}", v[i]));
                if (i != v.Length - 1)
                    s.Append(delimiter);
            }
            return s.ToString();
        }

        public static string hex(uint[] v, string delimiter="")
        {
            int i;
            StringBuilder s = new StringBuilder();
            for (i = 0; i < v.Length; i++)
            {
                s.Append(String.Format("{0:X8}", v[i]));
                if(i != v.Length-1)
                    s.Append(delimiter);
            }
            return s.ToString();
        }

       public static uint swap32(uint v)
        {
            return ((v >> 24) & 0xff) |
                   ((v << 8) & 0xff0000) |
                   ((v >> 8) & 0xff00) |
                   ((v << 24) & 0xff000000);
        }

        public static byte[] UintArrSwap32ToByteArr(uint[] a)
        {
            byte[] b = new byte[a.Length << 2];
            for(int i=0; i<a.Length; i++)
            {
                b[(i << 2) + 3] = (byte)(a[i] & 0xff);
                b[(i << 2) + 2] = (byte)((a[i]>>8) & 0xff);
                b[(i << 2) + 1] = (byte)((a[i]>>16) & 0xff);
                b[(i << 2) + 0] = (byte)((a[i]>>24) & 0xff);
            }
            return b;
        }

    }
}
