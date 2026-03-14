using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraZoomController : MonoBehaviour
{
    [Header("Cinemachine Setup")]
    public CinemachineCamera virtualCamera;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f; 
    public float minDistance = 1f; // Cho phép zoom gần hơn
    public float maxDistance = 50f;
    public float smoothSpeed = 10f;

    private CinemachineFollow _cinemachineFollow;
    private Vector3 _defaultOffsetDirection;
    
    // Lưu trữ giá trị gốc tuyệt đối
    private float _initialDistance; 
    private float _targetDistance;

    private void Awake()
    {
        if (virtualCamera != null)
        {
            _cinemachineFollow = virtualCamera.GetComponent<CinemachineFollow>();

            if (_cinemachineFollow != null)
            {
                // Bước này cực kỳ quan trọng: Lưu lại "Chân lý" ban đầu
                _defaultOffsetDirection = _cinemachineFollow.FollowOffset.normalized;
                _initialDistance = _cinemachineFollow.FollowOffset.magnitude;
                
                // Gán target bằng initial để lúc đầu không bị giật camera
                _targetDistance = _initialDistance;
            }
        }
    }

    private void Update()
    {
        HandleZoom();
    }

    private void HandleZoom()
    {
        if (_cinemachineFollow == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();

            // Kéo xuống (y âm) -> Giảm distance -> To lên
            // Kéo lên (y dương) -> Tăng distance -> Nhỏ đi
            _targetDistance += delta.y * zoomSpeed * 0.01f;
            
            // Giới hạn khoảng cách
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }

        // Nội suy từ khoảng cách HIỆN TẠI tới TARGET
        float currentDistance = _cinemachineFollow.FollowOffset.magnitude;
        float newDistance = Mathf.Lerp(currentDistance, _targetDistance, Time.deltaTime * smoothSpeed);
        
        // Luôn dựa trên hướng Offset gốc để không làm lệch góc nhìn
        _cinemachineFollow.FollowOffset = _defaultOffsetDirection * newDistance;
    }

    // Bạn có thể dùng hàm này nếu muốn reset về vị trí "to như lúc đầu" bằng code
    public void ResetToInitialZoom()
    {
        _targetDistance = _initialDistance;
    }
}