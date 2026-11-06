using UnityEngine;

/// <summary>
/// 木の「幹」の子オブジェクトに付ける(BoxCollider)。
/// 斧の当たり判定がこれに当たったら木こり成功。
/// 幹のBoxColliderは Is Trigger OFF でOK(プレイヤーが通り抜けないように)。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TreeTrunk : MonoBehaviour
{
    public ChoppableTree Tree { get; private set; }

    private void Awake()
    {
        Tree = GetComponentInParent<ChoppableTree>();
    }
}