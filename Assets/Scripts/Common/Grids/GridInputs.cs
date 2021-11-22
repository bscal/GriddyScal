using Assets.Scripts.Common.FluidSimulation.Cellular_Automata;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.Grids
{
    public class GridInputs : MonoBehaviour
    {

        public GridBehaviour GridReference;

        private void Update()
        {
            bool rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            bool leftClicked = Mouse.current.leftButton.isPressed;
            bool middleClicked = Mouse.current.middleButton.wasPressedThisFrame;

            if (!rightClicked && !leftClicked && !middleClicked) return;

            var camera = Camera.main;
            var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100.0f))
            {
                var diff = hit.point - GridReference.transform.position;
                int x = (int)diff.x;
                int y = (int)diff.y;
                Debug.Log(x + " " + y);

                if (rightClicked)
                {
                    CellularAutomataFluidControllerV2.Instance.SetState(x, y, CellState.STATE_GROUND);
                }
                else if (leftClicked)
                {
                    CellularAutomataFluidControllerV2.Instance.SetState(x, y, CellState.STATE_WATER);
                    CellularAutomataFluidControllerV2.Instance.AddMass(x, y, 1.0f);
                }
                else if (middleClicked)
                {
                    CellularAutomataFluidControllerV2.Instance.MarkAsInfiniteSource(x, y);
                }
            }
        }

    }
}
