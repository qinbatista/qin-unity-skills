using System.Collections.Generic;
using UnityEngine;

public sealed class PlainMoney : ScriptableObject
{
    public int money;
    public int gems;
    public List<int> claimedRewards = new List<int>();
}
