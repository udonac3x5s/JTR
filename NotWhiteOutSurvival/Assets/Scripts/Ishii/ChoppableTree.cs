using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Common;

/// <summary>
/// 木のPrefabのルートに付ける。
/// 子に TreeArea(エリア) と TreeTrunk(幹) を置く。
/// </summary>
public class ChoppableTree : MonoBehaviour
{
    [Header("木こり")]
    [Tooltip("何回当てたら切り倒せるか")]
    [SerializeField] private int hitsToChop = 1;

    [Header("報酬")]
    [SerializeField] private ItemType rewardItem = ItemType.Wood;
    [SerializeField] private int rewardAmount = 3;

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

        if (hp <= 0) Chop(by);
    }

    private void Chop(PlayerAxe by)
    {
        IsChopped = true;

        if (by != null && by.Inventory != null)
            by.Inventory.AddItem(rewardItem, rewardAmount);

        // コライダーを無効にすると OnTriggerExit が来ないので自分で抜けさせる
        foreach (var p in playersInArea) p.ExitTree(this);
        playersInArea.Clear();

        SetActiveState(false);
        onChopped?.Invoke();

        if (respawnTime > 0f) StartCoroutine(RespawnRoutine());
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
}