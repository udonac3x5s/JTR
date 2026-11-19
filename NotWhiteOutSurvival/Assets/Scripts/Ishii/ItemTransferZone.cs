using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Common;

/// <summary>
/// 指定エリアに入った PlayerInventory 持ちから、指定アイテムを1個ずつ受け取って積む。
/// 内部の個数は「飛び始めた瞬間」に相手から1個減り、見た目のアイテムが積み位置へ飛ぶ。
/// 到着したらその場に積まれて残り、ReceivedCount が増えてイベントが発火する。
/// ThrowItems で、このゾーンが持っているアイテムを任意の場所へ飛ばすこともできる。
/// </summary>
public class ItemTransferZone : MonoBehaviour
{
    [Header("対象")]
    [SerializeField] private ItemType itemType = ItemType.Wood;
    [Tooltip("エリア判定(Is Trigger ON推奨)")]
    [SerializeField] private BoxCollider area;
    [Tooltip("PlayerInventory を持つキャラのレイヤー")]
    [SerializeField] private LayerMask holderLayer = ~0;

    [Header("移動")]
    [Tooltip("1個が飛んでいる時間(秒)")]
    [SerializeField] private float moveDuration = 0.4f;
    [Tooltip("次の1個を飛ばすまでの間隔(秒)")]
    [SerializeField] private float interval = 0.1f;
    [SerializeField] private float arcHeight = 1f;

    [Header("見た目")]
    [Tooltip("飛ぶ・積まれるアイテムのPrefab(薪のモデルなど)")]
    [SerializeField] private GameObject visualPrefab;

    [Header("積む場所(目的地)")]
    [Tooltip("積み始める位置。空なら自分自身")]
    [SerializeField] private Transform pileRoot;
    [SerializeField] private int columns = 3;
    [SerializeField] private int rows = 2;
    [Tooltip("x: 横の間隔 / y: 1段の高さ / z: 奥行きの間隔")]
    [SerializeField] private Vector3 spacing = new Vector3(0.4f, 0.25f, 0.4f);
    [Tooltip("見た目として積む最大数(個数自体はこれを超えて増える)")]
    [SerializeField] private int maxVisible = 60;

    [Header("ThrowItems")]
    [Tooltip("ONなら着地した見た目をその場に残す(OFFなら消す)")]
    [SerializeField] private bool keepThrownOnLanding = true;

    [Header("イベント")]
    [Tooltip("1個到着するたびに呼ばれる(到着後の合計個数)")]
    public UnityEvent<int> onArrived;

    /// <summary>1個到着するたびに呼ばれる(到着後の合計個数)</summary>
    public event Action<int> OnArrived;

    /// <summary>個数が変わったとき(到着・ThrowItemsで減った後の個数)</summary>
    public event Action<int> OnCountChanged;

    /// <summary>ThrowItemsで1個着地するたびに呼ばれる(種類, 着地した見た目)。見た目を残さない設定でもここで受け取れる</summary>
    public event Action<ItemType, Transform> OnThrowArrived;

    /// <summary>このゾーンが今持っている個数</summary>
    public int ReceivedCount { get; private set; }

    private class Flyer
    {
        public Transform tr;
        public Vector3 from;
        public float t;
    }

    private readonly List<Flyer> flying = new List<Flyer>();
    private readonly List<Transform> pile = new List<Transform>();
    private readonly List<Transform> thrownPile = new List<Transform>();
    private readonly Dictionary<PlayerInventory, float> timers = new Dictionary<PlayerInventory, float>();
    private readonly HashSet<PlayerInventory> inside = new HashSet<PlayerInventory>();
    private readonly List<PlayerInventory> toRemove = new List<PlayerInventory>();
    private readonly Collider[] hits = new Collider[16];

    private void Awake()
    {
        if (area != null) area.isTrigger = true;
        if (pileRoot == null) pileRoot = transform;
    }

    private void Update()
    {
        UpdateZone();
        UpdateFlying();
    }

    // ================= エリアに入ったキャラから受け取る =================

