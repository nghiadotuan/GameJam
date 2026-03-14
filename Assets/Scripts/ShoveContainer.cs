using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ContainerState
{
    Stop,
    Move
}

public class ShoveContainer : MonoBehaviour
{
    public static ShoveContainer Instance;

    [Header("Danh sách Shove")] public List<ShoveMovement> shoveList = new List<ShoveMovement>();

    [Header("Trạng thái băng chuyền")] public ContainerState currentState = ContainerState.Stop;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        //SetupShovePositions();

        // Tự động cho băng chuyền chạy ngay khi game bắt đầu (nếu có Shove)
        //  if (shoveList.Count > 0) currentState = ContainerState.Move;
    }

    [ContextMenu("1. Setup Vị trí Xếp hàng")]
    [Button]
    public void SetupShovePositions()
    {
        if (shoveList == null || shoveList.Count == 0) return;
        if (ConveyorBelt.Instance == null || ConveyorBelt.Instance.StartShove == null)
        {
            ConveyorBelt.Instance = FindAnyObjectByType<ConveyorBelt>();
        }

        if (ConveyorBelt.Instance == null || ConveyorBelt.Instance.StartShove == null)
        {
            return;
        }
        float distance = GameConfig.Instance.distanceShove;
        Vector3 startPos = ConveyorBelt.Instance.StartShove.position;

        for (int i = 0; i < shoveList.Count; i++)
        {
            if (shoveList[i] == null) continue;

            if (i == 0)
            {
                shoveList[i].transform.position = startPos;
            }
            else
            {
                Vector3 prevPos = shoveList[i - 1].transform.position;
                shoveList[i].transform.position = new Vector3(prevPos.x - distance, prevPos.y, prevPos.z);
            }
        }
    }

    private void Update()
    {
        // Chỉ chạy khi State là Move và trong List còn Shove
        if (currentState != ContainerState.Move || shoveList.Count == 0) return;

        ShoveMovement headShove = shoveList[0];
        if (headShove == null) return;

        float speed = GameConfig.Instance.shoveSpeed;

        // Vị trí đích của Đầu tàu là x = 0 (giữ nguyên y, z)
        Vector3 headTarget = new Vector3(0f, headShove.transform.position.y, headShove.transform.position.z);

        // Tính khoảng cách Head sẽ đi trong frame này
        Vector3 currentHeadPos = headShove.transform.position;
        Vector3 nextHeadPos = Vector3.MoveTowards(currentHeadPos, headTarget, speed * Time.deltaTime);

        // CHÌA KHÓA: Tính ra độ dời (Delta) và áp dụng cho toàn bộ List. 
        // Đảm bảo cả cụm di chuyển y hệt nhau, không bao giờ sai lệch khoảng cách.
        Vector3 deltaMove = nextHeadPos - currentHeadPos;

        foreach (var shove in shoveList)
        {
            if (shove != null) shove.transform.position += deltaMove;
        }

        // Nếu Đầu tàu đã giẫm vạch x = 0 (tính trị tuyệt đối cho an toàn) -> Phanh lại!
        if (Mathf.Abs(headShove.transform.position.x) <= 0.001f)
        {
            currentState = ContainerState.Stop;
        }
    }

    [ContextMenu("2. Đẩy Shove đầu tiên ra (Move Out)")]
    public void TriggerMoveOutFrontShove()
    {
        if (shoveList.Count > 0)
        {
            // 1. Lấy thằng đầu tiên ra
            ShoveMovement frontShove = shoveList[0];

            // 2. Xóa nó khỏi List của Container
            shoveList.RemoveAt(0);

            // 3. Kích hoạt logic Move Out của nó
            frontShove.StartMoveOut();

            // 4. Cho băng chuyền chạy tiếp để thằng thứ 2 (giờ thành Index 0) lên điền vào chỗ trống
            if (shoveList.Count > 0)
            {
                currentState = ContainerState.Move;
            }
        }
    }
}