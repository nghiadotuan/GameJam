using UnityEngine;
using UnityEngine.InputSystem;

public class SmoothModelRotator : MonoBehaviour
{
    [Header("Cấu hình xoay")]
    public float rotationSpeed = 0.2f;    // Tốc độ xoay
    public float smoothSpeed = 5f;       // Độ mượt (càng cao càng nhạy)
    public float damping = 0.95f;        // Lực cản quán tính (0.9 -> 0.99)

    private Vector2 _rotationVelocity;   // Vận tốc xoay hiện tại
    private Vector2 _inputDelta;
    private bool _isDragging;

    // Khai báo các Action của New Input System
    private InputAction _touchClickAction;
    private InputAction _touchDeltaAction;

    private void Awake()
    {
        // Khởi tạo Input (Bạn có thể dùng PlayerInput component hoặc code tay như này)
        var map = new InputActionMap("ModelInteraction");
        
        // Action nhận diện nhấn giữ (chuột trái hoặc chạm màn hình)
        _touchClickAction = map.AddAction("Click", binding: "<Pointer>/press");
        
        // Action lấy giá trị di chuyển (delta) của pointer
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
        // Kiểm tra xem người dùng có đang nhấn/chạm không
        if (_touchClickAction.IsPressed())
        {
            _isDragging = true;
            _inputDelta = _touchDeltaAction.ReadValue<Vector2>();
            
            // Tính toán vận tốc xoay dựa trên đầu vào
            // Vuốt X (ngang) -> xoay quanh trục Y
            // Vuốt Y (dọc) -> xoay quanh trục X
            _rotationVelocity.x = _inputDelta.x * rotationSpeed;
            _rotationVelocity.y = _inputDelta.y * rotationSpeed;
        }
        else
        {
            _isDragging = false;
            // Áp dụng lực cản khi buông tay để tạo quán tính
            _rotationVelocity *= damping;
        }
    }

    private void ApplyRotation()
    {
        // Sử dụng Quaternion.Euler để tính toán góc xoay
        // Lưu ý: Delta X của tay tương ứng với xoay quanh trục Y của Model (Up)
        // Delta Y của tay tương ứng với xoay quanh trục X của Model (Right)
        float rotX = _rotationVelocity.y;
        float rotY = -_rotationVelocity.x;

        // Xoay dựa trên không gian thế giới để tránh hiện tượng đảo lộn trục khi model xoay
        transform.Rotate(Vector3.up, rotY, Space.World);
        transform.Rotate(Vector3.right, rotX, Space.World);

        // Khử rung lắc nhỏ khi vận tốc quá thấp
        if (!_isDragging && _rotationVelocity.magnitude < 0.01f)
        {
            _rotationVelocity = Vector2.zero;
        }
    }
}