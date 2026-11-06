using System;
using System.Collections.Generic;
using UnityEngine;
using Common;

/// <summary>
/// プレイヤーの所持アイテム管理。
/// 木こり・狩りなど他のシステムからは AddItem / RemoveItem だけ呼ぶ。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    /// <summary>アイテム数が変わったとき (種類, 変化後の個数)。UI更新などに使う</summary>
    public event Action<ItemType, int> OnItemChanged;

    private readonly Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();

    public int GetCount(ItemType type)
    {
        return items.TryGetValue(type, out int count) ? count : 0;
    }

    public void AddItem(ItemType type, int amount)
    {
        if (amount <= 0) return;
        int count = GetCount(type) + amount;
        items[type] = count;
        OnItemChanged?.Invoke(type, count);
        Debug.Log($"{type} を {amount} 個入手 (所持: {count})");
    }

    /// <summary>足りなければ何もせず false</summary>
    public bool RemoveItem(ItemType type, int amount)
    {
        if (amount <= 0) return true;
        int count = GetCount(type);
        if (count < amount) return false;
        items[type] = count - amount;
        OnItemChanged?.Invoke(type, count - amount);
        return true;
    }
}