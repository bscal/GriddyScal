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

    }
}
