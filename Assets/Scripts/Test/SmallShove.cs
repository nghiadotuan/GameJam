using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SmallShove : MonoBehaviour
{
    [ShowInInspector] public int NumBallFull { get; set; }
    [ShowInInspector] public ColorEnum SlotColor { get; set; } = ColorEnum.None;
    public List<Transform> ListPosBall;
    public int IndexPosBall { get; set; }

    [ShowInInspector] public int CurrentBallCount { get; set; }
    [ShowInInspector] public int PendingBallCount { get; set; }
    public System.Action<SmallShove> OnShoveFull;

    [ShowInInspector] public bool IsFull => (CurrentBallCount + PendingBallCount) >= NumBallFull;

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

        if (IsOverPos) return ListPosBall[^1];
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

    public void ResetShove()
    {
        SlotColor = ColorEnum.None;
        CurrentBallCount = 0;
        PendingBallCount = 0;
        IndexPosBall = 0;
        NumBallFull = 0;
    }
}