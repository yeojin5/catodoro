using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Catodoro
{
    /// <summary>타이머 몸체 안을 돌아다니는 고양이들.</summary>
    public class CatLayer : Canvas
    {
        const double PixelScale = 2;      // 스프라이트 1픽셀 = 화면 2픽셀
        const double Speed = 22;          // 초당 이동 거리(px)
        const double FrameSeconds = 0.2;  // 걷기 프레임 간격
        const double CornerRadius = 34;
        public const int MaxVisible = 16; // 화면에 동시에 보여 줄 최대 마리 수

        sealed class Cat
        {
            public Image Img;
            public ScaleTransform Flip;
            public int Type;
            public int Frame = -1;
            public double X, Y, TX, TY;
            public double Timer;   // 쉬는 중 남은 시간
            public double Anim;    // 걷기 애니메이션 누적 시간
            public bool Walking;
        }

        readonly List<Cat> cats = new List<Cat>();
        readonly Random rng = new Random();
        readonly DispatcherTimer timer;
        DateTime last;

        static double SpriteW => CatSprites.W * PixelScale;
        static double SpriteH => CatSprites.H * PixelScale;

        public CatLayer()
        {
            IsHitTestVisible = false;
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += (s, e) => Step();
            Loaded += (s, e) => { last = DateTime.UtcNow; timer.Start(); };
            Unloaded += (s, e) => timer.Stop();
            SizeChanged += (s, e) =>
                Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), CornerRadius, CornerRadius);
        }

        double AreaW => ActualWidth > 0 ? ActualWidth : 300;
        double AreaH => ActualHeight > 0 ? ActualHeight : 300;

        /// <summary>저장된 고양이들을 불러와 아무 곳에나 풀어 놓는다.</summary>
        public void SetCats(IEnumerable<int> types)
        {
            foreach (var c in cats) Children.Remove(c.Img);
            cats.Clear();
            var list = types.ToList();
            foreach (int t in list.Skip(Math.Max(0, list.Count - MaxVisible))) Add(t, false);
        }

        /// <summary>새 고양이 한 마리가 가운데에서 나타난다.</summary>
        public void Spawn(int type) => Add(type, true);

        void Add(int type, bool isNew)
        {
            if (cats.Count >= MaxVisible)
            {
                Children.Remove(cats[0].Img);
                cats.RemoveAt(0);
            }

            var flip = new ScaleTransform(1, 1);
            var img = new Image
            {
                Width = SpriteW,
                Height = SpriteH,
                Stretch = Stretch.Fill,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = flip,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);

            var cat = new Cat { Img = img, Flip = flip, Type = type };
            if (isNew)
            {
                cat.X = (AreaW - SpriteW) / 2;
                cat.Y = AreaH * 0.62;
                cat.Timer = 1.2;
                img.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
            }
            else
            {
                PickSpot(out cat.X, out cat.Y, cat.X, cat.Y, anywhere: true);
                cat.Timer = rng.NextDouble() * 4;
                flip.ScaleX = rng.Next(2) == 0 ? 1 : -1;
            }
            SetFrame(cat, isNew ? CatSprites.FrameSit : CatSprites.FrameWalkA);
            cats.Add(cat);
            Children.Add(img);
            Place(cat);
        }

        void PickSpot(out double x, out double y, double fromX, double fromY, bool anywhere)
        {
            double minX = 8, maxX = AreaW - 8 - SpriteW;
            double minY = 10, maxY = AreaH - 10 - SpriteH;
            x = minX; y = minY;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                x = minX + rng.NextDouble() * (maxX - minX);
                // 옆모습이라 주로 좌우로 걷는 편이 자연스럽다
                y = anywhere
                    ? minY + rng.NextDouble() * (maxY - minY)
                    : Math.Max(minY, Math.Min(maxY, fromY + (rng.NextDouble() - 0.5) * 90));
                if (!InCorner(x, y)) return;
            }
        }

        // 둥근 모서리에 걸려 잘려 보이는 자리는 피한다
        bool InCorner(double x, double y)
        {
            const double zone = 26;
            bool nearX = x < zone || x + SpriteW > AreaW - zone;
            bool nearY = y < zone || y + SpriteH > AreaH - zone;
            return nearX && nearY;
        }

        void Step()
        {
            DateTime now = DateTime.UtcNow;
            double dt = Math.Min(0.1, (now - last).TotalSeconds);
            last = now;
            if (dt <= 0) return;

            foreach (var c in cats)
            {
                if (c.Walking)
                {
                    double dx = c.TX - c.X, dy = c.TY - c.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double step = Speed * dt;
                    if (dist <= step)
                    {
                        c.X = c.TX; c.Y = c.TY;
                        c.Walking = false;
                        c.Timer = 1.5 + rng.NextDouble() * 5.5;
                        SetFrame(c, rng.Next(3) == 0 ? CatSprites.FrameWalkA : CatSprites.FrameSit);
                    }
                    else
                    {
                        c.X += dx / dist * step;
                        c.Y += dy / dist * step;
                        c.Anim += dt;
                        SetFrame(c, (int)(c.Anim / FrameSeconds) % 2 == 0 ? CatSprites.FrameWalkA : CatSprites.FrameWalkB);
                    }
                    Place(c);
                }
                else
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0)
                    {
                        PickSpot(out c.TX, out c.TY, c.X, c.Y, anywhere: false);
                        c.Walking = true;
                        c.Anim = 0;
                        c.Flip.ScaleX = c.TX > c.X ? -1 : 1; // 기본 그림은 왼쪽을 본다
                    }
                }
            }
        }

        static void SetFrame(Cat c, int frame)
        {
            if (c.Frame == frame) return;
            c.Frame = frame;
            c.Img.Source = CatSprites.Get(c.Type, frame);
        }

        static void Place(Cat c)
        {
            SetLeft(c.Img, Math.Round(c.X));
            SetTop(c.Img, Math.Round(c.Y));
            SetZIndex(c.Img, (int)c.Y); // 아래쪽에 있는 고양이가 앞에 보이게
        }
    }
}
