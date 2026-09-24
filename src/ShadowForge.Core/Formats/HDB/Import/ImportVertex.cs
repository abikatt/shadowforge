using System.Numerics;

namespace ShadowForge.Formats.HDB.Import;

public sealed class ImportVertex
{
    public Vector3 WorldPosition;
    public Vector3 WorldNormal;
    public Vector2 UV;
    public Vector3 WorldTangent;
    public ImportInfluence[] Influences = [];

    /// <summary>
    /// Iris (stage-1) UV from glTF TEXCOORD_1. Zero on unstaged geometry.
    /// </summary>
    public Vector2 UVEye;

    /// <summary>
    /// Eyelid (stage-2) UV from glTF TEXCOORD_2. Zero on unstaged geometry.
    /// </summary>
    public Vector2 UVEyelid;
}
