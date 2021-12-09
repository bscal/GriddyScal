using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(TileMap2DArray))]
public class TileMapUpdater : MonoBehaviour
{

    public TileMap2DArray TileMap;

    public Vector3[] Tiles;

    private int m_Counter;

    private void Start()
    {
        Tiles = new Vector3[TileMap.Size * 4];
    }

    void Update()
    {
        if (m_Counter++ % 60 == 0)
        {
            m_Counter = 0;
            for (int i = 0; i < TileMap.Size; i++)
            {
                int id = UnityEngine.Random.Range(0, 64);
                int offset = i * 4;
                Tiles[offset + 0] = new Vector3(0, 0, id);
                Tiles[offset + 1] = new Vector3(0, 1, id);
                Tiles[offset + 2] = new Vector3(1, 1, id);
                Tiles[offset + 3] = new Vector3(1, 0, id);
            }

            TileMap.SetTileUVs(Tiles);
        }
    }
}
