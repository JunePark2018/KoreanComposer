using Hangul;

namespace KoreanComposer.Tests;

public class KoreanKeymapTests
{
    [Theory]
    [InlineData('r', 'ㄱ')]
    [InlineData('k', 'ㅏ')]
    [InlineData('R', 'ㄲ')]
    [InlineData('O', 'ㅒ')]
    public void TryMap_KnownKeys_ReturnsExpectedJamo(char latin, char expected)
    {
        Assert.True(KoreanKeymap.TryMap(latin, out char jamo));
        Assert.Equal(expected, jamo);
    }

    [Fact]
    public void TryMap_UnknownKey_ReturnsFalse()
    {
        Assert.False(KoreanKeymap.TryMap('1', out _));
    }

    [Fact]
    public void IsSeparator_Backslash_ReturnsTrue()
    {
        Assert.True(KoreanKeymap.IsSeparator('\\'));
    }

    [Fact]
    public void IsSeparator_OtherChar_ReturnsFalse()
    {
        Assert.False(KoreanKeymap.IsSeparator('r'));
    }
}
