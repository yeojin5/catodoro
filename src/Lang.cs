using System.Collections.Generic;
using System.Globalization;

namespace Catodoro
{
    /// <summary>
    /// 화면에 보이는 모든 문구(한국어 / 영어).
    /// 언어는 설정 창에서 고르며, 고른 적이 없으면 윈도우 표시 언어를 따른다(한국어가 아니면 영어).
    /// </summary>
    public static class Lang
    {
        public const string Korean = "ko", English = "en";

        /// <summary>개발·점검용: 저장된 설정과 상관없이 이 언어로 보여 준다(--lang).</summary>
        public static string Override { get; set; }

        public static string Current => Normalize(Override) ?? Normalize(Store.Data.Language) ?? SystemDefault;

        public static bool IsEnglish => Current == English;

        static string SystemDefault =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Korean ? Korean : English;

        public static string Normalize(string code) => code == Korean || code == English ? code : null;

        static readonly string[] CatNamesKo = { "삼색이", "까망이", "치즈", "흰둥이", "복냥이" };
        static readonly string[] CatNamesEn = { "Patches", "Shadow", "Cheese", "Snowball", "Boknyang" };

        public static string CatName(int type)
        {
            string[] names = IsEnglish ? CatNamesEn : CatNamesKo;
            return names[((type % names.Length) + names.Length) % names.Length];
        }

        /// <summary>키에 해당하는 문구. {0}, {1} 자리는 args 로 채운다.</summary>
        public static string T(string key, params object[] args)
        {
            if (!Table.TryGetValue(key, out var pair)) return key;
            string text = IsEnglish ? pair.en : pair.ko;
            return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
        }

        static readonly Dictionary<string, (string ko, string en)> Table = new Dictionary<string, (string ko, string en)>
        {
            // ── 메인 화면 ──
            ["tip.settings"] = ("차단 목록 설정", "Block list settings"),
            ["tip.minimize"] = ("최소화", "Minimize"),
            ["tip.close"] = ("닫기", "Close"),

            ["cat.title"] = ("집중 성공!", "Focus complete!"),
            ["cat.sub"] = ("새 고양이 '{0}'가 놀러 왔어요.\n지금까지 모은 고양이: {1}마리",
                           "A new cat, {0}, came to visit.\nCats collected so far: {1}"),
            ["lucky.title"] = ("행운의 고양이 등장!", "A lucky cat appeared!"),
            ["lucky.sub"] = ("마네키네코 '{0}'가 복을 들고 왔어요.\n지금까지 모은 고양이: {1}마리",
                             "{0}, a maneki-neko, has come\nto bring you luck.\nCats collected so far: {1}"),
            ["cat.ok"] = ("좋아요", "Nice"),

            ["fail.title"] = ("고양이가 오지 않았어요", "No cat this time"),
            ["fail.sub"] = ("집중 시간 동안 차단 목록에 {0}번 접근했어요.\n다음에는 끝까지 참아 봐요!",
                            "Blocked items were opened {0} time(s)\nduring this session.\nHold out to the end next time!"),
            ["ok"] = ("확인", "OK"),

            ["giveup.title"] = ("정말 그만둘까요?", "Stop this session?"),
            ["giveup.sub"] = ("지금 멈추면 이번 고양이는 오지 않아요.", "If you stop now, no cat will come this time."),
            ["giveup.keep"] = ("계속 집중", "Keep going"),
            ["giveup.stop"] = ("그만두기", "Stop"),

            ["toast.stopped"] = ("집중을 중단했어요", "Session stopped"),
            ["toast.blocked"] = ("'{0}' 차단!\n이번에는 고양이가 오지 않아요", "Blocked '{0}'!\nNo cat this time"),
            ["toast.setTime"] = ("다이얼을 돌려 시간을 맞춰 주세요", "Turn the dial to set a time"),

            ["app.running"] = ("Catodoro가 이미 실행 중이에요.", "Catodoro is already running."),

            // ── 설정 창 ──
            ["set.window"] = ("Catodoro 설정", "Catodoro Settings"),
            ["set.heading"] = ("차단 목록", "Block list"),
            ["set.intro"] = ("집중 시간 동안 아래 프로그램과 사이트는 열 수 없어요. 열려고 하면 바로 닫히고, 그 회차에는 고양이가 오지 않아요.",
                             "During focus time the programs and sites below can't be opened. If you try, they are closed right away and no cat comes for that session."),
            ["set.tab.programs"] = ("  프로그램  ", "  Programs  "),
            ["set.tab.sites"] = ("  사이트  ", "  Sites  "),
            ["set.add"] = ("추가", "Add"),
            ["set.remove"] = ("선택 삭제", "Remove selected"),
            ["set.browse"] = ("exe 파일 찾아보기…", "Browse for exe…"),
            ["set.program.tip"] = ("실행 중인 프로그램을 고르거나 exe 이름을 직접 입력하세요",
                                   "Pick a running program or type an exe name"),
            ["set.program.hint"] = ("지금 실행 중인 프로그램을 목록에서 고르거나, 예: KakaoTalk.exe 처럼 직접 적어 주세요.",
                                    "Pick a program that is running now, or type a name such as Discord.exe."),
            ["set.site.hint"] = ("예: youtube.com — 주소를 통째로 붙여 넣어도 돼요. 하위 주소(m.youtube.com 등)도 함께 막혀요. Chrome, Edge, Whale, Brave, Firefox 등에서 동작해요.",
                                 "Example: youtube.com — you can paste a full address. Subdomains (such as m.youtube.com) are blocked too. Works in Chrome, Edge, Whale, Brave, Firefox and more."),
            ["set.topmost"] = ("항상 위에 표시", "Always on top"),
            ["set.close"] = ("닫기", "Close"),
            ["set.cats"] = ("모은 고양이 {0}마리", "Cats collected: {0}"),
            ["set.language"] = ("언어", "Language"),

            ["set.msg.protected"] = ("윈도우 동작에 꼭 필요한 프로그램이라 차단 목록에 넣을 수 없어요.",
                                     "This program is essential to Windows and can't be added to the block list."),
            ["set.msg.badProgram"] = ("프로그램 이름을 알아볼 수 없어요. 예: KakaoTalk.exe",
                                      "That doesn't look like a program name. Example: Discord.exe"),
            ["set.msg.badSite"] = ("사이트 주소를 알아볼 수 없어요. 예: youtube.com",
                                   "That doesn't look like a site address. Example: youtube.com"),
            ["set.browse.filter"] = ("프로그램 (*.exe)|*.exe", "Programs (*.exe)|*.exe"),
            ["set.browse.title"] = ("차단할 프로그램 선택", "Choose a program to block"),
        };
    }
}
