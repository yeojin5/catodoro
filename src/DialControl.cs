using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Catodoro
{
    /// <summary>
    /// 타이머 다이얼. 0~60분을 시계 방향 보라색 부채꼴로 보여 주고,
    /// 마우스로 끌어서(또는 휠로) 시간을 맞춘다.
    /// </summary>
    public class DialControl : FrameworkElement
    {
        public static readonly DependencyProperty MinutesProperty = DependencyProperty.Register(
            nameof(Minutes), typeof(double), typeof(DialControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>표시할 분(0~60). 타이머가 도는 동안에는 소수로 줄어든다.</summary>
        public double Minutes
        {
            get => (double)GetValue(MinutesProperty);
            set => SetValue(MinutesProperty, value);
        }

        /// <summary>true 이면 사용자가 다이얼을 돌릴 수 없다(집중 시간 중).</summary>
        public bool IsLocked
        {
            get => isLocked;
            set { isLocked = value; Cursor = value ? null : Cursors.Hand; }
        }

        /// <summary>사용자가 다이얼을 돌려 분을 바꿨을 때.</summary>
        public event Action<int> UserSetMinutes;

        static readonly Typeface NumFace = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        static readonly Brush NumBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x33, 0x31, 0x2E)));
        static readonly Brush DotBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3D, 0x3B, 0x38)));
        static readonly Brush FaceBrush = Brushes.White;
        static readonly Pen FacePen = FrozenPen(Color.FromRgb(0xE6, 0xE3, 0xDE), 1);
        static readonly Brush WedgeBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x8E, 0x80, 0xDD)));
        static readonly Brush KnobEdgeBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xEC, 0xEA, 0xE5)));
        static readonly Pen KnobEdgePen = FrozenPen(Color.FromRgb(0xD6, 0xD3, 0xCC), 0.7);
        static readonly Brush KnobTopBrush = Frozen(new LinearGradientBrush(Colors.White, Color.FromRgb(0xEF, 0xED, 0xE8), 90));
        static readonly Pen KnobTopPen = FrozenPen(Color.FromRgb(0xDE, 0xDB, 0xD5), 1);
        static readonly Brush KnobShadowBrush = MakeShadowBrush();

        bool isLocked;
        bool dragging;

        public DialControl()
        {
            Cursor = Cursors.Hand;
        }

        static Brush Frozen(Brush b) { b.Freeze(); return b; }
        static Pen FrozenPen(Color c, double t) { var p = new Pen(new SolidColorBrush(c), t); p.Freeze(); return p; }

        static Brush MakeShadowBrush()
        {
            var b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x50, 0, 0, 0), 0.0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x30, 0, 0, 0), 0.6));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1.0));
            b.Freeze();
            return b;
        }

        static Point Polar(Point c, double r, double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            double R = Math.Min(w, h) / 2;
            var c = new Point(w / 2, h / 2);

            // 빈 곳도 마우스 입력을 받도록
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            double rNum = R * 0.915, rDot = R * 0.79, rFace = R * 0.705, rKnob = R * 0.19;
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // 숫자 0, 5, ... 55
            for (int n = 0; n < 60; n += 5)
            {
                var ft = new FormattedText(n.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, NumFace, R * 0.105, NumBrush, ppd);
                Point p = Polar(c, rNum, n * 6);
                dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y - ft.Height / 2));
            }

            // 분 눈금 점
            for (int i = 0; i < 60; i++)
            {
                Point p = Polar(c, rDot, i * 6);
                double r = i % 5 == 0 ? R * 0.018 : R * 0.0095;
                dc.DrawEllipse(DotBrush, null, p, r, r);
            }

            // 흰 판
            dc.DrawEllipse(FaceBrush, FacePen, c, rFace, rFace);

            // 남은 시간 부채꼴 (12시에서 시계 방향)
            double m = Math.Max(0, Math.Min(60, Minutes));
            if (m >= 59.999)
            {
                dc.DrawEllipse(WedgeBrush, null, c, rFace, rFace);
            }
            else if (m > 0.001)
            {
                double deg = m * 6;
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(c, true, true);
                    g.LineTo(Polar(c, rFace, 0), false, false);
                    g.ArcTo(Polar(c, rFace, deg), new Size(rFace, rFace), 0, deg > 180, SweepDirection.Clockwise, false, false);
                }
                geo.Freeze();
                dc.DrawGeometry(WedgeBrush, null, geo);
            }

            // 가운데 손잡이
            var shadowCenter = new Point(c.X, c.Y + R * 0.035);
            dc.DrawEllipse(KnobShadowBrush, null, shadowCenter, rKnob * 1.5, rKnob * 1.5);
            const int bumps = 18;
            for (int i = 0; i < bumps; i++)
            {
                Point p = Polar(c, rKnob * 0.93, i * 360.0 / bumps);
                dc.DrawEllipse(KnobEdgeBrush, KnobEdgePen, p, rKnob * 0.2, rKnob * 0.2);
            }
            dc.DrawEllipse(KnobEdgeBrush, null, c, rKnob * 0.95, rKnob * 0.95);
            dc.DrawEllipse(KnobTopBrush, KnobTopPen, c, rKnob * 0.7, rKnob * 0.7);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (IsLocked) return; // 잠겨 있으면 창 끌기로 넘긴다
            Point p = e.GetPosition(this);
            if (DistanceFromCenter(p) > Math.Min(ActualWidth, ActualHeight) / 2) return;
            dragging = true;
            CaptureMouse();
            UpdateFromPoint(p, true);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) UpdateFromPoint(e.GetPosition(this), false);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            dragging = false;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (IsLocked) return;
            int m = (int)Math.Round(Minutes) + (e.Delta > 0 ? 1 : -1);
            SetByUser(Math.Max(0, Math.Min(60, m)));
            e.Handled = true;
        }

        double DistanceFromCenter(Point p)
        {
            double dx = p.X - ActualWidth / 2, dy = p.Y - ActualHeight / 2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal void UpdateFromPoint(Point p, bool first)
        {
            // 손잡이 정중앙 근처에서는 각도가 불안정하므로 무시
            if (DistanceFromCenter(p) < 4) return;

            double dx = p.X - ActualWidth / 2, dy = p.Y - ActualHeight / 2;
            double ang = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            if (ang < 0) ang += 360;
            int m = (int)Math.Round(ang / 6.0); // 0..60

            if (!first)
            {
                // 12시 방향을 넘어갈 때 0 ↔ 60 으로 튀지 않게 막는다
                int prev = (int)Math.Round(Minutes);
                if (prev >= 45 && m <= 15) m = 60;
                else if (prev <= 15 && m >= 45) m = 0;
            }
            SetByUser(m);
        }

        void SetByUser(int m)
        {
            if ((int)Math.Round(Minutes) == m && Math.Abs(Minutes - m) < 0.0001) return;
            Minutes = m;
            UserSetMinutes?.Invoke(m);
        }
    }
}
