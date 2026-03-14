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
        // Sắp xếp bóng từ gần tâm nổ đến xa nhất để tạo hiệu ứng lan sóng
        var sortedBalls = pack.balls
            .Where(b => b != null)
            .OrderBy(b => Vector3.Distance(b.transform.position, explosionPos))
            .ToList();

        pack.balls.Clear();

        foreach (GameObject ballObj in sortedBalls)
        {
            if (ballObj == null) continue;

            Ball ballScript = ballObj.GetComponent<Ball>();
            if (ballScript == null) continue;

            // Fire-and-forget: mỗi ball tự animate rồi tự add Rigidbody sau khi xong
            ballScript.BurstThenFall(explosionPos).Forget();

            // Delay giữa các ball để tạo hiệu ứng lan sóng từ tâm ra ngoài
            await UniTask.Delay(config.clickRippleDelayPerBallMs);
        }

        if (pack != null) Destroy(pack.gameObject, config.burstDuration + config.ballAutoDestroyDelay);
    }

    private void ExecuteDisablePack(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            _currentDisabledPack = hit.collider.GetComponentInParent<PackBalls>();
            if (_currentDisabledPack != null)
            {
                Vector3 explosionPos = hit.point;
                float explosionRadius = config.holdExplosionRadius;
                float maxSpeed = config.holdMaxSpeed;

                // Bước 1: Kích hoạt Rigidbody cho TOÀN BỘ bóng trong Pack trước
                GameObject[] ballArray = _currentDisabledPack.balls.ToArray();
                foreach (GameObject ballObj in ballArray)
                {
                    if (ballObj != null)
                    {
                        Ball b = ballObj.GetComponent<Ball>();
                        if (b != null) b.PreparePhysics();
                    }
                }

                // Bước 2: Set velocity trực tiếp, có giới hạn tốc độ tối đa
                foreach (GameObject ballObj in ballArray)
                {
                    if (ballObj != null)
                    {
                        Rigidbody rb = ballObj.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            Vector3 dir = (ballObj.transform.position - explosionPos);
                            float distance = dir.magnitude;
                            if (distance < Mathf.Epsilon) dir = Random.onUnitSphere;
                            dir.Normalize();
                            dir.y += config.holdUpwardsModifier;

                            float falloff = Mathf.Clamp01(1f - distance / explosionRadius);
                            rb.linearVelocity = dir * (maxSpeed * falloff);
                        }
                    }
                }

                _currentDisabledPack.balls.Clear();
                Destroy(_currentDisabledPack.gameObject, 3.5f);
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