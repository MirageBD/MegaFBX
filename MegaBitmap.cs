using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

class MegaBitmap
{
    public class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public UInt32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }
        public byte[] PalRed;
        public byte[] PalGreen;
        public byte[] PalBlue;
        public int XScale;
        public int YScale;

        protected GCHandle BitsHandle { get; private set; }

        public DirectBitmap(int width, int height, int xscale, int yscale)
        {
            Width = xscale * width;
            Height = yscale * height;
            Bits = new UInt32[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());
            XScale = xscale;
            YScale = yscale;

            PalRed = new byte[256];
            PalGreen = new byte[256];
            PalBlue = new byte[256];

            for (int i = 0; i < 256; i++)
            {
                PalRed[i] = (byte)i;
                PalGreen[i] = (byte)i;
                PalBlue[i] = (byte)i;
            }
        }

        public void SetColor(int x, int y, Color c)
        {
            Bits[x + (y * Width)] = (UInt32)c.ToArgb();
        }

        public Color GetColor(int x, int y)
        {
            return Color.FromArgb((int)Bits[x + (y * Width)]);
        }

        public void SetPixel(int x, int y, UInt32 u)
        {
            Bits[x + (y * Width)] = u;
        }

        public UInt32 GetPixel(int x, int y)
        {
            return Bits[x + (y * Width)];
        }

        public UInt32 GetPaletteColour(byte i)
        {
            UInt32 result = 0xFF000000;
            result += (UInt32)PalRed[i] << 16;
            result += (UInt32)PalGreen[i] << 8;
            result += (UInt32)PalBlue[i] << 0;
            return result;
        }

        public void FromArray(byte[,] array)
        {
            int arWidth = array.GetLength(0);
            int arHeight = array.GetLength(1);

            for (int y = 0; y < arHeight; y++)
            {
                for (int x = 0; x < arWidth; x++)
                {
                    for (int i = 0; i < XScale; i++)
                    {
                        for (int j = 0; j < YScale; j++)
                        {
                            this.SetPixel(x * XScale + i, y * YScale + j, GetPaletteColour(array[x, y]));
                        }
                    }
                }
            }
        }

        public void DrawGrid(int blocksizex, int blocksizey)
        {
            for (int y = 0; y < this.Height; y += YScale * blocksizey)
            {
                for (int x = 0; x < this.Width; x++)
                {
                    this.SetPixel(x, y, (UInt32)(0xFF402000));
                }
            }

            for (int x = 0; x < this.Width; x += XScale * blocksizex)
            {
                for (int y = 0; y < this.Height; y++)
                {
                    this.SetPixel(x, y, (UInt32)(0xFF402000));
                }
            }
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }
}

