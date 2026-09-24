using System.Buffers.Binary;
using System.Text;
using ShadowForge.Formats.EVT;
using ShadowForge.IO;

namespace ShadowForge.Tests.Formats.EVT;

public sealed class EventSceneReaderTests
{
    [Fact]
    public void Read_Synthetic_ObjectTrackDecodes()
    {
        var data = BuildScene();
        var scene = EventSceneReader.Read(data, "synthetic");

        Assert.Equal(20060810u, scene.Version);
        Assert.Equal(1280, scene.Width);
        Assert.Equal(720, scene.Height);
        Assert.Equal(5046, scene.Frames);
        Assert.Single(scene.Tracks);

        var track = scene.Tracks[0];
        Assert.Equal(EventTrackKind.Object, track.Kind);
        Assert.False(track.IsActor);
        Assert.Equal("ei26.hdb", track.Name);
        Assert.Equal("eo000", track.Label);
        Assert.Equal(3, track.Entries.Count);

        Assert.Equal(0, track.Entries[0].Frame);
        Assert.Equal(new[] { 1, 27, 22 }, track.Entries[0].Keys.Select(k => k.Type).ToArray());
        Assert.Equal(39, track.Entries[1].Frame);
        Assert.Equal(new[] { 2 }, track.Entries[1].Keys.Select(k => k.Type).ToArray());

        Assert.Empty(track.Entries[2].Keys);

        string text = EventSceneDecompiler.Decompile(scene);
        Assert.Contains("motion \"ev250_ei26_c0102.hmb\" speed=1 label=EVENT000 frames=39 x144=0 start=0", text);
        Assert.Contains("transform pos (0,0,0) rot deg (0,0,0) scale (1,1,1)", text);
        Assert.Contains("@39     hide", text);
    }

    [Fact]
    public void Read_KeyRunningPastEntry_Throws()
    {
        var data = BuildScene();

        int firstKey = EventScene.HeaderSize + EventTrack.RecordSize + EventEntry.RecordSize;
        BigEndian.WriteUInt32(data, firstKey + EventKey.SizeOffset, 0x1000);
        Assert.Throws<InvalidDataException>(() => EventSceneReader.Read(data, "corrupt"));
    }

    [SkippableFact]
    public void Read_Shipped_Ev250_WalksEveryTrack()
    {
        var scene = EventSceneReader.Read(RetailData.Read(@"event\ev250\ev250_evt_00.evt"), "ev250_evt_00");
        Assert.Equal(50, scene.Tracks.Count);
        Assert.Equal(5046, scene.Frames);
        var collar = Assert.Single(scene.Tracks, t => t.Name == "ei26.hdb");
        Assert.Equal(75, collar.Entries.Count);
        Assert.DoesNotContain(collar.Entries.SelectMany(e => e.Keys), k => k.Type is 11 or 28);
    }

    private static void U32(BinaryWriter w, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, v);
        w.Write(b);
    }

    private static void F32(BinaryWriter w, float v) => U32(w, BitConverter.SingleToUInt32Bits(v));

    private static void Name(BinaryWriter w, string s, int len)
    {
        var b = new byte[len];
        Encoding.ASCII.GetBytes(s, 0, s.Length, b, 0);
        w.Write(b);
    }

    private static byte[] BuildScene()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write(Encoding.ASCII.GetBytes("EVT\0"));
        w.Write(new byte[12]);
        U32(w, 20060810);
        U32(w, 0xFFA2A5A2);
        U32(w, 1280);
        U32(w, 720);
        F32(w, 39.6f);
        U32(w, 5046);
        U32(w, 1);

        U32(w, 2); U32(w, 3); Name(w, "ei26.hdb", 64); Name(w, "eo000", 64); U32(w, 0); U32(w, 0);

        var keys = new MemoryStream();
        var kw = new BinaryWriter(keys);
        U32(kw, 1); U32(kw, 12); U32(kw, 0);
        U32(kw, 27); U32(kw, 44); for (int i = 0; i < 6; i++) F32(kw, 0f); F32(kw, 1f); F32(kw, 1f); F32(kw, 1f);
        U32(kw, 22); U32(kw, 152); Name(kw, "ev250_ei26_c0102.hmb", 64); F32(kw, 1f); Name(kw, "EVENT000", 64); U32(kw, 39); U32(kw, 0); U32(kw, 0);
        kw.Flush();
        U32(w, 0); Name(w, "ei26.hdb", 64); U32(w, (uint)keys.Length); w.Write(keys.ToArray());

        U32(w, 39); Name(w, "ei26.hdb", 64); U32(w, 8); U32(w, 2); U32(w, 8);

        U32(w, 0); Name(w, "ei26.hdb", 64); U32(w, 8); U32(w, 0); w.Write(new byte[] { 8, 0, 0, 0 });

        w.Flush();
        return ms.ToArray();
    }
}
