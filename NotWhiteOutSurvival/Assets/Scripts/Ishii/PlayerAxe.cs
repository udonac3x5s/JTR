using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Common;
/// <summary>
/// プレイヤーに付ける。木のエリアにいる間、斧を振り続ける。
/// アイテムの追加は PlayerInventory に任せる。
/// </summary>
public class PlayerAxe : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private AxeHitbox axeHitbox;
    [Tooltip("木の近くにいるときだけ表示する斧のモデル(任意)")]
    [SerializeField] private GameObject axeVisual;
    [SerializeField] private Animator animator;
    [SerializeField] private string swingTrigger = "Swing";

    [Header("振りのタイミング(秒)")]
    [Tooltip("1回振るのにかかる時間")]
    [SerializeField] private float swingDuration = 0.6f;
    [Tooltip("振り始めてから当たり判定が出るまで")]
    [SerializeField] private float hitStart = 0.25f;
    [Tooltip("当たり判定が出ている時間")]
    [SerializeField] private float hitLength = 0.15f;
    [Tooltip("振り終わってから次に振るまで")]
    [SerializeField] private float swingInterval = 0.2f;

    public PlayerInventory Inventory => inventory;
    public bool IsNearTree => nearTrees.Count > 0;

    private readonly HashSet<ChoppableTree> nearTrees = new HashSet<ChoppableTree>();
    private readonly HashSet<ChoppableTree> hitThisSwing = new HashSet<ChoppableTree>();
    private Coroutine swingRoutine;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (axeHitbox == null) axeHitbox = GetComponentInChildren<AxeHitbox>(true);
        if (axeVisual != null) axeVisual.SetActive(false);
    }

    // ---- ChoppableTree から呼ばれる ----
    public void EnterTree(ChoppableTree tree)
    {
        nearTrees.Add(tree);
        if (axeVisual != null) axeVisual.SetActive(true);
        if (swingRoutine == null) swingRoutine = StartCoroutine(SwingLoop());
    }

    public void ExitTree(ChoppableTree tree)
    {
        nearTrees.Remove(tree);
        // 振っている途中なら、その1振りは最後までやってから止まる
    }

    // ---- AxeHitbox から呼ばれる ----
    public void OnAxeHit(ChoppableTree tree)
    {
        if (tree.IsChopped) return;
        if (!hitThisSwing.Add(tree)) return; // 1振りで同じ木に1回だけ
        tree.Hit(this);
    }

    private IEnumerator SwingLoop()
    {
        while (IsNearTree)
        {
            hitThisSwing.Clear();
            if (animator != null) animator.SetTrigger(swingTrigger);

            yield return new WaitForSeconds(hitStart);
            if (axeHitbox != null) axeHitbox.SetActive(true);

            yield return new WaitForSeconds(hitLength);
            if (axeHitbox != null) axeHitbox.SetActive(false);

            float rest = Mathf.Max(0f, swingDuration - hitStart - hitLength);
            yield return new WaitForSeconds(rest + swingInterval);
        }

        if (axeVisual != null) axeVisual.SetActive(false);
        swingRoutine = null;
    }

    private void OnDisable()
    {
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        swingRoutine = null;
        if (axeHitbox != null) axeHitbox.SetActive(false);
        nearTrees.Clear();
    }
}