using UnityEngine;

/// <summary>
/// プレイヤーに付ける。周りに落ちているアイテムを吸い寄せて回収する。
/// 回収したアイテムは PlayerInventory に入れる。
/// </summary>
public class PlayerCollector : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;

    [Header("吸い寄せ")]
    [Tooltip("この範囲に入ったアイテムを吸い寄せる")]
    [SerializeField] private float magnetRadius = 2.5f;
    [Tooltip("アイテムのレイヤー")]
    [SerializeField] private LayerMask itemLayer = ~0;
    [Tooltip("アイテムが吸い込まれる位置(プレイヤーの足元からの高さ)")]
    [SerializeField] private float collectHeight = 0.8f;

    public Vector3 CollectPoint => transform.position + Vector3.up * collectHeight;

    private readonly Collider[] hits = new Collider[32];

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, magnetRadius, hits, itemLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            var item = hits[i].GetComponentInParent<ItemPickup>();
            if (item != null && item.CanAttract) item.StartAttract(this);
        }
    }

    /// <summary>ItemPickup から呼ばれる</summary>
    public void Collect(ItemPickup item)
    {
        if (inventory != null) inventory.AddItem(item.ItemType, item.Amount);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
#endif
}