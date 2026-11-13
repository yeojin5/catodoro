using System.Windows.Media;

namespace Catodoro
{
    /// <summary>
    /// 코드에서 그리는 부분의 색. 따뜻한 초록색 테마.
    /// (XAML 쪽 색은 MainWindow.xaml / SettingsWindow.xaml 의 리소스에 같은 값으로 들어 있다)
    /// </summary>
    public static class Theme
    {
        public static readonly Color Accent = Rgb(0x93, 0xBF, 0x62);       // 부채꼴, 아이콘 바탕
        public static readonly Color AccentDark = Rgb(0x5E, 0x8C, 0x3A);   // 시작 아이콘, 강조 글자
        public static readonly Color Ink = Rgb(0x3C, 0x45, 0x2C);          // 숫자, 눈금
        public static readonly Color Face = Rgb(0xFF, 0xFE, 0xF6);         // 가운데 판
        public static readonly Color FaceEdge = Rgb(0xDD, 0xE3, 0xC8);
        public static readonly Color KnobTop = Rgb(0xFF, 0xFE, 0xF8);
        public static readonly Color KnobTopShade = Rgb(0xEE, 0xF0, 0xE0);
        public static readonly Color KnobEdge = Rgb(0xEA, 0xED, 0xDB);
        public static readonly Color KnobEdgeLine = Rgb(0xCF, 0xD6, 0xB8);

        public static readonly Color LedIdle = Rgb(0xC6, 0xCF, 0xB2);
        public static readonly Color LedOn = Rgb(0x4F, 0xD1, 0x5C);
        public static readonly Color LedBad = Rgb(0xF0, 0x52, 0x4F);

        static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    }
}
