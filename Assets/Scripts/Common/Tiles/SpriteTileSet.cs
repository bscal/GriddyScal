using UnityEngine;

public class SpriteTileSet : MonoBehaviour
{
    public Vector2Int Size;
    public Vector2Int TileSize;

    public GameObject Prefab;
    public Texture2D Texture;
    public Vector2Int TextureTileSize;

    private int m_TextureTilesPerRow;
    private int m_TextureTilesPerColumn;

    private SpriteTile[] m_Tiles;
    private Sprite[] m_SpriteMap;

    private void Awake()
    {
#if DEBUG
        UnityEngine.Assertions.Assert.IsNotNull(Texture);
#endif
        m_TextureTilesPerRow = Texture.width / TextureTileSize.x;
        m_TextureTilesPerColumn = Texture.height / TextureTileSize.y;
        m_SpriteMap = new Sprite[m_TextureTilesPerRow * m_TextureTilesPerColumn];
        for (int y = 0; y < m_TextureTilesPerColumn; y++)
        {
            for (int x = 0; x < m_TextureTilesPerRow; x++)
            {
                m_SpriteMap[x + y * m_TextureTilesPerRow] = Sprite.Create(Texture, GetTextureRectFromPos(x, y), new Vector2(8, 8), 16);
            }
        }

        int size = Size.x * Size.y;
        m_Tiles = new SpriteTile[size];

        for (int y = 0; y < Size.y; y++)
        {
            for (int x = 0; x < Size.x; x++)
            {
                GameObject go = Instantiate(Prefab, transform);
                SpriteTile tile = go.AddComponent<SpriteTile>();
                tile.Renderer = tile.gameObject.AddComponent<SpriteRenderer>();
                tile.Renderer.size = new Vector2(16, 16);
                tile.Renderer.sprite = GetTileFromId(Random.Range(32, 100));
                tile.transform.position = new Vector3(x, y);
                m_Tiles[x + y * Size.x] = tile;
            }
        }

        gameObject.SetActive(true);
    }

    void Start()
    {
    }

    void Update()
    {
    }

    private Rect GetTextureRectFromPos(int x, int y)
    {
        return new Rect(x * 16, y * 16, 16, 16);
    }

    private Sprite GetTileFromId(int tileId)
    {
        int x = tileId % m_TextureTilesPerRow;
        int y = tileId / m_TextureTilesPerRow;
        return m_SpriteMap[x + y * m_TextureTilesPerRow];
    }
}

public class SpriteTile : MonoBehaviour
{
    public int TileId;
    public SpriteRenderer Renderer;

}
