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

    [Header("UI Popups")]
    public GameObject winPopup;
    public GameObject losePopup;

    public bool isTransferringStash = false;
    public ShoveMovement currentTransferMainShove { get; private set; }

    public PopupLose popupLose;
    public PopupWin popupWin;

    private PackBalls _currentDisabledPack;
    private Vector2 _startPosition;
    private float _timer;
    private bool _hitBallOnDown; 
    private int _initialPackCount;
    private int _moveOutCount;
    private bool _hasLoggedWin;
    private bool _hasLoggedLose;

    public bool IsHoldingBall => currentState == InputState.PackDisabled;

    public Transform disablePhysicsTransform;

    private void Awake() => Instance = this;

    private void Start()
    {
        _initialPackCount = FindObjectsByType<PackBalls>(FindObjectsSortMode.None).Length;
        _moveOutCount = 0;
        _hasLoggedWin = false;
        _hasLoggedLose = false;

        // 1. Áp dụng material cho các xe trên băng chuyền ban đầu
        // if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList != null)
        // {
        //     foreach (var shove in ShoveContainer.Instance.shoveList)
        //     {
        //         if (shove != null) shove.ApplyMaterial();
        //     }
        // }
        
        // 2. Áp dụng material cho các xe Stash bên ngoài bãi đỗ
        if (stashShoves != null)
        {
            foreach (var stash in stashShoves)
            {
                if (stash != null) stash.ApplyMaterial();
            }
        }
    }

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
        if (isTransferringStash) return; // Khoá tương tác trong lúc bóng đang bay từ Stash vào Main
        
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

        ShoveMovement targetShove = null;
        bool isFirstPackForShove = false;

        // 1. ƯU TIÊN 1: TÌM SHOVE ĐANG CHÚA CÙNG MÀU VÀ CÒN CHỖ TRỐNG (TRÊN BĂNG CHUYỀN HOẶC STASH)
        if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            int maxShovesToCheck = Mathf.Min(2, ShoveContainer.Instance.shoveList.Count);
            for (int i = 0; i < maxShovesToCheck; i++)
            {
                var shove = ShoveContainer.Instance.shoveList[i];
                Shove shoveComp = shove.GetComponent<Shove>();
                
                if (shoveComp != null && shoveComp.Color == pack.colorIndex)
                {
                    // Đã có balls nhưng chưa đầy?
                    int totalCapacity = 0;
                    int totalBallsInIt = 0;
                    bool hasCapacitySet = false;

                    foreach (var s in shove.GetComponentsInChildren<SmallShove>())
                    {
                        if (s.NumBallFull > 0) hasCapacitySet = true;
                        totalCapacity += s.NumBallFull;
                        totalBallsInIt += s.CurrentBallCount + s.PendingBallCount;
                    }
                    totalBallsInIt += shove.InPipeBallCount;

                    // Nếu nó đang là xe được gán sức chứa, và còn sức chứa
                    if (hasCapacitySet && totalCapacity - totalBallsInIt > 0)
                    {
                        targetShove = shove;
                        targetShove.TargetColor = pack.colorIndex;
                        break; // Có xe đang hút dở thì dùng xe này
                    }
                }
            }
        }

        // ƯU TIÊN 1.5: KIỂM TRA TRONG STASH XEM CÓ XE NÀO CÙNG MÀU MÀ VẪN CÒN CHỖ KHÔNG
        if (targetShove == null && stashShoves != null)
        {
            foreach (var stash in stashShoves)
            {
                Shove stashShoveComp = stash.GetComponent<Shove>();
                if (stashShoveComp != null && stashShoveComp.Color == pack.colorIndex)
                {
                    int totalCapacity = 0;
                    int totalBallsInIt = 0;
                    bool hasCapacitySet = false;

                    foreach (var s in stash.GetComponentsInChildren<SmallShove>())
                    {
                        if (s.NumBallFull > 0) hasCapacitySet = true;
                        totalCapacity += s.NumBallFull;
                        totalBallsInIt += s.CurrentBallCount + s.PendingBallCount;
                    }
                    totalBallsInIt += stash.InPipeBallCount;

                    if (hasCapacitySet && totalCapacity - totalBallsInIt > 0)
                    {
                        targetShove = stash;
                        targetShove.TargetColor = pack.colorIndex;
                        break;
                    }
                }
            }
        }

        // 2. NẾU KHÔNG CÓ XE NÀO ĐỌC DỞ CÒN CHỖ, TÌM XE TRỐNG TRÊN BĂNG CHUYỀN (Cùng màu gốc)
        if (targetShove == null && ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            int maxShovesToCheck = Mathf.Min(2, ShoveContainer.Instance.shoveList.Count);
            for (int i = 0; i < maxShovesToCheck; i++)
            {
                var shove = ShoveContainer.Instance.shoveList[i];
                Shove shoveComp = shove.GetComponent<Shove>();

                // Nếu là xe cùng màu và HOÀN TOÀN TRỐNG (chưa từng set NumBallFull)
                if (shoveComp != null && shoveComp.Color == pack.colorIndex)
                {
                    bool isEmptyAndUnassigned = true;
                    foreach (var s in shove.GetComponentsInChildren<SmallShove>())
                    {
                        if (s.NumBallFull > 0 || s.CurrentBallCount > 0 || s.PendingBallCount > 0 || shove.InPipeBallCount > 0)
                        {
                            isEmptyAndUnassigned = false;
                            break;
                        }
                    }

                    if (isEmptyAndUnassigned)
                    {
                        targetShove = shove;
                        targetShove.TargetColor = pack.colorIndex;
                        isFirstPackForShove = true; // Đánh dấu đây là pack đầu tiên xác định sức chứa
                        break;
                    }
                }
            }
        }

        // 3. NẾU VẪN KHÔNG CÓ, TÌM 1 XE XÁM TRỐNG 
        if (targetShove == null && stashShoves != null)
        {
            foreach (var stash in stashShoves)
            {
                Shove stashShoveComp = stash.GetComponent<Shove>();
                
                // Điều kiện: Chưa được đổi màu bao giờ (ColorEnum.None) và hoàn toàn trống
                if (stashShoveComp != null && stashShoveComp.Color == ColorEnum.None)
                {
                    bool isEmptyAndUnassigned = true;
                    foreach (var s in stash.GetComponentsInChildren<SmallShove>())
                    {
                        if (s.NumBallFull > 0 || s.CurrentBallCount > 0 || s.PendingBallCount > 0 || stash.InPipeBallCount > 0)
                        {
                            isEmptyAndUnassigned = false;
                            break;
                        }
                    }

                    if (isEmptyAndUnassigned)
                    {
                        targetShove = stash;
                        stashShoveComp.Color = pack.colorIndex;
                        targetShove.TargetColor = pack.colorIndex;
                        targetShove.ApplyMaterial();
                        isFirstPackForShove = true; // Pack đầu tiên xác định sức chứa
                        break;
                    }
                }
            }
        }

        if (targetShove != null && isFirstPackForShove)
        {
            SmallShove[] smallShoves = targetShove.GetComponentsInChildren<SmallShove>();
            PackBalls ownerPack = null;

            if (!pack.IsOwner)
            {
                ownerPack = pack;
                pack.IsOwner = true;
            }
            else
            {
                PackBalls[] allPacks = Object.FindObjectsByType<PackBalls>(FindObjectsSortMode.None);
                foreach (var p in allPacks)
                {
                    if (p.colorIndex == pack.colorIndex && !p.IsOwner && p.balls.Count > 0)
                    {
                        ownerPack = p;
                        p.IsOwner = true;
                        break;
                    }
                }
            }

            int totalCapacityToSet = ownerPack != null ? ownerPack.balls.Count : sortedBalls.Count; 

            int emptyCount = smallShoves.Length;
            if (emptyCount > 0)
            {
                int baseCap = totalCapacityToSet / emptyCount;
                int remainder = totalCapacityToSet % emptyCount;

                for (int i = 0; i < smallShoves.Length; i++)
                {
                    int cap = baseCap + (i == smallShoves.Length - 1 ? remainder : 0);
                    // Cho phép sức chứa logic lớn hơn số Pos hiển thị (không khóa cứng theo số slot mesh).
                    smallShoves[i].NumBallFull = cap;
                    
                    if (smallShoves[i].NumBallFull > 0 && smallShoves[i].NumBallFull <= smallShoves[i].ListPosBall.Count)
                        smallShoves[i].IndexPosBall = smallShoves[i].ListPosBall.Count - smallShoves[i].NumBallFull;
                    else
                        smallShoves[i].IndexPosBall = 0;
                }
            }
        }

        if (targetShove == null)
        {
            Debug.LogWarning($"Pack {pack.name} không có Shove mục tiêu phù hợp lúc bấm. Vẫn cho bắn, xử lý thua ở bước ball không vào được Shove.");
            EvaluateLoseConditionForColor(pack.colorIndex, $"Pack={pack.name} khong tim thay target shove luc click.");
        }

        pack.balls.Clear();
        float maxForceMultiplier = 0.4f;
        bool hasLoggedNoReservationForThisPack = false;

        int index = 0;
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

                bool hasPipeReservation = false;
                if (targetShove != null)
                {
                    hasPipeReservation = targetShove.TryReserveInPipeBall();
                    if (!hasPipeReservation && !hasLoggedNoReservationForThisPack)
                    {
                        Debug.LogWarning($"Pack {pack.name}: Shove {targetShove.name} đã hết slot reserve an toàn, vẫn tiếp tục cho rơi toàn bộ ball.");
                        hasLoggedNoReservationForThisPack = true;
                    }
                }

                // Chỉ gán xe mục tiêu ở đây; slot SmallShove sẽ được chọn khi bóng đi hết ống.
                b.ExplodeSimple(finalForce, targetShove, hasPipeReservation, pack.colorIndex);

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

    public void RegisterShoveMoveOut()
    {
        if (_hasLoggedWin) return;

        _moveOutCount++;

        if (_initialPackCount > 0 && _moveOutCount >= _initialPackCount)
        {
            _hasLoggedWin = true;
            Debug.Log($"WIN! MoveOut: {_moveOutCount}/{_initialPackCount}");
            if (winPopup != null) winPopup.SetActive(true);
        }
    }

    public void EvaluateLoseConditionForColor(ColorEnum ballColor, string context = null)
    {
        if (_hasLoggedLose || ballColor == ColorEnum.None) return;
        if (HasAvailableStashForColor(ballColor)) return;
        if (FrontTwoShovesContainColor(ballColor)) return;

        _hasLoggedLose = true;
        Debug.Log($"LOSE! BallColor={ballColor} | Het stash shove phu hop va 2 shove dau khong trung mau. {context}");
        if (losePopup != null) losePopup.SetActive(true);
    }

    private bool HasAvailableStashForColor(ColorEnum color)
    {
        if (stashShoves == null || stashShoves.Count == 0) return false;

        foreach (var stash in stashShoves)
        {
            if (stash == null) continue;

            int totalCapacity = 0;
            int used = stash.InPipeBallCount;
            bool hasCapacitySet = false;
            bool isEmptyUnassigned = true;

            foreach (var s in stash.GetComponentsInChildren<SmallShove>())
            {
                if (s == null) continue;
                totalCapacity += s.NumBallFull;
                used += s.CurrentBallCount + s.PendingBallCount;
                if (s.NumBallFull > 0) hasCapacitySet = true;
                if (s.NumBallFull > 0 || s.CurrentBallCount > 0 || s.PendingBallCount > 0)
                {
                    isEmptyUnassigned = false;
                }
            }

            if (isEmptyUnassigned && stash.InPipeBallCount == 0)
            {
                return true;
            }

            if (stash.TargetColor == color && hasCapacitySet && (totalCapacity - used > 0))
            {
                return true;
            }
        }

        return false;
    }

    private bool FrontTwoShovesContainColor(ColorEnum color)
    {
        if (ShoveContainer.Instance == null || ShoveContainer.Instance.shoveList == null || ShoveContainer.Instance.shoveList.Count == 0)
        {
            return false;
        }

        int maxCheck = Mathf.Min(2, ShoveContainer.Instance.shoveList.Count);
        for (int i = 0; i < maxCheck; i++)
        {
            var shove = ShoveContainer.Instance.shoveList[i];
            if (shove == null) continue;

            ColorEnum shoveColor = shove.TargetColor;
            if (shoveColor == ColorEnum.None)
            {
                Shove shoveComp = shove.GetComponent<Shove>();
                if (shoveComp != null) shoveColor = shoveComp.Color;
            }

            if (shoveColor == color)
            {
                return true;
            }
        }

        return false;
    }

    public async UniTaskVoid TryTransferStashToMainAsync()
    {
        if (isTransferringStash) return;

        ShoveMovement targetMainShove = null;
        ShoveMovement fullStashShove = null;

        // 1. Tìm xem có xe Xám (Stash) nào đang FULL không
        foreach (var stash in stashShoves)
        {
            if (stash.TargetColor != ColorEnum.None)
            {
                bool allFull = true;
                foreach (var ss in stash.GetComponentsInChildren<SmallShove>())
                {
                    if (ss.CurrentBallCount < ss.NumBallFull || ss.NumBallFull == 0)
                    {
                        allFull = false;
                        break;
                    }
                }
                if (allFull)
                {
                    fullStashShove = stash;
                    break;
                }
            }
        }

        if (fullStashShove == null) return;

        // 2. Nếu có Stash FULL, tìm trong 2 xe đầu băng chuyền:
        //    Ưu tiên 1: xe trống CÙNG MÀU với stash
        //    Ưu tiên 2: xe trống MÀU XÁM (None) → gán màu stash cho nó
        if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            int maxShovesToCheck = Mathf.Min(2, ShoveContainer.Instance.shoveList.Count);
            ShoveMovement grayFallback = null;

            for (int i = 0; i < maxShovesToCheck; i++)
            {
                var mainShove = ShoveContainer.Instance.shoveList[i];
                Shove mainShoveComp = mainShove.GetComponent<Shove>();
                if (mainShoveComp == null) continue;

                bool isMainEmpty = true;
                foreach (var ss in mainShove.GetComponentsInChildren<SmallShove>())
                {
                    if (ss.NumBallFull > 0 || ss.CurrentBallCount > 0 || ss.PendingBallCount > 0 || mainShove.InPipeBallCount > 0)
                    {
                        isMainEmpty = false;
                        break;
                    }
                }

                if (!isMainEmpty) continue;

                if (mainShoveComp.Color == fullStashShove.TargetColor)
                {
                    // Ưu tiên 1: cùng màu → chọn luôn
                    targetMainShove = mainShove;
                    break;
                }

                if (mainShoveComp.Color == ColorEnum.None && grayFallback == null)
                {
                    // Ưu tiên 2: xe xám trống → lưu lại làm fallback
                    grayFallback = mainShove;
                }
            }

            // Nếu không tìm được xe cùng màu, dùng xe xám trống và gán màu stash
            if (targetMainShove == null && grayFallback != null)
            {
                targetMainShove = grayFallback;
                Shove grayComp = targetMainShove.GetComponent<Shove>();
                if (grayComp != null) grayComp.Color = fullStashShove.TargetColor;
            }
        }

        if (targetMainShove == null) return;

        isTransferringStash = true;
        currentTransferMainShove = targetMainShove;

        targetMainShove.TargetColor = fullStashShove.TargetColor;
        Shove mainTargetShoveComp = targetMainShove.GetComponent<Shove>();
        if (mainTargetShoveComp != null) mainTargetShoveComp.Color = fullStashShove.TargetColor;

        SmallShove[] stashSmallShoves = fullStashShove.GetComponentsInChildren<SmallShove>();
        SmallShove[] mainSmallShoves = targetMainShove.GetComponentsInChildren<SmallShove>();

        List<Ball> ballsToTransfer = new List<Ball>();
        foreach (var ss in stashSmallShoves)
        {
            ballsToTransfer.AddRange(ss.GetComponentsInChildren<Ball>());
        }

        if (ballsToTransfer.Count == 0)
        {
            ballsToTransfer = fullStashShove.GetComponentsInChildren<Ball>().ToList();
        }

        // Chừa chỗ cho các ball đã reserve và đang đi trong ống vào main shove này.
        int totalBalls = ballsToTransfer.Count + targetMainShove.InPipeBallCount;
        int emptyCount = mainSmallShoves.Length;
        
        if (emptyCount > 0)
        {
            int baseCap = totalBalls / emptyCount;
            int remainder = totalBalls % emptyCount;

            for (int i = 0; i < mainSmallShoves.Length; i++)
            {
                int cap = baseCap + (i == mainSmallShoves.Length - 1 ? remainder : 0);
                mainSmallShoves[i].NumBallFull = cap;
                mainSmallShoves[i].CurrentBallCount = 0;
                mainSmallShoves[i].PendingBallCount = 0; 
                
                if (cap > mainSmallShoves[i].ListPosBall.Count)
                    mainSmallShoves[i].IndexPosBall = mainSmallShoves[i].ListPosBall.Count - cap;
                else
                    mainSmallShoves[i].IndexPosBall = 0;
            }
        }

        // Tạo danh sách nhóm các quả bóng dựa trên cột (SmallShove) lúc nó đang đứng ở bãi Stash
        List<List<Ball>> stashBallsGrouped = new List<List<Ball>>();
        foreach (var ss in stashSmallShoves)
        {
            stashBallsGrouped.Add(ss.GetComponentsInChildren<Ball>().ToList());
        }

        foreach (var b in ballsToTransfer)
        {
            b.transform.SetParent(null, true);
        }

        List<UniTask> allTransferTasks = new List<UniTask>();

        bool hasBalls = true;
        while (hasBalls)
        {
            hasBalls = false;
            List<UniTask> batchTasks = new List<UniTask>();

            // Mỗi vòng lặp, rút đúng 1 quả bóng từ mỗi SmallShove trong Gara Stash để bắn cùng lúc
            for (int i = 0; i < stashBallsGrouped.Count; i++)
            {
                if (stashBallsGrouped[i].Count > 0)
                {
                    hasBalls = true;
                    Ball ball = stashBallsGrouped[i][0];
                    stashBallsGrouped[i].RemoveAt(0);

                    if (ball == null) continue;

                    SmallShove assignedShove = null;
                    foreach (var s in mainSmallShoves)
                    {
                        if (!s.IsFull)
                        {
                            assignedShove = s;
                            s.PendingBallCount++;
                            break;
                        }
                    }

                    if (assignedShove != null)
                    {
                        batchTasks.Add(ball.TransferToShoveAsync(assignedShove));
                    }
                }
            }

            if (batchTasks.Count > 0)
            {
                allTransferTasks.AddRange(batchTasks);
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.12f)); 
            }
        }

        await UniTask.WhenAll(allTransferTasks);

        fullStashShove.ResetToEmpty();

        currentTransferMainShove = null;
        isTransferringStash = false;

        if (ShoveContainer.Instance != null)
        {
            ShoveContainer.Instance.TryTriggerMoveOutIfFrontFull();
        }

        TryTransferStashToMainAsync().Forget();
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
                
                Shove shoveComp = newShoveObj.GetComponent<Shove>();
                if (shoveComp != null) shoveComp.Color = pack.colorIndex;

                shoveMove.ApplyMaterial();
                ShoveContainer.Instance.shoveList.Add(shoveMove);
            }
        }

        // 5. Cập nhật xếp hàng xe trên băng chuyền
        ShoveContainer.Instance.SetupShovePositions();

        Debug.Log($"<color=green>Thành công!</color> Đã dọn xe cũ và tự động sinh {allPacks.Length} chiếc Shove mới chở bóng theo màu.");
    }
}