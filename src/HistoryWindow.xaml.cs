using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Catodoro
{
    /// <summary>
    /// 집중 기록 창: 요약 숫자(오늘 / 최근 7일 / 전체), 최근 7일 막대그래프, 최근 기록 목록.
    /// "집중 시간"에는 끝까지 채운 회차만 더한다(도중에 그만둔 회차는 목록에만 보인다).
    /// </summary>
    public partial class HistoryWindow : Window
    {
        const int ChartDays = 7;
        const int RecentCount = 100;
        const double BarWidth = 20;      // 막대는 가늘게(24px 이하)
        const double PlotHeight = 82;    // 막대가 자랄 수 있는 높이
        const double DayRowHeight = 22;  // 요일 글자 줄 높이

        static readonly Brush Ink = Solid(0x5A, 0x44, 0x32);
        static readonly Brush Muted = Solid(0x8A, 0x75, 0x60);
        static readonly Brush BarFill = Solid(0x6F, 0x9F, 0x48);
        static readonly Brush Line = Solid(0xE8, 0xD8, 0xBC);
        static readonly Brush CardFill = Solid(0xFF, 0xFB, 0xF2);

        static Brush Solid(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public HistoryWindow()
        {
            InitializeComponent();
            Icon = AssetExporter.RenderIcon(64);

            Title = Lang.T("hist.window");
            HeadingText.Text = Lang.T("hist.heading");
            ChartTitle.Text = Lang.T("hist.chart");
            RecentTitle.Text = Lang.T("hist.recent");
            NoteText.Text = Lang.T("hist.note");
            CloseDialogButton.Content = Lang.T("hist.close");

            List<SessionRecord> history = Store.Data.History;
            DateTime today = DateTime.Today;

            BuildTiles(history, today);
            BuildChart(history, today);
            BuildRecent(history);
        }

        static int FocusSeconds(IEnumerable<SessionRecord> records) => records.Where(r => r.Completed).Sum(r => r.Seconds);
        static int Successes(IEnumerable<SessionRecord> records) => records.Count(r => r.Result == SessionRecord.Done);

        // ───────── 요약 숫자 ─────────

        void BuildTiles(List<SessionRecord> history, DateTime today)
        {
            var todayRecords = history.Where(r => r.StartTime.Date == today).ToList();
            var weekRecords = history.Where(r => r.StartTime.Date > today.AddDays(-ChartDays) && r.StartTime.Date <= today).ToList();

            Tiles.Children.Add(Tile(Lang.T("hist.today"), todayRecords));
            Tiles.Children.Add(Tile(Lang.T("hist.week"), weekRecords));
            Tiles.Children.Add(Tile(Lang.T("hist.all"), history));
        }

        static Border Tile(string label, List<SessionRecord> records)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 });
            panel.Children.Add(new TextBlock
            {
                Text = Lang.Duration(FocusSeconds(records)),
                Foreground = Ink, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2),
            });
            panel.Children.Add(new TextBlock { Text = Lang.T("hist.success", Successes(records)), Foreground = Muted, FontSize = 11.5 });
            return new Border
            {
                Background = CardFill, BorderBrush = Line, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(4, 0, 4, 0),
                Child = panel,
            };
        }

        // ───────── 최근 7일 막대그래프 ─────────

        void BuildChart(List<SessionRecord> history, DateTime today)
        {
            var days = Enumerable.Range(0, ChartDays).Select(i => today.AddDays(i - (ChartDays - 1))).ToList();
            var seconds = days.Select(d => FocusSeconds(history.Where(r => r.StartTime.Date == d))).ToList();
            var successes = days.Select(d => Successes(history.Where(r => r.StartTime.Date == d))).ToList();
            int max = Math.Max(1, seconds.Max());
            int maxIndex = seconds.IndexOf(seconds.Max());

            for (int i = 0; i < ChartDays; i++)
            {
                Chart.ColumnDefinitions.Add(new ColumnDefinition());
                bool isToday = i == ChartDays - 1;

                // 칸 전체가 마우스 대상(막대보다 넓게) — 올리면 그날의 자세한 숫자가 보인다
                var column = new Grid
                {
                    Background = Brushes.Transparent,
                    ToolTip = Lang.T("hist.bar.tip", Lang.DayLabel(days[i]), Lang.Duration(seconds[i]), successes[i]),
                };
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });          // 값 표시
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PlotHeight) });  // 막대
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });           // 바닥선
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DayRowHeight) }); // 요일

                double height = seconds[i] <= 0 ? 0 : Math.Max(3, PlotHeight * seconds[i] / max);

                // 값은 오늘과 가장 긴 날에만 직접 적는다(나머지는 마우스를 올리면 보인다)
                if (seconds[i] > 0 && (isToday || i == maxIndex))
                {
                    // 글자가 칸보다 넓을 수 있으므로 이웃 칸까지 걸쳐 놓는다(양 끝 칸은 안쪽으로 붙인다)
                    int first = Math.Max(0, i - 1), last = Math.Min(ChartDays - 1, i + 1);
                    var value = new TextBlock
                    {
                        Text = Lang.Duration(seconds[i]), Foreground = Ink, FontSize = 10.5,
                        IsHitTestVisible = false,
                        HorizontalAlignment = i == 0 ? HorizontalAlignment.Left
                            : i == ChartDays - 1 ? HorizontalAlignment.Right : HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        // 막대 꼭대기 바로 위: 요일 줄 + 바닥선 + 막대 높이만큼 아래에서 띄운다
                        Margin = new Thickness(2, 0, 2, DayRowHeight + 1 + height + 2),
                    };
                    Grid.SetColumn(value, first);
                    Grid.SetColumnSpan(value, last - first + 1);
                    Panel.SetZIndex(value, 1);
                    Chart.Children.Add(value);
                }

                if (height > 0)
                {
                    var bar = new Border
                    {
                        Width = BarWidth, Height = height, Background = BarFill,
                        CornerRadius = new CornerRadius(4, 4, 0, 0), // 위쪽만 둥글게, 바닥은 반듯하게
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                    };
                    Grid.SetRow(bar, 1);
                    column.Children.Add(bar);
                }

                var baseline = new Border { Background = Line };
                Grid.SetRow(baseline, 2);
                column.Children.Add(baseline);

                var day = new TextBlock
                {
                    Text = Lang.Weekday(days[i].DayOfWeek),
                    Foreground = isToday ? Ink : Muted,
                    FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
                    FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0),
                };
                Grid.SetRow(day, 3);
                column.Children.Add(day);

                Grid.SetColumn(column, i);
                Chart.Children.Add(column);
            }
        }

        // ───────── 최근 기록 목록 ─────────

        void BuildRecent(List<SessionRecord> history)
        {
            bool empty = history.Count == 0;
            EmptyText.Text = Lang.T("hist.empty");
            EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            RecentScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

            foreach (SessionRecord r in Enumerable.Reverse(history).Take(RecentCount))
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
                row.ColumnDefinitions.Add(new ColumnDefinition());

                AddCell(row, 0, r.StartTime.ToString("M'/'d  HH:mm", System.Globalization.CultureInfo.InvariantCulture), Muted);
                AddCell(row, 1, Lang.Duration(r.Seconds), Ink);
                AddCell(row, 2, ResultText(r), r.Result == SessionRecord.Done ? Ink : Muted);

                RecentList.Children.Add(row);
                RecentList.Children.Add(new Border { Height = 1, Background = Line });
            }
        }

        static string ResultText(SessionRecord r)
        {
            switch (r.Result)
            {
                case SessionRecord.Done: return Lang.T("hist.r.done", Lang.CatName(Math.Max(0, r.Cat)));
                case SessionRecord.Blocked: return Lang.T("hist.r.blocked", r.Violations);
                default: return Lang.T("hist.r.stopped");
            }
        }

        static void AddCell(Grid row, int column, string text, Brush brush)
        {
            var cell = new TextBlock { Text = text, Foreground = brush, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(cell, column);
            row.Children.Add(cell);
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
