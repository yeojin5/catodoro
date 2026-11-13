using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Catodoro
{
    /// <summary>
    /// 픽셀 고양이 스프라이트. 이미지 파일 없이 코드로 그린다.
    /// 몸통을 사각형 조각으로 칠한 뒤, 바깥 테두리는 자동으로 둘러 준다.
    /// 기본 방향은 왼쪽을 보는 모습(머리가 왼쪽, 꼬리가 오른쪽).
    /// </summary>
    public static class CatSprites
    {
        public const int W = 22;
        public const int H = 16;

        /// <summary>평소에 나오는 고양이 종류 수(0~3).</summary>
        public const int NormalTypeCount = 4;
        public const int Calico = 0, Black = 1, Tabby = 2, White = 3;

        /// <summary>
        /// 행운의 고양이(마네키네코). 흰 바탕에 삼색 얼룩, 빨간 목줄과 금방울, 짧은 꼬리.
        /// 앉아 있을 때 한쪽 앞발을 들어 손짓한다.
        /// </summary>
        public const int Lucky = 4;
        /// <summary>집중에 성공했을 때 행운의 고양이가 나올 확률.</summary>
        public const double LuckyChance = 0.10;

        /// <summary>전체 종류 수. 이름은 Lang.CatName 에 있다.</summary>
        public const int TypeCount = 5;

        public const int FrameCount = 4;
        /// <summary>FrameSitB 는 행운의 고양이가 앞발을 흔드는 두 번째 앉은 모습(다른 고양이는 FrameSit 과 같다).</summary>
        public const int FrameWalkA = 0, FrameWalkB = 1, FrameSit = 2, FrameSitB = 3;

        // 칸 종류: b 몸 / p 무늬1 / q 무늬2 / w 배·주둥이·발 / e 왼눈 / f 오른눈 / n 코 / i 귀 안쪽
        //          c 목줄 / g 방울 / o 테두리
        static readonly Dictionary<char, Color>[] Palettes =
        {
            Pal(b: "#FDFBF6", p: "#F2A03D", q: "#3D3638", w: "#FDFBF6", e: "#2B2B2B", f: "#2B2B2B", n: "#F29CA3", i: "#F7B7BC", o: "#4A3B3B"),
            Pal(b: "#35343E", p: "#35343E", q: "#35343E", w: "#35343E", e: "#8BE04E", f: "#8BE04E", n: "#7A6873", i: "#5C4C58", o: "#15141A"),
            Pal(b: "#F5A742", p: "#D97F2A", q: "#F5A742", w: "#FFF6E8", e: "#2B2B2B", f: "#2B2B2B", n: "#E8808A", i: "#F7B7BC", o: "#5A3A22"),
            Pal(b: "#FFFFFF", p: "#FFFFFF", q: "#FFFFFF", w: "#FFFFFF", e: "#4AA8F0", f: "#F2B830", n: "#F29CA3", i: "#F7B7BC", o: "#6B6B78"),
            Pal(b: "#FFFDF4", p: "#F2A03D", q: "#3D3638", w: "#FFFDF4", e: "#2B2B2B", f: "#2B2B2B", n: "#F0808A", i: "#E8484F", o: "#5A3F2A",
                c: "#E23B3B", g: "#F6C62E"),
        };

        static readonly BitmapSource[,] Cache = new BitmapSource[TypeCount, FrameCount];

        public static BitmapSource Get(int type, int frame)
        {
            type = ((type % TypeCount) + TypeCount) % TypeCount;
            frame = ((frame % FrameCount) + FrameCount) % FrameCount;
            return Cache[type, frame] ?? (Cache[type, frame] = Render(type, frame));
        }

        // 화면에 1.5배처럼 정수가 아닌 배율로 그릴 때 쓰는 큰 원본.
        // 픽셀을 그대로 Upscale 배 키워 두고, 화면에서 부드럽게 줄여 그리면 어떤 배율에서도 픽셀 크기가 고르게 보인다.
        public const int Upscale = 6;
        static readonly BitmapSource[,] BigCache = new BitmapSource[TypeCount, FrameCount];

        public static BitmapSource GetBig(int type, int frame)
        {
            type = ((type % TypeCount) + TypeCount) % TypeCount;
            frame = ((frame % FrameCount) + FrameCount) % FrameCount;
            if (BigCache[type, frame] != null) return BigCache[type, frame];

            BitmapSource small = Get(type, frame);
            var src = new int[W * H];
            small.CopyPixels(src, W * 4, 0);
            int bw = W * Upscale, bh = H * Upscale;
            var dst = new int[bw * bh];
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                    dst[y * bw + x] = src[(y / Upscale) * W + x / Upscale];
            var big = BitmapSource.Create(bw, bh, 96, 96, PixelFormats.Bgra32, null, dst, bw * 4);
            big.Freeze();
            return BigCache[type, frame] = big;
        }

        static Dictionary<char, Color> Pal(string b, string p, string q, string w, string e, string f, string n, string i, string o,
            string c = "#E23B3B", string g = "#F6C62E")
        {
            Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
            return new Dictionary<char, Color>
            {
                ['b'] = C(b), ['p'] = C(p), ['q'] = C(q), ['w'] = C(w),
                ['e'] = C(e), ['f'] = C(f), ['n'] = C(n), ['i'] = C(i), ['o'] = C(o),
                ['c'] = C(c), ['g'] = C(g),
            };
        }

        static BitmapSource Render(int type, int frame)
        {
            char[,] g = BuildBase(frame, type == Lucky);
            ApplyPattern(g, type);
            AddOutline(g);

            var pal = Palettes[type];
            var pixels = new int[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    char ch = g[x, y];
                    if (ch == '\0') continue;
                    Color c = pal[ch];
                    pixels[y * W + x] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                }
            }
            var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, pixels, W * 4);
            bmp.Freeze();
            return bmp;
        }

        static char[,] BuildBase(int frame, bool lucky)
        {
            var g = new char[W, H];
            void R(int x, int y, int w, int h, char c)
            {
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                        if (xx >= 0 && yy >= 0 && xx < W && yy < H) g[xx, yy] = c;
            }

            bool sitting = frame == FrameSit || frame == FrameSitB;
            if (sitting)
            {
                // 앉은 몸통
                R(4, 8, 7, 6, 'b');
                R(11, 9, 1, 5, 'b');
                // 가슴과 앞발
                R(4, 8, 3, 4, 'w');
                R(4, 12, 1, 2, 'w');
                R(6, 12, 1, 2, 'w');
                if (lucky)
                {
                    // 짧은 꼬리
                    R(12, 12, 2, 2, 'b');
                }
                else
                {
                    // 바닥에 늘어뜨린 꼬리
                    R(12, 13, 5, 1, 'b');
                    R(16, 12, 1, 1, 'b');
                }
            }
            else
            {
                if (lucky)
                {
                    // 짧고 동그란 꼬리
                    R(19, 6, 2, 2, 'b');
                }
                else
                {
                    // 위로 세운 꼬리
                    R(18, 4, 1, 3, 'b');
                    R(19, 2, 1, 3, 'b');
                    R(20, 2, 1, 1, 'b');
                }
                // 몸통
                R(6, 7, 13, 5, 'b');
                // 가슴과 배
                R(6, 8, 1, 4, 'w');
                R(7, 10, 1, 2, 'w');
                R(8, 11, 8, 1, 'w');
                // 다리 (걷는 두 프레임)
                if (frame == FrameWalkA)
                {
                    R(7, 12, 1, 1, 'b'); R(7, 13, 1, 1, 'w');
                    R(9, 12, 1, 1, 'b'); R(9, 13, 1, 1, 'w');
                    R(15, 12, 1, 1, 'b'); R(15, 13, 1, 1, 'w');
                    R(17, 12, 1, 1, 'b'); R(17, 13, 1, 1, 'w');
                }
                else
                {
                    R(6, 12, 1, 1, 'b'); R(6, 13, 1, 1, 'w');
                    R(10, 12, 1, 1, 'w');
                    R(14, 12, 1, 1, 'w');
                    R(18, 12, 1, 1, 'b'); R(18, 13, 1, 1, 'w');
                }
            }

            // 귀
            R(2, 1, 1, 1, 'b'); R(2, 2, 2, 1, 'b');
            R(6, 1, 1, 1, 'b'); R(5, 2, 2, 1, 'b');
            R(2, 2, 1, 1, 'i'); R(6, 2, 1, 1, 'i');
            // 머리
            R(2, 3, 5, 5, 'b');
            // 얼굴
            R(3, 5, 1, 1, 'e');
            R(5, 5, 1, 1, 'f');
            R(3, 6, 3, 2, 'w');
            R(4, 6, 1, 1, 'n');

            if (lucky)
            {
                if (sitting)
                {
                    // 빨간 목줄과 금방울
                    R(3, 8, 5, 1, 'c');
                    R(5, 9, 1, 1, 'g');
                    // 손짓하는 앞발: 귀 높이까지 든 발을 까딱인다
                    if (frame == FrameSit)
                    {
                        R(8, 3, 1, 6, 'w');   // 팔
                        R(8, 2, 2, 1, 'w');   // 발 (위로)
                        R(9, 3, 1, 1, 'w');
                    }
                    else
                    {
                        R(8, 4, 1, 5, 'w');   // 팔
                        R(8, 3, 2, 1, 'w');   // 발 (아래로 까딱)
                        R(9, 4, 2, 1, 'w');
                    }
                }
                else
                {
                    R(7, 7, 1, 2, 'c');
                    R(6, 8, 1, 1, 'c');
                    R(6, 9, 1, 1, 'g');
                }
            }
            return g;
        }

        static void ApplyPattern(char[,] g, int type)
        {
            void Paint(int x, int y, int w, int h, char c)
            {
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                        if (xx >= 0 && yy >= 0 && xx < W && yy < H && g[xx, yy] == 'b') g[xx, yy] = c;
            }

            if (type == Calico)
            {
                // 검은 얼룩
                Paint(2, 1, 2, 4, 'q');
                Paint(14, 7, 4, 3, 'q');
                Paint(18, 1, 3, 6, 'q');
                Paint(12, 12, 6, 2, 'q');
                // 주황 얼룩
                Paint(5, 1, 2, 4, 'p');
                Paint(8, 7, 4, 3, 'p');
                Paint(8, 10, 2, 1, 'p');
            }
            else if (type == Tabby)
            {
                // 등줄무늬
                for (int x = 9; x <= 17; x += 2) Paint(x, 7, 1, 2, 'p');
                Paint(4, 3, 1, 1, 'p');
                Paint(18, 5, 1, 1, 'p');
                Paint(19, 3, 1, 1, 'p');
                Paint(13, 13, 1, 1, 'p');
                Paint(15, 13, 1, 1, 'p');
            }
            else if (type == Lucky)
            {
                // 마네키네코 특유의 작은 삼색 얼룩: 한쪽 귀는 검정, 한쪽 귀와 등은 주황, 꼬리는 검정
                Paint(2, 1, 1, 1, 'q');
                Paint(2, 3, 1, 1, 'q');
                Paint(6, 1, 1, 1, 'p');
                Paint(5, 3, 2, 1, 'p');
                Paint(12, 7, 4, 2, 'p');
                Paint(9, 10, 2, 3, 'p');
                Paint(19, 6, 2, 2, 'q');
                Paint(12, 12, 2, 2, 'q');
            }
        }

        static void AddOutline(char[,] g)
        {
            var edge = new List<(int x, int y)>();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (g[x, y] != '\0') continue;
                    bool near =
                        (x > 0 && g[x - 1, y] != '\0') || (x < W - 1 && g[x + 1, y] != '\0') ||
                        (y > 0 && g[x, y - 1] != '\0') || (y < H - 1 && g[x, y + 1] != '\0');
                    if (near) edge.Add((x, y));
                }
            }
            foreach (var (x, y) in edge) g[x, y] = 'o';
        }
    }
}
