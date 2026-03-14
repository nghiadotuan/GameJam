using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance;

    public enum InputState
    {
        Idle,
        PotentialInteraction,
        LongPressing,
        Swiping,
        PackDisabled
    }

    [Header("Current State")] public InputState currentState = InputState.Idle;

    [Header("Settings")] public Camera mainCamera;
    public LayerMask ballLayer;
    public GameConfig config;

    private PackBalls _currentDisabledPack;
    private Vector2 _startPosition;
    private float _timer;
    private bool _hitBallOnDown; // Lưu xem lúc nhấn xuống có trúng bóng không

    public bool IsHoldingBall => currentState == InputState.PackDisabled;

    private void Awake() => Instance = this;

    private void Update()
    {
        HandleInputState();
        HandleReloadScene();
    }

    /// <summary>
    /// Nhấn R để reload lại scene hiện tại.
    /// </summary>
    private void HandleReloadScene()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

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
                if (dist > config.moveThreshold)
                {
                    currentState = InputState.Swiping;
                }
                // 2. GIỮ IM + TRÚNG BÓNG -> Chuyển sang Disable Pack
                else if (_timer >= config.longPressDuration && _hitBallOnDown)
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
            if (pack != null)
            {
                // Gọi hàm Async bằng UniTask.Forget() để chạy nền không chặn main thread
                RippleExplosionTask(pack, hit.point).Forget();
            }
        }
    }

    private async UniTaskVoid RippleExplosionTask(PackBalls pack, Vector3 explosionPos)
    {
        // Lấy danh sách bóng (không cần sắp xếp nữa vì bung đồng thời)
        var allBalls = pack.balls.Where(b => b != null).ToList();
        pack.balls.Clear();

        // Bán kính ảnh hưởng tối đa (những quả ngoài tầm này sẽ bung cực nhẹ)
        float maxRadius = config.clickExplosionRadius; 

        foreach (GameObject ballObj in allBalls)
        {
            Ball b = ballObj.GetComponent<Ball>();
            if (b != null)
            {
                // Tính khoảng cách từ bóng đến tâm click
                float dist = Vector3.Distance(ballObj.transform.position, explosionPos);
            
                // Tính hệ số lực: Gần tâm = 1.0, Xa tâm (>= maxRadius) = 0.1
                // Dùng AnimationCurve ở đây nếu muốn tinh chỉnh mượt hơn
                float forcePercent = Mathf.Clamp01(1f - (dist / maxRadius));
                forcePercent = Mathf.Max(forcePercent, 0.1f); // Đảm bảo quả ở xa vẫn nhích nhẹ

                // Kích hoạt đồng thời, truyền thêm hệ số lực vào
                b.BurstAndFall(explosionPos, forcePercent).Forget();
            }
        }

        if (pack != null) Destroy(pack.gameObject, 5f);
    }

    private void ExecuteDisablePack(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            _currentDisabledPack = hit.collider.GetComponentInParent<PackBalls>();
            if (_currentDisabledPack != null)
            {
                // Thay vì tự viết logic nổ ở đây, hãy dùng chung logic Ripple
                // Điều này đảm bảo hiệu ứng bung/rơi đồng nhất trong toàn bộ game
                RippleExplosionTask(_currentDisabledPack, hit.point).Forget();

                // Đặt về null để tránh việc EnableCurrentPack() gọi nhầm SetActive(true) 
                // vào một Pack đã bị phá hủy
                _currentDisabledPack = null;
            }
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