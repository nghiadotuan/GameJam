using UnityEngine;
using UnityEngine.InputSystem;

public class GameController : MonoBehaviour
{
    public static GameController Instance;

    public enum InputState { Idle, PotentialInteraction, LongPressing, Swiping, PackDisabled }
    
    [Header("Current State")]
    public InputState currentState = InputState.Idle;

    [Header("Settings")]
    public Camera mainCamera;
    public LayerMask ballLayer;
    public float longPressDuration = 0.5f;
    public float moveThreshold = 15f; // Giảm xuống một chút để xoay nhạy hơn

    private PackBalls _currentDisabledPack;
    private Vector2 _startPosition;
    private float _timer;
    private bool _hitBallOnDown; // Lưu xem lúc nhấn xuống có trúng bóng không

    public bool IsHoldingBall => currentState == InputState.PackDisabled;

    private void Awake() => Instance = this;

    private void Update() => HandleInputState();

    private void HandleInputState()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 currentMousePos = mouse.position.ReadValue();

        switch (currentState)
        {
            case InputState.Idle:
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _startPosition = currentMousePos;
                    _timer = 0;
                    // Kiểm tra xem có trúng bóng không nhưng CHƯA hành động gì cả
                    _hitBallOnDown = CheckIfHittingBall(_startPosition);
                    
                    // Chuyển sang trạng thái chờ tương tác (Bất kể bấm vào đâu)
                    currentState = InputState.PotentialInteraction;
                }
                break;

            case InputState.PotentialInteraction:
                _timer += Time.deltaTime;
                float dist = Vector2.Distance(_startPosition, currentMousePos);

                // 1. DI CHUYỂN -> Chuyển sang xoay ngay lập tức (Bất kể có trúng bóng hay không)
                if (dist > moveThreshold)
                {
                    currentState = InputState.Swiping;
                }
                // 2. GIỮ IM + TRÚNG BÓNG -> Chuyển sang Disable Pack
                else if (_timer >= longPressDuration && _hitBallOnDown)
                {
                    ExecuteDisablePack(_startPosition);
                    currentState = InputState.PackDisabled;
                }
                // 3. THẢ TAY SỚM
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    // Nếu bấm nhanh vào bóng thì mới Log
                    if (_hitBallOnDown) ExecuteClickLog(currentMousePos);
                    currentState = InputState.Idle;
                }
                break;

            case InputState.Swiping:
                // Trạng thái này cho phép SmoothModelRotator chạy
                if (mouse.leftButton.wasReleasedThisFrame) currentState = InputState.Idle;
                break;

            case InputState.PackDisabled:
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    EnableCurrentPack();
                    currentState = InputState.Idle;
                }
                break;
        }
    }

    private bool CheckIfHittingBall(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, 100f, ballLayer);
    }

    private void ExecuteClickLog(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            PackBalls pack = hit.collider.GetComponentInParent<PackBalls>();
            if (pack != null) Debug.Log($"<color=yellow>[Click]</color> Pack: <b>{pack.name}</b>");
        }
    }

    private void ExecuteDisablePack(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            _currentDisabledPack = hit.collider.GetComponentInParent<PackBalls>();
            if (_currentDisabledPack != null) _currentDisabledPack.gameObject.SetActive(false);
        }
    }

    private void EnableCurrentPack()
    {
        if (_currentDisabledPack != null)
        {
            _currentDisabledPack.gameObject.SetActive(true);
            _currentDisabledPack = null;
        }
    }
}