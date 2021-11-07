using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D;

public class PixelFreeCamera : MonoBehaviour
{
    public PixelPerfectCamera Camera;

    [Header("Speeds")]
    public float MoveSpeed = 8f;
    public float ZoomSpeed = 1f;

    [Header("Min/Max")]
    [Range(1, 128)]
    public int MinZoom = 1;
    [Range(2, 128)]
    public int MaxZoom = 32;

    private bool m_IsMoving;
    private Controls m_Controls;

    private void Awake()
    {
#if DEBUG
        UnityEngine.Assertions.Assert.IsFalse(MinZoom > MaxZoom, "MinZoom is greater then MaxZoom");
#endif

        m_Controls = new Controls();
        m_Controls.Enable();
        m_Controls.Newactionmap.CameraMove.started += OnStartMove;
        m_Controls.Newactionmap.CameraMove.canceled += OnEndMove;
        m_Controls.Newactionmap.CameraZoom.performed += OnCameraZoom;
    }

    private void OnDestroy()
    {
        m_Controls.Newactionmap.CameraMove.started -= OnStartMove;
        m_Controls.Newactionmap.CameraMove.canceled -= OnEndMove;
        m_Controls.Newactionmap.CameraZoom.performed -= OnCameraZoom;
        m_Controls.Disable();
    }

    private void Update()
    {
        if (m_IsMoving)
        {
            var value = m_Controls.Newactionmap.CameraMove.ReadValue<Vector2>();
            gameObject.transform.position += MoveSpeed * Time.deltaTime * new Vector3(value.x, value.y, 0f);
        }
    }

    private void OnStartMove(InputAction.CallbackContext ctx)
    {
        m_IsMoving = true;
    }

    private void OnEndMove(InputAction.CallbackContext ctx)
    {
        m_IsMoving = false;
    }

    private void OnCameraZoom(InputAction.CallbackContext ctx)
    {
        float value = ctx.ReadValue<float>();
        float newValue = Mathf.MoveTowards(Camera.assetsPPU, (value > 0) ? MinZoom : MaxZoom, ZoomSpeed);
        Camera.assetsPPU = Mathf.RoundToInt(newValue);
    }
}
