# KoreanComposer

A lightweight, dependency-free C# library for real-time Hangul (Korean) composition using the standard Dubeolsik (두벌식) keyboard layout.

Converts Latin keystrokes into fully composed Unicode syllable blocks (U+AC00–U+D7A3) on the fly — suitable for game engines, custom text input widgets, or any environment where you handle keyboard input directly rather than relying on an OS IME.

## Features

- Full Dubeolsik (KS X 5002) key mapping
- Real-time syllable composition: initial (초성) + vowel (중성) + final (종성)
- Compound vowels: ㅘ ㅙ ㅚ ㅝ ㅞ ㅟ ㅢ
- Compound finals: ㄳ ㄵ ㄶ ㄺ ㄻ ㄼ ㄽ ㄾ ㄿ ㅀ ㅄ
- Tense consonants (된소리): ㄲ ㄸ ㅃ ㅆ ㅉ
- Backspace decomposition (step-by-step)
- Separator key (`\`) to disambiguate syllable boundaries
- Escape sequence (`\\`) to type a literal `\`
- Zero dependencies — three `.cs` files, drop in anywhere

## Files

| File | Description |
|------|-------------|
| `KoreanKeymap.cs` | Maps Latin characters to Hangul jamo (U+3131–U+3163) |
| `KoreanComposer.cs` | Stateful composer that assembles jamo into syllable blocks |
| `KoreanInputHandler.cs` | All-in-one handler: keymap + composer + separator/escape logic |

## Installation

Copy the three `.cs` files into your project. No NuGet package required.

## Quick Start

For most use cases, `KoreanInputHandler` is all you need. It wraps the keymap, composer, and separator/escape logic into a single class.

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

## Separator Key

By default the composer greedily attaches consonants as finals (받침).

```
qkr  →  박   (ㄱ absorbed as final)
```

Use `\` to explicitly end a syllable before the next consonant:

```
qk\r  →  바ㄱ   (바 committed, then ㄱ starts fresh)
```

To type a literal `\`, escape it with a second backslash:

```
\\  →  \
```

`KoreanInputHandler` handles all of this automatically.

## API Reference

### `KoreanInputHandler` *(recommended)*

> Not thread-safe. Use one instance per UI thread, or create a separate instance per thread.

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Input(char latinKey)` | `(string Committed, char Composing)` | Feed one raw Latin keystroke. Handles keymap, separator, and escape internally. |
| `Backspace()` | `(bool Consumed, char Composing)` | Decompose one step. `Consumed = true` means the handler handled it — do **not** also delete from your buffer. `false` means no composition was active — perform a normal delete. |
| `Flush()` | `string` | Commit the current syllable and reset state. |
| `Reset()` | `void` | Discard the current composition without returning it. |

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ComposingChar` | `char` | The syllable currently being composed (`'\0'` if none). |
| `IsComposing` | `bool` | `true` if a syllable is in progress. |

---

### `KoreanComposer` *(low-level)*

Use this directly if you have your own keymap or separator logic.

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Input(char jamo)` | `(string Committed, char Composing)` | Feed one jamo character (U+3131–U+3163). |
| `Backspace()` | `(bool Consumed, char Composing)` | Decompose one step. |
| `Flush()` | `string` | Commit the current syllable and reset state. |
| `Reset()` | `void` | Discard the current composition without returning it. |

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ComposingChar` | `char` | The syllable currently being composed (`'\0'` if none). |
| `IsComposing` | `bool` | `true` if a syllable is in progress. |

---

### `KoreanKeymap` *(static)*

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `TryMap(char latin, out char jamo)` | `bool` | Convert a Latin character to its Dubeolsik jamo equivalent. Returns `false` if the character is not part of the layout. |
| `IsSeparator(char c)` | `bool` | Returns `true` if `c` is the separator character (`\`). |

## Dubeolsik Layout Reference

```
Normal:   q=ㅂ w=ㅈ e=ㄷ r=ㄱ t=ㅅ   y=ㅛ u=ㅕ i=ㅑ o=ㅐ p=ㅔ
          a=ㅁ s=ㄴ d=ㅇ f=ㄹ g=ㅎ   h=ㅗ j=ㅓ k=ㅏ l=ㅣ
          z=ㅋ x=ㅌ c=ㅊ v=ㅍ b=ㅠ   n=ㅜ m=ㅡ

Shift:    Q=ㅃ W=ㅉ E=ㄸ R=ㄲ T=ㅆ   O=ㅒ P=ㅖ
```

## Backspace Behavior

`Backspace()` decomposes the current syllable one step at a time:

```
Composing      After Backspace   Note
-----------    ---------------   ----
닭 (ㄷ+ㅏ+ㄺ)   달 (ㄷ+ㅏ+ㄹ)    compound final split: ㄺ → ㄹ
달 (ㄷ+ㅏ+ㄹ)   다 (ㄷ+ㅏ)       final removed
봐 (ㅂ+ㅘ)     보 (ㅂ+ㅗ)       compound vowel split: ㅘ → ㅗ
보 (ㅂ+ㅗ)     ㅂ                vowel removed
ㅂ             (none)            initial removed, composition ends
```

## TestConsole

A minimal console app for manual and automated testing is included in `TestConsole/`.

```
cd TestConsole
dotnet run
```

It runs a suite of automated cases and then enters an interactive mode where you can type Dubeolsik keys and see the composed result in real time.

## License

MIT — see [LICENSE](LICENSE).
