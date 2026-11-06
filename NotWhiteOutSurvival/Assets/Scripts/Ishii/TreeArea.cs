using UnityEngine;

/// <summary>
/// 木の「エリア」の子オブジェクトに付ける(BoxCollider / Is Trigger ON)。
/// プレイヤーが入ったら斧を振り始める。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TreeArea : MonoBehaviour
{
    private ChoppableTree tree;

    private void Awake()
    {
        tree = GetComponentInParent<ChoppableTree>();
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return; // 斧の当たり判定などは無視
        var player = other.GetComponentInParent<PlayerAxe>();
        if (player != null && tree != null) tree.OnPlayerEnter(player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;
        var player = other.GetComponentInParent<PlayerAxe>();
        if (player != null && tree != null) tree.OnPlayerExit(player);
    }
}