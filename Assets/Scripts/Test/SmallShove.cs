using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SmallShove : MonoBehaviour
{
    [ShowInInspector] public int NumBallFull { get; set; }
    [ShowInInspector] public ColorEnum SlotColor { get; set; } = ColorEnum.None;
    [ShowInInspector] public PackBalls SlotPackRef { get; set; }
    public List<Transform> ListPosBall;
    public int IndexPosBall { get; set; }

    [ShowInInspector] public int CurrentBallCount { get; set; }
    [ShowInInspector] public int PendingBallCount { get; set; }
    public System.Action<SmallShove> OnShoveFull;

    [ShowInInspector] public bool IsFull => NumBallFull > 0 && (CurrentBallCount + PendingBallCount) >= NumBallFull;

    public bool CanAcceptColor(ColorEnum color)
    {
        if (NumBallFull <= 0) return false;
        if (color == ColorEnum.None) return true;
        return SlotColor == ColorEnum.None || SlotColor == color;
    }

    public bool CanAcceptBall(ColorEnum color, PackBalls packRef)
    {
        if (!CanAcceptColor(color)) return false;

        if (packRef == null) return SlotPackRef == null;
        return SlotPackRef == null || SlotPackRef == packRef;
    }

    public bool TryLockForBall(ColorEnum color, PackBalls packRef)
    {
        if (!CanAcceptBall(color, packRef)) return false;

        if (color != ColorEnum.None && SlotColor == ColorEnum.None)
        {
            SlotColor = color;
        }

        if (packRef != null && SlotPackRef == null)
        {
            SlotPackRef = packRef;
        }

        return true;
    }

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

    public void ReceiveBall(Ball incomingBall = null)
    {
        ColorEnum incomingColor = incomingBall != null ? incomingBall.SourceColor : ColorEnum.None;
        PackBalls incomingPack = incomingBall != null ? incomingBall.SourcePack : null;

        if (SlotPackRef != null && incomingPack == null)
        {
            if (PendingBallCount > 0) PendingBallCount--;
            Debug.LogError($"[SmallShove] Reject ball without pack ref. Slot={name}, LockedPack={SlotPackRef.name}");
            return;
        }

        if (SlotColor != ColorEnum.None && incomingColor == ColorEnum.None)
        {
            if (PendingBallCount > 0) PendingBallCount--;
            Debug.LogError($"[SmallShove] Reject ball without color. Slot={name}, LockedColor={SlotColor}");
            return;
        }

        if (incomingPack != null)
        {
            if (SlotPackRef == null)
            {
                SlotPackRef = incomingPack;
            }
            else if (SlotPackRef != incomingPack)
            {
                if (PendingBallCount > 0) PendingBallCount--;
                Debug.LogError($"[SmallShove] Reject mixed pack ref. Slot={name}, IncomingPack={incomingPack.name}");
                return;
            }
        }

        if (incomingColor != ColorEnum.None)
        {
            if (SlotColor == ColorEnum.None)
            {
                SlotColor = incomingColor;
            }
            else if (SlotColor != incomingColor)
            {
                if (PendingBallCount > 0) PendingBallCount--;
                Debug.LogError($"[SmallShove] Reject mixed color. Slot={name}, SlotColor={SlotColor}, Incoming={incomingColor}");
                return;
            }
        }

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
        SlotPackRef = null;
        CurrentBallCount = 0;
        PendingBallCount = 0;
        IndexPosBall = 0;
        NumBallFull = 0;
    }
}