using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Catodoro
{
    /// <summary>
    /// 고양이 놀이터의 소품(캣타워, 방석, 밥그릇, 털실, 쥐 장난감). 고양이처럼 이미지 파일 없이 코드로 그린다.
    /// 글자 한 칸이 픽셀 하나이고, 바깥 테두리는 자동으로 둘러 준다.
    /// </summary>
    public static class PropSprites
    {
        public const int Tower = 0, Cushion = 1, Bowl = 2, Yarn = 3, Mouse = 4;
        public const int Count = 5;

        const string Outline = "#6B4A32";

        // 대문자 L, T 와 소문자 l 은 "가는 선"이라 둘레에 테두리를 만들지 않는다.
        const string ThinChars = "lLT";

        static readonly string[][] Art =
        {
            // 캣타워: 위에 올라앉는 판, 삼줄을 감은 기둥, 매달린 방울
            new[]
            {
                ".pppppppppppppp.",
                "ppPPPPPPPPPPPPpp",
                "pppppppppppppppp",
                "...l..SSSS......",
                "...l..ssss......",
                "..ttt.ssss......",
                "..tTt.SSSS......",
                "..ttt.ssss......",
                "......ssss......",
                "......SSSS......",
                "......ssss......",
                "......ssss......",
                "......SSSS......",
                "......ssss......",
                ".bbbbbbbbbbbbbb.",
                "bbbbbbbbbbbbbbbb",
            },
            // 방석: 고양이가 들어가 앉는 동그란 침대
            new[]
            {
                "..rrrrrrrrrrrrrrrrrrrr..",
                ".rrrrRRRRRRRRRRRRRRrrrr.",
                "rrrRRccccccccccccccRRrrr",
                "rrrRccccccccccccccccRrrr",
                "rrrRccccccccccccccccRrrr",
                "rrrrRRccccccccccccRRrrrr",
                ".rrrrrRRRRRRRRRRRRrrrrr.",
                "..rrrrrrrrrrrrrrrrrrrr..",
            },
            // 밥그릇
            new[]
            {
                ".kKkkKkkk.",
                "bbbbbbbbbb",
                "bBBBBBBBBb",
                ".bbbbbbbb.",
            },
            // 털실 뭉치
            new[]
            {
                "..yyyy.....",
                ".yyYyyy....",
                "yyYyyyYy...",
                "yYyyyYyy...",
                "yyyYYyyy...",
                ".yYyyyy..L.",
                "..yyyyLLL..",
            },
            // 쥐 장난감
            new[]
            {
                "..ee.....",
                ".mmmmm...",
                "mlmmmmm.T",
                ".mmmmmTT.",
            },
        };

        static readonly Dictionary<char, string>[] Palettes =
        {
            new Dictionary<char, string> { ['p'] = "#F7E7C8", ['P'] = "#EBD3A6", ['s'] = "#DDBD86", ['S'] = "#C39D62",
                                            ['b'] = "#F3E0BD", ['t'] = "#F28B8B", ['T'] = "#FFC4C4", ['l'] = Outline },
            new Dictionary<char, string> { ['r'] = "#F4B9A8", ['R'] = "#E49C8A", ['c'] = "#FFF2E2" },
            new Dictionary<char, string> { ['b'] = "#A5CFEA", ['B'] = "#86B9DD", ['k'] = "#BC864D", ['K'] = "#9C6B38" },
            new Dictionary<char, string> { ['y'] = "#C9B6EC", ['Y'] = "#A994DA", ['L'] = "#9A83CF" },
            new Dictionary<char, string> { ['m'] = "#C4BFCC", ['e'] = "#F5AEB8", ['l'] = "#3A3038", ['T'] = "#E79AA6" },
        };

        /// <summary>화면에 부드럽게 줄여 그릴 큰 원본의 배율(고양이와 같다).</summary>
        public const int Upscale = CatSprites.Upscale;

        static readonly BitmapSource[] Cache = new BitmapSource[Count];

        /// <summary>테두리를 포함한 소품의 픽셀 너비.</summary>
        public static int WidthOf(int prop) => Art[prop][0].Length + 2;

        /// <summary>테두리를 포함한 소품의 픽셀 높이.</summary>
        public static int HeightOf(int prop) => Art[prop].Length + 2;

        public static BitmapSource GetBig(int prop)
        {
            return Cache[prop] ?? (Cache[prop] = Render(prop));
        }

        static BitmapSource Render(int prop)
        {
            string[] rows = Art[prop];
            int w = WidthOf(prop), h = HeightOf(prop);

            // 그림을 한 칸씩 안쪽으로 옮겨 놓고, 빈 칸 중 그림에 닿은 칸을 테두리로 칠한다
            var g = new char[w, h];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] != '.') g[x + 1, y + 1] = rows[y][x];

            bool Solid(int x, int y) =>
                x >= 0 && y >= 0 && x < w && y < h && g[x, y] != '\0' && ThinChars.IndexOf(g[x, y]) < 0;
            var edge = new List<(int x, int y)>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (g[x, y] == '\0' && (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1)))
                        edge.Add((x, y));
            foreach (var (x, y) in edge) g[x, y] = 'o';

            var pal = new Dictionary<char, Color> { ['o'] = Hex(Outline) };
            foreach (var kv in Palettes[prop]) pal[kv.Key] = Hex(kv.Value);

            int bw = w * Upscale, bh = h * Upscale;
            var px = new int[bw * bh];
            for (int y = 0; y < bh; y++)
            {
                for (int x = 0; x < bw; x++)
                {
                    char ch = g[x / Upscale, y / Upscale];
                    if (ch == '\0') continue;
                    Color c = pal[ch];
                    px[y * bw + x] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                }
            }
            var bmp = BitmapSource.Create(bw, bh, 96, 96, PixelFormats.Bgra32, null, px, bw * 4);
            bmp.Freeze();
            return bmp;
        }

        static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    }
}
