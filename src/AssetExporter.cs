using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Catodoro
{
    /// <summary>아이콘과 스프라이트 시트를 그려서 파일로 내보낸다. (창 아이콘에도 사용)</summary>
    public static class AssetExporter
    {
        static readonly Color Purple = Color.FromRgb(0x8E, 0x80, 0xDD);

        public static void Export(string dir)
        {
            Directory.CreateDirectory(dir);
            SavePng(RenderSpriteSheet(8), Path.Combine(dir, "sprites.png"));
            WriteIco(Path.Combine(dir, "catodoro.ico"), new[] { 256, 64, 48, 32, 24, 16 });
        }

        /// <summary>보라색 원 위에 치즈 고양이가 서 있는 아이콘.</summary>
        public static BitmapSource RenderIcon(int size)
        {
            const int master = 256;
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.NearestNeighbor);
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Purple), null, new Point(master / 2.0, master / 2.0), master / 2.0, master / 2.0);
                const int scale = 9;
                double w = CatSprites.W * scale, h = CatSprites.H * scale;
                dc.DrawImage(CatSprites.Get(CatSprites.Tabby, CatSprites.FrameWalkA),
                    new Rect((master - w) / 2 + 2, (master - h) / 2 + 6, w, h));
            }
            var big = new RenderTargetBitmap(master, master, 96, 96, PixelFormats.Pbgra32);
            big.Render(dv);
            big.Freeze();
            if (size == master) return big;

            var small = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(small, BitmapScalingMode.Fant);
            using (DrawingContext dc = small.RenderOpen())
                dc.DrawImage(big, new Rect(0, 0, size, size));
            var result = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            result.Render(small);
            result.Freeze();
            return result;
        }

        static BitmapSource RenderSpriteSheet(int scale)
        {
            int cw = CatSprites.W * scale, ch = CatSprites.H * scale, pad = scale;
            int width = pad + CatSprites.FrameCount * (cw + pad);
            int height = pad + CatSprites.TypeCount * (ch + pad);
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.NearestNeighbor);
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xC9, 0xD3, 0xE0)), null, new Rect(0, 0, width, height));
                for (int t = 0; t < CatSprites.TypeCount; t++)
                    for (int f = 0; f < CatSprites.FrameCount; f++)
                        dc.DrawImage(CatSprites.Get(t, f), new Rect(pad + f * (cw + pad), pad + t * (ch + pad), cw, ch));
            }
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            return rtb;
        }

        public static void SavePng(BitmapSource bmp, string path)
        {
            File.WriteAllBytes(path, PngBytes(bmp));
        }

        static byte[] PngBytes(BitmapSource bmp)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        /// <summary>창 전체를 회색 바탕 위에 그려 PNG 로 저장한다(개발용 화면 확인).</summary>
        public static void SaveVisual(FrameworkElement el, string path, double scale = 2)
        {
            double w = el.ActualWidth, h = el.ActualHeight;
            var dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xB8, 0xC2, 0xCF)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(el) { Stretch = Stretch.None }, null, new Rect(0, 0, w, h));
            }
            var rtb = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(dv);
            SavePng(rtb, path);
        }

        static void WriteIco(string path, int[] sizes)
        {
            var images = new List<byte[]>();
            foreach (int s in sizes) images.Add(PngBytes(RenderIcon(s)));

            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((short)0);            // reserved
                bw.Write((short)1);            // type: icon
                bw.Write((short)sizes.Length); // count
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); // width
                    bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); // height
                    bw.Write((byte)0);   // palette
                    bw.Write((byte)0);   // reserved
                    bw.Write((short)1);  // planes
                    bw.Write((short)32); // bpp
                    bw.Write(images[i].Length);
                    bw.Write(offset);
                    offset += images[i].Length;
                }
                foreach (byte[] img in images) bw.Write(img);
            }
        }
    }
}
