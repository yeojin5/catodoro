using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace Catodoro
{
    /// <summary>실행 옵션. 평소에는 아무 옵션 없이 실행하고, 아래 옵션들은 개발·점검용이다.</summary>
    public class Options
    {
        public string DataDir;        // --data-dir <폴더>     설정을 다른 폴더에 저장
        public string Screenshot;     // --screenshot <png>    창을 그림으로 저장하고 종료
        public string ExportDir;      // --export-assets <폴더> 아이콘·스프라이트 시트 내보내기
        public string Probe;          // --probe <txt>         브라우저 창에서 읽힌 도메인 기록
        public string Overlay;        // --overlay success|fail|giveup|toast|settings|dialtest|knob
        public int? Minutes;          // --minutes <n>
        public int? AutoStartSeconds; // --autostart-seconds <n>
        public int? ExitAfterMs;      // --exit-after <ms>
        public int? AddCats;          // --add-cats <n>
        public int ShotDelayMs = 700; // --shot-delay <ms>
        public double ShotScale = 2;  // --shot-scale <배율>
        public bool Log;              // --log

        public bool IsTest => DataDir != null || Screenshot != null || ExportDir != null || Probe != null;

        public static Options Parse(string[] a)
        {
            var o = new Options();
            for (int i = 0; i < a.Length; i++)
            {
                string Next() => i + 1 < a.Length ? a[++i] : null;
                int? NextInt() => int.TryParse(Next(), out int v) ? v : (int?)null;
                switch (a[i])
                {
                    case "--data-dir": o.DataDir = Next(); break;
                    case "--screenshot": o.Screenshot = Next(); break;
                    case "--export-assets": o.ExportDir = Next(); break;
                    case "--probe": o.Probe = Next(); break;
                    case "--overlay": o.Overlay = Next(); break;
                    case "--minutes": o.Minutes = NextInt(); break;
                    case "--autostart-seconds": o.AutoStartSeconds = NextInt(); break;
                    case "--exit-after": o.ExitAfterMs = NextInt(); break;
                    case "--add-cats": o.AddCats = NextInt(); break;
                    case "--shot-delay": o.ShotDelayMs = NextInt() ?? 700; break;
                    case "--shot-scale": o.ShotScale = NextInt() ?? 2; break;
                    case "--log": o.Log = true; break;
                }
            }
            return o;
        }
    }

    public class App : Application
    {
        static Mutex singleInstance;

        [STAThread]
        public static void Main()
        {
            new App().Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Options opt = Options.Parse(e.Args);

            if (opt.ExportDir != null)
            {
                AssetExporter.Export(opt.ExportDir);
                Shutdown();
                return;
            }

            if (opt.Probe != null)
            {
                var sb = new StringBuilder();
                foreach (IntPtr hwnd in Blocker.BrowserWindows())
                    sb.AppendLine(hwnd.ToInt64() + "\t" + (Blocker.HostOf(Blocker.ReadUrl(hwnd)) ?? "(읽지 못함)"));
                File.WriteAllText(opt.Probe, sb.ToString());
                Shutdown();
                return;
            }

            if (!opt.IsTest)
            {
                singleInstance = new Mutex(true, "Catodoro.SingleInstance", out bool created);
                if (!created)
                {
                    MessageBox.Show("Catodoro가 이미 실행 중이에요.", "Catodoro", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }

            Store.LogEnabled = opt.Log;
            Store.Init(opt.DataDir);
            new MainWindow(opt).Show();
        }
    }
}
