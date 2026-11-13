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
        readonly Options opt;
        readonly DispatcherTimer tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        readonly Blocker blocker = new Blocker();
        readonly Random rng = new Random();

        bool running;        // 집중 시간이 돌아가는 중인가
        int setMinutes;      // 다이얼로 맞춰 둔 분
        DateTime endUtc;     // 집중이 끝나는 시각
        int violations;      // 이번 집중에서 차단 대상에 접근한 횟수
        DateTime sessionStart; // 이번 집중을 시작한 시각(기록용)
        int sessionSeconds;    // 이번 집중에 맞춰 둔 길이(초)
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
            Dial.KnobClicked += OnKnobClicked;
            Cats.SetArena(Dial.FaceRadius, Dial.KnobRadius);

            tick.Tick += (s, e) => OnTick();
            blocker.Violation += what => Dispatcher.BeginInvoke(new Action(() => OnViolation(what)));

            RestorePosition();
            Cats.SetCats(Store.Data.Cats);
            ApplyLanguage();
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
            sessionStart = DateTime.Now;
            sessionSeconds = (int)Math.Round(length.TotalSeconds);
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
                int type = PickCatType();
                Store.Data.Cats.Add(type);
                Store.Save();
                Cats.Spawn(type);
                Store.Log("cat spawned: " + Lang.CatName(type));
                Store.AddHistory(sessionStart, sessionSeconds, sessionSeconds, SessionRecord.Done, 0, type);
                ShowCatArrived(type, Store.Data.Cats.Count);
            }
            else
            {
                Store.AddHistory(sessionStart, sessionSeconds, sessionSeconds, SessionRecord.Blocked, count, -1);
                ShowOverlay(null, Lang.T("fail.title"), Lang.T("fail.sub", count), Lang.T("ok"), null, null, null);
            }
            SystemSounds.Asterisk.Play();
        }

        /// <summary>평소에는 4종 중 하나, 가끔(LuckyChance) 행운의 고양이.</summary>
        int PickCatType()
        {
            if (opt.ForceLucky || rng.NextDouble() < CatSprites.LuckyChance) return CatSprites.Lucky;
            return rng.Next(CatSprites.NormalTypeCount);
        }

        void ShowCatArrived(int type, int total)
        {
            bool lucky = type == CatSprites.Lucky;
            ShowOverlay(CatSprites.Get(type, CatSprites.FrameSit),
                Lang.T(lucky ? "lucky.title" : "cat.title"),
                Lang.T(lucky ? "lucky.sub" : "cat.sub", Lang.CatName(type), total),
                Lang.T("cat.ok"), null, null, null);
        }

        void ConfirmGiveUp(Action afterGiveUp)
        {
            ShowOverlay(null,
                Lang.T("giveup.title"),
                Lang.T("giveup.sub"),
                Lang.T("giveup.keep"), null,
                Lang.T("giveup.stop"), () =>
                {
                    Store.Log("session abandoned");
                    int elapsed = (int)Math.Min(sessionSeconds, (DateTime.Now - sessionStart).TotalSeconds);
                    Store.AddHistory(sessionStart, elapsed, sessionSeconds, SessionRecord.Stopped, violations, -1);
                    StopSession();
                    if (afterGiveUp != null) afterGiveUp();
                    else Toast(Lang.T("toast.stopped"));
                });
        }

        void OnViolation(string what)
        {
            if (!running) return;
            violations++;
            UpdateUi();
            Toast(Lang.T("toast.blocked", what));
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

        /// <summary>고정 문구(툴팁)를 현재 언어로 바꾼다. 나머지 문구는 보여 줄 때마다 Lang 에서 가져온다.</summary>
        void ApplyLanguage()
        {
            SettingsButton.ToolTip = Lang.T("tip.settings");
            HistoryButton.ToolTip = Lang.T("tip.history");
            MinimizeButton.ToolTip = Lang.T("tip.minimize");
            CloseButton.ToolTip = Lang.T("tip.close");
        }

        void UpdateUi()
        {
            UpdateTimeText();
            SettingsButton.IsEnabled = !running;
            Dial.IsRunning = running; // 손잡이 아이콘: ▶ 시작 / ■ 그만두기

            Color led = !running ? Theme.LedIdle : (violations > 0 ? Theme.LedBad : Theme.LedOn);
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

        /// <summary>다이얼 가운데 손잡이: 쉬는 중이면 시작, 집중 중이면 그만둘지 묻는다.</summary>
        void OnKnobClicked()
        {
            if (OverlayOpen) return;
            if (running) ConfirmGiveUp(null);
            else if (setMinutes <= 0) Toast(Lang.T("toast.setTime"));
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
            ApplyLanguage(); // 설정 창에서 언어를 바꿨을 수 있다
        }

        void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (OverlayOpen) return;
            new HistoryWindow { Owner = this, Topmost = Topmost }.ShowDialog();
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
                    ShowCatArrived(CatSprites.Tabby, 1);
                    break;
                case "lucky":
                    ShowCatArrived(CatSprites.Lucky, 7);
                    break;
                case "fail":
                    ShowOverlay(null, Lang.T("fail.title"), Lang.T("fail.sub", 2), Lang.T("ok"), null, null, null);
                    break;
                case "giveup":
                    ConfirmGiveUp(null);
                    break;
                case "toast":
                    Toast(Lang.T("toast.blocked", "youtube.com"));
                    break;
                case "knob":
                    OnKnobClicked(); // 가운데 손잡이를 누른 것과 같다
                    break;
                case "history":
                    var hist = new HistoryWindow();
                    hist.Show();
                    After(500, () =>
                    {
                        if (opt.Screenshot != null)
                            AssetExporter.SaveVisual((FrameworkElement)hist.Content, opt.Screenshot + ".history.png");
                        hist.Close();
                    });
                    break;
                case "giveup-now":
                    // 그만두기 확인에서 "그만두기"를 누른 것과 같다(기록 저장 점검용)
                    After(Math.Max(0, opt.ShotDelayMs - 300), () => { ConfirmGiveUp(null); OverlaySecondary_Click(this, new RoutedEventArgs()); });
                    break;
                case "settings":
                case "settings-switch":
                    var dlg = new SettingsWindow();
                    dlg.Show();
                    if (opt.Overlay == "settings-switch")
                    {
                        // 설정 창의 언어 선택을 지금과 반대 언어로 바꿔 본다
                        string other = Lang.IsEnglish ? Lang.Korean : Lang.English;
                        foreach (System.Windows.Controls.ComboBoxItem item in dlg.LanguageBox.Items)
                            if ((string)item.Tag == other) dlg.LanguageBox.SelectedItem = item;
                        ApplyLanguage();
                        Store.Log("language switched to " + Lang.Current + ", close tooltip = " + CloseButton.ToolTip);
                    }
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
                    AssetExporter.SaveVisual(Root, opt.Screenshot, opt.ShotScale);
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
