using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ShoveContainer : MonoBehaviour
{
    [Header("Danh sách Shove")]
    public List<ShoveMovement> shoveList = new List<ShoveMovement>();

    private void Start()
    {
        // Tự động xếp hàng khi game bắt đầu
       // SetupShovePositions();
    }

    [Button][ContextMenu("Setup Vị trí Xếp hàng (Test Editor)")]
    public void SetupShovePositions()
    {
        if (shoveList == null || shoveList.Count == 0)
        {
            Debug.LogWarning("Danh sách Shove đang trống!");
            return;
        }

        if (ConveyorBelt.Instance == null || ConveyorBelt.Instance.StartShove == null)
        {
            Debug.LogWarning("Chưa có ConveyorBelt hoặc chưa gán StartShove!");
            return;
        }

        // Lấy khoảng cách từ GameConfig
        float distance = GameConfig.Instance.distanceShove;
        Vector3 startPos = ConveyorBelt.Instance.StartShove.position;

        for (int i = 0; i < shoveList.Count; i++)
        {
            if (shoveList[i] == null) continue;

            if (i == 0)
            {
                // Index 0: Đặt chễm chệ ngay tại vạch Start của ConveyorBelt
                shoveList[i].transform.position = startPos;
            }
            else
            {
                // Index > 0: Lấy tọa độ thằng đằng trước, lùi X đi một khoảng distance, Y và Z giữ nguyên
                Vector3 prevPos = shoveList[i - 1].transform.position;
                
                shoveList[i].transform.position = new Vector3(
                    prevPos.x - distance, 
                    prevPos.y, 
                    prevPos.z
                );
            }
        }

        Debug.Log($"Đã xếp hàng xong cho {shoveList.Count} cục Shove!");
    }
}