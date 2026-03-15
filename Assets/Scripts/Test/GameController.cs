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
    private float _nextStashTransferCheckTime;

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

        // Poll nhẹ để stash tự đẩy bóng sang main khi có small phù hợp trống.
        if (!isTransferringStash && Time.time >= _nextStashTransferCheckTime)
        {
            _nextStashTransferCheckTime = Time.time + 0.08f;
            TryTransferStashToMainAsync().Forget();
        }
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
            int maxShovesToCheck = Mathf.Min(1, ShoveContainer.Instance.shoveList.Count);
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
                        if (s.NumBallFull > 0 && s.SlotPackRef == pack) hasCapacitySet = true;
                        if (s.SlotPackRef == pack)
                        {
                            totalCapacity += s.NumBallFull;
                            totalBallsInIt += s.CurrentBallCount + s.PendingBallCount;
                        }
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

        // 1.25. Nếu xe cùng màu đã hết capacity hiện tại nhưng vẫn còn SmallShove trống chưa gán,
        // thì pack này sẽ gán capacity cho đúng 1 SmallShove trống đó.
        if (targetShove == null && ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            int maxShovesToCheck = Mathf.Min(1, ShoveContainer.Instance.shoveList.Count);
            for (int i = 0; i < maxShovesToCheck; i++)
            {
                var shove = ShoveContainer.Instance.shoveList[i];
                Shove shoveComp = shove.GetComponent<Shove>();
                if (shoveComp == null || shoveComp.Color != pack.colorIndex) continue;

                bool hasEmptySmallShove = false;
                foreach (var s in shove.GetComponentsInChildren<SmallShove>())
                {
                    if (s.NumBallFull == 0 && s.CurrentBallCount == 0 && s.PendingBallCount == 0 && s.SlotColor == ColorEnum.None && s.SlotPackRef == null)
                    {
                        hasEmptySmallShove = true;
                        break;
                    }
                }

                if (hasEmptySmallShove)
                {
                    targetShove = shove;
                    targetShove.TargetColor = pack.colorIndex;
                    isFirstPackForShove = true;
                    break;
                }
            }
        }

        // ƯU TIÊN 1.5: KIỂM TRA TRONG STASH XEM CÓ SLOT NÀO CÙNG MÀU MÀ VẪN CÒN CHỖ KHÔNG
        if (targetShove == null && stashShoves != null)
        {
            foreach (var stash in stashShoves)
            {
                if (stash == null) continue;

                bool hasSameColorCapacity = false;
                foreach (var s in stash.GetComponentsInChildren<SmallShove>())
                {
                    if (s == null) continue;
                    if (s.SlotColor == pack.colorIndex && s.SlotPackRef == pack && s.NumBallFull > 0 && (s.CurrentBallCount + s.PendingBallCount) < s.NumBallFull)
                    {
                        hasSameColorCapacity = true;
                        break;
                    }
                }

                if (hasSameColorCapacity)
                {
                    targetShove = stash;
                    break;
                }
            }
        }

        // 1.75. Stash có SmallShove trống chưa gán màu/capacity thì pack mới sẽ gán vào 1 slot đó.
        if (targetShove == null && stashShoves != null)
        {
            foreach (var stash in stashShoves)
            {
                if (stash == null) continue;

                bool hasEmptySmallShove = false;
                foreach (var s in stash.GetComponentsInChildren<SmallShove>())
                {
                    if (s.NumBallFull == 0 && s.CurrentBallCount == 0 && s.PendingBallCount == 0 && s.SlotColor == ColorEnum.None && s.SlotPackRef == null)
                    {
                        hasEmptySmallShove = true;
                        break;
                    }
                }

                if (hasEmptySmallShove)
                {
                    targetShove = stash;
                    isFirstPackForShove = true;
                    break;
                }
            }
        }

        // 2. NẾU KHÔNG CÓ XE NÀO ĐỌC DỞ CÒN CHỖ, TÌM XE TRỐNG TRÊN BĂNG CHUYỀN (Cùng màu gốc)
        if (targetShove == null && ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            int maxShovesToCheck = Mathf.Min(1, ShoveContainer.Instance.shoveList.Count);
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

        // 3. NẾU VẪN KHÔNG CÓ, TÌM 1 STASH CÓ SLOT TRỐNG CHƯA GÁN
        if (targetShove == null && stashShoves != null)
        {
            foreach (var stash in stashShoves)
            {
                if (stash == null) continue;
                bool hasEmptySmallShove = false;
                foreach (var s in stash.GetComponentsInChildren<SmallShove>())
                {
                    if (s.NumBallFull == 0 && s.CurrentBallCount == 0 && s.PendingBallCount == 0 && s.SlotColor == ColorEnum.None && s.SlotPackRef == null)
                    {
                        hasEmptySmallShove = true;
                        break;
                    }
                }

                if (hasEmptySmallShove)
                {
                    targetShove = stash;
                    isFirstPackForShove = true;
                    break;
                }
            }
        }

        if (targetShove != null && isFirstPackForShove)
        {
            SmallShove[] smallShoves = targetShove.GetComponentsInChildren<SmallShove>();

            SmallShove targetSmallShove = null;
            foreach (var ss in smallShoves)
            {
                if (ss.NumBallFull == 0 && ss.CurrentBallCount == 0 && ss.PendingBallCount == 0 && ss.SlotColor == ColorEnum.None && ss.SlotPackRef == null)
                {
                    targetSmallShove = ss;
                    break;
                }
            }

            if (targetSmallShove != null)
            {
                int packCapacity = sortedBalls.Count;
                targetSmallShove.NumBallFull = packCapacity;
                targetSmallShove.SlotColor = pack.colorIndex;
                targetSmallShove.SlotPackRef = pack;

                if (targetSmallShove.NumBallFull > 0 && targetSmallShove.NumBallFull <= targetSmallShove.ListPosBall.Count)
                {
                    targetSmallShove.IndexPosBall = targetSmallShove.ListPosBall.Count - targetSmallShove.NumBallFull;
                }
                else
                {
                    targetSmallShove.IndexPosBall = 0;
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
                b.ExplodeSimple(finalForce, targetShove, hasPipeReservation, pack.colorIndex, pack);

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
        Debug.Log($"LOSE! BallColor={ballColor} | Het stash shove phu hop va shove dau khong trung mau. {context}");
        if (losePopup != null) losePopup.SetActive(true);
    }

    private bool HasAvailableStashForColor(ColorEnum color)
    {
        if (stashShoves == null || stashShoves.Count == 0) return false;

        foreach (var stash in stashShoves)
        {
            if (stash == null) continue;
            foreach (var s in stash.GetComponentsInChildren<SmallShove>())
            {
                if (s == null) continue;

                bool isEmptyUnassigned = s.NumBallFull == 0 && s.CurrentBallCount == 0 && s.PendingBallCount == 0 && s.SlotColor == ColorEnum.None;
                if (isEmptyUnassigned)
                {
                    return true;
                }

                if (s.SlotColor == color && s.NumBallFull > 0 && (s.CurrentBallCount + s.PendingBallCount) < s.NumBallFull)
                {
                    return true;
                }
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

        int maxCheck = Mathf.Min(1, ShoveContainer.Instance.shoveList.Count);
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
        SmallShove targetMainSmallShove = null;
        SmallShove sourceStashSmallShove = null;
        ColorEnum transferColor = ColorEnum.None;
        SmallShove partialSourceCandidate = null;
        ColorEnum partialSourceColor = ColorEnum.None;

        // 1. Tìm source small trong stash:
        //    Ưu tiên small đã FULL, nếu chưa có thì lấy small có bóng (>0) để đẩy dần liên tục.
        foreach (var stash in stashShoves)
        {
            if (stash == null) continue;

            foreach (var ss in stash.GetComponentsInChildren<SmallShove>())
            {
                if (ss == null || ss.SlotColor == ColorEnum.None || ss.NumBallFull <= 0) continue;

                if (ss.CurrentBallCount >= ss.NumBallFull)
                {
                    sourceStashSmallShove = ss;
                    transferColor = ss.SlotColor;
                    break;
                }

                if (partialSourceCandidate == null && ss.CurrentBallCount > 0)
                {
                    partialSourceCandidate = ss;
                    partialSourceColor = ss.SlotColor;
                }
            }

            if (sourceStashSmallShove != null) break;
        }

        if (sourceStashSmallShove == null && partialSourceCandidate != null)
        {
            sourceStashSmallShove = partialSourceCandidate;
            transferColor = partialSourceColor;
        }

        if (sourceStashSmallShove == null || transferColor == ColorEnum.None) return;

        // 2. Tìm target small ở xe đầu:
        //    Ưu tiên 1: small đã gán đúng màu + đúng pack và còn chỗ.
        //    Ưu tiên 2: small trống trong shove cùng màu.
        //    Ưu tiên 3: small trống trong shove xám.
        if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            int maxShovesToCheck = Mathf.Min(1, ShoveContainer.Instance.shoveList.Count);
            ShoveMovement grayFallbackShove = null;
            SmallShove grayFallbackSmall = null;

            for (int i = 0; i < maxShovesToCheck; i++)
            {
                var mainShove = ShoveContainer.Instance.shoveList[i];
                Shove mainShoveComp = mainShove.GetComponent<Shove>();
                if (mainShoveComp == null) continue;

                SmallShove emptySmall = null;
                foreach (var ss in mainShove.GetComponentsInChildren<SmallShove>())
                {
                    // Case tốt nhất: small đã cùng màu + cùng pack và còn capacity.
                    if (ss.NumBallFull > 0 &&
                        ss.SlotColor == transferColor &&
                        ss.SlotPackRef == sourceStashSmallShove.SlotPackRef &&
                        (ss.CurrentBallCount + ss.PendingBallCount) < ss.NumBallFull)
                    {
                        targetMainShove = mainShove;
                        targetMainSmallShove = ss;
                        break;
                    }

                    if (ss.NumBallFull == 0 && ss.CurrentBallCount == 0 && ss.PendingBallCount == 0 && ss.SlotPackRef == null)
                    {
                        emptySmall = ss;
                        break;
                    }
                }

                if (targetMainSmallShove != null) break;

                if (emptySmall == null) continue;

                if (mainShoveComp.Color == transferColor)
                {
                    targetMainShove = mainShove;
                    targetMainSmallShove = emptySmall;
                    break;
                }

                if (mainShoveComp.Color == ColorEnum.None && grayFallbackShove == null)
                {
                    grayFallbackShove = mainShove;
                    grayFallbackSmall = emptySmall;
                }
            }

            if (targetMainShove == null && grayFallbackShove != null)
            {
                targetMainShove = grayFallbackShove;
                targetMainSmallShove = grayFallbackSmall;
                Shove grayComp = targetMainShove.GetComponent<Shove>();
                if (grayComp != null) grayComp.Color = transferColor;
            }
        }

        if (targetMainShove == null || targetMainSmallShove == null) return;

        isTransferringStash = true;
        currentTransferMainShove = targetMainShove;

        targetMainShove.TargetColor = transferColor;
        Shove mainTargetShoveComp = targetMainShove.GetComponent<Shove>();
        if (mainTargetShoveComp != null) mainTargetShoveComp.Color = transferColor;

        List<Ball> ballsInSource = sourceStashSmallShove.GetComponentsInChildren<Ball>().ToList();
        int transferLimit = ballsInSource.Count;

        bool isExistingCompatibleTarget =
            targetMainSmallShove.NumBallFull > 0 &&
            targetMainSmallShove.SlotColor == transferColor &&
            targetMainSmallShove.SlotPackRef == sourceStashSmallShove.SlotPackRef;

        if (isExistingCompatibleTarget)
        {
            int remainCapacity = targetMainSmallShove.NumBallFull - (targetMainSmallShove.CurrentBallCount + targetMainSmallShove.PendingBallCount);
            transferLimit = Mathf.Min(transferLimit, Mathf.Max(0, remainCapacity));
        }
        else
        {
            int totalBalls = ballsInSource.Count;
            targetMainSmallShove.NumBallFull = totalBalls;
            targetMainSmallShove.SlotColor = transferColor;
            targetMainSmallShove.SlotPackRef = sourceStashSmallShove.SlotPackRef;
            targetMainSmallShove.CurrentBallCount = 0;
            targetMainSmallShove.PendingBallCount = 0;
            if (totalBalls > targetMainSmallShove.ListPosBall.Count)
                targetMainSmallShove.IndexPosBall = targetMainSmallShove.ListPosBall.Count - totalBalls;
            else
                targetMainSmallShove.IndexPosBall = 0;
        }

        if (transferLimit <= 0) return;

        List<Ball> ballsToTransfer = ballsInSource.Take(transferLimit).ToList();

        foreach (var b in ballsToTransfer)
        {
            b.transform.SetParent(null, true);
        }

        List<UniTask> allTransferTasks = new List<UniTask>();
        const float stashTransferFlyDuration = 0.12f;
        foreach (var ball in ballsToTransfer)
        {
            if (ball == null) continue;

            if (targetMainSmallShove.IsFull) break;

            if (!targetMainSmallShove.TryLockForBall(ball.SourceColor, ball.SourcePack))
            {
                Debug.LogWarning($"[StashTransfer] Skip ball {ball.name}: target small lock fail (Color={ball.SourceColor}).");
                continue;
            }

            targetMainSmallShove.PendingBallCount++;
            allTransferTasks.Add(ball.TransferToShoveAsync(targetMainSmallShove, stashTransferFlyDuration));
        }

        await UniTask.WhenAll(allTransferTasks);

        int remainInSource = sourceStashSmallShove.GetComponentsInChildren<Ball>().Length;
        sourceStashSmallShove.CurrentBallCount = remainInSource;
        sourceStashSmallShove.PendingBallCount = 0;
        if (remainInSource <= 0)
        {
            sourceStashSmallShove.ResetShove();
        }

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

        const int requiredPacksPerColorPerShove = 3;

        int smallShovesPerShove = shovePrefab.GetComponentsInChildren<SmallShove>(true).Length;
        if (smallShovesPerShove < requiredPacksPerColorPerShove)
        {
            Debug.LogWarning($"Shove Prefab cần ít nhất {requiredPacksPerColorPerShove} SmallShove để map đúng luật 3 pack/màu -> 1 shove. Hiện tại: {smallShovesPerShove}.");
            return;
        }

        int theoreticalByTotalOnly = Mathf.CeilToInt((float)allPacks.Length / requiredPacksPerColorPerShove);
        Debug.Log($"[GenerateShovesFromPacks] Tong pack={allPacks.Length}, luat=3 pack cung mau/1 shove, neu bo qua mau thi can toi thieu {theoreticalByTotalOnly} shove.");

        // Validate level: mỗi màu phải chia hết cho 3. Sai thì dừng sinh shove và báo lỗi level.
        var colorGroups = allPacks.GroupBy(p => p.colorIndex).ToList();
        List<string> invalidColorMessages = new List<string>();
        foreach (var colorGroup in colorGroups)
        {
            int packCountForColor = colorGroup.Count();
            if (packCountForColor % requiredPacksPerColorPerShove != 0)
            {
                invalidColorMessages.Add($"{colorGroup.Key}={packCountForColor}");
            }
        }

        if (invalidColorMessages.Count > 0)
        {
            Debug.LogError($"<color=red>[LEVEL ERROR]</color> Mau khong chia het cho 3: {string.Join(", ", invalidColorMessages)}. Dung GenerateShovesFromPacks.");
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

        // 3. Gom pack theo màu. Vì mỗi pack giờ tương ứng với 1 SmallShove,
        // nên mỗi xe Shove sẽ chứa tối đa số pack bằng số SmallShove của prefab.
        List<ColorEnum> shoveColorsToSpawn = new List<ColorEnum>();
        foreach (var colorGroup in colorGroups)
        {
            int packCountForColor = colorGroup.Count();
            int shoveCountForColor = packCountForColor / requiredPacksPerColorPerShove;
            Debug.Log($"[GenerateShovesFromPacks] Mau {colorGroup.Key}: {packCountForColor} pack => {shoveCountForColor} shove.");

            for (int i = 0; i < shoveCountForColor; i++)
            {
                shoveColorsToSpawn.Add(colorGroup.Key);
            }
        }

        // 4. Random thứ tự sinh xe
        List<ColorEnum> randomizedShoveColors = shoveColorsToSpawn.OrderBy(_ => Random.value).ToList();

        // 5. Sinh xe Shove mới theo danh sách màu đã chia batch
        foreach (var shoveColor in randomizedShoveColors)
        {
            Transform parentToUse = shoveParent != null ? shoveParent : ShoveContainer.Instance.transform;
            GameObject newShoveObj = Instantiate(shovePrefab, parentToUse);
            newShoveObj.name = $"Shove_{shoveColor}";

            ShoveMovement shoveMove = newShoveObj.GetComponent<ShoveMovement>();
            if (shoveMove != null)
            {
                // Gán màu đích mà Shove này sẽ khóa cố định ngay từ lúc mới sinh ra
                shoveMove.TargetColor = shoveColor;
                
                Shove shoveComp = newShoveObj.GetComponent<Shove>();
                if (shoveComp != null) shoveComp.Color = shoveColor;

                shoveMove.ApplyMaterial();
                ShoveContainer.Instance.shoveList.Add(shoveMove);
            }
        }

        // 6. Cập nhật xếp hàng xe trên băng chuyền
        ShoveContainer.Instance.SetupShovePositions();

        Debug.Log($"<color=green>Thành công!</color> Đã dọn xe cũ và tự động sinh {randomizedShoveColors.Count} chiếc Shove mới từ {allPacks.Length} PackBalls (chuẩn 3 pack cùng màu/1 shove).");
    }
}