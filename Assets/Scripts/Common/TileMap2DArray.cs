
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class TileMap2DArray : MonoBehaviour
{
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

    private int m_TextureTilesPerRow;

    private Tile[] m_Tiles;

    private MeshRenderer m_MeshRenderer;
    private MeshFilter m_MeshFilter;

    private System.Diagnostics.Stopwatch m_Watch = new System.Diagnostics.Stopwatch();

    private void Awake()
    {
#if DEBUG
        UnityEngine.Assertions.Assert.IsNotNull(Material);
#endif

        m_Watch.Start();

        int size = MapSize.x * MapSize.y;

        //m_Tiles = new Tile[size];

        m_MeshFilter = gameObject.AddComponent<MeshFilter>();
        m_MeshRenderer = gameObject.AddComponent<MeshRenderer>();
        m_MeshRenderer.sharedMaterial = Material;
        m_TextureTilesPerRow = Material.mainTexture.width / TextureTileSize.x;

        Mesh mesh = new Mesh {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        NativeArray<Vector3> vertices = new NativeArray<Vector3>(size * 4, Allocator.TempJob);
        GenVerticiesJob verticiesJob = new GenVerticiesJob {
            MapSize = MapSize,
            TileSize = TileSize,
            Verticies = vertices
        };
        var verticesHandle = verticiesJob.Schedule();

        NativeArray<Vector2> uvs = new NativeArray<Vector2>(size * 4, Allocator.TempJob);
        GenTextureUVJob uvJob = new GenTextureUVJob {
            MapSize = MapSize,
            UVs = uvs
        };
        var uvHandle = uvJob.Schedule();

        uint seed = (uint)Random.Range(int.MinValue, int.MaxValue);
        NativeArray<Vector2> terrainUVs = new NativeArray<Vector2>(size * 4, Allocator.TempJob);
        GenTerrainUVJob terrainJob = new GenTerrainUVJob {
            Seed = new Unity.Mathematics.Random(seed),
            MapSize = MapSize,
            TerrainUVs = terrainUVs
        };
        var terrainHandle = terrainJob.Schedule();

        NativeArray<int> triangles = new NativeArray<int>(size * 6, Allocator.TempJob);
        GenTrianglesJob trianglesJob = new GenTrianglesJob {
            MapSize = MapSize,
            Triangles = triangles
        };
        var trianglesHandle = trianglesJob.Schedule();

        // Wait for Verticies to Finish
        verticesHandle.Complete();
        mesh.vertices = verticiesJob.Verticies.ToArray();

        trianglesHandle.Complete();
        mesh.triangles = trianglesJob.Triangles.ToArray();

        uvHandle.Complete();
        mesh.uv = uvJob.UVs.ToArray();

        terrainHandle.Complete();
        mesh.uv3 = terrainJob.TerrainUVs.ToArray();

        m_MeshFilter.mesh = mesh;

        // end
        vertices.Dispose();
        uvs.Dispose();
        terrainUVs.Dispose();
        triangles.Dispose();

        gameObject.SetActive(true);

        m_Watch.Stop();
        Debug.Log($"TileMap2DArray took: {m_Watch.ElapsedMilliseconds}ms to Awake()");

        /*          
                Vector3[] m_Verticies = new Vector3[size * 4];
                Vector2[] m_Uvs = new Vector2[m_Verticies.Length];
                Vector2[] m_TerrainType = new Vector2[m_Verticies.Length];
                int[] m_Triangles = new int[size * 6];
                int x, y, iVertCount = 0, iIndexCount = 0;
                for (y = 0; y < MapHeight; y++)
                {
                    for (x = 0; x < MapWidth; x++)
                    {
                        int id = x + y * MapWidth;
                        m_Tiles[id] = new Tile();
                        m_Tiles[id].TileId = 0;

                        int xx = x * (int)TileSize.x;
                        int yy = y * (int)TileSize.y;
                        m_Verticies[iVertCount + 0] = new Vector3(xx, yy);
                        m_Verticies[iVertCount + 1] = new Vector3(xx, yy + TileSize.y);
                        m_Verticies[iVertCount + 2] = new Vector3(xx + TileSize.x, yy + TileSize.y);
                        m_Verticies[iVertCount + 3] = new Vector3(xx + TileSize.x, yy);

                        m_Uvs[iVertCount + 0] = new Vector2(0, 0);
                        m_Uvs[iVertCount + 1] = new Vector2(0, 1);
                        m_Uvs[iVertCount + 2] = new Vector2(1, 1);
                        m_Uvs[iVertCount + 3] = new Vector2(1, 0);

                        m_TerrainType[iVertCount + 0] = new Vector2(0, 0);
                        m_TerrainType[iVertCount + 1] = new Vector2(0, 0);
                        m_TerrainType[iVertCount + 2] = new Vector2(0, 0);
                        m_TerrainType[iVertCount + 3] = new Vector2(0, 0);

                        m_Triangles[iIndexCount + 0] += (iVertCount + 0);
                        m_Triangles[iIndexCount + 1] += (iVertCount + 1);
                        m_Triangles[iIndexCount + 2] += (iVertCount + 2);
                        m_Triangles[iIndexCount + 3] += (iVertCount + 0);
                        m_Triangles[iIndexCount + 4] += (iVertCount + 2);
                        m_Triangles[iIndexCount + 5] += (iVertCount + 3);

                        iIndexCount += 6;
                        iVertCount += 4;
                    }
                }

                m_MeshRenderer = gameObject.AddComponent<MeshRenderer>();
                m_MeshFilter = gameObject.AddComponent<MeshFilter>();

                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = m_Verticies;
                mesh.uv = m_Uvs;
                mesh.SetUVs(2, m_TerrainType);
                mesh.triangles = m_Triangles;
                mesh.RecalculateNormals();

                m_MeshFilter.mesh = mesh;
                m_MeshRenderer.material = Material;
                m_MeshRenderer.sharedMaterial = Material;

                m_TextureTilesPerRow = Material.mainTexture.width / TextureTileSize.x;
                gameObject.SetActive(true);*/
    }

    void Start()
    {
        //for (int i = 0; i < MapSize.x * MapSize.y; i++)
        //{
          //SetTileTerrain(i, Random.Range(32, 128));
        //}
    }

    void Update()
    {
    }

    public void UpdateMesh()
    {
        int iVertCount = 0;
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                int tileId = m_Tiles[x + y * MapSize.x].TileId;
                var uvs = GetUVRectangleFromPixels(GetUVCoordsOffset(tileId));
                m_MeshFilter.mesh.uv[iVertCount + 0] = uvs[0];
                m_MeshFilter.mesh.uv[iVertCount + 1] = uvs[1];
                m_MeshFilter.mesh.uv[iVertCount + 2] = uvs[2];
                m_MeshFilter.mesh.uv[iVertCount + 3] = uvs[3];
                iVertCount += 4;
            }
        }
    }


    public void SetUVCoords(int tile, int texTileId)
    {
        Vector2[] uvArray = GetUVRectangleFromPixels(GetUVCoords(texTileId));
        ApplyUVToUVArray(tile, uvArray);
    }

    public void SetUVCoords(int tile, int texTileX, int texTileY)
    {
        Vector2[] uvArray = GetUVRectangleFromPixels(GetUVCoords(texTileX, texTileY));
        ApplyUVToUVArray(tile, uvArray);
    }

    public RectInt GetUVCoords(int tileId)
    {
        int x = tileId % m_TextureTilesPerRow;
        int y = tileId / m_TextureTilesPerRow;
        return new RectInt(x * TextureTileSize.x, y * TextureTileSize.y, TextureTileSize.x, TextureTileSize.y);
    }

    public RectInt GetUVCoords(int tileX, int tileY)
    {
        return new RectInt(tileX, tileY, TextureTileSize.x, TextureTileSize.y);
    }

    public RectInt GetUVCoords(Vector2Int tilePos)
    {
        return new RectInt(tilePos.x, tilePos.y, TextureTileSize.x, TextureTileSize.y);
    }

    public RectInt GetUVCoordsOffset(int tileId)
    {
        int x = tileId % m_TextureTilesPerRow;
        int y = tileId / m_TextureTilesPerRow;
        return new RectInt(x * TextureTileSize.x, (Material.mainTexture.height - y * TextureTileSize.y) - TextureTileSize.y, TextureTileSize.x, TextureTileSize.y);
    }

    public RectInt GetUVCoordsOffset(int tileX, int tileY)
    {
        return new RectInt(tileX, Material.mainTexture.height - tileY - TextureTileSize.y, TextureTileSize.x, TextureTileSize.y);
    }

    private Vector2[] GetUVRectangleFromPixels(RectInt uvCoord)
    {
        return new Vector2[] {
                ConvertPixelsToUVCoordinates(uvCoord.x, uvCoord.y),
                ConvertPixelsToUVCoordinates(uvCoord.x, uvCoord.y + uvCoord.height),
                ConvertPixelsToUVCoordinates(uvCoord.x + uvCoord.width, uvCoord.y + uvCoord.height),
                ConvertPixelsToUVCoordinates(uvCoord.x + uvCoord.width, uvCoord.y)
            };
    }

    private Vector2[] GetUVRectangleFromPixels(int x, int y, int width, int height)
    {
        return new Vector2[] {
                ConvertPixelsToUVCoordinates(x, y),
                ConvertPixelsToUVCoordinates(x, y + height),
                ConvertPixelsToUVCoordinates(x + width, y + height),
                ConvertPixelsToUVCoordinates(x + width, y)
            };
    }


    private Vector2 ConvertPixelsToUVCoordinates(int x, int y)
    {
        return new Vector2((float)x / (float)Material.mainTexture.width, (float)y / (float)Material.mainTexture.height);
    }

    private void ApplyUVToUVArray(int tile, Vector2[] uv)
    {
        int offset = tile * 4;
        m_MeshFilter.mesh.uv[offset + 0] = uv[0];
        m_MeshFilter.mesh.uv[offset + 1] = uv[1];
        m_MeshFilter.mesh.uv[offset + 2] = uv[2];
        m_MeshFilter.mesh.uv[offset + 3] = uv[3];
    }

    public void SetTileTerrain(int tile, float terrainId)
    {
        ApplyTerrainUVArray(tile, new Vector2[] { new Vector2(terrainId, 0), new Vector2(terrainId, 0), new Vector2(terrainId, 0), new Vector2(terrainId, 0) });
    }

    private void ApplyTerrainUVArray(int tile, Vector2[] terrainTypes)
    {
        int offset = tile * 4;
        Vector2[] uv3 = m_MeshFilter.mesh.uv3;
        uv3[offset + 0] = terrainTypes[0];
        uv3[offset + 1] = terrainTypes[1];
        uv3[offset + 2] = terrainTypes[2];
        uv3[offset + 3] = terrainTypes[3];
        m_MeshFilter.mesh.uv3 = uv3;
    }
}

