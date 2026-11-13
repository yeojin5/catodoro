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
    /// 타이머의 둥근 나무 바닥(고양이 놀이터)과 그 안을 돌아다니는 고양이들.
    /// 고양이는 바닥 바깥으로 나가지 않고, 가운데 손잡이(시작 버튼)는 밟지 않고 돌아서 다닌다.
    /// 가끔 방석에 들어가 앉거나 캣타워 위로 뛰어올라 쉰다.
    /// </summary>
    public class CatLayer : Canvas
    {
        const double PixelScale = 1.5;    // 스프라이트 1픽셀 = 화면 1.5픽셀
        const double Speed = 18;          // 초당 이동 거리(px)
        const double JumpSpeed = 95;      // 캣타워를 오르내릴 때의 속도
        const double FrameSeconds = 0.2;  // 걷기 프레임 간격
        const double WaveSeconds = 0.4;   // 행운의 고양이 손짓 간격
        const double EdgeMargin = 15;     // 바닥 가장자리에서 고양이 중심까지 최소 거리
        const double KnobMargin = 15;     // 손잡이에서 고양이 중심까지 최소 거리
        const double FeetOffset = 9.75;   // 고양이 중심에서 발바닥까지의 거리
        const double BodyOffset = 6.75;   // 앉은 고양이의 몸이 그림 중심에서 벗어난 정도
        const int SpotChance = 30;        // 쉬다가 일어날 때 방석·캣타워로 갈 확률(%)
        public const int MaxVisible = 12; // 화면에 동시에 보여 줄 최대 마리 수

        // 고양이가 방석·캣타워를 쓰는 단계
        const int Free = 0, ToEntry = 1, ToSpot = 2, OnSpot = 3, Leaving = 4;

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
            public Spot Spot;           // 가고 있거나 앉아 있는 자리
            public int Phase;
        }

        /// <summary>고양이 한 마리가 차지할 수 있는 자리(방석 안, 캣타워 위).</summary>
        sealed class Spot
        {
            public double X, Y;            // 앉은 고양이의 몸 중심이 올 곳
            public bool HasEntry;          // true 이면 Entry 까지 걸어간 뒤 뛰어오른다
            public double EntryX, EntryY;
            public int Z;                  // 이 자리의 고양이가 소품보다 앞에 그려지도록 하는 순서
            public Cat Owner;
        }

        readonly List<Cat> cats = new List<Cat>();
        readonly Image[] props = new Image[PropSprites.Count];
        readonly List<Spot> spots = new List<Spot>();
        readonly Random rng = new Random();
        readonly DispatcherTimer timer;
        DateTime last;
        double faceRadius = 90, knobRadius = 26;

        static double SpriteW => CatSprites.W * PixelScale;
        static double SpriteH => CatSprites.H * PixelScale;

        public CatLayer()
        {
            IsHitTestVisible = false;
            for (int i = 0; i < props.Length; i++)
            {
                props[i] = new Image
                {
                    Source = PropSprites.GetBig(i),
                    Width = PropSprites.WidthOf(i) * PixelScale,
                    Height = PropSprites.HeightOf(i) * PixelScale,
                    Stretch = Stretch.Fill,
                };
                RenderOptions.SetBitmapScalingMode(props[i], BitmapScalingMode.HighQuality);
                Children.Add(props[i]);
            }

            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += (s, e) => Step();
            Loaded += (s, e) => { last = DateTime.UtcNow; timer.Start(); };
            Unloaded += (s, e) => timer.Stop();
            SizeChanged += (s, e) => UpdateLayoutParts();
        }

        double AreaW => ActualWidth > 0 ? ActualWidth : 298;
        double AreaH => ActualHeight > 0 ? ActualHeight : 298;
        double CenterX => AreaW / 2;
        double CenterY => AreaH / 2;
        double MinR => knobRadius + KnobMargin;
        double MaxR => faceRadius - EdgeMargin;

        /// <summary>돌아다닐 바닥의 반지름과, 피해 다닐 가운데 손잡이의 반지름.</summary>
        public void SetArena(double faceRadius, double knobRadius)
        {
            this.faceRadius = faceRadius;
            this.knobRadius = knobRadius;
            UpdateLayoutParts();
        }

        void UpdateLayoutParts()
        {
            // 바닥 바깥으로 삐져나온 꼬리 끝 등은 잘라 낸다
            Clip = new EllipseGeometry(new Point(CenterX, CenterY), faceRadius - 1, faceRadius - 1);
            LayoutProps();
        }

        /// <summary>소품을 바닥 위 정해진 자리에 놓고, 고양이가 쓸 자리를 다시 만든다.</summary>
        void LayoutProps()
        {
            foreach (var c in cats) Release(c);
            spots.Clear();

            // 자리는 바닥 중심에서 본 "소품 밑변 가운데"의 위치(바닥 반지름 100 기준)
            Point tower = PlaceProp(PropSprites.Tower, 62, 44);
            Point cushion = PlaceProp(PropSprites.Cushion, -46, 62);
            PlaceProp(PropSprites.Bowl, -60, -30);
            PlaceProp(PropSprites.Yarn, 30, -60);
            PlaceProp(PropSprites.Mouse, -74, 16);

            // 방석 안: 걸어 들어가 앉는다
            spots.Add(new Spot { X = cushion.X, Y = cushion.Y - 15, Z = (int)cushion.Y + 1 });

            // 캣타워 위: 기둥 왼쪽 바닥까지 걸어간 뒤 뛰어오른다
            double towerH = PropSprites.HeightOf(PropSprites.Tower) * PixelScale;
            spots.Add(new Spot
            {
                X = tower.X,
                Y = tower.Y - towerH + 4.5 - FeetOffset,
                HasEntry = true,
                EntryX = tower.X - 28,
                EntryY = tower.Y - FeetOffset + 1,
                Z = (int)tower.Y + 1,
            });
        }

        Point PlaceProp(int prop, double dx, double dy)
        {
            double k = faceRadius / 100.0;
            double bx = Math.Round(CenterX + dx * k), by = Math.Round(CenterY + dy * k);
            Image img = props[prop];
            SetLeft(img, Math.Round(bx - img.Width / 2));
            SetTop(img, by - img.Height);
            SetZIndex(img, (int)by);
            return new Point(bx, by);
        }

        /// <summary>저장된 고양이들을 불러와 바닥 위 아무 곳에나 풀어 놓는다.</summary>
        public void SetCats(IEnumerable<int> types)
        {
            foreach (var c in cats)
            {
                Release(c);
                Children.Remove(c.Img);
            }
            cats.Clear();
            var list = types.ToList();
            foreach (int t in list.Skip(Math.Max(0, list.Count - MaxVisible))) Add(t, false);
        }

        /// <summary>새 고양이 한 마리가 바닥 아래쪽에서 나타난다.</summary>
        public void Spawn(int type) => Add(type, true);

        void Add(int type, bool isNew)
        {
            if (cats.Count >= MaxVisible)
            {
                Release(cats[0]);
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

        /// <summary>비어 있는 방석·캣타워 중 손잡이를 가로지르지 않고 갈 수 있는 곳으로 출발한다.</summary>
        bool TryGoToSpot(Cat c)
        {
            foreach (Spot s in spots.OrderBy(_ => rng.Next()))
            {
                if (s.Owner != null) continue;
                double tx = s.HasEntry ? s.EntryX : s.X, ty = s.HasEntry ? s.EntryY : s.Y;
                if (DistanceToCenter(c.X, c.Y, tx, ty) < MinR - 3) continue;

                s.Owner = c;
                c.Spot = s;
                c.Flip.ScaleX = tx > c.X ? -1 : 1; // 기본 그림은 왼쪽을 본다
                if (s.HasEntry)
                {
                    c.Phase = ToEntry;
                    c.TX = tx; c.TY = ty;
                }
                else
                {
                    c.Phase = ToSpot;
                    c.TX = s.X + BodyOffset * c.Flip.ScaleX; c.TY = s.Y;
                }
                c.Walking = true;
                c.Anim = 0;
                return true;
            }
            return false;
        }

        void Release(Cat c)
        {
            if (c.Spot != null) c.Spot.Owner = null;
            c.Spot = null;
            c.Phase = Free;
        }

        /// <summary>선분 (x1,y1)-(x2,y2) 에서 바닥 중심까지의 최단 거리.</summary>
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
                if (c.Walking) StepWalking(c, dt);
                else StepResting(c, dt);
            }
        }

        void StepWalking(Cat c, double dt)
        {
            // 캣타워를 오르내리는 동안은 폴짝 뛴다
            bool jumping = c.Phase == Leaving || (c.Phase == ToSpot && c.Spot.HasEntry);
            double dx = c.TX - c.X, dy = c.TY - c.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double step = (jumping ? JumpSpeed : Speed) * dt;
            if (dist > step)
            {
                c.X += dx / dist * step;
                c.Y += dy / dist * step;
                c.Anim += dt;
                SetFrame(c, jumping ? CatSprites.FrameWalkB
                    : (int)(c.Anim / FrameSeconds) % 2 == 0 ? CatSprites.FrameWalkA : CatSprites.FrameWalkB);
                Place(c);
                return;
            }

            c.X = c.TX; c.Y = c.TY;
            c.Anim = 0;
            switch (c.Phase)
            {
                case ToEntry:
                    // 캣타워 밑에 도착: 위로 뛰어오른다
                    c.Phase = ToSpot;
                    c.Flip.ScaleX = c.Spot.X > c.X ? -1 : 1;
                    c.TX = c.Spot.X + BodyOffset * c.Flip.ScaleX;
                    c.TY = c.Spot.Y;
                    break;
                case ToSpot:
                    c.Phase = OnSpot;
                    c.Walking = false;
                    c.Timer = 5 + rng.NextDouble() * 7;
                    SetFrame(c, CatSprites.FrameSit);
                    break;
                case Leaving:
                    // 캣타워에서 내려왔다: 잠깐 서 있다가 다시 돌아다닌다
                    Release(c);
                    c.Walking = false;
                    c.Timer = 0.5;
                    SetFrame(c, CatSprites.FrameWalkA);
                    break;
                default:
                    c.Walking = false;
                    c.Timer = 1.5 + rng.NextDouble() * 5.5;
                    bool stand = c.Type != CatSprites.Lucky && rng.Next(3) == 0;
                    SetFrame(c, stand ? CatSprites.FrameWalkA : CatSprites.FrameSit);
                    break;
            }
            Place(c);
        }

        void StepResting(Cat c, double dt)
        {
            c.Timer -= dt;
            if (c.Type == CatSprites.Lucky && (c.Frame == CatSprites.FrameSit || c.Frame == CatSprites.FrameSitB))
            {
                // 손짓: 앞발을 올렸다 내렸다
                c.Anim += dt;
                SetFrame(c, (int)(c.Anim / WaveSeconds) % 2 == 0 ? CatSprites.FrameSit : CatSprites.FrameSitB);
            }
            if (c.Timer > 0) return;

            if (c.Phase == OnSpot)
            {
                if (c.Spot.HasEntry)
                {
                    // 캣타워에서 뛰어내린다
                    c.Phase = Leaving;
                    c.TX = c.Spot.EntryX; c.TY = c.Spot.EntryY;
                    c.Flip.ScaleX = c.TX > c.X ? -1 : 1;
                    c.Walking = true;
                    c.Anim = 0;
                    return;
                }
                Release(c); // 방석에서는 그냥 걸어 나온다
            }
            else if (rng.Next(100) < SpotChance && TryGoToSpot(c))
            {
                return;
            }

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
            // 발이 아래쪽에 있는 것이 앞에 보이게. 방석·캣타워에 있는 고양이는 그 소품보다 앞에 그린다.
            int z = (int)(c.Y + FeetOffset);
            if (c.Spot != null && c.Phase >= ToSpot) z = Math.Max(z, c.Spot.Z);
            SetZIndex(c.Img, z);
        }
    }
}
