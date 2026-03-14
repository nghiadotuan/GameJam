using UnityEngine;
using UnityEngine.InputSystem;

public class SmoothModelRotator : MonoBehaviour
{
    [Header("Cấu hình xoay")]
    public float rotationSpeed = 0.2f;    
    public float smoothSpeed = 5f;       
    public float damping = 0.95f;        

    private Vector2 _rotationVelocity;   
    private bool _isDragging;

    private InputAction _touchClickAction;
    private InputAction _touchDeltaAction;

    private void Awake()
    {
        var map = new InputActionMap("ModelInteraction");
        _touchClickAction = map.AddAction("Click", binding: "<Pointer>/press");
        _touchDeltaAction = map.AddAction("Delta", binding: "<Pointer>/delta");
        map.Enable();
    }

    private void Update()
    {
        HandleInput();
        ApplyRotation();
    }

    private void HandleInput()
    {
        if (GameController.Instance == null) return;

        // Chỉ xoay khi GameController xác nhận đang ở trạng thái Swiping
        if (GameController.Instance.currentState == GameController.InputState.Swiping)
        {
            _isDragging = true;
            Vector2 inputDelta = _touchDeltaAction.ReadValue<Vector2>();
            _rotationVelocity.x = inputDelta.x * rotationSpeed;
            _rotationVelocity.y = inputDelta.y * rotationSpeed;
        }
        else
        {
            _isDragging = false;
            _rotationVelocity *= damping;
        }
    }

    private void ApplyRotation()
    {
        // Nếu vận tốc quá nhỏ thì dừng hẳn để tối ưu
        if (!_isDragging && _rotationVelocity.magnitude < 0.001f)
        {
            _rotationVelocity = Vector2.zero;
            return;
        }

        float rotX = _rotationVelocity.y;
        float rotY = -_rotationVelocity.x;

        // Áp dụng xoay
        transform.Rotate(Vector3.up, rotY, Space.World);
        transform.Rotate(Vector3.right, rotX, Space.World);
    }
}