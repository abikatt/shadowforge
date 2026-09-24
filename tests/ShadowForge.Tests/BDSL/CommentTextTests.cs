using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Tests.BDSL;

public sealed class CommentTextTests
{
    [Fact]
    public void Decode_LowHalfFirst()
    {
        uint[] words = [0x42004F, 0x45004A, 0x540043, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.True(CommentText.TryDecode(words, out var text));
        Assert.Equal("OBJECT", text);
    }

    [Fact]
    public void Decode_Japanese()
    {
        uint[] words = [0x306E305D, 0x42004F, 0x45004A, 0x540043, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.True(CommentText.TryDecode(words, out var text));
        Assert.Equal("\u305D\u306EOBJECT", text);
    }

    [Fact]
    public void Encode_RoundTrips()
    {
        var text = "\u5BBF\u5C4B\u30DE\u30AF\u30ED\u958B\u59CB";
        var words = CommentText.Encode(text);
        Assert.Equal(14, words.Length);
        Assert.True(CommentText.TryDecode(words, out var back));
        Assert.Equal(text, back);
    }

    [Fact]
    public void Encode_Full28Units()
    {
        var text = new string('a', 28);
        var words = CommentText.Encode(text);
        Assert.True(CommentText.TryDecode(words, out var back));
        Assert.Equal(text, back);
    }

    [Fact]
    public void Encode_RejectsTooLongAndNul()
    {
        Assert.Throws<FormatException>(() => CommentText.Encode(new string('a', 29)));
        Assert.Throws<FormatException>(() => CommentText.Encode("a\0b"));
    }

    [Fact]
    public void Decode_RejectsGarbageAfterTerminator()
    {
        uint[] words = [0x00000041, 0, 0x42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.False(CommentText.TryDecode(words, out _));
    }

    [Fact]
    public void Decode_RejectsControlCharacters()
    {
        uint[] lineFeed = [0x000A0041, 0x42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.False(CommentText.TryDecode(lineFeed, out _));
        uint[] carriageReturn = [0x000D0041, 0x42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.False(CommentText.TryDecode(carriageReturn, out _));
        uint[] tab = [0x00090041, 0x42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.False(CommentText.TryDecode(tab, out _));
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.False(CommentText.TryDecode(new uint[6], out _));
    }
}
