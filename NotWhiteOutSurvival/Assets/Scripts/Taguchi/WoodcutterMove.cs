using UnityEngine;

/// <summary>
/// 木こりNPCを、まだ切られていない最寄りの木まで移動させる。
/// 伐採は同じNPCに付けた PlayerAxe に任せる。
/// </summary>
public class WoodcutterMove : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerAxe playerAxe;
    private ChoppableTree[] trees;

    [Header("移動設定")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stopDistance = 0.1f;
    [Tooltip("PlayerAxe が木の範囲内にいる場合、対象の木からこの距離以内なら移動を止めます。")]
    [SerializeField, Min(0f)] private float axeStopDistance = 2f;
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField] private float rotationOffsetY = 0f;

    [Header("動作確認用（実行中に表示）")]
    [SerializeField] private ChoppableTree currentTree;
    [SerializeField] private bool isMoving;

    private bool autoFindTrees;
    private float nextSearchTime;

    private void Awake()
    {
        if (playerAxe == null)
            playerAxe = GetComponent<PlayerAxe>();
    }

    private void Start()
    {
        if (playerAxe == null)
        {
            Debug.LogError("WoodcutterMove: 同じNPCに PlayerAxe を付けてください。", this);
            enabled = false;
            return;
        }

        autoFindTrees = trees == null || trees.Length == 0;
        if (autoFindTrees)
            SearchTrees();

        FindNextTree();
    }

    private void Update()
    {
        // 切り終えた木は対象から外し、次の木を探す。
        if (currentTree == null || currentTree.IsChopped || !currentTree.isActiveAndEnabled)
            FindNextTree();

        if (currentTree == null)
        {
            isMoving = false;

            // 木が復活・新規生成された場合も見つけられるよう定期的に確認する。
            if (autoFindTrees && Time.time >= nextSearchTime)
            {
                SearchTrees();
                FindNextTree();
                nextSearchTime = Time.time + 1f;
            }
            return;
        }

        // 地面と平行な方向だけ移動する（Y座標は維持）。
        Vector3 direction = currentTree.transform.position - transform.position;
        direction.y = 0f;
        float distance = direction.magnitude;

        // 正面を木に向ける。モデルが逆向きなら Rotation Offset Y を180にする。
        if (distance > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction)
                * Quaternion.Euler(0f, rotationOffsetY, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // PlayerAxe が伐採範囲に入ったら止まる（斧処理は PlayerAxe に任せる）。
        // 切った木の範囲判定が残っても、次の木が遠ければ移動を再開できる。
        if (distance <= stopDistance ||
            (playerAxe.IsNearTree && distance <= axeStopDistance))
        {
            isMoving = false;
            return;
        }

        isMoving = true;
        Vector3 destination = new Vector3(
            currentTree.transform.position.x,
            transform.position.y,
            currentTree.transform.position.z);

        transform.position = Vector3.MoveTowards(
            transform.position, destination, moveSpeed * Time.deltaTime);
    }

    private void SearchTrees()
    {
        trees = FindObjectsOfType<ChoppableTree>();
    }

    private void FindNextTree()
    {
        currentTree = null;
        float bestDistanceSqr = float.MaxValue;

        if (trees == null) return;

        foreach (ChoppableTree tree in trees)
        {
            if (tree == null || !tree.isActiveAndEnabled || tree.IsChopped)
                continue;

            Vector3 direction = tree.transform.position - transform.position;
            direction.y = 0f;
            float distanceSqr = direction.sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                currentTree = tree;
            }
        }
    }
}
