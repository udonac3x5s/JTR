using System;
using System.Collections.Generic;
using UnityEngine;
using Common;

/// <summary>
/// プレイヤーに付ける。インベントリの個数に合わせて、背中にアイテムを積んで見せる。
/// アイテムの種類ごとに「列」を作り、列を横に並べる。
///   1種類 → 真後ろ / 2種類 → 左右に分かれて、2つの中央が真後ろ(3種類以上も均等に並ぶ)
/// 先に持ったものほど左に並ぶ。持っていた種類が0個になると列は消えて、残りが中央に寄る。
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
    [Tooltip("積み始める位置(プレイヤーの背中に置いた空オブジェクト)。この位置の左右に列が並ぶ")]
    [SerializeField] private Transform stackRoot;

    [Header("積むアイテム")]
    [SerializeField] private List<StackEntry> entries = new List<StackEntry>();
    [Tooltip("1種類あたりの、見た目として積む最大数(インベントリの個数はこれを超えてもOK)")]
    [SerializeField] private int maxVisible = 30;

    [Header("並び")]
    [Tooltip("列と列の横の間隔。アイテムの幅より少し大きめにする")]
    [SerializeField] private float columnSpacing = 0.5f;

    [Header("動き")]
    [Tooltip("積んだアイテムが目標位置に追いつく速さ(列が左右に動く速さも同じ)")]
    [SerializeField] private float followSpeed = 15f;
    [Tooltip("移動したときに上のほうが遅れて揺れる量")]
    [SerializeField] private float swayAmount = 0.04f;
    [SerializeField] private float swaySmooth = 6f;

    private class StackedItem
    {
        public Transform tr;
        public float height;
    }

    /// <summary>1種類ぶんの列</summary>
    private class Column
    {
        public ItemType type;
        public readonly List<StackedItem> items = new List<StackedItem>();
    }

    // 左から順に並ぶ(先に持った種類が左)
    private readonly List<Column> columns = new List<Column>();
    private Vector3 lastPos;
    private Vector3 smoothLocalVel;

    /// <summary>
    /// 指定した種類のアイテムが次に積まれる位置(ワールド)。回収したアイテムをここへ飛ばす。
    /// まだ列がない種類は、新しく右端に列ができる前提の位置を返す。
    /// </summary>
    public Vector3 GetNextSlotPosition(ItemType type)
    {
        int index = columns.FindIndex(c => c.type == type);
        int total = columns.Count;
        float h = 0f;

        if (index < 0)
        {
            index = total; // 新しい列は右端にできる
            total++;
        }
        else
        {
            h = ColumnHeight(columns[index]);
        }

        return stackRoot.TransformPoint(new Vector3(ColumnX(index, total), h, 0f));
    }

    /// <summary>種類を指定しない古い呼び出し用。entries の先頭の種類の位置を返す</summary>
    public Vector3 NextSlotPosition
    {
        get
        {
            ItemType type = entries.Count > 0 ? entries[0].type : ItemType.Wood;
            return GetNextSlotPosition(type);
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

        Column col = columns.Find(c => c.type == type);
        int current = col != null ? col.items.Count : 0;

        // 増えた分をその種類の列の一番上に積む
        while (current < count && current < maxVisible)
        {
            if (col == null)
            {
                // 初めて持った種類は、右端に新しい列を作る(すでにある列は左へ寄る)
                col = new Column { type = type };
                columns.Add(col);
            }

            Vector3 spawnLocal = new Vector3(
                ColumnX(columns.IndexOf(col), columns.Count), ColumnHeight(col), 0f);
            col.items.Add(new StackedItem { tr = CreateVisual(entry, spawnLocal), height = entry.itemHeight });
            current++;
        }

        // 減った分はその列の一番上から消す
        while (col != null && col.items.Count > count && col.items.Count > 0)
        {
            int last = col.items.Count - 1;
            Destroy(col.items[last].tr.gameObject);
            col.items.RemoveAt(last);
        }

        // 空になった列は消す(残りの列が中央に寄る)
        if (col != null && col.items.Count == 0) columns.Remove(col);
    }

    /// <summary>列の左右位置(stackRootのローカルX)。列全体の中央が stackRoot の真後ろになる</summary>
    private float ColumnX(int index, int total)
    {
        return (index - (total - 1) * 0.5f) * columnSpacing;
    }

    private static float ColumnHeight(Column col)
    {
        float h = 0f;
        foreach (var s in col.items) h += s.height;
        return h;
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

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        for (int ci = 0; ci < columns.Count; ci++)
        {
            float x = ColumnX(ci, columns.Count);
            float h = 0f;
            foreach (var s in columns[ci].items)
            {
                // 上にあるほど進行方向と逆に大きくずれる
                Vector3 target = new Vector3(x, h, 0f) - smoothLocalVel * (swayAmount * h);
                s.tr.localPosition = Vector3.Lerp(s.tr.localPosition, target, t);
                h += s.height;
            }
        }
    }
}