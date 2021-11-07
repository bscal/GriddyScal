using UnityEngine;

public class TileSet : MonoBehaviour
{
    [SerializeField]
    public int WidthInTiles, HeightInTiles;
    [SerializeField]
    public int TileWidth, TileHeight;
    [SerializeField]
    public Material Material;

    private int WidthInPixels, HeightInPixels;
    private float m_TileStepX, m_TileStepY;

    private void Awake()
    {
        WidthInPixels = WidthInTiles * TileWidth;
        HeightInPixels = HeightInTiles * TileHeight;

        m_TileStepX = (float)TileWidth / (float)WidthInPixels;
        m_TileStepY = (float)TileHeight / (float)HeightInPixels;
    }

    public UVRect GetTile(int x, int y)
    {
        print($"{x}, {y}");
        float posX = m_TileStepX * (float)x;
        float posY = m_TileStepY * (float)y;
        float posXX = posX + m_TileStepX;
        float posYY = posY + m_TileStepY;
        return new UVRect(posX, posY, posX, posYY, posXX, posYY, posXX, posY);
    }

    public UVRect GetTile(int index)
    {
        var xy = IndexToXY(index);
        return GetTile(xy.x, xy.y);
    }

    public Vector2Int IndexToXY(int index)
    {
        return new Vector2Int(index % WidthInTiles, index / WidthInTiles);
    }
}

public struct UVRect
{
    public static readonly UVRect ZERO = new UVRect(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

    public Vector2 uv0;
    public Vector2 uv1;
    public Vector2 uv2;
    public Vector2 uv3;

    public UVRect(float uv0x, float uv0y, float uv1x, float uv1y, float uv2x, float uv2y, float uv3x, float uv3y)
    {
        uv0 = new Vector2(uv0x, uv0y);
        uv1 = new Vector2(uv1x, uv1y);
        uv2 = new Vector2(uv2x, uv2y);
        uv3 = new Vector2(uv3x, uv3y);
    }

    public override string ToString()
    {
        return $"uv0: {uv0}, uv1: {uv1}, uv2: {uv2}, uv3: {uv3}";
    }
}