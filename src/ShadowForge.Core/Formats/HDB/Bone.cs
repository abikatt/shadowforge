namespace ShadowForge.Formats.HDB;

public sealed class Bone
{
    public int Index { get; set; }
    public int HFlag { get; set; }

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }

    public float EulerX { get; set; }
    public float EulerY { get; set; }
    public float EulerZ { get; set; }

    public float ScaleX { get; set; } = 1f;
    public float ScaleY { get; set; } = 1f;
    public float ScaleZ { get; set; } = 1f;

    /// <summary>
    /// Two euler triples, A then B. The local rotation is A * Euler * B.
    /// </summary>
    public float[] ExtraEuler { get; set; } = new float[6];

    public int ChildIndex { get; set; } = -1;
    public int ParentIndex { get; set; } = -1;

    public string Name { get; set; } = "";
}
