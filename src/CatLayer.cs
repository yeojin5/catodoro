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
    /// <summary>
    /// 타이머의 둥근 판 안을 돌아다니는 고양이들.
    /// 판 바깥으로 나가지 않고, 가운데 손잡이(시작 버튼)는 밟지 않고 돌아서 다닌다.
    /// </summary>
    public class CatLayer : Canvas
    {
        const double PixelScale = 1.5;    // 스프라이트 1픽셀 = 화면 1.5픽셀
        const double Speed = 18;          // 초당 이동 거리(px)
        const double FrameSeconds = 0.2;  // 걷기 프레임 간격
        const double EdgeMargin = 15;     // 판 가장자리에서 고양이 중심까지 최소 거리
        const double KnobMargin = 15;     // 손잡이에서 고양이 중심까지 최소 거리
        public const int MaxVisible = 12; // 화면에 동시에 보여 줄 최대 마리 수

        sealed class Cat
        {
            public Image Img;
            public ScaleTransform Flip;
            public int Type;
            public int Frame = -1;
            public double X, Y, TX, TY; // 고양이 "중심"의 좌표
            public double Timer;        // 쉬는 중 남은 시간
            public double Anim;         // 걷기 애니메이션 누적 시간
            public bool Walking;
        }

        readonly List<Cat> cats = new List<Cat>();
        readonly Random rng = new Random();
        readonly DispatcherTimer timer;
        DateTime last;
        double faceRadius = 90, knobRadius = 26;

        static double SpriteW => CatSprites.W * PixelScale;
        static double SpriteH => CatSprites.H * PixelScale;

        public CatLayer()
        {
            IsHitTestVisible = false;
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += (s, e) => Step();
            Loaded += (s, e) => { last = DateTime.UtcNow; timer.Start(); };
            Unloaded += (s, e) => timer.Stop();
            SizeChanged += (s, e) => UpdateClip();
        }

        double AreaW => ActualWidth > 0 ? ActualWidth : 298;
        double AreaH => ActualHeight > 0 ? ActualHeight : 298;
        double CenterX => AreaW / 2;
        double CenterY => AreaH / 2;
        double MinR => knobRadius + KnobMargin;
        double MaxR => faceRadius - EdgeMargin;

        /// <summary>돌아다닐 판의 반지름과, 피해 다닐 가운데 손잡이의 반지름.</summary>
        public void SetArena(double faceRadius, double knobRadius)
        {
            this.faceRadius = faceRadius;
            this.knobRadius = knobRadius;
            UpdateClip();
        }

        void UpdateClip()
        {
            // 판 바깥으로 삐져나온 꼬리 끝 등은 잘라 낸다
            Clip = new EllipseGeometry(new Point(CenterX, CenterY), faceRadius - 1, faceRadius - 1);
        }

        /// <summary>저장된 고양이들을 불러와 판 위 아무 곳에나 풀어 놓는다.</summary>
        public void SetCats(IEnumerable<int> types)
        {
            foreach (var c in cats) Children.Remove(c.Img);
            cats.Clear();
            var list = types.ToList();
            foreach (int t in list.Skip(Math.Max(0, list.Count - MaxVisible))) Add(t, false);
        }

        /// <summary>새 고양이 한 마리가 판 아래쪽에서 나타난다.</summary>
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
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

            var cat = new Cat { Img = img, Flip = flip, Type = type };
            if (isNew)
            {
                cat.X = CenterX;
                cat.Y = CenterY + (MinR + MaxR) / 2;
                cat.Timer = 1.2;
                img.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
            }
            else
            {
                RandomSpot(out cat.X, out cat.Y);
                cat.Timer = rng.NextDouble() * 4;
                flip.ScaleX = rng.Next(2) == 0 ? 1 : -1;
            }
            SetFrame(cat, isNew ? CatSprites.FrameSit : CatSprites.FrameWalkA);
            cats.Add(cat);
            Children.Add(img);
            Place(cat);
        }

        /// <summary>손잡이와 가장자리 사이(고리 모양 영역)의 아무 지점.</summary>
        void RandomSpot(out double x, out double y)
        {
            double a = rng.NextDouble() * Math.PI * 2;
            // 넓이에 고르게 퍼지도록 반지름은 제곱근으로 뽑는다
            double r = Math.Sqrt(MinR * MinR + rng.NextDouble() * (MaxR * MaxR - MinR * MinR));
            x = CenterX + r * Math.Cos(a);
            y = CenterY + r * Math.Sin(a);
        }

        /// <summary>지금 자리에서 손잡이를 가로지르지 않고 걸어갈 수 있는 다음 목적지를 고른다.</summary>
        bool PickTarget(Cat c, out double tx, out double ty)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                RandomSpot(out tx, out ty);
                double dx = tx - c.X, dy = ty - c.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 18) continue;
                // 옆모습이라 너무 가파르게 오르내리면 어색하다 (처음 몇 번은 완만한 길만)
                if (attempt < 12 && Math.Abs(dy) > Math.Abs(dx) * 0.8) continue;
                if (DistanceToCenter(c.X, c.Y, tx, ty) < MinR - 3) continue;
                return true;
            }
            tx = c.X; ty = c.Y;
            return false;
        }

        /// <summary>선분 (x1,y1)-(x2,y2) 에서 판 중심까지의 최단 거리.</summary>
        double DistanceToCenter(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1, dy = y2 - y1;
            double len2 = dx * dx + dy * dy;
            double t = len2 <= 0 ? 0 : ((CenterX - x1) * dx + (CenterY - y1) * dy) / len2;
            t = Math.Max(0, Math.Min(1, t));
            double px = x1 + t * dx - CenterX, py = y1 + t * dy - CenterY;
            return Math.Sqrt(px * px + py * py);
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
                    if (c.Timer > 0) continue;
                    if (PickTarget(c, out c.TX, out c.TY))
                    {
                        c.Walking = true;
                        c.Anim = 0;
                        c.Flip.ScaleX = c.TX > c.X ? -1 : 1; // 기본 그림은 왼쪽을 본다
                    }
                    else
                    {
                        c.Timer = 1; // 갈 곳을 못 찾았으면 조금 더 쉰다
                    }
                }
            }
        }

        static void SetFrame(Cat c, int frame)
        {
            if (c.Frame == frame) return;
            c.Frame = frame;
            c.Img.Source = CatSprites.GetBig(c.Type, frame);
        }

        static void Place(Cat c)
        {
            SetLeft(c.Img, c.X - SpriteW / 2);
            SetTop(c.Img, c.Y - SpriteH / 2);
            SetZIndex(c.Img, (int)c.Y); // 아래쪽에 있는 고양이가 앞에 보이게
        }
    }
}
