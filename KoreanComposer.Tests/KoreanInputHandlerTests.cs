using Hangul;

namespace KoreanComposer.Tests;

public class KoreanInputHandlerTests
{
    private static string TypeAll(KoreanInputHandler handler, string keys)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char k in keys)
        {
            var (committed, _) = handler.Input(k);
            sb.Append(committed);
        }
        sb.Append(handler.Flush());
        return sb.ToString();
    }

    [Fact]
    public void GreedyFinal_AbsorbsConsonant()
    {
        var handler = new KoreanInputHandler();
        Assert.Equal("박", TypeAll(handler, "qkr"));
    }

    [Fact]
    public void Separator_ForcesSyllableBoundary()
    {
        var handler = new KoreanInputHandler();
        Assert.Equal("바ㄱ", TypeAll(handler, "qk\\r"));
    }

    [Fact]
    public void DoubleSeparator_OutputsLiteralBackslash()
    {
        var handler = new KoreanInputHandler();
        Assert.Equal("가\\나", TypeAll(handler, "rk\\\\sk"));
    }

    [Fact]
    public void NonKeymapCharacter_FlushesAndPassesThrough()
    {
        var handler = new KoreanInputHandler();
        Assert.Equal("가 ", TypeAll(handler, "rk "));
    }

    [Fact]
    public void Backspace_NotConsumed_WhenNoComposition()
    {
        var handler = new KoreanInputHandler();
        var (consumed, composing) = handler.Backspace();
        Assert.False(consumed);
        Assert.Equal('\0', composing);
    }

    [Fact]
    public void Backspace_CancelsPendingEscape()
    {
        var handler = new KoreanInputHandler();
        handler.Input('\\');
        var (consumed, _) = handler.Backspace();
        Assert.True(consumed);
        // 이스케이프 취소 후 일반 입력 재개
        Assert.Equal("가", TypeAll(handler, "rk"));
    }

    [Fact]
    public void LoneVowel_OutputsJamoDirectly()
    {
        var handler = new KoreanInputHandler();
        Assert.Equal("ㅏ", TypeAll(handler, "k"));
    }
}
