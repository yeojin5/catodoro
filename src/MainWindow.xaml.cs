using System;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Catodoro
{
    public partial class MainWindow : Window
    {
        static readonly Color LedIdle = Color.FromRgb(0xCB, 0xD3, 0xC8);
        static readonly Color LedOn = Color.FromRgb(0x35, 0xD0, 0x5A);
        static readonly Color LedBad = Color.FromRgb(0xF0, 0x52, 0x4F);

        readonly Options opt;
        readonly DispatcherTimer tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        readonly Blocker blocker = new Blocker();
        readonly Random rng = new Random();

        bool running;        // 집중 시간이 돌아가는 중인가
        int setMinutes;      // 다이얼로 맞춰 둔 분
        DateTime endUtc;     // 집중이 끝나는 시각
        int violations;      // 이번 집중에서 차단 대상에 접근한 횟수
        bool forceClose;
        Action overlayPrimaryAction, overlaySecondaryAction;

        public MainWindow(Options opt)
        {
            InitializeComponent();
            this.opt = opt;

            Icon = AssetExporter.RenderIcon(64);
            Topmost = Store.Data.Topmost;
            setMinutes = Math.Max(0, Math.Min(60, opt.Minutes ?? Store.Data.LastMinutes));
            Dial.Minutes = setMinutes;
            Dial.UserSetMinutes += m => { setMinutes = m; UpdateTimeText(); };

            tick.Tick += (s, e) => OnTick();
            blocker.Violation += what => Dispatcher.BeginInvoke(new Action(() => OnViolation(what)));

            RestorePosition();
            Cats.SetCats(Store.Data.Cats);
            UpdateUi();

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        // ───────────────────────── 집중 시간 ─────────────────────────

        void StartSession(TimeSpan length)
        {
            running = true;
            violations = 0;
            endUtc = DateTime.UtcNow + length;
            Dial.IsLocked = true;

            Store.Data.LastMinutes = setMinutes;
            Store.Save();
            Store.Log("session start " + length.TotalSeconds + "s");

            blocker.Start(Store.Data.Programs, Store.Data.Sites);
            tick.Start();
            UpdateUi();
            OnTick();
        }

        void StopSession()
        {
            running = false;
            tick.Stop();
            blocker.Stop();
            Dial.IsLocked = false;
            Dial.Minutes = setMinutes;
            UpdateUi();
        }

        void OnTick()
        {
            if (!running) return;
            TimeSpan left = endUtc - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                Finish();
                return;
            }
            Dial.Minutes = left.TotalMinutes;
            UpdateTimeText();
        }

        void Finish()
        {
            int count = violations;
            StopSession();
            Store.Log("session finished, violations=" + count);

            if (count == 0)
            {
                int type = rng.Next(CatSprites.TypeCount);
                Store.Data.Cats.Add(type);
                Store.Save();
                Cats.Spawn(type);
                Store.Log("cat spawned: " + CatSprites.Names[type]);
                ShowOverlay(CatSprites.Get(type, CatSprites.FrameSit),
                    "집중 성공!",
                    $"새 고양이 '{CatSprites.Names[type]}'가 놀러 왔어요.\n지금까지 모은 고양이: {Store.Data.Cats.Count}마리",
                    "좋아요", null, null, null);
            }
            else
            {
                ShowOverlay(null,
                    "고양이가 오지 않았어요",
                    $"집중 시간 동안 차단 목록에 {count}번 접근했어요.\n다음에는 끝까지 참아 봐요!",
                    "확인", null, null, null);
            }
            SystemSounds.Asterisk.Play();
        }

        void ConfirmGiveUp(Action afterGiveUp)
        {
            ShowOverlay(null,
                "정말 그만둘까요?",
                "지금 멈추면 이번 고양이는 오지 않아요.",
                "계속 집중", null,
                "그만두기", () =>
                {
                    Store.Log("session abandoned");
                    StopSession();
                    if (afterGiveUp != null) afterGiveUp();
                    else Toast("집중을 중단했어요");
                });
        }

        void OnViolation(string what)
        {
            if (!running) return;
            violations++;
            UpdateUi();
            Toast($"'{what}' 차단!\n이번에는 고양이가 오지 않아요");
            SystemSounds.Hand.Play();
        }

        // ───────────────────────── 화면 갱신 ─────────────────────────

        void UpdateTimeText()
        {
            if (running)
            {
                int s = (int)Math.Ceiling(Math.Max(0, (endUtc - DateTime.UtcNow).TotalSeconds));
                TimeText.Text = $"{s / 60:00}:{s % 60:00}";
            }
            else
            {
                TimeText.Text = $"{setMinutes:00}:00";
            }
        }

        void UpdateUi()
        {
            UpdateTimeText();
            SettingsButton.IsEnabled = !running;
            TopButton.ToolTip = running ? "그만두기" : "집중 시작";

            // 집중 중에는 버튼이 눌린 채로 내려가 있다
            TopButtonShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(running ? 8 : 0, TimeSpan.FromMilliseconds(140)));

            Color led = !running ? LedIdle : (violations > 0 ? LedBad : LedOn);
            Led.Fill = new SolidColorBrush(led);
            LedGlow.Color = led;
            if (running)
            {
                var pulse = new DoubleAnimation(0.95, 0.35, TimeSpan.FromSeconds(1.1))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                LedGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulse);
            }
            else
            {
                LedGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
                LedGlow.Opacity = 0;
            }
        }

        void Toast(string message)
        {
            ToastText.Text = message;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3200))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3700))));
            ToastBox.BeginAnimation(OpacityProperty, anim);
        }

        void ShowOverlay(BitmapSource image, string title, string sub,
            string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            OverlayImage.Source = image;
            OverlayImage.Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;
            OverlayTitle.Text = title;
            OverlaySub.Text = sub;
            OverlayPrimary.Content = primary;
            OverlaySecondary.Content = secondary;
            OverlaySecondary.Visibility = secondary == null ? Visibility.Collapsed : Visibility.Visible;
            overlayPrimaryAction = onPrimary;
            overlaySecondaryAction = onSecondary;
            OverlayBox.Visibility = Visibility.Visible;
            OverlayBox.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        }

        void HideOverlay()
        {
            OverlayBox.BeginAnimation(OpacityProperty, null);
            OverlayBox.Visibility = Visibility.Collapsed;
        }

        bool OverlayOpen => OverlayBox.Visibility == Visibility.Visible;

        // ───────────────────────── 입력 ─────────────────────────

        void TopButton_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (OverlayOpen) return;
            if (running) ConfirmGiveUp(null);
            else if (setMinutes <= 0) Toast("다이얼을 돌려 시간을 맞춰 주세요");
            else StartSession(TimeSpan.FromMinutes(setMinutes));
        }

        void Body_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        }

        void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (running || OverlayOpen) return;
            var dlg = new SettingsWindow { Owner = this, Topmost = Topmost };
            dlg.ShowDialog();
            Topmost = Store.Data.Topmost;
        }

        void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        void OverlayPrimary_Click(object sender, RoutedEventArgs e)
        {
            Action a = overlayPrimaryAction;
            HideOverlay();
            a?.Invoke();
        }

        void OverlaySecondary_Click(object sender, RoutedEventArgs e)
        {
            Action a = overlaySecondaryAction;
            HideOverlay();
            a?.Invoke();
        }

        // ───────────────────────── 창 열고 닫기 ─────────────────────────

        void RestorePosition()
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Rect work = SystemParameters.WorkArea;
            double left = Store.Data.Left ?? work.Right - Width - 24;
            double top = Store.Data.Top ?? work.Bottom - Height - 24;

            // 모니터 구성이 바뀌어 화면 밖에 놓이는 일을 막는다
            double vx = SystemParameters.VirtualScreenLeft, vy = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            Left = Math.Max(vx, Math.Min(vx + vw - Width, left));
            Top = Math.Max(vy, Math.Min(vy + vh - Height, top));
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (running && !forceClose)
            {
                e.Cancel = true;
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                if (!OverlayOpen)
                    ConfirmGiveUp(() => { forceClose = true; Close(); });
                return;
            }
            blocker.Stop();
            if (!opt.IsTest && WindowState == WindowState.Normal)
            {
                Store.Data.Left = Left;
                Store.Data.Top = Top;
            }
            Store.Data.LastMinutes = setMinutes;
            Store.Save();
        }

        // ───────────────────────── 개발·점검용 옵션 ─────────────────────────

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (opt.AddCats is int n)
                Cats.SetCats(System.Linq.Enumerable.Range(0, n).Select(i => i % CatSprites.TypeCount));

            if (opt.AutoStartSeconds is int secs)
                StartSession(TimeSpan.FromSeconds(secs));

            switch (opt.Overlay)
            {
                case "success":
                    ShowOverlay(CatSprites.Get(CatSprites.Tabby, CatSprites.FrameSit), "집중 성공!",
                        "새 고양이 '치즈'가 놀러 왔어요.\n지금까지 모은 고양이: 1마리", "좋아요", null, null, null);
                    break;
                case "fail":
                    ShowOverlay(null, "고양이가 오지 않았어요",
                        "집중 시간 동안 차단 목록에 2번 접근했어요.\n다음에는 끝까지 참아 봐요!", "확인", null, null, null);
                    break;
                case "giveup":
                    ConfirmGiveUp(null);
                    break;
                case "toast":
                    Toast("'youtube.com' 차단!\n이번에는 고양이가 오지 않아요");
                    break;
                case "settings":
                    var dlg = new SettingsWindow();
                    dlg.Show();
                    After(500, () =>
                    {
                        if (opt.Screenshot != null)
                            AssetExporter.SaveVisual((FrameworkElement)dlg.Content, opt.Screenshot + ".settings.png");
                        dlg.Close();
                    });
                    break;
                case "dialtest":
                    // 다이얼을 (각도) 순서대로 끌었을 때 몇 분이 되는지 기록한다
                    double[] angles = { 120, 200, 350, 10, 40, 350, 300, 5, 350, 180 };
                    for (int i = 0; i < angles.Length; i++)
                    {
                        double a = angles[i] * Math.PI / 180;
                        var p = new Point(Dial.ActualWidth / 2 + 80 * Math.Sin(a), Dial.ActualHeight / 2 - 80 * Math.Cos(a));
                        Dial.UpdateFromPoint(p, i == 0);
                        Store.Log($"dial {angles[i]}deg -> {Dial.Minutes} (set={setMinutes}, text={TimeText.Text})");
                    }
                    break;
            }

            if (opt.Screenshot != null)
            {
                After(opt.ShotDelayMs, () =>
                {
                    AssetExporter.SaveVisual(Root, opt.Screenshot);
                    if (opt.ExitAfterMs == null) { forceClose = true; Close(); }
                });
            }
            if (opt.ExitAfterMs is int ms)
                After(ms, () => { forceClose = true; Close(); });
        }

        void After(int ms, Action action)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (s, e) => { t.Stop(); action(); };
            t.Start();
        }
    }
}
