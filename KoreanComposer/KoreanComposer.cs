using System.Collections.Generic;

namespace Hangul
{
    /// <summary>
    /// 한글 자모 조합기.
    /// 두벌식 자모 문자(U+3131–U+3163)를 한 글자씩 받아
    /// 유니코드 음절 블록(U+AC00–U+D7A3)으로 실시간 조합한다.
    ///
    /// <b>스레드 안전성:</b> 인스턴스 상태를 공유하므로 스레드 안전하지 않다.
    /// 반드시 단일 스레드(UI 스레드 등)에서만 사용하거나,
    /// 스레드마다 별도 인스턴스를 생성해야 한다.
    ///
    /// <b>비자모 문자:</b> <see cref="Input"/>은 U+3131–U+3163 범위의 자모만 처리한다.
    /// 스페이스·엔터·특수문자 등은 호출 전에 걸러야 한다.
    /// 걸러지지 않은 문자는 조합을 강제 확정한 뒤 그대로 Committed에 포함되어 반환된다.
    ///
    /// 사용법:
    /// <code>
    /// var composer = new KoreanComposer();
    /// var (committed, composing) = composer.Input('ㄱ');   // ("", 'ㄱ')
    ///     (committed, composing) = composer.Input('ㅏ');   // ("", '가')
    ///     (committed, composing) = composer.Input('ㄴ');   // ("", '간')
    ///     (committed, composing) = composer.Input('ㅏ');   // ("가", '나')
    /// string tail = composer.Flush();                       // "나"
    /// </code>
    /// </summary>
    public sealed class KoreanComposer
    {
        // ── Unicode base ──────────────────────────────────────────────────────
        private const int SyllableBase = 0xAC00;
        private const int JungCount    = 21;
        private const int JongCount    = 28;

        // ── 초성 테이블 (19개) ────────────────────────────────────────────────
        // 인덱스 순서: ㄱ ㄲ ㄴ ㄷ ㄸ ㄹ ㅁ ㅂ ㅃ ㅅ ㅆ ㅇ ㅈ ㅉ ㅊ ㅋ ㅌ ㅍ ㅎ
        private static readonly char[] s_cho =
        {
            'ㄱ','ㄲ','ㄴ','ㄷ','ㄸ','ㄹ','ㅁ','ㅂ','ㅃ','ㅅ',
            'ㅆ','ㅇ','ㅈ','ㅉ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ'
        };

        // ── 중성 테이블 (21개) ────────────────────────────────────────────────
        // 인덱스 순서: ㅏ ㅐ ㅑ ㅒ ㅓ ㅔ ㅕ ㅖ ㅗ ㅘ ㅙ ㅚ ㅛ ㅜ ㅝ ㅞ ㅟ ㅠ ㅡ ㅢ ㅣ
        private static readonly char[] s_jung =
        {
            'ㅏ','ㅐ','ㅑ','ㅒ','ㅓ','ㅔ','ㅕ','ㅖ','ㅗ','ㅘ',
            'ㅙ','ㅚ','ㅛ','ㅜ','ㅝ','ㅞ','ㅟ','ㅠ','ㅡ','ㅢ','ㅣ'
        };

        // ── 종성 테이블 (28개, 인덱스 0 = 없음) ──────────────────────────────
        // 인덱스 순서: (없음) ㄱ ㄲ ㄳ ㄴ ㄵ ㄶ ㄷ ㄹ ㄺ ㄻ ㄼ ㄽ ㄾ ㄿ ㅀ ㅁ ㅂ ㅄ ㅅ ㅆ ㅇ ㅈ ㅊ ㅋ ㅌ ㅍ ㅎ
        private static readonly char[] s_jong =
        {
            '\0','ㄱ','ㄲ','ㄳ','ㄴ','ㄵ','ㄶ','ㄷ','ㄹ','ㄺ',
            'ㄻ','ㄼ','ㄽ','ㄾ','ㄿ','ㅀ','ㅁ','ㅂ','ㅄ','ㅅ',
            'ㅆ','ㅇ','ㅈ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ'
        };

        // ── 역방향 인덱스 ─────────────────────────────────────────────────────
        private static readonly Dictionary<char, int> s_choIdx;
        private static readonly Dictionary<char, int> s_jungIdx;
        private static readonly Dictionary<char, int> s_jongIdx;  // 0 제외

        // ── 복합 중성 역방향: 합성_중성_idx → 기반_중성_idx (백스페이스용) ──
        private static readonly Dictionary<int, int> s_jungBase;

