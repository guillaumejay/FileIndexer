using FileIndexer.Data;

namespace FileIndexer.Tests;

// Unit tests for the FTS5 query builder. This is the logic behind issue #2:
// punctuation-only input must not produce an (invalid) empty MATCH expression.
public class BuildFtsQueryTests
{
    [Fact]
    public void SingleWord_BecomesPrefixSearch()
    {
        Assert.Equal("\"anim\"*", IndexDbContext.BuildFtsQuery("anim"));
    }

    [Fact]
    public void MultipleWords_EachBecomePrefixSearch()
    {
        Assert.Equal("\"foo\"* \"bar\"*", IndexDbContext.BuildFtsQuery("foo bar"));
    }

    [Fact]
    public void WordWithPunctuation_UsesNearForAdjacency()
    {
        // "foo-bar" -> the tokenizer splits on '-' into "foo","bar", combined with NEAR(...,0)
        Assert.Equal("NEAR(\"foo\" \"bar\", 0)", IndexDbContext.BuildFtsQuery("foo-bar"));
    }

    [Theory]
    [InlineData("d&d", "(NEAR(\"d\" \"d\", 0) OR \"dnd\"* OR NEAR(\"d\" \"and\" \"d\", 0))")]
    [InlineData("dnd", "(\"dnd\"* OR NEAR(\"d\" \"d\", 0) OR NEAR(\"d\" \"and\" \"d\", 0))")]
    public void Ampersand_AlsoMatchesSpelledOutForms(string input, string expected)
    {
        Assert.Equal(expected, IndexDbContext.BuildFtsQuery(input));
    }

    [Theory]
    [InlineData("and")]
    [InlineData("ant")]
    public void WordsShapedLikeXnY_StayPlainWords(string input)
    {
        Assert.Equal($"\"{input}\"*", IndexDbContext.BuildFtsQuery(input));
    }

    [Fact]
    public void Pipe_SeparatesAlternatives()
    {
        Assert.Equal("(\"osr\"* OR \"oldschool\"*) \"box\"*", IndexDbContext.BuildFtsQuery("osr|oldschool box"));
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
        Assert.Equal("\"hello\"*", IndexDbContext.BuildFtsQuery("hello +++"));
    }
}
