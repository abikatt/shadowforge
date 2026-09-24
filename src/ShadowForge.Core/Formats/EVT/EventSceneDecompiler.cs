using System.Globalization;
using System.Text;
using ShadowForge.IO;

namespace ShadowForge.Formats.EVT;

public static class EventSceneDecompiler
{
    private static void Write(EventScene scene, TextWriter w, string? trackFilter, bool raw)
    {
        w.WriteLine(FormattableString.Invariant(
            $"{scene.Name}: version={scene.Version} screen={scene.Width}x{scene.Height} scale={scene.Scale:0.###} frames={scene.Frames} tracks={scene.Tracks.Count}"));
        for (int i = 0; i < scene.Tracks.Count; i++)
        {
            var track = scene.Tracks[i];
            if (trackFilter is not null &&
                !track.Name.Contains(trackFilter, StringComparison.OrdinalIgnoreCase) &&
                !track.Label.Contains(trackFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            w.WriteLine(FormattableString.Invariant(
                $"track[{i}] type={track.Type} ({KindName(track)}) name=\"{track.Name}\" label=\"{track.Label}\" flags=0x{track.Flags:X} extra=0x{track.Extra:X} entries={track.Entries.Count}"));
            foreach (var entry in track.Entries)
            {
                if (entry.Keys.Count == 0)
                {
                    w.WriteLine(FormattableString.Invariant($"  @{entry.Frame,-6} end"));
                    continue;
                }
                foreach (var key in entry.Keys)
                {
                    string? text = raw ? null : DescribeKey(track, key);
                    w.WriteLine(FormattableString.Invariant($"  @{entry.Frame,-6} {text ?? RawKey(key)}"));
                }
            }
        }
    }

    /// <summary>
    /// One line per key, grouped by track and entry frame. Object keys with a known layout
    /// are decoded, and every other key is shown as its payload words so nothing is hidden.
    /// A track filter matches a substring of the track name or label.
    /// </summary>
    public static string Decompile(EventScene scene, string? trackFilter = null, bool raw = false)
    {
        var sb = new StringBuilder();
        using var w = new StringWriter(sb, CultureInfo.InvariantCulture);
        Write(scene, w, trackFilter, raw);
        return sb.ToString();
    }

    private static string KindName(EventTrack track)
    {
        if (track.Type == (int)EventTrackKind.Object)
            return track.IsActor ? "actor" : track.Name.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? "map" : "object";
        return Enum.IsDefined(typeof(EventTrackKind), track.Type)
            ? ((EventTrackKind)track.Type).ToString().ToLowerInvariant()
            : "unknown";
    }

    /// <summary>
    /// Null when the key's layout is not known.
    /// </summary>
    private static string? DescribeKey(EventTrack track, EventKey key)
    {
        if (track.Type != (int)EventTrackKind.Object)
            return null;
        var d = key.Data;
        bool actor = track.IsActor;
        switch ((ObjectKey)key.Type)
        {
            case ObjectKey.Show:
                return d.Length >= 4 ? $"show flag={U32(d, 0)}" : "show";
            case ObjectKey.Hide:
                return "hide";
            case ObjectKey.Motion:
            case ObjectKey.MotionAt:
                if (d.Length < 140) return null;
                string motion = FormattableString.Invariant(
                    $"motion \"{Name(d, 0)}\" speed={F(d, 64)} label={Name(d, 68)} frames={U32(d, 132)} x144={U32(d, 136)}");
                if (key.Type == (int)ObjectKey.MotionAt && d.Length >= 144)
                    motion += $" start={U32(d, 140)}";
                return motion;
        }
        if (actor)
            return null;
        switch ((ObjectKey)key.Type)
        {
            case ObjectKey.Position:
                return d.Length >= 12 ? $"position {V3(d, 0)}" : null;
            case ObjectKey.Rotation:
                return d.Length >= 12 ? $"rotation deg {V3(d, 0)}" : null;
            case ObjectKey.Scale:
                return d.Length >= 12 ? $"scale {V3(d, 0)}" : null;
            case ObjectKey.PositionLerp:
                return d.Length >= 16 ? $"position over {U32(d, 0)} frames to {V3(d, 4)}" : null;
            case ObjectKey.RotationLerp:
                return d.Length >= 16 ? $"rotation over {U32(d, 0)} frames to deg {V3(d, 4)}" : null;
            case ObjectKey.ScaleLerp:
                return d.Length >= 16 ? $"scale over {U32(d, 0)} frames to {V3(d, 4)}" : null;
            case ObjectKey.Attach:
                return d.Length >= 148
                    ? $"attach to {Name(d, 16)} bone {Name(d, 80)} for {U32(d, 0)} frames offset {V3(d, 4)} flags=0x{U32(d, 144):X}"
                    : null;
            case ObjectKey.AttachRotated:
                return d.Length >= 164
                    ? $"attach to {Name(d, 16)} bone {Name(d, 80)} for {U32(d, 0)} frames offset {V3(d, 4)} rot deg {V3(d, 148)} flags=0x{U32(d, 144):X} rotateOffset={U32(d, 160)}"
                    : null;
            case ObjectKey.MovePath:
                return "move path";
            case ObjectKey.GuidePath:
                return "guide path";
            case ObjectKey.Color:
                return d.Length >= 16 ? $"color {Rgba(d, 0)}" : null;
            case ObjectKey.ColorLerp:
                return d.Length >= 20 ? $"color over {U32(d, 0)} frames to {Rgba(d, 4)}" : null;
            case ObjectKey.Transform:
                return d.Length >= 36 ? $"transform pos {V3(d, 0)} rot deg {V3(d, 12)} scale {V3(d, 24)}" : null;
            case ObjectKey.MeshParts:
                return d.Length >= 4 ? $"mesh parts mask=0x{U32(d, 0):X}" : null;
            case ObjectKey.MaterialColor:
                return d.Length >= 52
                    ? $"material over {U32(d, 0)} frames slot0 {V3(d, 4)} slot1 {V3(d, 28)} slot2 {V3(d, 16)} slot3 {V3(d, 40)}"
                    : null;
        }
        return null;
    }

    /// <summary>
    /// Type, size and up to 12 payload words, each shown as a float when it reads as a plausible one.
    /// </summary>
    private static string RawKey(EventKey key)
    {
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant($"key{key.Type}/{key.Size}"));
        var d = key.Data;
        int words = d.Length / 4;
        int shown = Math.Min(words, 12);
        for (int i = 0; i < shown; i++)
        {
            uint u = U32(d, i * 4);
            float f = F(d, i * 4);
            sb.Append(' ');
            if (u == 0)
                sb.Append('0');
            else if (Math.Abs(f) is >= 1e-4f and < 1e6f)
                sb.Append(f.ToString("0.###", CultureInfo.InvariantCulture));
            else if (u < 0x10000)
                sb.Append(u.ToString(CultureInfo.InvariantCulture));
            else
                sb.Append("0x").Append(u.ToString("X8", CultureInfo.InvariantCulture));
        }
        if (words > shown)
            sb.Append(FormattableString.Invariant($" ... ({d.Length} bytes)"));
        string text = EventSceneReader.ReadName(d, 0, d.Length);
        if (text.Length >= 4 && text.All(c => c >= ' ' && c < 127))
            sb.Append(" \"").Append(text).Append('"');
        return sb.ToString();
    }

    private static uint U32(byte[] d, int o) => BigEndian.ReadUInt32(d, o);
    private static float F(byte[] d, int o) => BigEndian.ReadFloat(d, o);
    private static string Name(byte[] d, int o) => EventSceneReader.ReadName(d, o);

    private static string V3(byte[] d, int o) => FormattableString.Invariant(
        $"({F(d, o):0.###},{F(d, o + 4):0.###},{F(d, o + 8):0.###})");

    private static string Rgba(byte[] d, int o) => FormattableString.Invariant(
        $"({U32(d, o)},{U32(d, o + 4)},{U32(d, o + 8)},{U32(d, o + 12)})");
}
