namespace ShadowForge.Formats.EVT;

/// <summary>
/// Track type words, by the child task the event player creates for each.
/// </summary>
public enum EventTrackKind
{
    Effect = 1,
    Object = 2,
    Camera = 5,
    Text = 8,
    Fade = 10,
    Bgm = 12,
    Se = 13,
    Gimmick = 17,
    EnvSe = 18,
}
