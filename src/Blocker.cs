using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace Catodoro
{
    /// <summary>
    /// 집중 시간 동안 차단 목록을 지킨다.
    ///  - 프로그램: 목록에 있는 프로세스가 떠 있으면 종료한다.
    ///  - 사이트: 브라우저 창에 열린 페이지 주소를 읽어(UI 자동화, 관리자 권한 불필요)
    ///            목록의 도메인이면 그 탭을 닫는다.
    /// 집중을 시작한 "뒤에" 새로 열려고 한 경우에만 Violation 이벤트를 낸다.
    /// </summary>
    public sealed class Blocker
    {
        static readonly HashSet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "firefox", "brave", "whale", "opera", "opera_gx", "vivaldi",
        };

        /// <summary>차단 대상에 접근하려 했을 때(작업 스레드에서 호출됨). 인자는 화면에 보여 줄 이름.</summary>
        public event Action<string> Violation;

        readonly object gate = new object();
        readonly Dictionary<string, DateTime> lastReport = new Dictionary<string, DateTime>();
        readonly int selfPid = Process.GetCurrentProcess().Id;

        Thread thread;
        volatile bool running;
        string[] programs = new string[0];
        string[] sites = new string[0];
        DateTime startLocal;
        bool initialSweepDone;

        public void Start(IEnumerable<string> programList, IEnumerable<string> siteList)
        {
            Stop();
            lock (gate)
            {
                programs = programList.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();
                sites = siteList.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
                lastReport.Clear();
                startLocal = DateTime.Now;
                initialSweepDone = false;
                if (programs.Length == 0 && sites.Length == 0) return;
                running = true;
                thread = new Thread(Loop) { IsBackground = true, Name = "Catodoro.Blocker" };
                thread.SetApartmentState(ApartmentState.MTA);
                thread.Start();
            }
        }

        public void Stop()
        {
            Thread t;
            lock (gate)
            {
                running = false;
                t = thread;
                thread = null;
            }
            if (t != null && t != Thread.CurrentThread) t.Join(3000);
        }

        void Loop()
        {
            while (running)
            {
                try { CheckPrograms(); } catch (Exception ex) { Store.Log("program check error: " + ex.Message); }
                try { CheckSites(); } catch (Exception ex) { Store.Log("site check error: " + ex.Message); }
                for (int i = 0; i < 7 && running; i++) Thread.Sleep(100);
            }
        }

        void Report(string what)
        {
            DateTime now = DateTime.UtcNow;
            if (lastReport.TryGetValue(what, out DateTime prev) && (now - prev).TotalSeconds < 5) return;
            lastReport[what] = now;
            Store.Log("violation: " + what);
            Violation?.Invoke(what);
        }

        // ───────────────────────── 프로그램 ─────────────────────────

        void CheckPrograms()
        {
            foreach (string name in programs)
            {
                Process[] found;
                try { found = Process.GetProcessesByName(name); }
                catch { continue; }

                bool openedDuringFocus = false;
                foreach (Process p in found)
                {
                    try
                    {
                        if (p.Id == selfPid) continue;

                        DateTime started;
                        try { started = p.StartTime; } catch { started = DateTime.MinValue; }
                        bool fresh = started > startLocal.AddSeconds(1);
                        if (fresh) openedDuringFocus = true;

                        // 집중 시작 전에 이미 열려 있던 창은 처음 몇 초 동안 정상 종료를 먼저 요청한다
                        bool polite = !fresh && (DateTime.Now - startLocal).TotalSeconds < 4 && p.MainWindowHandle != IntPtr.Zero;
                        if (polite) p.CloseMainWindow();
                        else p.Kill();
                        Store.Log((polite ? "close requested: " : "killed: ") + name + " pid=" + p.Id);
                    }
                    catch
                    {
                        // 이미 꺼졌거나 권한이 없는 프로세스
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
                if (openedDuringFocus) Report(name + ".exe");
            }
        }

        // ───────────────────────── 사이트 ─────────────────────────

        void CheckSites()
        {
            if (sites.Length == 0) return;

            // 집중을 시작할 때 이미 열려 있던 탭은 닫기만 하고 접근으로 세지 않는다.
            // (차단 탭이 하나도 안 보이는 첫 순간, 늦어도 15초 뒤부터는 접근으로 센다)
            if (!initialSweepDone && (DateTime.Now - startLocal).TotalSeconds > 15) initialSweepDone = true;

            bool anyHit = false;
            foreach (IntPtr hwnd in BrowserWindows())
            {
                if (!running) return;
                string host = HostOf(ReadUrl(hwnd));
                string hit = MatchSite(host, sites);
                if (hit == null) continue;

                anyHit = true;
                string how = CloseActiveTab(hwnd);
                Store.Log("blocked site " + host + " via " + how + (initialSweepDone ? "" : " (already open)"));
                if (initialSweepDone) Report(hit);
            }
            if (!anyHit) initialSweepDone = true;
        }

        public static string MatchSite(string host, IEnumerable<string> siteList)
        {
            if (string.IsNullOrEmpty(host)) return null;
            foreach (string s in siteList)
                if (host == s || host.EndsWith("." + s, StringComparison.Ordinal)) return s;
            return null;
        }

        /// <summary>주소 문자열에서 도메인만 뽑는다. "https://www.youtube.com/a?b" → "youtube.com"</summary>
        public static string HostOf(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string s = url.Trim();
            int scheme = s.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) s = s.Substring(scheme + 3);
            int end = s.IndexOfAny(new[] { '/', '?', '#' });
            if (end >= 0) s = s.Substring(0, end);
            int at = s.LastIndexOf('@');
            if (at >= 0) s = s.Substring(at + 1);
            int port = s.LastIndexOf(':');
            if (port >= 0) s = s.Substring(0, port);
            s = s.Trim().TrimEnd('.').ToLowerInvariant();
            if (s.StartsWith("www.", StringComparison.Ordinal)) s = s.Substring(4);
            return s.Length == 0 ? null : s;
        }

        /// <summary>화면에 보이는(최소화되지 않은) 브라우저 창들.</summary>
        public static List<IntPtr> BrowserWindows()
        {
            var result = new List<IntPtr>();
            var names = new Dictionary<uint, string>();
            var sb = new StringBuilder(256);
            EnumWindows((hwnd, lp) =>
            {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindowTextLength(hwnd) == 0) return true;
                sb.Clear();
                GetClassName(hwnd, sb, sb.Capacity);
                string cls = sb.ToString();
                if (!cls.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal) && cls != "MozillaWindowClass") return true;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (!names.TryGetValue(pid, out string pname))
                {
                    try { using (var p = Process.GetProcessById((int)pid)) pname = p.ProcessName; }
                    catch { pname = ""; }
                    names[pid] = pname;
                }
                if (Browsers.Contains(pname)) result.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>브라우저 창에서 지금 보이는 탭의 주소를 읽는다. 못 읽으면 null.</summary>
        public static string ReadUrl(IntPtr hwnd)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                if (root == null) return null;

                // 1) 페이지 문서 요소의 값이 곧 실제로 열린 주소다
                AutomationElement doc = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
                if (doc != null && doc.TryGetCurrentPattern(ValuePattern.Pattern, out object dv))
                {
                    string v = ((ValuePattern)dv).Current.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                // 2) 안 되면 주소창. 단, 입력 중(포커스가 있을 때)인 글자는 주소로 보지 않는다
                AutomationElement edit = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                if (edit != null && !edit.Current.HasKeyboardFocus && edit.TryGetCurrentPattern(ValuePattern.Pattern, out object ev))
                {
                    string v = ((ValuePattern)ev).Current.Value;
                    if (!string.IsNullOrWhiteSpace(v) && !v.Contains(" ") && v.Contains(".")) return v;
                }
            }
            catch
            {
                // 창이 닫히는 중이거나 접근성 정보가 아직 없는 경우
            }
            return null;
        }

        /// <summary>현재 탭을 닫는다. 어떤 방법을 썼는지 돌려준다(로그용).</summary>
        static string CloseActiveTab(IntPtr hwnd)
        {
            // 1) 선택된 탭의 "닫기" 버튼을 직접 누른다 (창이 뒤에 있어도 동작)
            try
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                var selectedTabs = root.FindAll(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                    new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true)));
                foreach (AutomationElement tab in selectedTabs)
                {
                    var buttons = tab.FindAll(TreeScope.Children,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                    foreach (AutomationElement b in buttons)
                    {
                        string cls = b.Current.ClassName ?? "";
                        if (cls.IndexOf("CloseButton", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (b.TryGetCurrentPattern(InvokePattern.Pattern, out object ip))
                        {
                            ((InvokePattern)ip).Invoke();
                            return "tab close button";
                        }
                    }
                }
            }
            catch { }

            // 2) 그 창이 맨 앞에 있을 때만 Ctrl+W 를 보낸다 (다른 프로그램에 키가 들어가지 않도록 직전에 다시 확인)
            if (GetForegroundWindow() == hwnd)
            {
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_W, 0, 0, UIntPtr.Zero);
                keybd_event(VK_W, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                return "ctrl+w";
            }

            // 3) 그래도 안 되면 창을 내려 버린다
            ShowWindow(hwnd, SW_MINIMIZE);
            return "minimize";
        }

        // ───────────────────────── Win32 ─────────────────────────

        const byte VK_CONTROL = 0x11, VK_W = 0x57;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const int SW_MINIMIZE = 6;

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    }
}
