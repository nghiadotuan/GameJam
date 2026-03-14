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

    [Header("Material Mapping")]
    [Tooltip("Danh sách Material tương ứng với ColorEnum. Vị trí 0 là None (Material Xám).")]
    public List<Material> materialMapping = new List<Material>();

    [Header("Stash Containers")]
    [Tooltip("Danh sách các xe Shove phụ bên ngoài (màu xám). Sẽ được dùng khi 2 xe chính đã kín màu khác.")]
    public List<ShoveMovement> stashShoves = new List<ShoveMovement>();

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
        var sortedBalls = allBalls.OrderBy(b => Vector3.Distance(b.transform.position, clickPoint)).ToList();

        SmallShove[] smallShoves = null;
        ShoveMovement targetShove = null;

        if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            // Tìm Shove đầu tiên trên băng chuyền phù hợp (chưa gán Pack nào hoặc đã gán đúng Pack này)
            // CHỈ KIỂM TRA TỐI ĐA 2 SHOVE ĐỨNG ĐẦU
            int maxShovesToCheck = Mathf.Min(2, ShoveContainer.Instance.shoveList.Count);
            for (int i = 0; i < maxShovesToCheck; i++)
            {
                var shove = ShoveContainer.Instance.shoveList[i];
                if (shove.TargetColor == ColorEnum.None || shove.TargetColor == pack.colorIndex)
                {
                    targetShove = shove;
                    targetShove.TargetColor = pack.colorIndex; // Khóa Shove này lại bằng màu của Pack
                    smallShoves = targetShove.GetComponentsInChildren<SmallShove>();
                    break;
                }
            }
        }

        // ===============================================
        // FALLBACK: KHI 2 SHOVE CHÍNH ĐÃ KÍN BỞI MÀU KHÁC
        // Kiểm tra danh sách xe xám phụ (Stash Shoves)
        // ===============================================
        if (targetShove == null && stashShoves != null && stashShoves.Count > 0)
        {
            foreach (var stash in stashShoves)
            {
                if (stash.TargetColor == ColorEnum.None || stash.TargetColor == pack.colorIndex)
                {
                    targetShove = stash;
                    smallShoves = stash.GetComponentsInChildren<SmallShove>();

                    // Nếu chiếc Shove này vốn là màu xám (None), thì hóa phép đổi màu nó
                    if (stash.TargetColor == ColorEnum.None)
                    {
                        stash.TargetColor = pack.colorIndex;

                        // (Tuỳ chọn) Đổi ngay Material của nó để người chơi dễ nhận biết
                        if (materialMapping.Count > (int)pack.colorIndex && materialMapping[(int)pack.colorIndex] != null)
                        {
                            var renderer = stash.GetComponentInChildren<MeshRenderer>();
                            if (renderer != null)
                            {
                                renderer.material = materialMapping[(int)pack.colorIndex];
                            }
                        }
                    }
                    break;
                }
            }
        }

        if (targetShove == null)
        {
            Debug.LogWarning("Không tìm thấy Shove chính/phụ nào trống để chứa Pack này!");
            return;
        }

        pack.balls.Clear();

        float maxForceMultiplier = 0.4f;
        var index = 0;
        
        foreach (GameObject ballObj in sortedBalls)
        {
            Ball b = ballObj.GetComponent<Ball>();
            if (b != null)
            {
                Vector3 blastDir = (ballObj.transform.position - clickPoint).normalized;
                blastDir.y += 0.2f; 
                blastDir = blastDir.normalized;

                float distToClick = Vector3.Distance(ballObj.transform.position, clickPoint);
                float forcePercent = Mathf.Clamp01(1f - (distToClick / config.clickExplosionRadius));

                Vector3 finalForce = blastDir * (maxForceMultiplier * forcePercent);

                if (EffectController.Instance != null)
                {
                    EffectController.Instance.PlayEffect(0, b.transform.position);
                }

                SmallShove assignedShove = null;
                if (smallShoves != null)
                {
                    // Chạy gán sức chứa ban đầu nếu toàn bộ Shove chưa từng được allocate (NumBallFull == 0)
                    bool needsInitCapacity = false;
                    foreach (var s in smallShoves)
                    {
                        if (s.NumBallFull == 0) needsInitCapacity = true;
                    }

                    if (needsInitCapacity)
                    {
                        int totalBallsToFill = sortedBalls.Count;
                        int emptyCount = smallShoves.Length;
                        
                        if (emptyCount > 0)
                        {
                            int baseCap = totalBallsToFill / emptyCount;
                            int remainder = totalBallsToFill % emptyCount;

                            for (int i = 0; i < smallShoves.Length; i++)
                            {
                                int cap = baseCap + (i == smallShoves.Length - 1 ? remainder : 0);
                                smallShoves[i].NumBallFull = smallShoves[i].CurrentBallCount + cap;
                                
                                if (cap > smallShoves[i].ListPosBall.Count)
                                    smallShoves[i].IndexPosBall = smallShoves[i].ListPosBall.Count - cap;
                                else
                                    smallShoves[i].IndexPosBall = 0;
                            }
                        }
                    }

                    // Tìm 1 cái chưa full (Cả Current + Pending) để nhét vào
                    foreach (var s in smallShoves)
                    {
                        if (!s.IsFull)
                        {
                            assignedShove = s;
                            s.PendingBallCount++; // Tạm thời xí chổ
                            break;
                        }
                    }
                }

                b.ExplodeSimple(finalForce, assignedShove);

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

    // =====================================================================
    // --- CÔNG CỤ TẠO SHOVE TỰ ĐỘNG THEO SỐ LƯỢNG PACKBALS ---
    // =====================================================================

    [Header("Auto Gen Shoves")]
    [Tooltip("Prefab của xe Shove")]
    public GameObject shovePrefab;
    [Tooltip("Vị trí cha chứa các xe Shove sinh ra")]
    public Transform shoveParent;

    [Button][ContextMenu("2. Sinh Shove Tự Động Theo PackBalls")]
    public void GenerateShovesFromPacks()
    {
        if (shovePrefab == null)
        {
            Debug.LogWarning("Chưa gắn Prefab Shove!");
            return;
        }

        if (ShoveContainer.Instance == null)
        {
            ShoveContainer.Instance = FindAnyObjectByType<ShoveContainer>();
            if (ShoveContainer.Instance == null)
            {
                Debug.LogWarning("Không tìm thấy ShoveContainer!");
                return;
            }
        }

        // 1. Tìm toàn bộ PackBalls đang có trên Scene
        PackBalls[] allPacks = FindObjectsByType<PackBalls>(FindObjectsSortMode.None);
        if (allPacks.Length == 0)
        {
            Debug.LogWarning("Không có PackBalls nào trên scene!");
            return;
        }

        // 2. Xóa sạch các xe Shove cũ trong Container và trên Scene
        for (int i = ShoveContainer.Instance.shoveList.Count - 1; i >= 0; i--)
        {
            if (ShoveContainer.Instance.shoveList[i] != null)
            {
                DestroyImmediate(ShoveContainer.Instance.shoveList[i].gameObject);
            }
        }
        ShoveContainer.Instance.shoveList.Clear();

        // 3. (Tùy chọn) Xáo trộn ngẫu nhiên thứ tự PackBalls để sinh xe ngẫu nhiên thứ tự
        // (Nếu bạn muốn xe chạy ra tuần tự thì bỏ qua đoạn xáo trộn này)
        List<PackBalls> randomizedPacks = allPacks.OrderBy(x => Random.value).ToList();

        // 4. Sinh xe Shove mới cho từng Pack
        foreach (var pack in randomizedPacks)
        {
            Transform parentToUse = shoveParent != null ? shoveParent : ShoveContainer.Instance.transform;
            GameObject newShoveObj = Instantiate(shovePrefab, parentToUse);
            newShoveObj.name = $"Shove_{pack.colorIndex}";

            ShoveMovement shoveMove = newShoveObj.GetComponent<ShoveMovement>();
            if (shoveMove != null)
            {
                // Gán màu đích mà Shove này sẽ khóa cố định ngay từ lúc mới sinh ra
                shoveMove.TargetColor = pack.colorIndex;
                ShoveContainer.Instance.shoveList.Add(shoveMove);
            }
        }

        // 5. Cập nhật xếp hàng xe trên băng chuyền
        ShoveContainer.Instance.SetupShovePositions();

        Debug.Log($"<color=green>Thành công!</color> Đã dọn xe cũ và tự động sinh {allPacks.Length} chiếc Shove mới chở bóng theo màu.");
    }
}