    private void UpdateZone()
    {
        if (area == null) return;

        Vector3 center = area.transform.TransformPoint(area.center);
        Vector3 half = Vector3.Scale(area.size, area.transform.lossyScale) * 0.5f;
        int n = Physics.OverlapBoxNonAlloc(center, half, hits, area.transform.rotation,
                                           holderLayer, QueryTriggerInteraction.Ignore);

        inside.Clear();
        for (int i = 0; i < n; i++)
        {
            var inv = hits[i].GetComponentInParent<PlayerInventory>();
            if (inv == null || !inside.Add(inv)) continue;

            timers.TryGetValue(inv, out float timer);
            timer -= Time.deltaTime;

            if (timer <= 0f && inv.GetCount(itemType) > 0)
            {
                SendOne(inv);
                timer = interval;
            }
            timers[inv] = timer;
        }

        // エリアから出たキャラのタイマーを消す
        toRemove.Clear();
        foreach (var k in timers.Keys) if (!inside.Contains(k)) toRemove.Add(k);
        foreach (var k in toRemove) timers.Remove(k);
    }

    private void SendOne(PlayerInventory inv)
    {
        if (!inv.RemoveItem(itemType, 1)) return; // 相手の個数を1個減らす

        // 背中に積んでいれば、その一番上の位置から飛ばす
        var back = inv.GetComponent<PlayerBackStack>();
        Vector3 from = back != null ? back.NextSlotPosition : inv.transform.position + Vector3.up;

        if (visualPrefab == null) { Arrive(); return; }

        var go = Instantiate(visualPrefab, from, Quaternion.identity);
        MakeVisualOnly(go);
        flying.Add(new Flyer { tr = go.transform, from = from, t = 0f });
    }

    private void UpdateFlying()
    {
        // 古い順に着地するので、先頭の個体から順に積み位置が割り当てられる
        for (int i = flying.Count - 1; i >= 0; i--)
        {
            var f = flying[i];
            f.t += Time.deltaTime / Mathf.Max(moveDuration, 0.01f);

            // 「今積まれている数 + 飛んでいる順番」の位置が着地先
            Vector3 to = SlotPosition(pile.Count + i);
            float t = Mathf.Clamp01(f.t);
            Vector3 p = Vector3.Lerp(f.from, to, t);
            p.y += 4f * arcHeight * t * (1f - t);
            f.tr.position = p;
            f.tr.rotation = Quaternion.Slerp(f.tr.rotation, pileRoot.rotation, t);

            if (f.t >= 1f)
            {
                flying.RemoveAt(i);
                LandOnPile(f.tr);
                Arrive();
            }
        }
    }

    /// <summary>飛んできた見た目をそのまま積みの一員にする</summary>
    private void LandOnPile(Transform tr)
    {
        if (pile.Count >= maxVisible)
        {
            Destroy(tr.gameObject);
            return;
        }

        tr.SetParent(pileRoot, true);
        tr.position = SlotPosition(pile.Count);
        tr.rotation = pileRoot.rotation;
        pile.Add(tr);
    }

    private void Arrive()
    {
        ReceivedCount++;
        OnCountChanged?.Invoke(ReceivedCount);
        OnArrived?.Invoke(ReceivedCount);
        onArrived?.Invoke(ReceivedCount);
    }

    /// <summary>積んだ見た目を全部消す(個数 ReceivedCount は変えない)。ThrowItemsで残した分も消す</summary>
    public void ClearPile()
    {
        foreach (var t in pile) if (t != null) Destroy(t.gameObject);
        pile.Clear();
        foreach (var t in thrownPile) if (t != null) Destroy(t.gameObject);
        thrownPile.Clear();
    }

    // ================= 積み位置 =================

    private Vector3 SlotPosition(int index)
    {
        return pileRoot.TransformPoint(SlotLocal(index));
    }

    // 任意の中心位置を基準にした積み位置(向きは pileRoot に合わせる)
    private Vector3 SlotPositionAt(Vector3 origin, int index)
    {
        return origin + pileRoot.rotation * SlotLocal(index);
    }

