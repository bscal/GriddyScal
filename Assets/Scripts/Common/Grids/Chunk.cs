using Common.Grids.Cells;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Common.Grids
{

    public struct ChunkData
    {
        public ChunkState State;
        public bool IsDirty;
    }

    public class Chunk
    {
        public ChunkState State;
        public int x, y, Width, Height;
        public bool IsDirty;

        public Color[] Colors;

        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public int Count;
        public float MaxMass = 1.0f;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;

        public TileMap2DArray Grid;

        public Chunk(TileMap2DArray Grid, int x, int y, int w, int h)
        {
            this.Grid = Grid;
            this.x = x;
            this.y = y;
            this.Width = w;
            this.Height = h;
            int size = Width * Height;
            Colors = new Color[size * 4];
        }

        public void Create(GameObject gameObject, Mesh mesh, Material material)
        {
            gameObject.name = $"Chunk({x}, {y})";
            gameObject.transform.position = new Vector3(x * Width, y * Height);
            GameObject = gameObject;

            MeshFilter = gameObject.AddComponent<MeshFilter>();
            MeshFilter.mesh = mesh;

            MeshRenderer = gameObject.AddComponent<MeshRenderer>();
            MeshRenderer.sharedMaterial = material;
            MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshRenderer.receiveShadows = false;

            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new(Width, Height, .01f);
        }

        public void UpdateState()
        {
            ChunkState newState;
            if (MeshRenderer.isVisible)
            {
                newState = ChunkState.LOADED;
            }
            else
            {
                newState = ChunkState.FROZEN;
            }

            if (newState != State)
            {
                State = newState;
                long key = GetKey();
                var chunkData = Grid.NativeChunkMap[key];
                chunkData.State = State;
                chunkData.IsDirty = false;
                Grid.NativeChunkMap[key] = chunkData;
            }
        }

        public static readonly float4 CYAN = new(0f, 1f, 1f, 1f);
        public static readonly float4 BLUE = new(0f, 0f, 1f, 1f);
        public void UpdateMesh()
        {
            IsDirty = false;

            int vertexIndex = 0;
            Vector2Int worldPos = ChunkToWorldPos();
            for (int y = worldPos.y; y < worldPos.y + Height; y++)
            {
                for (int x = worldPos.x; x < worldPos.x + Width; x++)
                {
                    var i = x + y * Grid.MapSize.x;
                    var state = Grid.States.States[i];
                    if (!state.IsSolid)
                    {
                        if (Grid.States.m_TilePhysicsSystem.Mass[i] >= MinMass)
                        {
                            Grid.States.States[i] = CellStateManager.Instance.Cells[2].GetDefaultState();
                        }
                        else
                        {
                            Grid.States.States[i] = CellStateManager.Instance.Cells[0].GetDefaultState();
                        }
                    }
                    else
                        Grid.States.m_TilePhysicsSystem.Mass[i] = 0f;

                    // Get any updated values
                    state = Grid.States.States[i];
                    if (state.Equals(CellStateManager.Instance.Cells[2].GetDefaultState()))
                        SetColor(vertexIndex, Color.Lerp(Color.cyan, Color.blue, Grid.States.m_TilePhysicsSystem.Mass[i]));
                    else
                        SetColor(vertexIndex, Float4ToColor(state.CellColor));
                    vertexIndex += 4;
                }
            }

            MeshFilter.mesh.SetColors(Colors);
        }

        public void Serialize()
        {

        }

        public void Deserialize()
        {

        }

        public Color Float4ToColor(float4 f4)
        {
            return new Color
            {
                r = f4.x,
                g = f4.y,
                b = f4.z,
                a = f4.w
            };
        }

        public long GetKey() => (long)y << 32 | (long)x;

        public Vector2Int ChunkToWorldPos()
        {
            return new Vector2Int
            {
                x = Width * x,
                y = Height * y,
            };
        }

        public void SetColor(int index, Color color)
        {
            Colors[index] = color;
            Colors[index + 1] = color;
            Colors[index + 2] = color;
            Colors[index + 3] = color;
        }

    }

    public enum ChunkState : byte
    {
        UNLOADED = 0,
        LOADED = 1,
        PERMANENTLY_LOADED = 2,
        FROZEN = 3,
    }

}