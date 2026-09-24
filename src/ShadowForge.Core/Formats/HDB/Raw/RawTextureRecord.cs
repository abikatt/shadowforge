using System.Text;
using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Raw;

public sealed class RawTextureRecord
{
    public string Name { get; set; } = "";

    public float FloatParam { get; set; }

    /// <summary>
    /// 2 marks a normal map.
    /// </summary>
    public uint FlagField18 { get; set; }

    /// <summary>
    /// Assigns the texture name. The writer NUL-pads the whole field, so a shorter name
    /// leaves nothing of the previous one behind.
    /// </summary>
    public void SetName(string name)
    {
        int width = Encoding.ASCII.GetByteCount(name);
        if (width > TextureData.NameWidth)
            throw new ArgumentException(
                $"Texture name '{name}' is {width} bytes and the field holds {TextureData.NameWidth}.", nameof(name));
        Name = name;
    }
}
