using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Catodoro
{
    public partial class SettingsWindow : Window
    {
        bool loading = true;

        public SettingsWindow()
        {
            InitializeComponent();
            Icon = AssetExporter.RenderIcon(64);
            TopmostCheck.IsChecked = Store.Data.Topmost;
            ProgramInput.ItemsSource = RunningApps();

            foreach (ComboBoxItem item in LanguageBox.Items)
                if ((string)item.Tag == Lang.Current) LanguageBox.SelectedItem = item;
            loading = false;

            ApplyLanguage();
            Refresh();
        }

        /// <summary>창의 모든 글자를 현재 언어로 채운다.</summary>
        void ApplyLanguage()
        {
            Title = Lang.T("set.window");
            HeadingText.Text = Lang.T("set.heading");
            LanguageLabel.Text = Lang.T("set.language");
            IntroText.Text = Lang.T("set.intro");

            ProgramsTab.Header = Lang.T("set.tab.programs");
            SitesTab.Header = Lang.T("set.tab.sites");
            ProgramInput.ToolTip = Lang.T("set.program.tip");
            ProgramHint.Text = Lang.T("set.program.hint");
            SiteHint.Text = Lang.T("set.site.hint");

            AddProgramButton.Content = Lang.T("set.add");
            AddSiteButton.Content = Lang.T("set.add");
            BrowseButton.Content = Lang.T("set.browse");
            RemoveProgramButton.Content = Lang.T("set.remove");
            RemoveSiteButton.Content = Lang.T("set.remove");

            TopmostCheck.Content = Lang.T("set.topmost");
            CloseDialogButton.Content = Lang.T("set.close");
            CatCountText.Text = Lang.T("set.cats", Store.Data.Cats.Count);
        }

        void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loading || !(LanguageBox.SelectedItem is ComboBoxItem item)) return;
            Store.Data.Language = (string)item.Tag;
            Store.Save();
            Lang.Override = null; // 직접 고른 언어가 우선
            ApplyLanguage();
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

        void Info(string key)
        {
            MessageBox.Show(this, Lang.T(key), "CaToDoRo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ───────── 프로그램 ─────────

        void AddProgram(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            if (Store.IsProtectedProgram(input))
            {
                Info("set.msg.protected");
                return;
            }
            string name = Store.NormalizeProgram(input);
            if (name == null)
            {
                Info("set.msg.badProgram");
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
            var dlg = new OpenFileDialog { Filter = Lang.T("set.browse.filter"), Title = Lang.T("set.browse.title") };
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
                Info("set.msg.badSite");
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
