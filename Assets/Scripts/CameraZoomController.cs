using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Cinemachine;

public class CameraZoomController : MonoBehaviour
{
    [Header("Cinemachine Setup")]
    public CinemachineCamera virtualCamera;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f;
    public float minDistance = 2f;
    public float maxDistance = 20f;
    public float smoothSpeed = 10f;

    private CinemachineFollow _cinemachineFollow;
    private Vector3 _defaultOffset;
    private float _defaultDistance;
    private float _targetDistance;

    private void Awake()
    {
        if (virtualCamera != null)
        {
            _cinemachineFollow = virtualCamera.GetComponent<CinemachineFollow>();

            if (_cinemachineFollow != null)
            {
                _defaultOffset = _cinemachineFollow.FollowOffset;
                _defaultDistance = _defaultOffset.magnitude;
                _targetDistance = _defaultDistance;
            }
            else
            {
                Debug.LogError("[CameraZoomController] Không tìm thấy CinemachineFollow trên virtualCamera.");
            }
        }
        else
        {
            Debug.LogError("[CameraZoomController] virtualCamera chưa được gán.");
        }
    }

    private void Update()
    {
        HandleZoom();
    }

    /// <summary>
    /// Giữ chuột phải + kéo lên -> zoom ra, kéo xuống -> zoom vào.
    /// Thả chuột phải -> camera trở về khoảng cách mặc định ban đầu.
    /// </summary>
    private void HandleZoom()
    {
        if (_cinemachineFollow == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            // delta.y dương = kéo lên = zoom ra (tăng distance)
            // delta.y âm   = kéo xuống = zoom vào (giảm distance)
            _targetDistance -= delta.y * zoomSpeed * 0.01f;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }
        else
        {
            // Thả chuột phải -> trở về khoảng cách mặc định
            _targetDistance = _defaultDistance;
        }

        // Nội suy mượt theo hướng offset ban đầu, chỉ thay đổi độ dài
        Vector3 offsetDirection = _defaultOffset.normalized;
        float currentDistance = _cinemachineFollow.FollowOffset.magnitude;
        float newDistance = Mathf.Lerp(currentDistance, _targetDistance, Time.deltaTime * smoothSpeed);
        _cinemachineFollow.FollowOffset = offsetDirection * newDistance;
    }
}
