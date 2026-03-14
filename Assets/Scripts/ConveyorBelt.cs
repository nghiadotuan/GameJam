using System;
using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public Transform StartShove;
    public Transform EndShove;

    public static ConveyorBelt Instance;

    private void Awake()
    {
        Instance = this;
    }
}