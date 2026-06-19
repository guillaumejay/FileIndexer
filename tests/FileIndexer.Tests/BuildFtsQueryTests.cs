using FileIndexer.Data;

namespace FileIndexer.Tests;

// Unit tests for the FTS5 query builder. This is the logic behind issue #2:
// punctuation-only input must not produce an (invalid) empty MATCH expression.
public class BuildFtsQueryTests
{
    [Fact]
    public void SingleWord_BecomesPrefixSearch()
    {
        Assert.Equal("anim*", IndexDbContext.BuildFtsQuery("anim"));
    }

    [Fact]
    public void MultipleWords_EachBecomePrefixSearch()
    {
        Assert.Equal("foo* bar*", IndexDbContext.BuildFtsQuery("foo bar"));
    }

    [Fact]
    public void WordWithPunctuation_UsesNearForAdjacency()
    {
        // "d&d" -> the tokenizer splits on '&' into "d","d", combined with NEAR(...,0)
        Assert.Equal("NEAR(\"d\" \"d\", 0)", IndexDbContext.BuildFtsQuery("d&d"));
    }

    [Theory]
    [InlineData("+++")]
    [InlineData("--")]
    [InlineData("???")]
    [InlineData("   ")]
    [InlineData("&")]
    public void PunctuationOnly_ReturnsEmpty(string input)
    {
        // The fix for #2: no tokens => empty string, so callers can skip the MATCH entirely.
        Assert.Equal(string.Empty, IndexDbContext.BuildFtsQuery(input));
    }

    [Fact]
    public void MixedValidAndPunctuation_KeepsValidTokens()
    {
        Assert.Equal("hello*", IndexDbContext.BuildFtsQuery("hello +++"));
    }
}