        // ── 복합 중성: (기반_중성_idx, 추가_중성_idx) → 합성_중성_idx ─────────
        private static readonly Dictionary<(int, int), int> s_compoundJung = new()
        {
            { ( 8,  0),  9 },  // ㅗ+ㅏ=ㅘ
            { ( 8,  1), 10 },  // ㅗ+ㅐ=ㅙ
            { ( 8, 20), 11 },  // ㅗ+ㅣ=ㅚ
            { (13,  4), 14 },  // ㅜ+ㅓ=ㅝ
            { (13,  5), 15 },  // ㅜ+ㅔ=ㅞ
            { (13, 20), 16 },  // ㅜ+ㅣ=ㅟ
            { (18, 20), 19 },  // ㅡ+ㅣ=ㅢ
        };

        // ── 복합 종성 형성: (현재_종성_idx, 추가_자음_초성_idx) → 합성_종성_idx ─
        // 분리 정보는 s_splitJong 이 담당하므로 여기선 결과 인덱스만 저장한다.
        private static readonly Dictionary<(int, int), int> s_compoundJong = new()
        {
            { ( 1,  9),  3 },  // ㄱ+ㅅ→ㄳ
            { ( 4, 12),  5 },  // ㄴ+ㅈ→ㄵ
            { ( 4, 18),  6 },  // ㄴ+ㅎ→ㄶ
            { ( 8,  0),  9 },  // ㄹ+ㄱ→ㄺ
            { ( 8,  6), 10 },  // ㄹ+ㅁ→ㄻ
            { ( 8,  7), 11 },  // ㄹ+ㅂ→ㄼ
            { ( 8,  9), 12 },  // ㄹ+ㅅ→ㄽ
            { ( 8, 16), 13 },  // ㄹ+ㅌ→ㄾ
            { ( 8, 17), 14 },  // ㄹ+ㅍ→ㄿ
            { ( 8, 18), 15 },  // ㄹ+ㅎ→ㅀ
            { (17,  9), 18 },  // ㅂ+ㅅ→ㅄ
        };

        // ── 복합 종성 분리: 합성_종성_idx → (남는_종성_idx, 분리_초성_idx) ────
        private static readonly Dictionary<int, (int RemainJong, int SplitCho)> s_splitJong = new()
        {
            {  3, ( 1,  9) },  // ㄳ → ㄱ + ㅅ
            {  5, ( 4, 12) },  // ㄵ → ㄴ + ㅈ
            {  6, ( 4, 18) },  // ㄶ → ㄴ + ㅎ
            {  9, ( 8,  0) },  // ㄺ → ㄹ + ㄱ
            { 10, ( 8,  6) },  // ㄻ → ㄹ + ㅁ
            { 11, ( 8,  7) },  // ㄼ → ㄹ + ㅂ
            { 12, ( 8,  9) },  // ㄽ → ㄹ + ㅅ
            { 13, ( 8, 16) },  // ㄾ → ㄹ + ㅌ
            { 14, ( 8, 17) },  // ㄿ → ㄹ + ㅍ
            { 15, ( 8, 18) },  // ㅀ → ㄹ + ㅎ
            { 18, (17,  9) },  // ㅄ → ㅂ + ㅅ
        };

        // ── 정적 생성자 ───────────────────────────────────────────────────────
        static KoreanComposer()
        {
            s_choIdx  = BuildIndex(s_cho);
            s_jungIdx = BuildIndex(s_jung);

            s_jongIdx = new Dictionary<char, int>();
            for (int i = 1; i < s_jong.Length; i++)
                s_jongIdx[s_jong[i]] = i;

            s_jungBase = new Dictionary<int, int>();
            foreach (var kvp in s_compoundJung)
                s_jungBase[kvp.Value] = kvp.Key.Item1;
        }

        // ── 조합 상태 ─────────────────────────────────────────────────────────
        private int _cho  = -1;  // 초성 인덱스 (-1 = 없음)
        private int _jung = -1;  // 중성 인덱스 (-1 = 없음)
        private int _jong =  0;  // 종성 인덱스 (0 = 없음)

        // 초성 없이 입력된 모음 인덱스 (-1 = 없음). 결합할 초성이 없을 때
        // 복합 모음(ㅗ+ㅏ=ㅘ 등)을 계속 결합할 수 있도록 별도로 보관한다.
        private int _bareJung = -1;

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>현재 조합 중인 글자. 조합 중이 아니면 '\0'.</summary>
        public char ComposingChar
        {
            get
            {
                if (_cho < 0) return _bareJung < 0 ? '\0' : s_jung[_bareJung];
                if (_jung < 0) return s_cho[_cho];
                return MakeSyllable(_cho, _jung, _jong);
            }
        }

