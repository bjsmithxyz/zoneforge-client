using System;

public enum TerrainLayer { Grass = 0, Dirt = 1, Stone = 2, Ravine = 3 }

/// <summary>
/// Pure-C# utility for encoding/decoding terrain chunk byte arrays and
/// converting between world-space coordinates and chunk/local indices.
/// No Unity or SpacetimeDB dependencies.
/// </summary>
public static class TerrainChunkData
{
    public const int ChunkSize  = 32;
    public const int PointCount = ChunkSize * ChunkSize; // 1024
    public const int HeightBytes = PointCount * 4;       // 4096
    public const int SplatBytes  = PointCount * 4;       // 4096

    // -----------------------------------------------------------------------
    // Coordinate math

    /// <summary>Returns chunk column/row for a world X/Z position.</summary>
    public static void WorldToChunk(float worldX, float worldZ, int chunkSize, out int chunkX, out int chunkZ)
    {
        chunkX = (int)Math.Floor(worldX / chunkSize);
        chunkZ = (int)Math.Floor(worldZ / chunkSize);
    }

    /// <summary>
    /// Returns the flat index into a 32x32 chunk's data arrays for a world position.
    /// Caller must already know the point falls within the correct chunk.
    /// </summary>
    public static int WorldToLocalIndex(float worldX, float worldZ, int chunkSize)
    {
        int lx = (int)Math.Floor(worldX) % chunkSize;
        int lz = (int)Math.Floor(worldZ) % chunkSize;
        lx = ((lx % chunkSize) + chunkSize) % chunkSize; // handle negatives
        lz = ((lz % chunkSize) + chunkSize) % chunkSize;
        return lz * chunkSize + lx;
    }

    /// <summary>World-space origin of a chunk (bottom-left corner at Y=0).</summary>
    public static (float x, float z) ChunkOrigin(int chunkX, int chunkZ, int chunkSize = ChunkSize) =>
        (chunkX * chunkSize, chunkZ * chunkSize);

    // -----------------------------------------------------------------------
    // Height encoding (little-endian float32)

    public static float GetHeight(byte[] data, int index)
    {
        int offset = index * 4;
        return BitConverter.ToSingle(data, offset);
    }

    public static void SetHeight(byte[] data, int index, float value)
    {
        int offset = index * 4;
        byte[] bytes = BitConverter.GetBytes(value);
        data[offset]     = bytes[0];
        data[offset + 1] = bytes[1];
        data[offset + 2] = bytes[2];
        data[offset + 3] = bytes[3];
    }

    // -----------------------------------------------------------------------
    // Splat encoding (RGBA u8 per point, normalised)

    public static byte GetSplatByte(byte[] splat, int index, int channel)
        => splat[index * 4 + channel];

    public static float GetSplatWeight(byte[] splat, int index, TerrainLayer layer)
        => splat[index * 4 + (int)layer] / 255f;

    /// <summary>
    /// Sets the weight for <paramref name="layer"/> at <paramref name="index"/>
    /// and redistributes the remaining weight proportionally across other channels.
    /// </summary>
    public static void SetSplatLayer(byte[] splat, int index, TerrainLayer layer, float weight)
    {
        weight = Math.Max(0f, Math.Min(1f, weight));
        int offset = index * 4;
        int ch = (int)layer;

        // Read current other-channel weights.
        float[] cur = new float[4];
        float otherTotal = 0f;
        for (int i = 0; i < 4; i++)
        {
            cur[i] = splat[offset + i] / 255f;
            if (i != ch) otherTotal += cur[i];
        }

        // Redistribute remaining (1 - weight) across other channels.
        float remaining = 1f - weight;
        for (int i = 0; i < 4; i++)
        {
            if (i == ch)
                splat[offset + i] = (byte)(weight * 255f + 0.5f);
            else
            {
                float newW = otherTotal > 0f ? (cur[i] / otherTotal) * remaining : 0f;
                splat[offset + i] = (byte)(newW * 255f + 0.5f);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Flat default chunk helpers

    public static byte[] DefaultHeightData(float height)
    {
        byte[] data = new byte[HeightBytes];
        byte[] hb = BitConverter.GetBytes(height);
        for (int i = 0; i < PointCount; i++)
        {
            int offset = i * 4;
            data[offset]     = hb[0];
            data[offset + 1] = hb[1];
            data[offset + 2] = hb[2];
            data[offset + 3] = hb[3];
        }
        return data;
    }

    public static byte[] DefaultSplatData()
    {
        // Full Grass (R=255, G=0, B=0, A=0)
        byte[] data = new byte[SplatBytes];
        for (int i = 0; i < PointCount; i++)
            data[i * 4] = 255;
        return data;
    }
}
