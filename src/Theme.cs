using System.Windows.Media;

namespace Catodoro
{
    /// <summary>
    /// 코드에서 그리는 부분의 색. 크림색 몸체, 나무 바닥, 초록 러그의 포근한 고양이 방 테마.
    /// (XAML 쪽 색은 MainWindow.xaml / SettingsWindow.xaml / HistoryWindow.xaml 에 같은 계열로 들어 있다)
    /// </summary>
    public static class Theme
    {
        public static readonly Color Accent = Rgb(0x93, 0xBF, 0x62);       // 아이콘 바탕
        public static readonly Color AccentDark = Rgb(0x5E, 0x8C, 0x3A);   // 시작 아이콘, 강조 글자
        public static readonly Color Ink = Rgb(0x5A, 0x44, 0x32);          // 숫자
        public static readonly Color Dot = Rgb(0x9A, 0x80, 0x66);          // 눈금 점

        // 고양이들이 돌아다니는 나무 바닥
        public static readonly Color FloorRim = Rgb(0xC4, 0x97, 0x62);     // 바닥 테두리(울타리)
        public static readonly Color[] FloorPlanks =
        {
            Rgb(0xF0, 0xD5, 0xA8), Rgb(0xEB, 0xCD, 0x9D), Rgb(0xF3, 0xDB, 0xB2),
        };
        public static readonly Color FloorSeam = Rgb(0xD6, 0xB3, 0x82);    // 널빤지 사이 홈
        public static readonly Color FloorGrain = Rgb(0xE2, 0xC2, 0x90);   // 나뭇결

        // 남은 시간을 보여 주는 러그
        public static readonly Color Rug = Rgb(0xA4, 0xCB, 0x80);
        public static readonly Color RugEdge = Rgb(0x8D, 0xB9, 0x68);
        public static readonly Color RugStitch = Rgb(0xF4, 0xF8, 0xDC);

        public static readonly Color KnobTop = Rgb(0xFF, 0xFB, 0xF1);
        public static readonly Color KnobTopShade = Rgb(0xF4, 0xE8, 0xD2);
        public static readonly Color KnobEdge = Rgb(0xFB, 0xF2, 0xE0);
        public static readonly Color KnobEdgeLine = Rgb(0xD8, 0xC0, 0x9C);

        public static readonly Color LedIdle = Rgb(0xDB, 0xCB, 0xB0);
        public static readonly Color LedOn = Rgb(0x4F, 0xD1, 0x5C);
        public static readonly Color LedBad = Rgb(0xF0, 0x52, 0x4F);

        static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    }
}
