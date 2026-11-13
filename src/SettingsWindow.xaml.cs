using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Catodoro
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Icon = AssetExporter.RenderIcon(64);
            TopmostCheck.IsChecked = Store.Data.Topmost;
            CatCountText.Text = $"모은 고양이 {Store.Data.Cats.Count}마리";
            ProgramInput.ItemsSource = RunningApps();
            Refresh();
        }

        void Refresh()
        {
            ProgramList.ItemsSource = Store.Data.Programs.Select(p => p + ".exe").ToList();
            SiteList.ItemsSource = Store.Data.Sites.ToList();
        }

        /// <summary>창이 떠 있는(= 사용자가 쓰는) 프로그램들의 exe 이름.</summary>
        static List<string> RunningApps()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle)
                        && Store.NormalizeProgram(p.ProcessName) != null)
                        names.Add(p.ProcessName + ".exe");
                }
                catch { }
                finally { p.Dispose(); }
            }
            return names.ToList();
        }

        // ───────── 프로그램 ─────────

        void AddProgram(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            if (Store.IsProtectedProgram(input))
            {
                MessageBox.Show(this, "윈도우 동작에 꼭 필요한 프로그램이라 차단 목록에 넣을 수 없어요.",
                    "Catodoro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string name = Store.NormalizeProgram(input);
            if (name == null)
            {
                MessageBox.Show(this, "프로그램 이름을 알아볼 수 없어요. 예: KakaoTalk.exe",
                    "Catodoro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!Store.Data.Programs.Contains(name))
            {
                Store.Data.Programs.Add(name);
                Store.Save();
                Refresh();
            }
            ProgramInput.Text = "";
        }

        void AddProgram_Click(object sender, RoutedEventArgs e) => AddProgram(ProgramInput.Text);

        void ProgramInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { AddProgram(ProgramInput.Text); e.Handled = true; }
        }

        void BrowseProgram_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "프로그램 (*.exe)|*.exe", Title = "차단할 프로그램 선택" };
            if (dlg.ShowDialog(this) == true) AddProgram(dlg.FileName);
        }

        void RemoveProgram_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramList.SelectedItem is string item)
            {
                Store.Data.Programs.Remove(Store.NormalizeProgram(item) ?? item);
                Store.Save();
                Refresh();
            }
        }

        // ───────── 사이트 ─────────

        void AddSite()
        {
            if (string.IsNullOrWhiteSpace(SiteInput.Text)) return;
            string site = Store.NormalizeSite(SiteInput.Text);
            if (site == null)
            {
                MessageBox.Show(this, "사이트 주소를 알아볼 수 없어요. 예: youtube.com",
                    "Catodoro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!Store.Data.Sites.Contains(site))
            {
                Store.Data.Sites.Add(site);
                Store.Save();
                Refresh();
            }
            SiteInput.Text = "";
        }

        void AddSite_Click(object sender, RoutedEventArgs e) => AddSite();

        void SiteInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { AddSite(); e.Handled = true; }
        }

        void RemoveSite_Click(object sender, RoutedEventArgs e)
        {
            if (SiteList.SelectedItem is string item)
            {
                Store.Data.Sites.Remove(item);
                Store.Save();
                Refresh();
            }
        }

        // ───────── 기타 ─────────

        void TopmostCheck_Click(object sender, RoutedEventArgs e)
        {
            Store.Data.Topmost = TopmostCheck.IsChecked == true;
            Store.Save();
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
