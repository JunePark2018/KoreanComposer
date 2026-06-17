using Hangul;

namespace KoreanComposer.Tests;

public class KoreanComposerTests
{
    private static string ComposeAll(IEnumerable<char> jamos)
    {
        var composer = new Hangul.KoreanComposer();
        var sb = new System.Text.StringBuilder();
        foreach (char j in jamos)
        {
            var (committed, _) = composer.Input(j);
            sb.Append(committed);
        }
        sb.Append(composer.Flush());
        return sb.ToString();
    }

    [Fact]
    public void SimpleSyllable_ComposesCorrectly()
    {
        Assert.Equal("가", ComposeAll("ㄱㅏ"));
    }

    [Fact]
    public void SyllableWithFinal_ComposesCorrectly()
    {
        Assert.Equal("간", ComposeAll("ㄱㅏㄴ"));
    }

    [Fact]
    public void FinalSplitsToNextInitial_WhenVowelFollows()
    {
        // 간 + 아 → 가나 (ㄴ이 받침에서 다음 초성으로 이동)
        Assert.Equal("가나", ComposeAll("ㄱㅏㄴㅏ"));
    }

    [Fact]
    public void CompoundVowel_Composes()
    {
        Assert.Equal("봐", ComposeAll("ㅂㅗㅏ"));
    }

    [Fact]
    public void CompoundFinal_Composes()
    {
        Assert.Equal("닭", ComposeAll("ㄷㅏㄹㄱ"));
    }

    [Fact]
    public void TenseConsonant_DoesNotAttachAsFinal()
    {
        // ㄸ는 받침 불가 → 보 확정 후 새 음절 ㄸ 시작
        var composer = new Hangul.KoreanComposer();
        composer.Input('ㅂ');
        composer.Input('ㅗ');
        var (committed, composing) = composer.Input('ㄸ');
        Assert.Equal("보", committed);
        Assert.Equal('ㄸ', composing);
    }

    [Fact]
    public void LoneVowel_WithNoPrecedingConsonant_OutputsJamoDirectly()
    {
        var composer = new Hangul.KoreanComposer();
        var (committed, composing) = composer.Input('ㅏ');
        Assert.Equal(string.Empty, committed);
        Assert.Equal('ㅏ', composing);
        Assert.True(composer.IsComposing);
        Assert.Equal("ㅏ", composer.Flush());
    }

    [Fact]
    public void LoneCompoundVowel_CombinesWithoutInitial()
    {
        // 초성 없이 ㅗ + ㅏ → ㅘ (받침 없는 ㅇ을 끼워넣지 않고 자모만 결합)
        Assert.Equal("ㅘ", ComposeAll("ㅗㅏ"));
    }

    [Fact]
    public void LoneVowels_NonCombinable_OutputSeparately()
    {
        // ㅏ + ㅣ는 복합 모음 조합표에 없으므로 각각 따로 확정
        Assert.Equal("ㅏㅣ", ComposeAll("ㅏㅣ"));
    }

    [Fact]
    public void LoneVowel_ThenConsonant_CommitsVowelBeforeNewInitial()
    {
        var composer = new Hangul.KoreanComposer();
        composer.Input('ㅗ');
        var (committed, composing) = composer.Input('ㄱ');
        Assert.Equal("ㅗ", committed);
        Assert.Equal('ㄱ', composing);
    }

    [Fact]
    public void Backspace_SplitsCompoundFinal()
    {
        var composer = new Hangul.KoreanComposer();
        foreach (char j in "ㄷㅏㄹㄱ") composer.Input(j); // 닭
        var (consumed, composing) = composer.Backspace();
        Assert.True(consumed);
        Assert.Equal('달', composing);
    }

    [Fact]
    public void Backspace_SplitsCompoundVowel()
    {
        var composer = new Hangul.KoreanComposer();
        foreach (char j in "ㅂㅗㅏ") composer.Input(j); // 봐
        var (consumed, composing) = composer.Backspace();
        Assert.True(consumed);
        Assert.Equal('보', composing);
    }

    [Fact]
    public void Backspace_RemovesVowel_LeavingInitialOnly()
    {
        var composer = new Hangul.KoreanComposer();
        composer.Input('ㅂ');
        composer.Input('ㅗ');
        var (consumed, composing) = composer.Backspace();
        Assert.True(consumed);
        Assert.Equal('ㅂ', composing);
    }

    [Fact]
    public void Backspace_RemovesInitial_EndsComposition()
    {
        var composer = new Hangul.KoreanComposer();
        composer.Input('ㅂ');
        var (consumed, composing) = composer.Backspace();
        Assert.True(consumed);
        Assert.Equal('\0', composing);
        Assert.False(composer.IsComposing);
    }

    [Fact]
    public void Backspace_WithNoComposition_IsNotConsumed()
    {
        var composer = new Hangul.KoreanComposer();
        var (consumed, composing) = composer.Backspace();
        Assert.False(consumed);
        Assert.Equal('\0', composing);
    }

    [Fact]
    public void Flush_CommitsComposingChar()
    {
        var composer = new Hangul.KoreanComposer();
        composer.Input('ㄱ');
        composer.Input('ㅏ');
        Assert.Equal("가", composer.Flush());
        Assert.False(composer.IsComposing);
    }

    [Fact]
    public void Reset_DiscardsComposition()
    {
        var composer = new Hangul.KoreanComposer();
        composer.Input('ㄱ');
        composer.Input('ㅏ');
        composer.Reset();
        Assert.False(composer.IsComposing);
        Assert.Equal(string.Empty, composer.Flush());
    }
}