        /// <summary>조합 진행 중이면 true.</summary>
        public bool IsComposing => _cho >= 0 || _bareJung >= 0;

        /// <summary>
        /// 자모 문자 하나를 입력한다.
        /// </summary>
        /// <param name="jamo">U+3131–U+3163 범위의 자모 문자.</param>
        /// <returns>
        /// Committed: 텍스트에 즉시 삽입할 문자열 (조합 진행 중이면 빈 문자열).
        /// Composing: 커서 자리에 미리보기로 보여줄 조합 중 글자 ('\0'이면 없음).
        /// </returns>
        public (string Committed, char Composing) Input(char jamo)
        {
            if (s_choIdx.ContainsKey(jamo))  return InputConsonant(jamo);
            if (s_jungIdx.ContainsKey(jamo)) return InputVowel(jamo);

            // 알 수 없는 문자: 현재 조합 확정 후 그대로 출력
            string flush = Flush();
            return (flush + jamo, '\0');
        }

        /// <summary>
        /// 백스페이스 처리. 조합 중인 글자가 있으면 내부에서 한 단계 분해한다.
        /// </summary>
        /// <returns>
        /// Consumed: true면 조합 내부에서 분해됨 — 호출자는 별도의 문자 삭제 불필요.
        ///           false면 조합 없음 — 호출자가 일반 백스페이스를 수행해야 함.
        /// Composing: 분해 후 새 조합 중 글자 ('\0'이면 조합 종료).
        /// </returns>
        public (bool Consumed, char Composing) Backspace()
        {
            if (_cho < 0)
            {
                // 초성 없이 모음만 보관 중 → 복합 모음이면 분해, 단순 모음이면 제거
                if (_bareJung < 0) return (false, '\0');
                _bareJung = FindBaseJung(_bareJung);
                return (true, ComposingChar);
            }

            // 종성 있음 → 종성 한 단계 분해
            if (_jong > 0)
            {
                _jong = s_splitJong.TryGetValue(_jong, out var split)
                    ? split.RemainJong   // 복합 종성: 첫 번째 자음만 남김
                    : 0;                 // 단순 종성: 제거
                return (true, ComposingChar);
            }

            // 중성 있음 → 중성 한 단계 분해
            if (_jung >= 0)
            {
                int baseJung = FindBaseJung(_jung);
                _jung = baseJung;  // 복합 중성이면 기반으로, 단순이면 -1
                return (true, ComposingChar);
            }

            // 초성만 있음 → 조합 취소
            Reset();
            return (true, '\0');
        }

        /// <summary>
        /// 조합 중인 글자를 확정하고 상태를 초기화한다.
        /// 포커스 해제, 스페이스 입력, 비자모 문자 입력 전에 호출.
        /// </summary>
        /// <returns>확정된 문자열. 조합 중이 아니면 빈 문자열.</returns>
        public string Flush()
        {
            if (_cho < 0 && _bareJung < 0) return string.Empty;
            char c = ComposingChar;
            Reset();
            return c.ToString();
        }

        /// <summary>조합 상태 완전 초기화.</summary>
        public void Reset()
        {
            _cho  = -1;
            _jung = -1;
            _jong =  0;
            _bareJung = -1;
        }

        // ── 자음 입력 처리 ────────────────────────────────────────────────────

