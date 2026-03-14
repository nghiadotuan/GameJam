using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraZoomController : MonoBehaviour
{
    [Header("Cinemachine Setup")]
    public CinemachineCamera virtualCamera;
    public GameConfig config;

    private CinemachineFollow _cinemachineFollow;
    private Vector3 _defaultOffsetDirection;
    private float _initialDistance;
    private float _targetDistance;

    private void Awake()
    {
        if (virtualCamera != null)
        {
            _cinemachineFollow = virtualCamera.GetComponent<CinemachineFollow>();
            if (_cinemachineFollow != null)
            {
                _defaultOffsetDirection = _cinemachineFollow.FollowOffset.normalized;
                _initialDistance = _cinemachineFollow.FollowOffset.magnitude;
                _targetDistance = _initialDistance;
            }
        }
    }

    private void Update()
    {
        HandleZoom();
    }

    /// <summary>
    /// Giữ chuột phải + kéo lên -> zoom ra, kéo xuống -> zoom vào.
    /// </summary>
    private void HandleZoom()
    {
        if (_cinemachineFollow == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _targetDistance += delta.y * config.zoomSpeed * 0.01f;
            _targetDistance = Mathf.Clamp(_targetDistance, config.zoomMinDistance, config.zoomMaxDistance);
        }

        float currentDistance = _cinemachineFollow.FollowOffset.magnitude;
        float newDistance = Mathf.Lerp(currentDistance, _targetDistance, Time.deltaTime * config.zoomSmoothSpeed);
        _cinemachineFollow.FollowOffset = _defaultOffsetDirection * newDistance;
    }

    /// <summary>
    /// Reset camera về khoảng cách ban đầu.
    /// </summary>
    public void ResetToInitialZoom()
    {
        _targetDistance = _initialDistance;
    }
}