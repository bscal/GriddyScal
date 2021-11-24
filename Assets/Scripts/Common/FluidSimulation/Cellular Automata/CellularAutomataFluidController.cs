using Common.Grids;
using UnityEngine;
using UnityEngine.InputSystem;

public class CellularAutomataFluidController : MonoBehaviour
{

    public static CellularAutomataFluidController Instance { get; private set; }

    public SpriteGrid Grid;

    public float MaxMass = 1.0f;
    public float MinMass = 0.0001f;
    public float MaxCompression = 0.02f;
    public float MinFlow = .01f;
    public float MaxSpeed = 1f;

    private float m_MaxMassSqr;

    private float[] m_Mass;
    private float[] m_NewMass;
    private int[] m_States;

    private void Awake()
    {
        Instance = this;
        m_MaxMassSqr = MaxMass * MaxMass;
    }

    private void Start()
    {
        m_Mass = new float[Grid.grid.Size];
        m_NewMass = new float[Grid.grid.Size];
        m_States = new int[Grid.grid.Size];
    }

    void Update()
    {
        HandleInputs();
        SimulateCompression();
    }

    private void HandleInputs()
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
            if (rightClicked)
            {
                SetState(x, y, CellState.STATE_GROUND);
            }
            else if (leftClicked)
            {
                SetState(x, y, CellState.STATE_WATER);
                AddMass(x, y, 1.0f);
            }
            else if (middleClicked)
            {
                MarkAsInfiniteSource(x, y);
            }
        }
    }

    void SimulateCompression()
    {
        float remainingMass;

        for (int x = 0; x < Grid.grid.Width; x++)
        {
            for (int y = 0; y < Grid.grid.Height; y++)
            {
                int id = GetCellId(x, y);
                if (m_States[id] == CellState.STATE_GROUND) continue;

                remainingMass = m_Mass[id];
                if (remainingMass <= 0) continue;

                float flow;
                // Down
                int downId = GetCellId(x, y - 1);
                if (m_States[downId] != CellState.STATE_GROUND)
                {
                    flow = GetStableMass(remainingMass + m_Mass[downId]) - m_Mass[downId];
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = Mathf.Clamp(flow, 0, Mathf.Min(MaxSpeed, remainingMass));

                    m_NewMass[id] -= flow;
                    m_NewMass[downId] += flow;
                    remainingMass -= flow;
                }

                if (remainingMass <= 0) continue;

                // Left
                int leftId = GetCellId(x - 1, y);
                if (m_States[leftId] != CellState.STATE_GROUND)
                {
                    flow = (m_Mass[id] - m_Mass[leftId]) / 4;
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = Mathf.Clamp(flow, 0, remainingMass);

                    m_NewMass[id] -= flow;
                    m_NewMass[leftId] += flow;
                    remainingMass -= flow;
                }


                if (remainingMass <= 0) continue;

                // Right
                int rightId = GetCellId(x + 1, y);
                if (m_States[rightId] != CellState.STATE_GROUND)
                {
                    flow = (m_Mass[id] - m_Mass[rightId]) / 4;
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = Mathf.Clamp(flow, 0, remainingMass);

                    m_NewMass[id] -= flow;
                    m_NewMass[rightId] += flow;
                    remainingMass -= flow;
                }

                if (remainingMass <= 0) continue;

                // Up
                int upId = GetCellId(x, y + 1);
                if (m_States[upId] != CellState.STATE_GROUND)
                {
                    flow = remainingMass - GetStableMass(remainingMass + m_Mass[upId]);
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = Mathf.Clamp(flow, 0, Mathf.Min(MaxSpeed, remainingMass));

                    m_NewMass[id] -= flow;
                    m_NewMass[upId] += flow;
                }
            }
        }

        // Copies new mass
        m_NewMass.CopyTo(m_Mass, 0);

        for (int x = 0; x < Grid.grid.Width; x++)
        {
            for (int y = 0; y < Grid.grid.Height; y++)
            {
                int id = GetCellId(x, y);

                if (m_States[id] == CellState.STATE_GROUND) continue;
                else if (m_States[id] == CellState.STATE_INFINITE)
                {
                    m_Mass[id] = 1;
                    UpdateState(x, y, id, m_States[id]);
                    continue;
                }
                m_States[id] = (m_Mass[id] > MinMass) ? CellState.STATE_WATER : CellState.STATE_AIR;
                UpdateState(x, y, id, m_States[id]);
            }
        }
    }

    public void MarkAsInfiniteSource(int x, int y)
    {
        m_States[GetCellId(x, y)] = CellState.STATE_INFINITE;
    }

    public void SetMass(int x, int y, float v)
    {
        var id = GetCellId(x, y);
        m_Mass[id] = Mathf.Clamp(m_Mass[id] + v, MinMass, MaxMass);
    }

    public void AddMass(int x, int y, float v)
    {
        int id = GetCellId(x, y);
        float mass = Mathf.Clamp(m_Mass[id] + v, MinMass, MaxMass);
        m_Mass[id] = mass;
    }

    public void SetState(int x, int y, int state, bool updateState = true)
    {
        int id = GetCellId(x, y);
        m_States[id] = state;
        if (updateState) UpdateState(x, y, id, state);
    }

    public void UpdateState(int x, int y, int id, int state)
    {
        if (state == CellState.STATE_GROUND) Grid.SetColor(x, y, Color.black);
        else if (state == CellState.STATE_AIR) Grid.SetColor(x, y, Color.white);
        else Grid.SetColor(x, y, Color.Lerp(Color.cyan, Color.blue, m_Mass[id]));
    }

    public float GetStableMass(float totalMass)
    {
        // All water goes to lower cell
        if (totalMass <= 1) return 1;
        else if (totalMass < 2 * MaxMass + MaxCompression) return (m_MaxMassSqr + totalMass * MaxCompression) / (MaxMass + MaxCompression);
        else return (totalMass + MaxCompression) / 2;
    }

    public int GetCellId(int x, int y)
    {
        x = Mathf.Clamp(x, 0, Grid.grid.Width - 1);
        y = Mathf.Clamp(y, 0, Grid.grid.Height - 1);
        return x + y * Grid.grid.Width;
    }
}

public struct CellState
{
    public const int STATE_NONE = -1;
    public const int STATE_AIR = 0;
    public const int STATE_GROUND = 1;
    public const int STATE_WATER = 2;
    public const int STATE_INFINITE = 3;

    public int State;
}