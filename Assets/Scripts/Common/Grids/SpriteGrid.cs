using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Common.Grids
{
    /// <summary>
    /// A Basic implementations of a Grid with made with GameObject and Sprites, not designed for preformance
    /// </summary>
    public class SpriteGrid : GridBehaviour
    {
        public GameObject Prefab;
        [Header("Grid")]
        public TileGrid<SpriteGridCell> grid;
        public int CellWidth, CellHeight;
        public bool Is3D;
        [Header("Texture")]
        public Texture2D Texture;
        public int TextureTileWidth, TextureTileHeight;

        private Sprite[] m_LoadedSprites;
        private int m_TextureTilesPerRow;

        private void Awake()
        {
            m_TextureTilesPerRow = Texture.width / TextureTileWidth;
            m_LoadedSprites = new Sprite[m_TextureTilesPerRow * (Texture.height / TextureTileHeight)];
            LoadSprites();

            grid.ForEach((x, y) =>
            {
                GameObject go = Instantiate(Prefab, transform);
                if (Is3D)
                {
                    var collider = go.AddComponent<BoxCollider>();
                    collider.size = new Vector3(CellWidth, CellHeight, .01f);
                }
                else
                {
                    var collider = go.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(CellWidth, CellHeight);
                }
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.size = new Vector2(CellWidth, CellHeight);
                renderer.sprite = GetTileFromId(0);
                go.transform.position = new Vector3(x * CellWidth, y * CellHeight);
                grid.GetCells()[x, y] = new SpriteGridCell()
                {
                    Object = go,
                    Renderer = renderer,
                };
            });
        }

        public Sprite GetTileFromId(int tileId) => m_LoadedSprites[tileId];

        private void LoadSprites()
        {
            for (int y = 0; y < Texture.height / TextureTileHeight; y++)
            {
                for (int x = 0; x < m_TextureTilesPerRow; x++)
                {
                    m_LoadedSprites[x + y * m_TextureTilesPerRow] 
                        = Sprite.Create(Texture, GetTextureRectFromPos(x, y), new Vector2(CellWidth, CellHeight) / 2, TextureTileWidth);
                }
            }
        }

        private Rect GetTextureRectFromPos(int x, int y) => new(x * TextureTileWidth, y * TextureTileHeight, TextureTileWidth, TextureTileHeight);

        public void SetSprite(int x, int y, int tileId)
        {
            grid.GetCells()[x, y].Renderer.sprite = GetTileFromId(tileId);
        }

        public void SetColor(int x, int y, Color color)
        {
            grid.GetCells()[x, y].Renderer.color = color;
        }

        public struct SpriteGridCell
        {
            public GameObject Object;
            public SpriteRenderer Renderer;
        }

    }
}
