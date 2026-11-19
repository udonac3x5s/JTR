using UnityEngine;
using Common;

public class TESTMOVEITEM : MonoBehaviour
{

    public ItemType item;

    public int nowItem;

    public GameObject targetPosition;

    public ItemTransferZone itemTransferZone;

    [ContextMenu("Throw Items")]
    public void ThrowItems()
    {
        itemTransferZone.ThrowItems(nowItem, 2, targetPosition.transform.position, item);
    }



}
