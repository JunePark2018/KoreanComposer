using Hangul;

// ── 자동 테스트 ───────────────────────────────────────────────────────────────
// 두벌식 키 → 자모:  r=ㄱ k=ㅏ s=ㄴ e=ㄷ a=ㅁ f=ㄹ h=ㅗ j=ㅓ l=ㅣ
//                    q=ㅂ t=ㅅ d=ㅇ g=ㅎ i=ㅑ u=ㅕ y=ㅛ n=ㅜ m=ㅡ
//                    R=ㄲ E=ㄸ Q=ㅃ T=ㅆ W=ㅉ  o=ㅐ p=ㅔ O=ㅒ P=ㅖ

var cases = new (string Label, string Keys, string Expected)[]
{
    // 기본 자음+모음
    ("기본 가나",             "rksk",       "가나"),
    // 받침
    ("받침: 각",              "rrk",        "ㄱ가"),
    ("받침: 간",              "rks",        "간"),
    ("받침: 갈",              "rkf",        "갈"),
    // 받침 → 다음 음절 초성으로 분리
    ("받침 분리: 가나",       "rksk",       "가나"),
    ("받침 분리: 간아",       "rksdk",      "간아"),
    // 복합 받침
    ("복합 받침: 닭",         "Ekfrr",      "딹ㄱ"),
    ("복합 받침: 삶",         "tkfak",      "살마"),
    // 복합 받침 분리 (모음이 오면)
    ("복합 받침 분리: 닭아",  "ekfrdk",     "닭아"),
    // 복합 모음
    ("복합 모음: 봐",         "qhk",        "봐"),
    ("복합 모음: 뭐아",       "anjk",       "뭐아"),
    ("복합 모음: 외",         "dhl",        "외"),
    // 된소리
    ("된소리: 까",            "Rk",         "까"),
    ("된소리: 따",            "Ek",         "따"),
    ("된소리: 빠",            "Qk",         "빠"),
    ("된소리: 싸",            "Tk",         "싸"),
    ("된소리: 짜",            "Wk",         "짜"),
    // 모음 단독 (ㅇ 자동 삽입)
    ("모음 단독: 아",         "k",          "아"),
    ("모음 단독: 아아",       "kk",         "아아"),
    // Shift 모음
    ("Shift 모음: 얘",        "dO",         "얘"),
    ("Shift 모음: 예",        "dP",         "예"),
    // 구분자 '\'
    ("구분자: 바ㄱ",          @"qk\r",      "바ㄱ"),
    ("구분자: 박",            "qkr",        "박"),
    ("구분자: 닭",            @"ekfr",      "닭"),
    ("구분자: 달ㄱ",          @"ekf\r",     "달ㄱ"),
    // 이스케이프 '\\'
    (@"이스케이프: \",        @"\\",        @"\"),
    (@"이스케이프: 가\나",    @"rk\\sk",    @"가\나"),
};

int pass = 0, fail = 0;

foreach (var (label, keys, expected) in cases)
{
    var handler = new KoreanInputHandler();
    var buf = new System.Text.StringBuilder();

    foreach (char k in keys)
    {
        var (committed, _) = handler.Input(k);
        buf.Append(committed);
    }
    buf.Append(handler.Flush());

    string got = buf.ToString();
    bool ok = got == expected;
    if (ok) pass++; else fail++;
    Console.WriteLine($"[{(ok ? "OK  " : "FAIL")}] {label,-24} keys={keys,-10} expected={expected,-8} got={got}");
}

Console.WriteLine($"\n{pass} passed, {fail} failed");

// ── 실시간 입력 모드 ──────────────────────────────────────────────────────────
// 마지막 입력 후 AUTO_FLUSH_MS 밀리초 동안 입력이 없으면 조합 중인 글자를 자동 확정한다.
// Console.KeyAvailable 폴링으로 단일 스레드에서 처리하므로 별도 동기화 불필요.
const int AUTO_FLUSH_MS = 2000;
const int POLL_MS       = 50;

Console.WriteLine("\n--- 실시간 입력 (ESC 또는 Enter로 종료, 2초 무입력 시 자동 확정) ---");

var rtHandler = new KoreanInputHandler();
var rtBuf     = new System.Text.StringBuilder();
DateTime lastInput = DateTime.MaxValue;  // 아직 아무것도 입력 안 한 상태

void Redraw()
{
    // 현재 줄 전체를 지우고 다시 그린다
    Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
    Console.Write("입력> " + rtBuf);
    char composing = rtHandler.ComposingChar;
    if (composing != '\0')
        Console.Write($"\x1b[4m{composing}\x1b[0m");  // 조합 중인 글자는 밑줄
}

while (true)
{
    // 자동 확정: 마지막 입력 후 AUTO_FLUSH_MS 경과
    if (rtHandler.IsComposing &&
        lastInput != DateTime.MaxValue &&
        (DateTime.Now - lastInput).TotalMilliseconds >= AUTO_FLUSH_MS)
    {
        rtBuf.Append(rtHandler.Flush());
        Redraw();
    }

    if (!Console.KeyAvailable)
    {
        Thread.Sleep(POLL_MS);
        continue;
    }

    var key = Console.ReadKey(intercept: true);
    lastInput = DateTime.Now;

    // 종료
    if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter)
    {
        rtBuf.Append(rtHandler.Flush());
        Console.WriteLine($"\r\n최종: {rtBuf}");
        break;
    }

    // 백스페이스
    if (key.Key == ConsoleKey.Backspace)
    {
        var (consumed, _) = rtHandler.Backspace();
        if (!consumed && rtBuf.Length > 0)
            rtBuf.Remove(rtBuf.Length - 1, 1);
        Redraw();
        continue;
    }

    // 일반 문자
    var (committed, composing2) = rtHandler.Input(key.KeyChar);
    rtBuf.Append(committed);
    _ = composing2;  // Redraw()가 ComposingChar로 직접 읽음
    Redraw();
}
