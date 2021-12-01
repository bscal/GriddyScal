using Common.FluidSimulation.Cellular_Automata;
using UnityEngine;
using UnityEngine.InputSystem;

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
                m_SpriteMap[x + y * m_TextureTilesPerRow] = Sprite.Create(Texture, GetTextureRectFromPos(x, y), new Vector2(TileSize.x, TileSize.y) / 2, TextureTileSize.x);
            }
        }

        int size = Size.x * Size.y;
        m_Tiles = new SpriteTile[size];

        for (int y = 0; y < Size.y; y++)
        {
            for (int x = 0; x < Size.x; x++)
            {
                GameObject go = Instantiate(Prefab, transform);
                go.GetComponent<BoxCollider>().size = new Vector3(TileSize.x, TileSize.y, .01f);
                SpriteTile tile = go.AddComponent<SpriteTile>();
                tile.Renderer = tile.gameObject.AddComponent<SpriteRenderer>();
                tile.Renderer.size = new Vector2(TileSize.x, TileSize.y);
                tile.Renderer.sprite = GetTileFromId(0);
                tile.transform.position = new Vector3(x * TileSize.x, y * TileSize.y);
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
        bool rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
        bool leftClicked = Mouse.current.leftButton.isPressed;
        bool middleClicked = Mouse.current.middleButton.wasPressedThisFrame; 

        if (!rightClicked && !leftClicked && !middleClicked) return;


        var camera = Camera.main;
        var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
 
        if (Physics.Raycast(ray, out RaycastHit hit, 100.0f))
        {
            var diff = hit.point - transform.position;
            int x = Mathf.RoundToInt(diff.x);
            int y = Mathf.RoundToInt(diff.y);
            Debug.Log(x + " " + y);

            if (rightClicked)
            {
                CellularAutomataFluidController.Instance.SetState(x, y, CellState.STATE_GROUND);
            }
            else if (leftClicked)
            {
                CellularAutomataFluidController.Instance.SetState(x, y, CellState.STATE_WATER);
                CellularAutomataFluidController.Instance.AddMass(x, y, 1.0f);
            }
            else if (middleClicked)
            {
                CellularAutomataFluidController.Instance.MarkAsInfiniteSource(x, y);
            }
        }
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

    public void SetSprite(int x, int y, int tileId)
    {
        m_Tiles[x + y * Size.x].Renderer.sprite = GetTileFromId(tileId);
    }

    public void SetColor(int x, int y, Color color)
    {
        m_Tiles[x + y * Size.x].Renderer.color = color;
    }
}

public class SpriteTile : MonoBehaviour
{
    public int TileId;
    public SpriteRenderer Renderer;

    private void OnMouseDown()
    {
        Debug.Log("TEST");
    }


}
