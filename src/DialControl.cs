using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Catodoro
{
    /// <summary>
    /// 타이머 다이얼. 가운데는 고양이들이 노는 나무 바닥이고, 남은 시간(0~60분)은
    /// 그 위에 깔린 초록 러그가 시계 방향 부채꼴로 보여 준다.
    /// 마우스로 끌어서(또는 휠로) 시간을 맞춘다. 가운데 손잡이를 누르면 시작/그만두기.
    /// </summary>
    public class DialControl : FrameworkElement
    {
        // 반지름 대비 각 부분의 크기
        const double NumRatio = 0.915, DotRatio = 0.808, FaceRatio = 0.738, KnobRatio = 0.195;

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
        public bool IsLocked { get; set; }

        /// <summary>집중 중이면 손잡이에 ■(그만두기), 아니면 ▶(시작)을 그린다.</summary>
        public bool IsRunning
        {
            get => isRunning;
            set { isRunning = value; InvalidateVisual(); }
        }

        /// <summary>나무 바닥(고양이들이 돌아다니는 곳)의 반지름.</summary>
        public double FaceRadius => Radius * FaceRatio;

        /// <summary>가운데 손잡이의 반지름.</summary>
        public double KnobRadius => Radius * KnobRatio;

        /// <summary>사용자가 다이얼을 돌려 분을 바꿨을 때.</summary>
        public event Action<int> UserSetMinutes;

        /// <summary>가운데 손잡이를 눌렀을 때.</summary>
        public event Action KnobClicked;

        static readonly Typeface NumFace = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        static readonly Brush NumBrush = Frozen(new SolidColorBrush(Theme.Ink));
        static readonly Brush DotBrush = Frozen(new SolidColorBrush(Theme.Dot));
        static readonly Brush RimBrush = Frozen(new SolidColorBrush(Theme.FloorRim));
        static readonly Brush[] PlankBrushes = Array.ConvertAll(Theme.FloorPlanks, c => Frozen(new SolidColorBrush(c)));
        static readonly Brush SeamBrush = Frozen(new SolidColorBrush(Theme.FloorSeam));
        static readonly Brush GrainBrush = Frozen(new SolidColorBrush(Theme.FloorGrain));
        static readonly Brush WedgeBrush = Frozen(new SolidColorBrush(Theme.Rug));
        static readonly Pen WedgeEdgePen = FrozenPen(Theme.RugEdge, 1);
        static readonly Pen StitchPen = MakeStitchPen();
        static readonly Brush KnobEdgeBrush = Frozen(new SolidColorBrush(Theme.KnobEdge));
        static readonly Pen KnobEdgePen = FrozenPen(Theme.KnobEdgeLine, 0.7);
        static readonly Brush KnobTopBrush = Frozen(new LinearGradientBrush(Theme.KnobTop, Theme.KnobTopShade, 90));
        static readonly Brush KnobTopHoverBrush = Frozen(new SolidColorBrush(Theme.KnobTop));
        static readonly Brush KnobTopPressedBrush = Frozen(new SolidColorBrush(Theme.KnobEdge));
        static readonly Pen KnobTopPen = FrozenPen(Theme.KnobEdgeLine, 1);
        static readonly Brush IconBrush = Frozen(new SolidColorBrush(Theme.AccentDark));
        static readonly Brush KnobShadowBrush = MakeShadowBrush();

        bool isRunning;
        bool dragging;
        bool knobDown;
        bool knobHover;

        static Brush Frozen(Brush b) { b.Freeze(); return b; }
        static Pen FrozenPen(Color c, double t) { var p = new Pen(new SolidColorBrush(c), t); p.Freeze(); return p; }

        static Brush MakeShadowBrush()
        {
            var b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, 0x4A, 0x30, 0x14), 0.0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x32, 0x4A, 0x30, 0x14), 0.6));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x4A, 0x30, 0x14), 1.0));
            b.Freeze();
            return b;
        }

        // 러그 가장자리의 바느질 선
        static Pen MakeStitchPen()
        {
            var p = new Pen(new SolidColorBrush(Theme.RugStitch), 1.5)
            {
                DashStyle = new DashStyle(new double[] { 2.2, 2.2 }, 0),
                DashCap = PenLineCap.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            p.Freeze();
            return p;
        }

        // 나무 바닥 그림은 크기가 바뀔 때만 다시 만든다
        Drawing floor;
        double floorRadius = -1;

        /// <summary>널빤지를 엇갈려 깐 나무 바닥. 중심이 (0,0)인 좌표로 그린다.</summary>
        static Drawing BuildFloor(double r)
        {
            const int plankH = 12;                 // 널빤지 한 줄의 높이
            int[] lengths = { 58, 70, 50, 64 };    // 줄마다 다른 널빤지 길이
            var group = new DrawingGroup();
            using (DrawingContext dc = group.Open())
            {
                int top = -(int)Math.Ceiling(r / plankH) * plankH;
                int reach = (int)Math.Ceiling(r) + 1;
                int row = 0;
                for (int y = top; y < reach; y += plankH, row++)
                {
                    int len = lengths[row % lengths.Length];
                    int x = -reach - (row * 37 % len);
                    for (int n = 0; x < reach; x += len, n++)
                    {
                        // 줄과 칸 번호로 정해지는 "가짜 무작위" 값: 실행할 때마다 같은 바닥이 나온다
                        int h = (row * 31 + n * 17 + row * n * 7) & 0xFF;
                        dc.DrawRectangle(PlankBrushes[h % PlankBrushes.Length], null, new Rect(x, y, len, plankH));
                        // 나뭇결 두 줄
                        dc.DrawRectangle(GrainBrush, null, new Rect(x + 6 + h % 20, y + 3, 9 + h % 7, 1));
                        dc.DrawRectangle(GrainBrush, null, new Rect(x + 24 + h % 16, y + 8, 6 + h % 9, 1));
                        // 널빤지 끝의 홈
                        dc.DrawRectangle(SeamBrush, null, new Rect(x + len - 1, y, 1, plankH));
                    }
                    // 줄 사이의 홈
                    dc.DrawRectangle(SeamBrush, null, new Rect(-reach, y + plankH - 1, reach * 2, 1));
                }
            }
            group.Freeze();
            return group;
        }

        double Radius
        {
            get
            {
                double w = ActualWidth > 0 ? ActualWidth : Width, h = ActualHeight > 0 ? ActualHeight : Height;
                return double.IsNaN(w) || double.IsNaN(h) ? 0 : Math.Min(w, h) / 2;
            }
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

            double rNum = R * NumRatio, rDot = R * DotRatio, rFace = R * FaceRatio, rKnob = R * KnobRatio;
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

            // 나무 바닥: 테두리(울타리)를 먼저 깔고, 그 안쪽 원에 널빤지를 그린다
            dc.DrawEllipse(RimBrush, null, c, rFace + 3, rFace + 3);
            if (floor == null || Math.Abs(floorRadius - rFace) > 0.01)
            {
                floor = BuildFloor(rFace);
                floorRadius = rFace;
            }
            var faceClip = new EllipseGeometry(c, rFace, rFace);
            faceClip.Freeze();
            dc.PushClip(faceClip);
            dc.PushTransform(new TranslateTransform(Math.Round(c.X), Math.Round(c.Y)));
            dc.DrawDrawing(floor);
            dc.Pop();

            // 남은 시간만큼 깔린 러그 (12시에서 시계 방향 부채꼴)
            double m = Math.Max(0, Math.Min(60, Minutes));
            double rStitch = rFace - 6;
            if (m >= 59.999)
            {
                dc.DrawEllipse(WedgeBrush, null, c, rFace, rFace);
                dc.DrawEllipse(null, StitchPen, c, rStitch, rStitch);
            }
            else if (m > 0.001)
            {
                double deg = m * 6;
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(c, true, true);
                    g.LineTo(Polar(c, rFace, 0), true, false);
                    g.ArcTo(Polar(c, rFace, deg), new Size(rFace, rFace), 0, deg > 180, SweepDirection.Clockwise, false, false);
                }
                geo.Freeze();
                dc.DrawGeometry(WedgeBrush, WedgeEdgePen, geo);

                // 바느질 선은 러그 양 끝에서 조금 안쪽까지만
                double inset = Math.Min(deg / 2, 4);
                if (deg - inset * 2 > 1)
                {
                    var arc = new StreamGeometry();
                    using (var g = arc.Open())
                    {
                        g.BeginFigure(Polar(c, rStitch, inset), false, false);
                        g.ArcTo(Polar(c, rStitch, deg - inset), new Size(rStitch, rStitch), 0, deg - inset * 2 > 180,
                            SweepDirection.Clockwise, true, false);
                    }
                    arc.Freeze();
                    dc.DrawGeometry(null, StitchPen, arc);
                }
            }
            dc.Pop();

            // 가운데 손잡이 (= 시작 버튼)
            var shadowCenter = new Point(c.X, c.Y + R * 0.035);
            dc.DrawEllipse(KnobShadowBrush, null, shadowCenter, rKnob * 1.5, rKnob * 1.5);
            const int bumps = 18;
            for (int i = 0; i < bumps; i++)
            {
                Point p = Polar(c, rKnob * 0.93, i * 360.0 / bumps);
                dc.DrawEllipse(KnobEdgeBrush, KnobEdgePen, p, rKnob * 0.2, rKnob * 0.2);
            }
            dc.DrawEllipse(KnobEdgeBrush, null, c, rKnob * 0.95, rKnob * 0.95);
            Brush top = knobDown ? KnobTopPressedBrush : (knobHover ? KnobTopHoverBrush : KnobTopBrush);
            dc.DrawEllipse(top, KnobTopPen, c, rKnob * 0.72, rKnob * 0.72);

            // 손잡이 위 아이콘: ▶ 시작 / ■ 그만두기
            double s = rKnob * 0.30;
            if (isRunning)
            {
                dc.DrawRoundedRectangle(IconBrush, null, new Rect(c.X - s, c.Y - s, s * 2, s * 2), s * 0.3, s * 0.3);
            }
            else
            {
                var tri = new StreamGeometry();
                using (var g = tri.Open())
                {
                    g.BeginFigure(new Point(c.X - s * 0.8, c.Y - s * 1.15), true, true);
                    g.LineTo(new Point(c.X + s * 1.25, c.Y), true, true);
                    g.LineTo(new Point(c.X - s * 0.8, c.Y + s * 1.15), true, true);
                }
                tri.Freeze();
                dc.DrawGeometry(IconBrush, null, tri);
            }
        }

        bool OverKnob(Point p) => DistanceFromCenter(p) <= KnobRadius;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);

            // 손잡이는 잠겨 있어도 누를 수 있다(그만두기)
            if (OverKnob(p))
            {
                knobDown = true;
                CaptureMouse();
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (IsLocked) return; // 잠겨 있으면 창 끌기로 넘긴다
            if (DistanceFromCenter(p) > Radius) return;
            dragging = true;
            CaptureMouse();
            UpdateFromPoint(p, true);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            if (dragging)
            {
                UpdateFromPoint(p, false);
                return;
            }

            bool over = OverKnob(p);
            if (over != knobHover)
            {
                knobHover = over;
                InvalidateVisual();
            }
            Cursor = over || (!IsLocked && DistanceFromCenter(p) <= Radius) ? Cursors.Hand : null;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (knobHover)
            {
                knobHover = false;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (knobDown)
            {
                bool click = OverKnob(e.GetPosition(this));
                knobDown = false;
                ReleaseMouseCapture();
                InvalidateVisual();
                e.Handled = true;
                if (click) KnobClicked?.Invoke();
                return;
            }
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            dragging = false;
            if (knobDown)
            {
                knobDown = false;
                InvalidateVisual();
            }
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
