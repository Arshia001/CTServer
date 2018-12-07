using System;
using System.Collections.Generic;
using System.Text;

namespace BackgammonLogic
{
    public enum Color
    {
        White = 1,
        Black = -1
    }

    public static class ColorExtensions
    {
        public static Color Flip(this Color Color) => (Color)(-(int)Color);

        public static byte AsIndex(this Color Color) => Color == Color.White ? (byte)1 : (byte)0;

        public static sbyte AsSign(this Color Color) => (sbyte)Color;
    }
}
