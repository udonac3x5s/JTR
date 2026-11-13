using System;
using System.Collections.Generic;
using UnityEngine;
using Common;

/// <summary>
/// プレイヤーに付ける。インベントリの個数に合わせて、背中にアイテムを積んで見せる。
/// 見た目だけなので当たり判定はなし(生成時にColliderなどを消す)。
/// </summary>
public class PlayerBackStack : MonoBehaviour
{
    [Serializable]
    public class StackEntry
    {
        public ItemType type = ItemType.Wood;
        [Tooltip("背中に積む見た目のPrefab(モデルだけでOK)")]
        public GameObject visualPrefab;
        [Tooltip("1個あたりの高さ")]
        public float itemHeight = 0.25f;
    }

    [Header("参照")]
    [SerializeField] private PlayerInventory inventory;
    [Tooltip("積み始める位置(プレイヤーの背中に置いた空オブジェクト)")]
    [SerializeField] private Transform stackRoot;

    [Header("積むアイテム(上から順に下に積まれる)")]
    [SerializeField] private List<StackEntry> entries = new List<StackEntry>();
    [Tooltip("見た目として積む最大数(インベントリの個数はこれを超えてもOK)")]
    [SerializeField] private int maxVisible = 30;

    [Header("動き")]
    [Tooltip("積んだアイテムが目標位置に追いつく速さ")]
    [SerializeField] private float followSpeed = 15f;
    [Tooltip("移動したときに上のほうが遅れて揺れる量")]
    [SerializeField] private float swayAmount = 0.04f;
    [SerializeField] private float swaySmooth = 6f;

    private class StackedItem
    {
        public ItemType type;
        public Transform tr;
        public float height;
    }

    private readonly List<StackedItem> stack = new List<StackedItem>();
    private Vector3 lastPos;
    private Vector3 smoothLocalVel;

    /// <summary>次に積まれる位置(ワールド)。回収したアイテムをここへ飛ばす</summary>
    public Vector3 NextSlotPosition
    {
        get
        {
            float h = 0f;
            foreach (var s in stack) h += s.height;
            return stackRoot.TransformPoint(Vector3.up * h);
        }
    }

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (stackRoot == null) stackRoot = transform;
        lastPos = transform.position;
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.OnItemChanged += OnItemChanged;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.OnItemChanged -= OnItemChanged;
    }

    private void Start()
    {
        foreach (var e in entries) OnItemChanged(e.type, inventory.GetCount(e.type));
    }

    private void OnItemChanged(ItemType type, int count)
    {
        StackEntry entry = entries.Find(e => e.type == type);
        if (entry == null || entry.visualPrefab == null) return;

        int current = CountOf(type);

        // 増えた分を一番上に積む
        while (current < count && stack.Count < maxVisible)
        {
            Vector3 spawnLocal = stackRoot.InverseTransformPoint(NextSlotPosition);
            stack.Add(new StackedItem { type = type, tr = CreateVisual(entry, spawnLocal), height = entry.itemHeight });
            current++;
        }

        // 減った分はその種類の一番上から消す(下の物は自動で詰める)
        for (int i = stack.Count - 1; i >= 0 && current > count; i--)
        {
            if (stack[i].type != type) continue;
            Destroy(stack[i].tr.gameObject);
            stack.RemoveAt(i);
            current--;
        }
    }

    private int CountOf(ItemType type)
    {
        int n = 0;
        foreach (var s in stack) if (s.type == type) n++;
        return n;
    }

    private Transform CreateVisual(StackEntry entry, Vector3 localPos)
    {
        GameObject go = Instantiate(entry.visualPrefab, stackRoot);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;

        // 当たり判定・物理・拾う処理を外して見た目だけにする
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Destroy(c); }
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var p in go.GetComponentsInChildren<ItemPickup>(true)) { p.enabled = false; Destroy(p); }

        return go.transform;
    }

    private void LateUpdate()
    {
        // プレイヤーのローカル速度(揺れ用)
        Vector3 vel = (transform.position - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = transform.position;
        Vector3 localVel = stackRoot.InverseTransformDirection(vel);
        localVel.y = 0f;
        smoothLocalVel = Vector3.Lerp(smoothLocalVel, localVel, swaySmooth * Time.deltaTime);

        float h = 0f;
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        foreach (var s in stack)
        {
            // 上にあるほど進行方向と逆に大きくずれる
            Vector3 target = Vector3.up * h - smoothLocalVel * (swayAmount * h);
            s.tr.localPosition = Vector3.Lerp(s.tr.localPosition, target, t);
            h += s.height;
        }
    }
}