        private (string Committed, char Composing) InputConsonant(char jamo)
        {
            int choIdx  = s_choIdx[jamo];
            int jongIdx = s_jongIdx.TryGetValue(jamo, out int j) ? j : 0;

            // 빈 상태: 새 초성 시작 (보관 중인 모음이 있으면 먼저 확정)
            if (_cho < 0)
            {
                string bare = _bareJung < 0 ? string.Empty : s_jung[_bareJung].ToString();
                _bareJung = -1;
                _cho = choIdx;
                return (bare, ComposingChar);
            }

            // 초성만: 이전 초성 확정 → 새 초성
            if (_jung < 0)
            {
                char prev = s_cho[_cho];
                _cho = choIdx; _jung = -1; _jong = 0;
                return (prev.ToString(), ComposingChar);
            }

            // 초성+중성, 종성 없음: 종성으로 흡수 시도
            // ㄲ·ㄸ·ㅃ·ㅆ·ㅉ 등 쌍자음은 s_jongIdx에 등록되지 않으므로 jongIdx == 0이 되어
            // 종성으로 흡수되지 않고 아래 '종성 불가' 경로로 자동 분기된다.
            if (_jong == 0)
            {
                if (jongIdx > 0)
                {
                    _jong = jongIdx;
                    return (string.Empty, ComposingChar);
                }
                // ㄸ·ㅃ·ㅉ 등 종성 불가 → 현재 음절 확정, 새 초성
                string syl = MakeSyllable(_cho, _jung, 0).ToString();
                _cho = choIdx; _jung = -1; _jong = 0;
                return (syl, ComposingChar);
            }

            // 초성+중성+종성: 복합 종성 시도
            if (jongIdx > 0 && s_compoundJong.TryGetValue((_jong, choIdx), out int newJong))
            {
                _jong = newJong;
                return (string.Empty, ComposingChar);
            }

            // 복합 불가 → 현재 음절 확정, 새 초성
            {
                string syl = MakeSyllable(_cho, _jung, _jong).ToString();
                _cho = choIdx; _jung = -1; _jong = 0;
                return (syl, ComposingChar);
            }
        }

        // ── 모음 입력 처리 ────────────────────────────────────────────────────

        private (string Committed, char Composing) InputVowel(char jamo)
        {
            int jungIdx = s_jungIdx[jamo];

            // 빈 상태: 결합할 초성이 없음 — 보관 중인 모음과 복합 모음 결합 시도
            if (_cho < 0)
            {
                if (_bareJung < 0)
                {
                    _bareJung = jungIdx;
                    return (string.Empty, ComposingChar);
                }
                if (s_compoundJung.TryGetValue((_bareJung, jungIdx), out int compound))
                {
                    _bareJung = compound;
                    return (string.Empty, ComposingChar);
                }
                // 복합 불가 → 보관 중인 모음 확정, 새 모음 보관 시작
                string prevJamo = s_jung[_bareJung].ToString();
                _bareJung = jungIdx;
                return (prevJamo, ComposingChar);
            }

            // 초성만: 모음 결합
            if (_jung < 0)
            {
                _jung = jungIdx;
                return (string.Empty, ComposingChar);
            }

            // 초성+중성, 종성 없음: 복합 중성 시도
            if (_jong == 0)
            {
                if (s_compoundJung.TryGetValue((_jung, jungIdx), out int compound))
                {
                    _jung = compound;
                    return (string.Empty, ComposingChar);
                }
                // 복합 불가 → 현재 음절 확정, 새 ㅇ+모음
                string syl = MakeSyllable(_cho, _jung, 0).ToString();
                _cho = 11; _jung = jungIdx; _jong = 0;
                return (syl, ComposingChar);
            }

            // 초성+중성+종성: 종성 분리 → 현재 음절 확정, 분리된 자음+모음으로 새 음절
            int remainJong, splitCho;
            if (s_splitJong.TryGetValue(_jong, out var split))
            {
                remainJong = split.RemainJong;
                splitCho   = split.SplitCho;
            }
            else
            {
                // 단순 종성: 종성 글자를 초성으로 변환
                remainJong = 0;
                splitCho   = JongToCho(_jong);
            }

            string committed = MakeSyllable(_cho, _jung, remainJong).ToString();
            _cho = splitCho; _jung = jungIdx; _jong = 0;
            return (committed, ComposingChar);
        }

        // ── 내부 유틸 ─────────────────────────────────────────────────────────

        private static char MakeSyllable(int cho, int jung, int jong)
            => (char)(SyllableBase + (cho * JungCount + jung) * JongCount + jong);

        private static int JongToCho(int jongIdx)
        {
            char jamoChar = s_jong[jongIdx];
            return s_choIdx.TryGetValue(jamoChar, out int choIdx) ? choIdx : 11;  // fallback: ㅇ
        }

        // 복합 중성의 기반 중성 인덱스 반환 (백스페이스용). 단순 중성이면 -1.
        private static int FindBaseJung(int compoundIdx)
            => s_jungBase.TryGetValue(compoundIdx, out int baseIdx) ? baseIdx : -1;

        private static Dictionary<char, int> BuildIndex(char[] arr)
        {
            var dict = new Dictionary<char, int>();
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] != '\0') dict[arr[i]] = i;
            return dict;
        }
    }
}
