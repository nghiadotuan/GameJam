using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SmallShove : MonoBehaviour
{
    public int NumBallFull { get; set; }
    public List<Transform> ListPosBall;
    public int IndexPosBall { get; set; }

    [Button]
    private void SetListPosBall()
    {
        ListPosBall = new List<Transform>();
        foreach (Transform trf in transform)
        {
            if (trf.name.Contains("Pos")) ListPosBall.Add(trf);
        }
    }

    private Vector3 GetPos()
    {
        if (IndexPosBall < 0)
        {
            IndexPosBall++;
            return ListPosBall[0].position;
        }
        if (IsOverPos) return transform.position;
        var pos = ListPosBall[IndexPosBall].position;
        IndexPosBall++;
        return pos;
    }

    public bool IsOverPos => IndexPosBall >= ListPosBall.Count;
}