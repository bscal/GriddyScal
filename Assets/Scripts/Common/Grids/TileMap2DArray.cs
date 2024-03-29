using Common.FluidSimulation.Cellular_Automata;
using Common.Grids;
using Common.Grids.Cells;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/**
 * <summary>
 * Rectangle Tilemap that uses a 2DTextureArray and mesh generation for tiles.
 * UVs are Vector3 where the z value is used for the array index
 * </summary>
 */
public class TileMap2DArray : MonoBehaviour, ISerializationCallbackReceiver
{
    private const int VERTEX_COUNT = 4;

    [Header("Texture")]
    [SerializeField]
    public Material Material;
    [SerializeField]
    public Vector2Int TextureTileSize;

    [Header("Map Sizes")]
    [SerializeField]
    public Vector2Int MapSize;
    [SerializeField]
    public Vector2Int TileSize;
    public int ChunkSize;

    public GameObject ChunkPrefab;

    public CellularAutomataSand States;

    public int Size { get; protected set; }

    private Unity.Mathematics.Random m_Random;
    private MeshRenderer m_MeshRenderer;
    private MeshFilter m_MeshFilter;

    private System.Diagnostics.Stopwatch m_Watch = new System.Diagnostics.Stopwatch();

    public List<Chunk> ChunkList;
    public NativeHashMap<long, ChunkData> NativeChunkMap;

