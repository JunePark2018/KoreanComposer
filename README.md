# KoreanComposer

[English](#english) | [한국어](#한국어)

---

## 한국어

두벌식 키보드 입력을 실시간으로 한글 음절로 조합하는 경량 C# 라이브러리입니다.

Latin 키 입력을 유니코드 음절 블록(U+AC00–U+D7A3)으로 즉시 변환합니다. OS IME를 사용할 수 없는 게임 엔진, 커스텀 텍스트 위젯 등에 적합합니다.

### 기능

- 두벌식 표준 자판 (KS X 5002) 완전 지원
- 실시간 음절 조합: 초성 + 중성 + 종성
- 복합 모음: ㅘ ㅙ ㅚ ㅝ ㅞ ㅟ ㅢ
- 복합 받침: ㄳ ㄵ ㄶ ㄺ ㄻ ㄼ ㄽ ㄾ ㄿ ㅀ ㅄ
- 된소리: ㄲ ㄸ ㅃ ㅆ ㅉ
- 백스페이스 단계별 분해
- 구분자 키(`\`)로 음절 경계 명시
- 이스케이프(`\\`)로 리터럴 `\` 입력
- 의존성 없음 — `.cs` 파일 3개

### 파일 구성

| 파일 | 설명 |
|------|------|
| `KoreanKeymap.cs` | Latin 문자 → 자모 변환 (두벌식 배열) |
| `KoreanComposer.cs` | 자모 → 음절 조합 상태 머신 |
| `KoreanInputHandler.cs` | 키맵 + 조합기 + 구분자/이스케이프 통합 핸들러 |

### 설치

```
dotnet add package KoreanComposer
```

### 빠른 시작

대부분의 경우 `KoreanInputHandler` 하나로 충분합니다.

```csharp
using Hangul;

var handler = new KoreanInputHandler();

// Latin 키 입력을 그대로 넣으면 됩니다. 전처리 불필요.
// Input()은 두 값을 반환합니다:
//   Committed — 텍스트 버퍼에 즉시 삽입할 문자열
//   Composing — 커서 위치에 미리보기로 보여줄 조합 중 글자 ('\0'이면 없음)

foreach (char key in userInput)
{
    var (committed, composing) = handler.Input(key);
    textBuffer.Append(committed);
    cursor.Preview = composing;
}

// 포커스 해제 또는 조합 종료 시:
textBuffer.Append(handler.Flush());
```

### 입력 예시

```
키    Committed   Composing   설명
--    ---------   ---------   ----
r                 ㄱ          초성 ㄱ
k                 가          모음 ㅏ 결합 → 가
s                 간          받침 ㄴ 흡수 → 간
d     간          아          ㄴ이 다음 초성으로 분리, 아 시작
k     아          나          ㄴ + ㅏ → 나
      나                      Flush() → 나
```

결과: `간나`

### 구분자 키

기본적으로 자음은 받침으로 흡수됩니다.

```
qkr  →  박   (ㄱ이 받침으로 흡수)
```

`\`로 음절을 명시적으로 확정할 수 있습니다.

```
qk\r  →  바ㄱ   (바 확정 후 ㄱ이 새 초성으로 시작)
\\    →  \      (리터럴 백슬래시)
```

### API

#### `KoreanInputHandler` *(권장)*

> 스레드 안전하지 않음. UI 스레드 등 단일 스레드에서 사용할 것.

| 메서드 | 반환 | 설명 |
|--------|------|------|
| `Input(char latinKey)` | `(string Committed, char Composing)` | Latin 키 하나 입력. 키맵·구분자·이스케이프 내부 처리. |
| `Backspace()` | `(bool Consumed, char Composing)` | 한 단계 분해. `Consumed=true`면 핸들러가 처리 — 호출자가 별도 삭제 불필요. `false`면 조합 없음 — 일반 백스페이스 수행. |
| `Flush()` | `string` | 조합 중인 글자 확정 후 상태 초기화. |
| `Reset()` | `void` | 조합 중인 글자 폐기 후 상태 초기화. |

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `ComposingChar` | `char` | 현재 조합 중인 글자 (`'\0'`이면 없음). |
| `IsComposing` | `bool` | 조합 진행 중이면 `true`. |

#### `KoreanComposer` *(저수준)*

커스텀 키맵이나 구분자 로직이 필요한 경우 직접 사용.

| 메서드 | 반환 | 설명 |
|--------|------|------|
| `Input(char jamo)` | `(string Committed, char Composing)` | 자모 문자 하나 입력 (U+3131–U+3163). |
| `Backspace()` | `(bool Consumed, char Composing)` | 한 단계 분해. |
| `Flush()` | `string` | 조합 확정 후 상태 초기화. |
| `Reset()` | `void` | 조합 폐기 후 상태 초기화. |

#### `KoreanKeymap` *(정적)*

| 메서드 | 반환 | 설명 |
|--------|------|------|
| `TryMap(char latin, out char jamo)` | `bool` | Latin → 자모 변환. 자판 범위 밖이면 `false`. |
| `IsSeparator(char c)` | `bool` | 구분자(`\`)이면 `true`. |

### 두벌식 자판 배열

```
일반:  q=ㅂ w=ㅈ e=ㄷ r=ㄱ t=ㅅ   y=ㅛ u=ㅕ i=ㅑ o=ㅐ p=ㅔ
       a=ㅁ s=ㄴ d=ㅇ f=ㄹ g=ㅎ   h=ㅗ j=ㅓ k=ㅏ l=ㅣ
       z=ㅋ x=ㅌ c=ㅊ v=ㅍ b=ㅠ   n=ㅜ m=ㅡ

Shift: Q=ㅃ W=ㅉ E=ㄸ R=ㄲ T=ㅆ   O=ㅒ P=ㅖ
```

### 백스페이스 동작

```
조합 중       백스페이스 후     비고
----------    -------------    ----
닭 (ㄷ+ㅏ+ㄺ)  달 (ㄷ+ㅏ+ㄹ)   복합 받침 분해: ㄺ → ㄹ
달 (ㄷ+ㅏ+ㄹ)  다 (ㄷ+ㅏ)      받침 제거
봐 (ㅂ+ㅘ)    보 (ㅂ+ㅗ)      복합 모음 분해: ㅘ → ㅗ
보 (ㅂ+ㅗ)    ㅂ               모음 제거
ㅂ            (없음)           초성 제거, 조합 종료
```

### 라이선스

MIT — [LICENSE](LICENSE) 참고.

---

## English

A lightweight, dependency-free C# library for real-time Hangul (Korean) composition using the standard Dubeolsik (두벌식) keyboard layout.

Converts Latin keystrokes into fully composed Unicode syllable blocks (U+AC00–U+D7A3) on the fly — suitable for game engines, custom text input widgets, or any environment where you handle keyboard input directly rather than relying on an OS IME.

### Features

- Full Dubeolsik (KS X 5002) key mapping
- Real-time syllable composition: initial (초성) + vowel (중성) + final (종성)
- Compound vowels: ㅘ ㅙ ㅚ ㅝ ㅞ ㅟ ㅢ
- Compound finals: ㄳ ㄵ ㄶ ㄺ ㄻ ㄼ ㄽ ㄾ ㄿ ㅀ ㅄ
- Tense consonants (된소리): ㄲ ㄸ ㅃ ㅆ ㅉ
- Backspace decomposition (step-by-step)
- Separator key (`\`) to disambiguate syllable boundaries
- Escape sequence (`\\`) to type a literal `\`
- Zero dependencies — three `.cs` files, drop in anywhere

### Files

| File | Description |
|------|-------------|
| `KoreanKeymap.cs` | Maps Latin characters to Hangul jamo (U+3131–U+3163) |
| `KoreanComposer.cs` | Stateful composer that assembles jamo into syllable blocks |
| `KoreanInputHandler.cs` | All-in-one handler: keymap + composer + separator/escape logic |

### Installation

```
dotnet add package KoreanComposer
```

### Quick Start

For most use cases, `KoreanInputHandler` is all you need.

```csharp
using Hangul;

var handler = new KoreanInputHandler();

// Feed raw Latin keystrokes directly — no pre-processing needed.
// Each Input() call returns:
//   Committed  — text ready to be inserted into your document
//   Composing  — the in-progress syllable to show at the cursor (preview)

foreach (char key in userInput)
{
    var (committed, composing) = handler.Input(key);
    textBuffer.Append(committed);
    cursor.Preview = composing;  // '\0' means no preview
}

// When input focus is lost or composition must end:
textBuffer.Append(handler.Flush());
```

### Step-by-step example

```
Key   Committed   Composing   Explanation
---   ---------   ---------   -----------
r                 ㄱ          initial consonant ㄱ
k                 가          vowel ㅏ added → 가
s                 간          final consonant ㄴ added → 간
d     간          아          ㄴ splits off as next initial; 아 starts
k     아          나          ㄴ + ㅏ → 나
      나                      Flush() → 나
```

Result: `간나`

### Separator Key

By default the composer greedily attaches consonants as finals (받침).

```
qkr  →  박   (ㄱ absorbed as final)
```

Use `\` to explicitly end a syllable before the next consonant:

```
qk\r  →  바ㄱ   (바 committed, then ㄱ starts fresh)
\\    →  \      (literal backslash)
```

`KoreanInputHandler` handles all of this automatically.

### API Reference

#### `KoreanInputHandler` *(recommended)*

> Not thread-safe. Use one instance per UI thread, or create a separate instance per thread.

| Method | Returns | Description |
|--------|---------|-------------|
| `Input(char latinKey)` | `(string Committed, char Composing)` | Feed one raw Latin keystroke. Handles keymap, separator, and escape internally. |
| `Backspace()` | `(bool Consumed, char Composing)` | Decompose one step. `Consumed = true` means the handler handled it — do **not** also delete from your buffer. `false` means no composition was active — perform a normal delete. |
| `Flush()` | `string` | Commit the current syllable and reset state. |
| `Reset()` | `void` | Discard the current composition without returning it. |

| Property | Type | Description |
|----------|------|-------------|
| `ComposingChar` | `char` | The syllable currently being composed (`'\0'` if none). |
| `IsComposing` | `bool` | `true` if a syllable is in progress. |

#### `KoreanComposer` *(low-level)*

Use this directly if you have your own keymap or separator logic.

| Method | Returns | Description |
|--------|---------|-------------|
| `Input(char jamo)` | `(string Committed, char Composing)` | Feed one jamo character (U+3131–U+3163). |
| `Backspace()` | `(bool Consumed, char Composing)` | Decompose one step. |
| `Flush()` | `string` | Commit the current syllable and reset state. |
| `Reset()` | `void` | Discard the current composition without returning it. |

| Property | Type | Description |
|----------|------|-------------|
| `ComposingChar` | `char` | The syllable currently being composed (`'\0'` if none). |
| `IsComposing` | `bool` | `true` if a syllable is in progress. |

#### `KoreanKeymap` *(static)*

| Method | Returns | Description |
|--------|---------|-------------|
| `TryMap(char latin, out char jamo)` | `bool` | Convert a Latin character to its Dubeolsik jamo equivalent. Returns `false` if the character is not part of the layout. |
| `IsSeparator(char c)` | `bool` | Returns `true` if `c` is the separator character (`\`). |

### Dubeolsik Layout Reference

```
Normal:   q=ㅂ w=ㅈ e=ㄷ r=ㄱ t=ㅅ   y=ㅛ u=ㅕ i=ㅑ o=ㅐ p=ㅔ
          a=ㅁ s=ㄴ d=ㅇ f=ㄹ g=ㅎ   h=ㅗ j=ㅓ k=ㅏ l=ㅣ
          z=ㅋ x=ㅌ c=ㅊ v=ㅍ b=ㅠ   n=ㅜ m=ㅡ

Shift:    Q=ㅃ W=ㅉ E=ㄸ R=ㄲ T=ㅆ   O=ㅒ P=ㅖ
```

### Backspace Behavior

```
Composing      After Backspace   Note
-----------    ---------------   ----
닭 (ㄷ+ㅏ+ㄺ)   달 (ㄷ+ㅏ+ㄹ)    compound final split: ㄺ → ㄹ
달 (ㄷ+ㅏ+ㄹ)   다 (ㄷ+ㅏ)       final removed
봐 (ㅂ+ㅘ)     보 (ㅂ+ㅗ)       compound vowel split: ㅘ → ㅗ
보 (ㅂ+ㅗ)     ㅂ                vowel removed
ㅂ             (none)            initial removed, composition ends
```

### TestConsole

A minimal console app for manual and automated testing is included in `TestConsole/`.

```
cd TestConsole
dotnet run
```

### License

MIT — see [LICENSE](LICENSE).