public struct GenTextureUVJob : IJob
{
    [ReadOnly] public Vector2Int MapSize;
    public NativeArray<Vector2> UVs;

    public void Execute()
    {
        int iVertCount = 0;
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                UVs[iVertCount + 0] = new Vector2(0, 0);
                UVs[iVertCount + 1] = new Vector2(0, 1);
                UVs[iVertCount + 2] = new Vector2(1, 1);
                UVs[iVertCount + 3] = new Vector2(1, 0);
                iVertCount += 4;
            }
        }
    }
}

public struct GenTerrainUVJob : IJob
{
    [ReadOnly] public Unity.Mathematics.Random Seed;
    [ReadOnly] public Vector2Int MapSize;
    public NativeArray<Vector2> TerrainUVs;

    public void Execute()
    {
        int iVertCount = 0;
        for (int y = 0; y < MapSize.y; y++)
        {
            for (int x = 0; x < MapSize.x; x++)
            {
                int terrain = Seed.NextInt(32, 256);
                TerrainUVs[iVertCount + 0] = new Vector2(terrain, 0);
                TerrainUVs[iVertCount + 1] = new Vector2(terrain, 0);
                TerrainUVs[iVertCount + 2] = new Vector2(terrain, 0);
                TerrainUVs[iVertCount + 3] = new Vector2(terrain, 0);
                //TerrainUVs[iVertCount + 0] = new Vector2(0, 0);
                //TerrainUVs[iVertCount + 1] = new Vector2(0, 0);
                //TerrainUVs[iVertCount + 2] = new Vector2(0, 0);
                //TerrainUVs[iVertCount + 3] = new Vector2(0, 0);
                iVertCount += 4;
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

public struct GenTilesjob : IJob
{
    [ReadOnly] public int Size;
    public Tile[] Tiles;

    public void Execute()
    {
        for (int i = 0; i < Size; i++)
        {
            Tiles[i] = new Tile();
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