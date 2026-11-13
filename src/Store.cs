using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Catodoro
{
    /// <summary>집중 한 회차의 기록.</summary>
    [DataContract]
    public class SessionRecord
    {
        public const string Done = "done";       // 끝까지 집중해서 고양이를 받음
        public const string Blocked = "blocked"; // 시간은 채웠지만 차단 대상을 열려고 함
        public const string Stopped = "stopped"; // 도중에 그만둠

        /// <summary>시작 시각(현지 시간), "yyyy-MM-ddTHH:mm:ss".</summary>
        [DataMember] public string Start;
        /// <summary>실제로 집중한 시간(초). 그만둔 회차는 그만둘 때까지의 시간.</summary>
        [DataMember] public int Seconds;
        /// <summary>맞춰 둔 시간(초).</summary>
        [DataMember] public int Planned;
        [DataMember] public string Result;
        [DataMember] public int Violations;
        /// <summary>받은 고양이 종류. 없으면 -1.</summary>
        [DataMember] public int Cat = -1;

        public DateTime StartTime =>
            DateTime.TryParseExact(Start, "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime t) ? t : DateTime.MinValue;

        /// <summary>시간을 끝까지 채운 회차인가(집중 시간 합계에 넣는 기준).</summary>
        public bool Completed => Result == Done || Result == Blocked;
    }

    [DataContract]
    public class AppData
    {
        /// <summary>기록은 최근 이만큼만 남긴다.</summary>
        public const int MaxHistory = 3000;

        /// <summary>차단할 프로그램. 확장자 없는 소문자 프로세스 이름(예: "kakaotalk").</summary>
        [DataMember] public List<string> Programs;
        /// <summary>차단할 사이트 도메인(예: "youtube.com").</summary>
        [DataMember] public List<string> Sites;
        /// <summary>지금까지 모은 고양이들의 종류 번호.</summary>
        [DataMember] public List<int> Cats;
        /// <summary>집중 기록(오래된 것부터).</summary>
        [DataMember] public List<SessionRecord> History;
        [DataMember] public double? Left;
        [DataMember] public double? Top;
        [DataMember] public int LastMinutes;
        [DataMember] public bool Topmost;
        /// <summary>화면 언어: "ko" / "en". 비어 있으면 윈도우 표시 언어를 따른다.</summary>
        [DataMember] public string Language;

        public static AppData CreateDefault() => new AppData { LastMinutes = 25, Topmost = true }.Normalize();

        public AppData Normalize()
        {
            Programs ??= new List<string>();
            Sites ??= new List<string>();
            Cats ??= new List<int>();
            History ??= new List<SessionRecord>();
            History.RemoveAll(r => r == null || r.StartTime == DateTime.MinValue);
            if (History.Count > MaxHistory) History.RemoveRange(0, History.Count - MaxHistory);
            if (LastMinutes < 0 || LastMinutes > 60) LastMinutes = 25;
            Language = Lang.Normalize(Language);
            return this;
        }
    }

    /// <summary>설정과 고양이 목록을 %AppData%\Catodoro\settings.json 에 저장한다.</summary>
    public static class Store
    {
        // 실수로 차단 목록에 넣으면 윈도우가 망가질 수 있는 프로세스들
        static readonly HashSet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "catodoro", "explorer", "system", "idle", "csrss", "winlogon", "wininit", "services", "lsass",
            "smss", "svchost", "dwm", "fontdrvhost", "sihost", "taskhostw", "ctfmon", "conhost", "registry",
            "runtimebroker", "searchhost", "startmenuexperiencehost", "shellexperiencehost", "taskmgr",
            "securityhealthservice", "msmpeng", "audiodg", "spoolsv", "dllhost",
        };

        public static AppData Data { get; private set; } = AppData.CreateDefault();
        public static bool LogEnabled { get; set; }

        static string dir;
        static readonly object LogLock = new object();

        static string FilePath => Path.Combine(dir, "settings.json");

        public static void Init(string overrideDir)
        {
            dir = overrideDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Catodoro");
            Directory.CreateDirectory(dir);
            Load();
        }

        static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var fs = File.OpenRead(FilePath))
                    {
                        var ser = new DataContractJsonSerializer(typeof(AppData));
                        Data = ((AppData)ser.ReadObject(fs) ?? AppData.CreateDefault()).Normalize();
                    }
                    return;
                }
            }
            catch
            {
                // 파일이 깨졌으면 기본값으로 시작한다
            }
            Data = AppData.CreateDefault();
        }

        public static void Save()
        {
            try
            {
                string tmp = FilePath + ".tmp";
                using (var fs = File.Create(tmp))
                {
                    new DataContractJsonSerializer(typeof(AppData)).WriteObject(fs, Data);
                }
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch
            {
                // 저장 실패는 앱을 멈출 일이 아니다
            }
        }

        /// <summary>집중 한 회차를 기록에 남기고 저장한다.</summary>
        public static void AddHistory(DateTime start, int seconds, int planned, string result, int violations, int cat)
        {
            Data.History.Add(new SessionRecord
            {
                Start = start.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                Seconds = Math.Max(0, seconds),
                Planned = Math.Max(0, planned),
                Result = result,
                Violations = violations,
                Cat = cat,
            });
            if (Data.History.Count > AppData.MaxHistory) Data.History.RemoveRange(0, Data.History.Count - AppData.MaxHistory);
            Save();
        }

        public static void Log(string message)
        {
            if (!LogEnabled || dir == null) return;
            try
            {
                lock (LogLock)
                    File.AppendAllText(Path.Combine(dir, "log.txt"), $"{DateTime.Now:HH:mm:ss.fff} {message}\r\n");
            }
            catch { }
        }

        /// <summary>"C:\...\KakaoTalk.exe", "kakaotalk.exe", "KakaoTalk" → "kakaotalk". 넣을 수 없는 이름이면 null.</summary>
        public static string NormalizeProgram(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string name = input.Trim().Trim('"');
            try { name = Path.GetFileName(name); } catch { return null; }
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            name = name.Trim().ToLowerInvariant();
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            return Protected.Contains(name) ? null : name;
        }

        public static bool IsProtectedProgram(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string name = input.Trim().Trim('"');
            try { name = Path.GetFileNameWithoutExtension(name); } catch { return false; }
            return Protected.Contains(name);
        }

        /// <summary>"https://www.youtube.com/watch?v=1" → "youtube.com". 도메인이 아니면 null.</summary>
        public static string NormalizeSite(string input)
        {
            string host = Blocker.HostOf(input);
            if (host == null || !host.Contains(".") || host.Any(char.IsWhiteSpace)) return null;
            return host;
        }
    }
}