    private void Awake()
    {
#if DEBUG
        UnityEngine.Assertions.Assert.IsNotNull(ChunkPrefab);
        UnityEngine.Assertions.Assert.IsNotNull(Material);
#endif
        m_Watch.Start();

        Size = MapSize.x * MapSize.y;
        Vector2Int ChunkMapSize = new(ChunkSize, ChunkSize);
        int chunks = ChunkSize * ChunkSize;
        uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        m_Random = new Unity.Mathematics.Random(seed);

        Debug.Log($"Generating TileMap[{MapSize.x}, {MapSize.y}]. Seed: {seed} | ChunkSize: {ChunkSize}");

        NativeChunkMap = new NativeHashMap<long, ChunkData>(chunks, Allocator.Persistent);
        //ChunkMap = new Dictionary<long, Chunk>();
        ChunkList = new List<Chunk>();

        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.MarkDynamic();

        NativeArray<Vector3> vertices = new NativeArray<Vector3>(chunks * 4, Allocator.TempJob);
        GenVerticiesJob verticiesJob = new GenVerticiesJob
        {
            MapSize = ChunkMapSize,
            TileSize = TileSize,
            Verticies = vertices
        };
        var verticesHandle = verticiesJob.Schedule();

        NativeArray<Vector3> uvs = new NativeArray<Vector3>(chunks * 4, Allocator.TempJob);
        GenTextureUVJob uvJob = new GenTextureUVJob
        {
            Seed = m_Random,
            MapSize = ChunkMapSize,
            UVs = uvs
        };
        var uvHandle = uvJob.Schedule();

        NativeArray<Vector4> colors = new(chunks * 4, Allocator.TempJob);
        GenColorUVs cJob = new()
        {
            MapSize = ChunkMapSize,
            UVs = colors,
        };
        var cHandler = cJob.Schedule();

        NativeArray<int> triangles = new NativeArray<int>(chunks * 6, Allocator.TempJob);
        GenTrianglesJob trianglesJob = new GenTrianglesJob
        {
            MapSize = ChunkMapSize,
            Triangles = triangles
        };
        var trianglesHandle = trianglesJob.Schedule();

        // Wait for Verticies to Finish because cant set uvs without them
        verticesHandle.Complete();
        mesh.vertices = verticiesJob.Verticies.ToArray();

        trianglesHandle.Complete();
        mesh.triangles = trianglesJob.Triangles.ToArray();

        uvHandle.Complete();
        mesh.SetUVs(0, uvJob.UVs);

        cHandler.Complete();
        mesh.SetUVs(1, cJob.UVs);

        // free
        vertices.Dispose();
        uvs.Dispose();
        colors.Dispose();
        triangles.Dispose();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        for (int y = 0; y < MapSize.y / ChunkMapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x / ChunkMapSize.x; x++)
            {
                var go = Instantiate(ChunkPrefab, gameObject.transform);

                Chunk chunk = new(this, x, y, ChunkSize, ChunkSize);
                chunk.Create(go, mesh, Material);
                chunk.State = ChunkState.LOADED;

                long key = XYToKey(x, y);

                NativeChunkMap[key] = new ChunkData()
                {
                    State = chunk.State,
                };
                //ChunkMap.Add(key, chunk);
                ChunkList.Add(chunk);
            }
        }
        gameObject.SetActive(true);
        m_Watch.Stop();
        Debug.Log($"TileMap2DArray took: {m_Watch.ElapsedMilliseconds}ms to Awake()");
    }

    private void OnDestroy()
    {
        NativeChunkMap.Dispose();
    }

    void Update()
    {
        foreach (Chunk chunk in ChunkList)
        {
            chunk.UpdateState();
            if (chunk.State == ChunkState.LOADED)
            {
                var nativeChunk = NativeChunkMap[chunk.GetKey()];
                if (nativeChunk.IsDirty)
                {
                    nativeChunk.IsDirty = false;
                    chunk.UpdateMesh();
                    NativeChunkMap[chunk.GetKey()] = nativeChunk;
                }
            }
        }
    }

    public static long XYToKey(int x, int y) => (long)y << 32 | (long)x;

    public static Vector2Int KeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));

    public List<Vector3> GetTileUVs()
    {
        List<Vector3> uvs = new();
        m_MeshFilter.mesh.GetUVs(0, uvs);
        return uvs;
    }

    public void SetTileUVs(in List<Vector3> uvs)
    {
        m_MeshFilter.mesh.SetUVs(0, uvs);
    }

    public void SetTileTerrain(int tile, int terrainId, in List<Vector3> uvs)
    {
        int offset = tile * VERTEX_COUNT;
        uvs[offset + 0] = new Vector3(0, 0, terrainId);
        uvs[offset + 1] = new Vector3(0, 1, terrainId);
        uvs[offset + 2] = new Vector3(1, 1, terrainId);
        uvs[offset + 3] = new Vector3(1, 0, terrainId);
    }

    public void SetTileUVs(Vector3[] uvs)
    {
        m_MeshFilter.mesh.SetUVs(0, uvs);
    }

    public void SetTileUVs(NativeArray<float3> uvs)
    {
        m_MeshFilter.mesh.SetUVs(0, uvs);
    }

    public void SetTileTerrain(int tile, int terrainId, NativeArray<float3> uvs)
    {
        int offset = tile * VERTEX_COUNT;
        uvs[offset + 0] = new float3(0, 0, terrainId);
        uvs[offset + 1] = new float3(0, 1, terrainId);
        uvs[offset + 2] = new float3(1, 1, terrainId);
        uvs[offset + 3] = new float3(1, 0, terrainId);
    }

    /**
     * <summary>
     * A short cut to update 1 tile's uvs. You should use:
     * <code>GetTileUVs(), SetTileTerrain(), and SetTileUVs()</code> 
     * to update mass tiles more effeciently.
     * </summary>
     */
    public void SetSingleTileTerrain(int tile, int terrainId)
    {
        var uvs = GetTileUVs();
        SetTileTerrain(tile, terrainId, uvs);
        SetTileUVs(uvs);
    }

    public int GetTileTerrainId(int tile)
    {
        var uvs = GetTileUVs();
        return (int)uvs[tile * VERTEX_COUNT].z;
    }

    // *************************
    public List<Vector4> GetMeshColors()
    {
        List<Vector4> uvs = new();
        m_MeshFilter.mesh.GetUVs(1, uvs);
        return uvs;
    }

    public void SetMeshColors(Vector4[] uvs)
    {
        m_MeshFilter.mesh.SetUVs(1, uvs);
    }

    public void SetMeshColors(NativeArray<float4> uvs)
    {
        m_MeshFilter.mesh.SetUVs(1, uvs, 0, Size * 4, UnityEngine.Rendering.MeshUpdateFlags.DontNotifyMeshUsers | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices | UnityEngine.Rendering.MeshUpdateFlags.DontResetBoneBounds | UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
    }

    public void SetColor(int tile, Color color, in List<Vector4> uvs)
    {
        var vec4 = new Vector4(color.r, color.g, color.b, color.a);
        int offset = tile * VERTEX_COUNT;
        uvs[offset + 0] = vec4;
        uvs[offset + 1] = vec4;
        uvs[offset + 2] = vec4;
        uvs[offset + 3] = vec4;
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (TextureTileSize.x < 1 || TextureTileSize.y < 1)
            Debug.LogError("TextureTileSize is less then 1");
        if (TileSize.x < 1 || TileSize.y < 1)
            Debug.LogError("TileSize is less then 1");
        if (MapSize.x < 1 || MapSize.y < 1)
            Debug.LogError("MapSize is less then 1");
        if (MapSize.x % ChunkSize != 0 || MapSize.y % ChunkSize != 0)
            Debug.LogError($"ChunkSize does not match MapSize: X {MapSize.x}, Y {MapSize.y}, Chunk {ChunkSize}");
    }
}

