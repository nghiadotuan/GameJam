using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
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

    [Header("Đường đi thực tế cho Ball")]
    [Tooltip("List này CODE SẼ TỰ ĐỘNG ĐIỀN bằng Catmull-Rom, Ball sẽ chạy theo list này")]
    public List<Transform> pipeNodes = new List<Transform>();

    [Header("Cấu hình Đường đi gốc (Thưa thớt)")]
    [Tooltip("Kéo thả các Transform gãy khúc của bạn vào đây")]
    public List<Transform> originalWaypoints = new List<Transform>();
    
    [Tooltip("Số lượng điểm muốn chèn thêm vào giữa 2 node gốc. Càng cao càng cong mượt.")]
    public int midpointsCount = 4;
    
    [Tooltip("Thư mục cha để chứa các Transform mới được đẻ ra")]
    public Transform generatedNodesContainer;

    [Header("Current State")] public InputState currentState = InputState.Idle;

    [Header("Settings")] public Camera mainCamera;
    public LayerMask ballLayer;
    public GameConfig config;

    private PackBalls _currentDisabledPack;
    private Vector2 _startPosition;
    private float _timer;
    private bool _hitBallOnDown; 

    public bool IsHoldingBall => currentState == InputState.PackDisabled;

    public Transform disablePhysicsTransform;

    private void Awake() => Instance = this;

    private void Update()
    {
        HandleInputState();
        HandleReloadScene();
    }

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
                    _hitBallOnDown = CheckIfHittingBall(_startPosition);
                    currentState = InputState.PotentialInteraction;
                }
                break;

            case InputState.PotentialInteraction:
                _timer += Time.deltaTime;
                float dist = Vector2.Distance(_startPosition, currentMousePos);

                if (dist > config.moveThreshold)
                {
                    currentState = InputState.Swiping;
                }
                else if (_timer >= config.longPressDuration && _hitBallOnDown)
                {
                    ExecuteDisablePack(_startPosition);
                    currentState = InputState.PackDisabled;
                }
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (_hitBallOnDown) ExecuteClickLog(currentMousePos);
                    currentState = InputState.Idle;
                }
                break;

            case InputState.Swiping:
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
                RippleExplosionTask(pack, hit.point).Forget();
            }
        }
    }

    private async UniTaskVoid RippleExplosionTask(PackBalls pack, Vector3 clickPoint)
    {
        Vector3 centerPos = SmoothModelRotator.Instance.transform.position;

        var allBalls = pack.balls.Where(b => b != null).ToList();
        pack.balls.Clear();

        var sortedBalls = allBalls.OrderBy(b => Vector3.Distance(b.transform.position, clickPoint)).ToList();

        float maxForceMultiplier = 0.4f;
        var index = 0;
        
        foreach (GameObject ballObj in sortedBalls)
        {
            Ball b = ballObj.GetComponent<Ball>();
            if (b != null)
            {
                Vector3 blastDir = (ballObj.transform.position - clickPoint).normalized;
                blastDir.y += 0.5f; 
                blastDir = blastDir.normalized;

                float distToClick = Vector3.Distance(ballObj.transform.position, clickPoint);
                float forcePercent = Mathf.Clamp01(1f - (distToClick / config.clickExplosionRadius));

                Vector3 finalForce = blastDir * (maxForceMultiplier * forcePercent);

                if (EffectController.Instance != null)
                {
                    EffectController.Instance.PlayEffect(0, b.transform.position);
                }

                b.ExplodeSimple(finalForce);

                if (index % 3 == 0)
                    await UniTask.Yield();
                index++;
            }
        }
    }

    private void ExecuteDisablePack(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            _currentDisabledPack = hit.collider.GetComponentInParent<PackBalls>();
            if (_currentDisabledPack != null)
            {
                RippleExplosionTask(_currentDisabledPack, hit.point).Forget();
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

    // =====================================================================
    // --- CÔNG CỤ TẠO ĐƯỜNG CONG TỰ ĐỘNG (CATMULL-ROM) ---
    // =====================================================================

    [Button][ContextMenu("1. Sinh Đường Cong Mượt (Curved Midpoints)")]
    public void GenerateCurvedPath()
    {
        if (originalWaypoints == null || originalWaypoints.Count < 2)
        {
            Debug.LogWarning("Cần ít nhất 2 điểm trong Original Waypoints để làm mượt!");
            return;
        }

        if (generatedNodesContainer == null)
        {
            GameObject containerObj = new GameObject("Generated_Curved_Nodes");
            containerObj.transform.SetParent(this.transform);
            generatedNodesContainer = containerObj.transform;
        }

        for (int i = generatedNodesContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(generatedNodesContainer.GetChild(i).gameObject);
        }
        pipeNodes.Clear();

        for (int i = 0; i < originalWaypoints.Count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? originalWaypoints[0].position - (originalWaypoints[1].position - originalWaypoints[0].position) : originalWaypoints[i - 1].position;
            Vector3 p1 = originalWaypoints[i].position;
            Vector3 p2 = originalWaypoints[i + 1].position;
            Vector3 p3 = (i == originalWaypoints.Count - 2) ? originalWaypoints[i + 1].position + (originalWaypoints[i + 1].position - originalWaypoints[i].position) : originalWaypoints[i + 2].position;

            Transform node = CreateNodeObj(p1, pipeNodes.Count);
            pipeNodes.Add(node);

            for (int step = 1; step <= midpointsCount; step++)
            {
                float t = (float)step / (midpointsCount + 1);
                Vector3 curvedPos = GetCatmullRomPosition(t, p0, p1, p2, p3);
                
                Transform midNode = CreateNodeObj(curvedPos, pipeNodes.Count);
                pipeNodes.Add(midNode);
            }
        }

        Transform lastNode = CreateNodeObj(originalWaypoints[originalWaypoints.Count - 1].position, pipeNodes.Count);
        pipeNodes.Add(lastNode);

        Debug.Log($"Thành công! Đã làm mượt từ {originalWaypoints.Count} điểm gốc thành {pipeNodes.Count} điểm cong.");
    }

    private Transform CreateNodeObj(Vector3 pos, int index)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = $"Node_{index:000}";
        obj.transform.position = pos;
        obj.transform.localScale = Vector3.one * 0.05f; 
        obj.transform.SetParent(generatedNodesContainer);
        
        DestroyImmediate(obj.GetComponent<Collider>());
        
        return obj.transform;
    }

    private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;
        return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
    }
}