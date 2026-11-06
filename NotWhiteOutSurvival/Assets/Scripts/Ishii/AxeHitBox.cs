using UnityEngine;

/// <summary>
/// 斧の当たり判定に付ける(BoxCollider / Is Trigger ON)。
/// 振っている間だけ有効になり、TreeTrunk に当たったら木に伝える。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class AxeHitbox : MonoBehaviour
{
    private PlayerAxe owner;
    private BoxCollider box;

    private void Awake()
    {
        owner = GetComponentInParent<PlayerAxe>();
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        // トリガー判定のために Rigidbody が必要(物理では動かさない)
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SetActive(false);
    }

    public void SetActive(bool active)
    {
        box.enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        var trunk = other.GetComponentInParent<TreeTrunk>();
        if (trunk == null || trunk.Tree == null) return;
        owner.OnAxeHit(trunk.Tree);
    }
}