public struct GenTextureUVJob : IJob
{
    [ReadOnly] public Unity.Mathematics.Random Seed;
    [ReadOnly] public Vector2Int MapSize;
    public NativeArray<Vector3> UVs;

    public void Execute()
    {
        int iVertCount = 0;
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                UVs[iVertCount + 0] = new Vector3(0, 0, 0);
                UVs[iVertCount + 1] = new Vector3(0, 1, 0);
                UVs[iVertCount + 2] = new Vector3(1, 1, 0);
                UVs[iVertCount + 3] = new Vector3(1, 0, 0);
                iVertCount += 4;
            }
        }
    }
}

public struct GenColorUVs : IJob
{
    [ReadOnly] public Vector2Int MapSize;
    public NativeArray<Vector4> UVs;

    public void Execute()
    {
        int iVertCount = 0;
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                UVs[iVertCount + 0] = Vector4.one;
                UVs[iVertCount + 1] = Vector4.one;
                UVs[iVertCount + 2] = Vector4.one;
                UVs[iVertCount + 3] = Vector4.one;
                iVertCount += 4;
            }
        }
    }
}

public struct GenTileArray : IJob
{
    [ReadOnly] public Unity.Mathematics.Random Seed;
    [ReadOnly] public Vector2Int MapSize;
    public NativeArray<FluidTile> Tiles;

    public void Execute()
    {
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                float d = Seed.NextFloat(.25f, .75f);
                Tiles[x + y * MapSize.x] = new FluidTile()
                {
                    Velocity = Vector3.one,
                    PrevVelocity = Vector3.one,
                    Density = d,
                    PrevDensity = d,
                };
            }
        }
    }
}

public struct GenTrianglesJob : IJob
{
    [ReadOnly] public Vector2Int MapSize;
    public NativeArray<int> Triangles;

    public void Execute()
    {
        int iVertCount = 0, iIndexCount = 0;
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                Triangles[iIndexCount + 0] += (iVertCount + 0);
                Triangles[iIndexCount + 1] += (iVertCount + 1);
                Triangles[iIndexCount + 2] += (iVertCount + 2);
                Triangles[iIndexCount + 3] += (iVertCount + 0);
                Triangles[iIndexCount + 4] += (iVertCount + 2);
                Triangles[iIndexCount + 5] += (iVertCount + 3);
                iVertCount += 4;
                iIndexCount += 6;
            }
        }
    }
}

public struct GenVerticiesJob : IJob
{
    [ReadOnly] public Vector2Int MapSize;
    [ReadOnly] public Vector2Int TileSize;
    public NativeArray<Vector3> Verticies;

    public void Execute()
    {
        int x, y, iVertCount = 0;
        for (y = 0; y < MapSize.y; y++)
        {
            for (x = 0; x < MapSize.x; x++)
            {
                int xx = x * TileSize.x;
                int yy = y * TileSize.y;
                Verticies[iVertCount + 0] = new Vector3(xx, yy);
                Verticies[iVertCount + 1] = new Vector3(xx, yy + TileSize.y);
                Verticies[iVertCount + 2] = new Vector3(xx + TileSize.x, yy + TileSize.y);
                Verticies[iVertCount + 3] = new Vector3(xx + TileSize.x, yy);
                iVertCount += 4;
            }
        }
    }
}