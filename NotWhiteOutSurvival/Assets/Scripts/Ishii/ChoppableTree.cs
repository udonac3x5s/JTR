using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// 木のPrefabのルートに付ける。
/// 子に TreeArea(エリア) と TreeTrunk(幹) を置く。
/// 斧が当たるたびにアイテムを周りにばらまき、hitsToChop 回当たったら切り倒す。
/// </summary>
public class ChoppableTree : MonoBehaviour
{
    [Header("木こり")]
    [Tooltip("何回当てたら切り倒せるか")]
    [SerializeField] private int hitsToChop = 3;

    [Header("ドロップ")]
    [Tooltip("落とすアイテムのPrefab(薪など)")]
    [SerializeField] private ItemPickup dropPrefab;
    [Tooltip("斧が当たるたびに落とす個数")]
    [FormerlySerializedAs("dropCount")]
    [SerializeField] private int dropCountPerHit = 1;
    [Tooltip("切り倒した最後の一撃で、さらに追加で落とす個数(0なら追加なし)")]
    [SerializeField] private int dropCountOnChop = 0;
    [Tooltip("アイテムが飛び出す高さ(木の根元から)")]
    [SerializeField] private float dropOriginHeight = 1f;
    [SerializeField] private float scatterMinRadius = 1f;
    [SerializeField] private float scatterMaxRadius = 2f;

    [Header("切り倒した後")]
    [Tooltip("0以下なら復活しない")]
    [SerializeField] private float respawnTime = 10f;
    [Tooltip("切り倒したときに隠す見た目(木のモデル)")]
    [SerializeField] private GameObject visual;
    [Tooltip("切り倒したときに無効にするコライダー(エリア・幹)。空なら子から自動取得")]
    [SerializeField] private Collider[] treeColliders;

    [Header("演出用イベント")]
    public UnityEvent onHit;
    public UnityEvent onChopped;
    public UnityEvent onRespawn;

    public bool IsChopped { get; private set; }

    private int hp;
    private readonly HashSet<PlayerAxe> playersInArea = new HashSet<PlayerAxe>();

    public int CurrentHP => Mathf.Max(0, hp);
    public int MaxHP => Mathf.Max(1, hitsToChop);

    private void Awake()
    {
        hp = hitsToChop;
        if (treeColliders == null || treeColliders.Length == 0)
            treeColliders = GetComponentsInChildren<Collider>(true);
    }

    // ---- TreeArea から呼ばれる ----
    public void OnPlayerEnter(PlayerAxe player)
    {
        if (IsChopped) return;
        if (playersInArea.Add(player)) player.EnterTree(this);
    }

    public void OnPlayerExit(PlayerAxe player)
    {
        if (playersInArea.Remove(player)) player.ExitTree(this);
    }

    // ---- AxeHitbox から呼ばれる ----
    public void Hit(PlayerAxe by)
    {
        if (IsChopped) return;

        hp--;
        onHit?.Invoke();

        // 攻撃が当たるたびにアイテムを散らす
        DropItems(dropCountPerHit);

        if (hp <= 0) Chop();
    }

    private void Chop()
    {
        IsChopped = true;

        // 最後の一撃だけの追加ドロップ(既定は0)
        DropItems(dropCountOnChop);

        // コライダーを無効にすると OnTriggerExit が来ないので自分で抜けさせる
        foreach (var p in playersInArea) p.ExitTree(this);
        playersInArea.Clear();

        SetActiveState(false);
        onChopped?.Invoke();

        if (respawnTime > 0f) StartCoroutine(RespawnRoutine());
    }

    private void DropItems(int count)
    {
        if (count <= 0) return;

        Vector3 ground = transform.position;
        ItemDropper.Drop(dropPrefab, count,
            ground + Vector3.up * dropOriginHeight, ground,
            scatterMinRadius, scatterMaxRadius);
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        hp = hitsToChop;
        IsChopped = false;
        SetActiveState(true);
        onRespawn?.Invoke();
    }

    private void SetActiveState(bool active)
    {
        if (visual != null) visual.SetActive(active);
        foreach (var c in treeColliders)
            if (c != null) c.enabled = active;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, scatterMinRadius);
        Gizmos.DrawWireSphere(transform.position, scatterMaxRadius);
    }
#endif
}