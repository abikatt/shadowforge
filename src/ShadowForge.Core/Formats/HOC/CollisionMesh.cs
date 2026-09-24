using System.Numerics;
using ShadowForge.IO;

namespace ShadowForge.Formats.HOC;

/// <summary>
/// A stage's collision mesh, referenced from its .map as PARTS OCT "{stage}c.hocb". The file
/// is big-endian and positions are already in stage world space, so placed-model transforms
/// do not apply. A 0x10-byte header ("COH@", version, file size, chunk count) is followed by
/// 0x10-byte chunk rows of (offset, size, tag, reserved), each offset relative to its row.
/// Tag 0x200 is the triangle array (0x48 stride) and tag 0x203 the surface array (0x20
/// stride) that each triangle's leading self-relative pointer selects.
/// </summary>
public sealed class CollisionMesh
{
    public const uint Magic = 0x434F4840;
    public const int HeaderSize = 0x10;
    public const int ChunkRowSize = 0x10;
    public const int TriangleStride = 0x48;
    public const int SurfaceStride = 0x20;

    private const uint TagTriangles = 0x200;
    private const uint TagSurfaces = 0x203;

    private const int TriSurfacePtr = 0x00;
    private const int TriVertices = 0x08;
    private const int TriNormal = 0x2C;

    public required IReadOnlyList<Triangle> Triangles { get; init; }

    /// <summary>
    /// Surface descriptors, indexed by <see cref="Triangle.Surface"/>.
    /// </summary>
    public required IReadOnlyList<Surface> Surfaces { get; init; }

    public static CollisionMesh ReadFile(string path) => Read(File.ReadAllBytes(path));

    public static CollisionMesh Read(byte[] data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException($"HOC file truncated: {data.Length} bytes");

        uint magic = BigEndian.ReadUInt32(data, 0);
        if (magic != Magic)
            throw new InvalidDataException($"Invalid HOC magic: 0x{magic:X8}");

        int chunkCount = BigEndian.ReadInt32(data, 0x0C);
        if (chunkCount < 0 || HeaderSize + (long)chunkCount * ChunkRowSize > data.Length)
            throw new InvalidDataException($"HOC chunk count out of range: {chunkCount}");

        (int Offset, int Size)? tris = null, surfs = null;
        for (int i = 0; i < chunkCount; i++)
        {
            int row = HeaderSize + i * ChunkRowSize;
            int offset = row + BigEndian.ReadInt32(data, row);
            int size = BigEndian.ReadInt32(data, row + 4);
            uint tag = BigEndian.ReadUInt32(data, row + 8);
            if (offset < 0 || size < 0 || (long)offset + size > data.Length)
                throw new InvalidDataException($"HOC chunk {i} (tag 0x{tag:X}) out of range");

            if (tag == TagTriangles) tris = (offset, size);
            else if (tag == TagSurfaces) surfs = (offset, size);
        }

        var surfaces = ReadSurfaces(data, surfs);
        return new CollisionMesh
        {
            Surfaces = surfaces,
            Triangles = ReadTriangles(data, tris, surfs?.Offset ?? 0, surfaces.Count),
        };
    }

    private static List<Surface> ReadSurfaces(byte[] data, (int Offset, int Size)? chunk)
    {
        var list = new List<Surface>();
        if (chunk is not { } c) return list;

        for (int o = c.Offset; o + SurfaceStride <= c.Offset + c.Size; o += SurfaceStride)
        {
            list.Add(new Surface
            {
                Material = BigEndian.ReadUInt32(data, o),
                Flags = BigEndian.ReadUInt32(data, o + 4),
                DebugColor = BigEndian.ReadUInt32(data, o + 8),
            });
        }
        return list;
    }

    private static List<Triangle> ReadTriangles(byte[] data, (int Offset, int Size)? chunk,
        int surfaceBase, int surfaceCount)
    {
        var list = new List<Triangle>();
        if (chunk is not { } c) return list;

        for (int o = c.Offset; o + TriangleStride <= c.Offset + c.Size; o += TriangleStride)
        {
            int surfaceOffset = o + BigEndian.ReadInt32(data, o + TriSurfacePtr);
            int surface = -1;
            if (surfaceCount > 0 && surfaceOffset >= surfaceBase)
            {
                int index = (surfaceOffset - surfaceBase) / SurfaceStride;
                if (index < surfaceCount && (surfaceOffset - surfaceBase) % SurfaceStride == 0)
                    surface = index;
            }

            list.Add(new Triangle(
                ReadVector3(data, o + TriVertices),
                ReadVector3(data, o + TriVertices + 12),
                ReadVector3(data, o + TriVertices + 24),
                ReadVector3(data, o + TriNormal),
                surface));
        }
        return list;
    }

    private static Vector3 ReadVector3(byte[] data, int offset) => new(
        BigEndian.ReadFloat(data, offset),
        BigEndian.ReadFloat(data, offset + 4),
        BigEndian.ReadFloat(data, offset + 8));
}
