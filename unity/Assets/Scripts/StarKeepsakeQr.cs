using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Echoes
{
    // A fixed version 3-L QR code is sufficient for an IPv4 URL and its session route token.
    internal static class StarKeepsakeQr
    {
        private const int Size = 29, DataWords = 55, CorrectionWords = 15, Quiet = 4;

        public static Texture2D Texture(string url)
        {
            bool[,] modules = Encode(url);
            if (modules == null) return null;
            int side = Size + Quiet * 2;
            var pixels = new Color32[side * side];
            var white = new Color32(255,255,255,255);
            var black = new Color32(7,10,16,255);
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    pixels[(side - y - 1) * side + x] = x >= Quiet && x < Quiet + Size && y >= Quiet && y < Quiet + Size &&
                        modules[x - Quiet, y - Quiet] ? black : white;
            var result = new Texture2D(side, side, TextureFormat.RGBA32, false) {
                name = "Star Sea keepsake QR", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            result.SetPixels32(pixels);
            result.Apply(false, true);
            return result;
        }

        internal static bool[,] Encode(string url)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(url);
            if (bytes.Length > 53) return null;
            var bits = new List<bool>(DataWords * 8);
            void Append(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
            }
            Append(4, 4);
            Append(bytes.Length, 8);
            foreach (byte value in bytes) Append(value, 8);
            Append(0, Math.Min(4, DataWords * 8 - bits.Count));
            Append(0, (8 - bits.Count % 8) % 8);
            for (int pad = 0; bits.Count < DataWords * 8; pad++) Append(pad % 2 == 0 ? 0xEC : 0x11, 8);

            var words = new byte[DataWords];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i]) words[i / 8] |= (byte)(1 << (7 - i % 8));
            foreach (byte value in Correction(words)) Append(value, 8);

            var code = new bool[Size, Size];
            var occupied = new bool[Size, Size];
            void Reserve(int x, int y, bool value)
            {
                if ((uint)x >= Size || (uint)y >= Size) return;
                code[x,y] = value;
                occupied[x,y] = true;
            }
            void Finder(int x, int y)
            {
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        Reserve(x + dx, y + dy, distance != 2 && distance != 4);
                    }
            }
            for (int i = 0; i < Size; i++)
            {
                Reserve(6,i,i % 2 == 0);
                Reserve(i,6,i % 2 == 0);
            }
            Finder(3,3);
            Finder(Size-4,3);
            Finder(3,Size-4);
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    Reserve(22 + x,22 + y,Math.Max(Math.Abs(x),Math.Abs(y)) != 1);
            const int formatLowMaskZero = 0x77C4;
            bool Format(int index) => ((formatLowMaskZero >> index) & 1) != 0;
            for (int i = 0; i < 6; i++) Reserve(8,i,Format(i));
            Reserve(8,7,Format(6));
            Reserve(8,8,Format(7));
            Reserve(7,8,Format(8));
            for (int i = 9; i < 15; i++) Reserve(14-i,8,Format(i));
            for (int i = 0; i < 8; i++) Reserve(Size-1-i,8,Format(i));
            for (int i = 8; i < 15; i++) Reserve(8,Size-15+i,Format(i));
            Reserve(8,Size-8,true);
            int next = 0;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int row = 0; row < Size; row++)
                    for (int edge = 0; edge < 2; edge++)
                    {
                        int x = right - edge;
                        int y = ((right + 1) & 2) == 0 ? Size - 1 - row : row;
                        if (occupied[x,y]) continue;
                        code[x,y] = (next < bits.Count && bits[next]) ^ ((x + y) % 2 == 0);
                        next++;
                    }
            }
            return code;
        }

        private static byte[] Correction(byte[] data)
        {
            var generator = new byte[CorrectionWords];
            generator[CorrectionWords - 1] = 1;
            byte root = 1;
            for (int i = 0; i < CorrectionWords; i++)
            {
                for (int j = 0; j < CorrectionWords; j++)
                    generator[j] = (byte)(Multiply(generator[j], root) ^ (j + 1 < CorrectionWords ? generator[j + 1] : 0));
                root = Multiply(root, 2);
            }
            var remainder = new byte[CorrectionWords];
            foreach (byte value in data)
            {
                int factor = value ^ remainder[0];
                for (int i = 0; i < CorrectionWords; i++)
                    remainder[i] = (byte)((i + 1 < CorrectionWords ? remainder[i + 1] : 0) ^ Multiply(generator[i], factor));
            }
            return remainder;
        }

        private static byte Multiply(int a, int b)
        {
            int result = 0;
            for (int i = 7; i >= 0; i--)
            {
                result = ((result << 1) ^ ((result >> 7) * 0x11D)) ^ ((b >> i & 1) * a);
            }
            return (byte)result;
        }
    }
}
