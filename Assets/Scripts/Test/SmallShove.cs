using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SmallShove : MonoBehaviour
{
    public int NumBallFull { get; set; }
    public List<Transform> ListPosBall;
    public int IndexPosBall { get; set; }
    
    public int CurrentBallCount { get; private set; }
    public int PendingBallCount { get; set; }
    public System.Action<SmallShove> OnShoveFull;

    public bool IsFull => (CurrentBallCount + PendingBallCount) >= NumBallFull;

    [Button]
    private void SetListPosBall()
    {
        ListPosBall = new List<Transform>();
        foreach (Transform trf in transform)
        {
            if (trf.name.Contains("Pos")) ListPosBall.Add(trf);
        }
    }

    public Transform GetPosTransform()
    {
        if (IndexPosBall < 0)
        {
            IndexPosBall++;
            return ListPosBall[0];
        }
        if (IsOverPos) return transform;
        var posTrf = ListPosBall[IndexPosBall];
        IndexPosBall++;
        return posTrf;
    }

    public void ReceiveBall()
    {
        if (PendingBallCount > 0) PendingBallCount--;
        CurrentBallCount++;
        
        if (CurrentBallCount >= NumBallFull && NumBallFull > 0)
        {
            OnShoveFull?.Invoke(this);
        }
    }

    public bool IsOverPos => IndexPosBall >= ListPosBall.Count;
}