    private Vector3 SlotLocal(int index)
    {
        int perLayer = Mathf.Max(1, columns * rows);
        int layer = index / perLayer;
        int inLayer = index % perLayer;
        int x = inLayer % columns;
        int z = inLayer / columns;

        return new Vector3(
            (x - (columns - 1) * 0.5f) * spacing.x,
            layer * spacing.y,
            (z - (rows - 1) * 0.5f) * spacing.z);
    }

    private static void MakeVisualOnly(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Destroy(c); }
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var p in go.GetComponentsInChildren<ItemPickup>(true)) { p.enabled = false; Destroy(p); }
    }

    // ================= ThrowItems =================

    /// <summary>
    /// このゾーンが持っているアイテムを、指定個、指定した場所に飛ばす。
    /// 持っている数より多く指定した場合は、持っている分だけ飛ばす。
    /// </summary>
    /// <param name="itemNow">飛ばす先にすでにある個数(積み位置の開始番号。itemNow + 0, 1, 2... に着地)</param>
    /// <param name="count">飛ばす個数</param>
    /// <param name="targetPosition">飛ばす先(積み始めの中心位置)</param>
    /// <param name="item2">飛ばすアイテムの種類(このゾーンの itemType と同じであること)</param>
    public void ThrowItems(int itemNow, int count, Vector3 targetPosition, ItemType item2)
    {
        if (count <= 0) return;

        if (item2 != itemType)
        {
            Debug.LogWarning($"ThrowItems: このゾーンは {itemType} を扱うため、{item2} は飛ばせません。", this);
            return;
        }

        int n = Mathf.Min(count, ReceivedCount);
        if (n <= 0) return;

        StartCoroutine(ThrowRoutine(itemNow, n, targetPosition, item2));
    }

    private IEnumerator ThrowRoutine(int itemNow, int n, Vector3 target, ItemType type)
    {
        for (int i = 0; i < n; i++)
        {
            if (ReceivedCount <= 0) yield break;

            // 内部の個数を1個減らす
            ReceivedCount--;
            OnCountChanged?.Invoke(ReceivedCount);

            // 積んである見た目の一番上を取り外して、そのまま飛ばす
            Transform tr = null;
            if (pile.Count > 0)
            {
                int last = pile.Count - 1;
                tr = pile[last];
                pile.RemoveAt(last);
                if (tr != null) tr.SetParent(null, true);
            }
            else if (visualPrefab != null)
            {
                // 見た目が上限(maxVisible)を超えていた分は、山の位置から新しく出す
                var go = Instantiate(visualPrefab, pileRoot.position, pileRoot.rotation);
                MakeVisualOnly(go);
                tr = go.transform;
            }

            Vector3 to = SlotPositionAt(target, itemNow + i);

            if (tr != null) StartCoroutine(FlyRoutine(tr, to, type));
            else OnThrowArrived?.Invoke(type, null);

            if (i < n - 1) yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator FlyRoutine(Transform tr, Vector3 to, ItemType type)
    {
        Vector3 from = tr.position;
        Quaternion startRot = tr.rotation;

        float t = 0f;
        while (t < 1f)
        {
            if (tr == null) yield break;
            t += Time.deltaTime / Mathf.Max(moveDuration, 0.01f);
            float k = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += 4f * arcHeight * k * (1f - k);
            tr.position = p;
            tr.rotation = Quaternion.Slerp(startRot, pileRoot.rotation, k);
            yield return null;
        }

        tr.position = to;
        tr.rotation = pileRoot.rotation;
        OnThrowArrived?.Invoke(type, tr);

        if (keepThrownOnLanding) thrownPile.Add(tr);
        else Destroy(tr.gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Transform root = pileRoot != null ? pileRoot : transform;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
        int perLayer = Mathf.Max(1, columns * rows);
        for (int i = 0; i < perLayer; i++)
        {
            int x = i % columns, z = i / columns;
            Vector3 local = new Vector3(
                (x - (columns - 1) * 0.5f) * spacing.x, 0f, (z - (rows - 1) * 0.5f) * spacing.z);
            Gizmos.DrawWireCube(root.TransformPoint(local), new Vector3(spacing.x * 0.9f, spacing.y, spacing.z * 0.9f));
        }
    }
#endif
}