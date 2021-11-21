using System;
using UnityEngine;

namespace Common.Grids
{

    public abstract class GridBehaviour : MonoBehaviour
    {
    }

    [Serializable]
    public class TileGrid<T> : ISerializationCallbackReceiver
    {

        public int Width, Height;
        public int Size => m_Elements.Length;

        private T[,] m_Elements;

        public TileGrid(int width, int height)
        {
            Width = width;
            Height = height;
            m_Elements = new T[width, height];
        }

        public int GetSize() => m_Elements.Length;

        public int ConvertXYToIndex(int x, int y) => x + y * Width;

        public Vector2Int ConvertIndexToXY(int index) => new(index % Width, index / Width);

        public T GetCell(int x, int y) => m_Elements[x, y];

        public T GetCellSafe(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Width - 1);
            y = Mathf.Clamp(y, 0, Height - 1);
            return GetCell(x, y);
        }

        public bool CellExists(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height;

        public void SetCell(int x, int y, T v)
        {
            if (CellExists(x, y))
                m_Elements[x, y] = v;
        }

        public T[,] GetCells() => m_Elements;

        public void ForEach(Action<int, int> consumer)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    consumer.Invoke(x, y);
                }
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            m_Elements = new T[Width, Height];
        }
    }
}
