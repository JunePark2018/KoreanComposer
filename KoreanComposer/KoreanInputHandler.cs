namespace Hangul
{
    /// <summary>
    /// 두벌식 Latin 키 입력을 받아 한글 음절로 조합하는 통합 핸들러.
    /// <see cref="KoreanKeymap"/> + <see cref="KoreanComposer"/> + 구분자/이스케이프 처리를 하나로 묶는다.
    ///
    /// 대부분의 사용자는 <see cref="KoreanComposer"/>를 직접 다루는 대신 이 클래스를 사용하면 된다.
    ///
    /// <b>구분자 규칙:</b>
    /// <list type="bullet">
    ///   <item><description><c>\</c> — 현재 조합을 강제 확정. 출력 없음. 예: <c>qk\r</c> → <c>바ㄱ</c></description></item>
    ///   <item><description><c>\\</c> — 리터럴 <c>\</c> 출력. 예: <c>rk\\sk</c> → <c>가\나</c></description></item>
    /// </list>
    ///
    /// <b>스레드 안전성:</b> 단일 스레드에서만 사용할 것.
    /// </summary>
    public sealed class KoreanInputHandler
    {
        private readonly KoreanComposer _composer = new();
        private bool _escapePending;

        /// <summary>현재 조합 중인 글자. 조합 중이 아니면 '\0'.</summary>
        public char ComposingChar => _composer.ComposingChar;

        /// <summary>조합 진행 중이면 true.</summary>
        public bool IsComposing => _composer.IsComposing;

        /// <summary>
        /// Latin 키 하나를 입력한다.
        /// </summary>
        /// <param name="latinKey">Window.TextInput 이벤트에서 받은 문자.</param>
        /// <returns>
        /// Committed: 텍스트에 즉시 삽입할 문자열.
        /// Composing: 커서 자리에 미리보기로 보여줄 조합 중 글자 ('\0'이면 없음).
        /// </returns>
        public (string Committed, char Composing) Input(char latinKey)
        {
            if (_escapePending)
            {
                _escapePending = false;
                if (KoreanKeymap.IsSeparator(latinKey))
                {
                    // \\ → 리터럴 '\'
                    return (_composer.Flush() + '\\', '\0');
                }
                // \ + 다른 문자 → flush(구분자) 후 그 문자 정상 처리
                string sep = _composer.Flush();
                var (next, composing) = ProcessKey(latinKey);
                return (sep + next, composing);
            }

            if (KoreanKeymap.IsSeparator(latinKey))
            {
                _escapePending = true;
                return (string.Empty, _composer.ComposingChar);
            }

            return ProcessKey(latinKey);
        }

        /// <summary>
        /// 백스페이스 처리. 이스케이프 대기 중이면 대기 상태만 취소한다.
        /// </summary>
        /// <returns>
        /// Consumed: true면 핸들러 내부에서 처리됨 — 호출자는 별도의 문자 삭제 불필요.
        ///           false면 조합 없음 — 호출자가 일반 백스페이스를 수행해야 함.
        /// Composing: 분해 후 새 조합 중 글자 ('\0'이면 조합 종료).
        /// </returns>
        public (bool Consumed, char Composing) Backspace()
        {
            if (_escapePending)
            {
                _escapePending = false;
                return (true, _composer.ComposingChar);
            }
            return _composer.Backspace();
        }

        /// <summary>조합 중인 글자를 확정하고 상태를 초기화한다.</summary>
        public string Flush()
        {
            _escapePending = false;
            return _composer.Flush();
        }

        /// <summary>조합 상태 완전 초기화.</summary>
        public void Reset()
        {
            _escapePending = false;
            _composer.Reset();
        }

        private (string Committed, char Composing) ProcessKey(char latinKey)
        {
            if (!KoreanKeymap.TryMap(latinKey, out char jamo))
            {
                return (_composer.Flush() + latinKey, '\0');
            }
            return _composer.Input(jamo);
        }
    }
}
