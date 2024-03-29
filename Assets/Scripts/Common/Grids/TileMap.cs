using UnityEngine;
using UnityEngine.U2D;
using System;

public class TileMap : MonoBehaviour
{
    [Header("Texture")]
    [SerializeField]
    public Material Material;
    [SerializeField]
    public Vector2Int TextureTileSize;

    [Header("Map Sizes")]
    [SerializeField]
    public int MapWidth;
    [SerializeField]
    public int MapHeight;
    [SerializeField]
    public Vector2Int TileSize;

    public Texture2D Texture;

    private int m_TextureTilesPerRow;

    private Tile[] m_Tiles;
    private Vector3[] m_Verticies;
    private Vector2[] m_Uvs;
    private int[] m_Triangles;

    private MeshRenderer m_MeshRenderer;
    private MeshFilter m_MeshFilter;

    private void Awake()
    {
#if DEBUG
        UnityEngine.Assertions.Assert.IsNotNull(Material);
#endif
        int size = MapWidth * MapHeight;
        m_Tiles = new Tile[size];
        m_Verticies = new Vector3[size * 4];
        m_Uvs = new Vector2[size * 4];
        m_Triangles = new int[size * 6];

        int x, y, iVertCount = 0, iIndexCount = 0;
        for (y = 0; y < MapHeight; y++)
        {
            for (x = 0; x < MapWidth; x++)
            {
                m_Tiles[x + y * MapWidth] = new Tile();
                m_Tiles[x + y * MapWidth].TileId = UnityEngine.Random.Range(5, 21);

                m_Verticies[iVertCount + 0] = new Vector3(x* 16, y * 16);
                m_Verticies[iVertCount + 1] = new Vector3(x* 16, y * 16 + 16f);
                m_Verticies[iVertCount + 2] = new Vector3(x* 16 + 16f, y * 16 + 16f);
                m_Verticies[iVertCount + 3] = new Vector3(x* 16 + 16f, y * 16);

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

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = m_Verticies;
        mesh.triangles = m_Triangles;
        m_MeshFilter.mesh = mesh;
        m_MeshRenderer.sharedMaterial = Material;

        m_TextureTilesPerRow = Material.mainTexture.width / TextureTileSize.x;
        gameObject.SetActive(true);
    }

    void Start()
    {
        UpdateMesh();
    }

    void Update()
    {
        //SetUVCoords(Random.Range(0, m_Tiles.Length), Random.Range(32, 128));
    }

    public void UpdateMesh()
    {
        int iVertCount = 0;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                int tileId = m_Tiles[x + y * MapWidth].TileId;
                var uvs = GetUVRectangleFromPixels(GetUVCoordsOffset(tileId));
                m_Uvs[iVertCount + 0] = uvs[0];
                m_Uvs[iVertCount + 1] = uvs[1];
                m_Uvs[iVertCount + 2] = uvs[2];
                m_Uvs[iVertCount + 3] = uvs[3];
                iVertCount += 4;
            }
        }
        m_MeshFilter.mesh.uv = m_Uvs;
    }


    public void SetUVCoords(int tile, int texTileId)
    {
        Vector2[] uvArray = GetUVRectangleFromPixels(GetUVCoords(texTileId));
        ApplyUVToUVArray(tile, uvArray, m_Uvs);
        m_MeshFilter.mesh.uv = m_Uvs;
    }

    public void SetUVCoords(int tile, int texTileX, int texTileY)
    {
        Vector2[] uvArray = GetUVRectangleFromPixels(GetUVCoords(texTileX, texTileY));
        ApplyUVToUVArray(tile, uvArray, m_Uvs);
        m_MeshFilter.mesh.uv = m_Uvs;
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

    private void ApplyUVToUVArray(int tile, Vector2[] uv, in Vector2[] meshUV)
    {
        int offset = tile * 4;
        meshUV[offset] = uv[0];
        meshUV[offset + 1] = uv[1];
        meshUV[offset + 2] = uv[2];
        meshUV[offset + 3] = uv[3];
    }

    private void SpaceTextures()
    {
        int xx = Texture.width / 16;
        int yy = Texture.height / 16;
        Texture2D modifiedTexture = new Texture2D(Texture.width + (xx * 4), Texture.height + (yy * 4));
        for (int iy = 0; iy < yy; iy++)
        {
            for (int ix = 0; ix < xx; ix++)
            {
                Color[] c = new Color[20 * 20];
                for (int i = 0; i < c.Length; i++)
                    c[i] = new Color(1, 0, 1);

                var ixx = ix * 20;
                var iyy = iy * 20;

                var ixxx = ix * 16;
                var iyyy = iy * 16;
                modifiedTexture.SetPixels(ixx, iyy, 16 + 4, 16 + 4, c);

                var tile = Texture.GetPixels(ixxx, iyyy, 16, 16);
                modifiedTexture.SetPixels(ixx, iyy, 16, 16, tile);
            }
        }
        modifiedTexture.Apply();
        modifiedTexture.filterMode = FilterMode.Point;
        modifiedTexture.wrapMode = TextureWrapMode.Clamp;

        Material.mainTexture = modifiedTexture;
    }
}

public struct RectInt
{
    public int x, y, width, height;

    public RectInt(int x, int y, int w, int h)
    {
        this.x = x;
        this.y = y;
        this.width = w;
        this.height = h;
    }

    public override string ToString()
    {
        return $"x:{x}, y:{y}, w:{width}, h:{height}";
    }
}