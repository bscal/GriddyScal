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

    public struct SerializbleChunk
    {
        public ChunkState State;
        public int x, y, Width, Height;
        public bool IsDirty;
        public Color[] Colors;

        public void Serialize()
        {

        }

        public void Deserialize()
        {

        }
    }

    public class Chunk
    {
        public ChunkState State;
        public int x, y, Width, Height;
        public bool IsDirty;

        public Vector4[] Positions;
        public Vector4[] Colors;
        public int[] TileIds;

        private ComputeBuffer m_ArgsBuffer;
        private ComputeBuffer m_PositionBuffer;
        private ComputeBuffer m_ColorBuffer;
        private ComputeBuffer m_TileBuffer;

        private Bounds m_Bounds;

        //public GameObject GameObject;
        //public MeshFilter MeshFilter;
        //public MeshRenderer MeshRenderer;

        public float MinMass = 0.005f;

        private int m_Size;

        public TileMap2DArray Grid;
        public Matrix4x4[] Matrix;
        public MaterialPropertyBlock PropertyBlock;

        public Chunk(TileMap2DArray Grid, int x, int y, int w, int h)
        {
            this.Grid = Grid;
            this.x = x;
            this.y = y;
            this.Width = w;
            this.Height = h;
            this.m_Bounds = new Bounds(Vector3.zero, new Vector3(Grid.MapSize.x * 2, Grid.MapSize.y * 2, 100));
            m_Size = Width * Height;
            Colors = new Vector4[m_Size];
            Positions = new Vector4[m_Size];
            TileIds = new int[m_Size];
        }

       ~Chunk()
        {
            m_PositionBuffer.Dispose();
            m_ColorBuffer.Dispose();
            m_TileBuffer.Dispose();
            m_ArgsBuffer.Dispose();
        }

        public void Create()
        {
            /*            gameObject.name = $"Chunk({x}, {y})";
                        gameObject.transform.position = new Vector3(x * Width, y * Height);
                        GameObject = gameObject;

                        MeshFilter = gameObject.AddComponent<MeshFilter>();
                        MeshFilter.sharedMesh = mesh;

                        MeshRenderer = gameObject.AddComponent<MeshRenderer>();
                        MeshRenderer.sharedMaterial = material;
                        MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        MeshRenderer.receiveShadows = false;*/

            //var collider = gameObject.AddComponent<BoxCollider>();
            //collider.size = new(Width, Height, .01f);

            uint[] args = new uint[5];
            m_ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            args[0] = (uint)Grid.Mesh.GetIndexCount(0);
            args[1] = (uint)m_Size;
            args[2] = (uint)Grid.Mesh.GetIndexStart(0);
            args[3] = (uint)Grid.Mesh.GetBaseVertex(0);
            m_ArgsBuffer.SetData(args);

            m_PositionBuffer = new ComputeBuffer(m_Size, 4 * 4);
            m_ColorBuffer = new ComputeBuffer(m_Size, 4 * 4);
            m_TileBuffer = new ComputeBuffer(m_Size, 4);

            int i = 0;
            for (int chunkY = 0; chunkY < Height; chunkY++)
            {
                for (int chunkX = 0; chunkX < Width; chunkX++)
                {
                    int xx = x + chunkX;
                    int yy = y + chunkY;
                    Positions[i] = new Vector4(xx, yy, 0, 1);
                    Colors[i] = new Vector4(1, 1, 1, 1);
                    TileIds[i] = 0;
                    i++;
                }
            }

            m_PositionBuffer.SetData(Positions);
            m_ColorBuffer.SetData(Colors);
            m_TileBuffer.SetData(TileIds);

            Grid.Material.SetBuffer("positionBuffer", m_PositionBuffer);
            Grid.Material.SetBuffer("colorBuffer", m_ColorBuffer);
            Grid.Material.SetBuffer("tileBuffer", m_TileBuffer);
        }
        
        public void Render()
        {
            //Graphics.DrawMesh(Grid.Mesh, new Vector3(x, y), Quaternion.identity, Grid.Material, 0, null, 0, PropertyBlock, false, false, false);
            //Graphics.DrawMeshInstanced(Grid.Mesh, 0, Grid.Material, Matrix, 0, PropertyBlock);
            Graphics.DrawMeshInstancedIndirect(Grid.Mesh, 0, Grid.Material, m_Bounds, m_ArgsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows: false, lightProbeUsage: UnityEngine.Rendering.LightProbeUsage.Off);
        }

        public void UpdateState()
        {
            ChunkState newState;
            newState = ChunkState.LOADED;

            if (newState != State)
            {
                State = newState;
                long key = GetKey();
                var chunkData = Grid.NativeChunkMap[key];
                chunkData.State = State;
                chunkData.IsDirty = false;
                Grid.NativeChunkMap[key] = chunkData;
            }

            //Material.SetBuffer("colorBuffer", m_ColorBuffer);
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