using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shove : MonoBehaviour
{
    [SerializeField] private ColorEnum _color;

    public int NumBallFull { get; set; }

    public ColorEnum Color
    {
        get => _color;
        set => _color = value;
    }

    public List<SmallShove> ListSmallShove;


}