using System;
using System.Collections.Generic;
using UnityEngine;
using Common;

/// <summary>
/// NPCの移動：A→B（受取待ち）→C。単独テストでも、CustomerQueueManagerによる複数体管理でも使用可能。
/// </summary>
public class CustomerMoveTest : MonoBehaviour
{
    [Header("地点（単独テスト時のみ設定）")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform pointC;

    // 複数体で動くときはCustomerQueueManager側から経由地点が渡されます。
    // 元の単独テストと同様、Prefab側ではInspectorに表示しません。
    private Transform[] waypointsAToB;
    private Transform[] waypointsBToC;

    // 以前のPrefabに保存された値は保持する（単独配置したNPC用）。
    // Manager管理中はすべてManagerの値を優先する。
    [SerializeField, HideInInspector, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField, HideInInspector, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField, HideInInspector] private float rotationOffsetY = 0f;
    [SerializeField, HideInInspector] private bool returnToAAfterC = false;

    [SerializeField, HideInInspector, Min(1)] private int orderLevel = 1;

    // レベルごとに「注文候補」「注文される種類数」「各アイテムの個数」を設定する。
    [Serializable]
    public class OrderCandidate
    {
        [Tooltip("既存のCommon.ItemTypeを使用します")]
        public ItemType itemType = ItemType.Wood;

        [Min(1)] public int minCount = 1;
        [Min(1)] public int maxCount = 3;

        public OrderCandidate() { }

        public OrderCandidate(ItemType type, int min, int max)
        {
            itemType = type;
            minCount = min;
            maxCount = max;
        }
    }

    [Serializable]
    public class OrderLevelSettings
    {
        [Tooltip("1回の注文に含まれるアイテムの種類数（最小）")]
        [Min(1)] public int minKinds = 1;

        [Tooltip("1回の注文に含まれるアイテムの種類数（最大）")]
        [Min(1)] public int maxKinds = 1;

        [Tooltip("このレベルで注文される可能性があるアイテム")]
        public List<OrderCandidate> candidates = new List<OrderCandidate>();
    }

    // 単独配置のNPCのみが利用する旧注文設定。Manager管理中は参照しない。
    [SerializeField, HideInInspector]
    private List<OrderLevelSettings> orderLevels =
        new List<OrderLevelSettings>
        {
            new OrderLevelSettings
            {
                minKinds = 1,
                maxKinds = 1,
                candidates = new List<OrderCandidate>
                {
                    new OrderCandidate(ItemType.Wood, 1, 3)
                }
            },
        };

    [Serializable]
    public class OrderItem
    {
        public ItemType itemType;
        [Min(1)] public int requiredCount;
        [Min(0)] public int receivedCount;

        public int RemainingCount => Mathf.Max(0, requiredCount - receivedCount);

        public OrderItem(ItemType type, int required)
        {
            itemType = type;
            requiredCount = required;
            receivedCount = 0;
        }
    }

    [Header("今回の注文（再抽選で更新）")]
    [SerializeField] private List<OrderItem> currentOrder = new List<OrderItem>();

    [SerializeField] private string currentOrderSummary = "未生成";

    [Header("注文総数（旧CurrentRandomNumber互換）")]
    [SerializeField] private int currentRandomNumber;

    public int CurrentRandomNumber => currentRandomNumber;
    public int OrderLevel => managedByQueue && queueManager != null
        ? queueManager.OrderLevel : orderLevel;
    public IReadOnlyList<OrderItem> CurrentOrder => currentOrder;
    public string CurrentOrderSummary => currentOrderSummary;

    // 次にA地点から出発するときに適用する。
    public void SetOrderLevel(int level)
    {
        if (managedByQueue && queueManager != null)
            queueManager.SetOrderLevel(level); // Manager管理中は全NPC共通のレベル
        else
            orderLevel = Mathf.Max(1, level);
    }

    [Header("購入成功フラグ（テスト用）")]
    public bool hasReceivedItem = false;

    [Header("現在の動作（確認用）")]
    [SerializeField] private string currentState = "未開始";

    public float MoveSpeed => managedByQueue && queueManager != null
        ? queueManager.MoveSpeed : Mathf.Max(0.01f, moveSpeed);
    public float QueueProgress { get; private set; }
    public bool IsWaitingAtB => state == MoveState.WaitingAtB;

    private enum MoveState { ToB, WaitingAtB, ToC, Finished, WaitingToReturn }
    private MoveState state;
    private Transform[] currentWaypoints;
    private Transform destination;
    private int waypointIndex;
    private bool managedByQueue;
    private CustomerQueueManager queueManager;

    /// <summary>生成直後、CustomerQueueManagerから呼び出します。</summary>
    public void InitializeForQueue(CustomerQueueManager manager,
        Transform a, Transform b, Transform c,
        Transform[] routeAToB, Transform[] routeBToC)
    {
        queueManager = manager;
        managedByQueue = true;
        pointA = a;
        pointB = b;
        pointC = c;
        waypointsAToB = routeAToB;
        waypointsBToC = routeBToC;
        RestartAtA();
    }

    private void Start()
    {
        // Instantiate後にマネージャーが初期化していれば、単独テストの開始処理は不要。
        if (managedByQueue) return;

        if (pointA == null || pointB == null || pointC == null)
        {
            Debug.LogError("CustomerMoveTest: Point A / B / C を設定してください。", this);
            enabled = false;
            return;
        }
        RestartAtA();
    }

    private void Update()
    {
        if (managedByQueue)
        {
            // 列のA→B移動と受取判定はマネージャーが行う。
            // B→Cだけは各NPC自身が移動する。
            if (state == MoveState.ToC)
                MoveAlongRoute();
            return;
        }

        switch (state)
        {
            case MoveState.ToB:
            case MoveState.ToC:
                MoveAlongRoute();
                break;
            case MoveState.WaitingAtB:
                if (hasReceivedItem) BeginMoveToC();
                break;
        }
    }

    // レベルに応じて、複数種類・個数の注文を作り直す。
    private void GenerateOrder()
    {
        currentOrder.Clear();
        currentOrderSummary = "未設定";
        currentRandomNumber = 0;

        // 複数体ならManagerの注文設定、単独テストなら旧Prefab設定を利用。
        IReadOnlyList<OrderLevelSettings> activeLevels =
            managedByQueue && queueManager != null
                ? queueManager.OrderLevels : orderLevels;

        if (activeLevels == null || activeLevels.Count == 0)
        {
            Debug.LogWarning("注文レベルの設定がありません。", this);
            return;
        }

        int level = OrderLevel;
        int index = Mathf.Clamp(level - 1, 0, activeLevels.Count - 1);
        OrderLevelSettings settings = activeLevels[index];

        if (settings == null || settings.candidates == null)
        {
            Debug.LogWarning("このレベルの注文候補がありません。", this);
            return;
        }

        // 同じItemTypeは1回の注文に重複しない。
        var candidates = new List<OrderCandidate>();
        var usedTypes = new HashSet<ItemType>();

        foreach (OrderCandidate candidate in settings.candidates)
        {
            if (candidate == null)
                continue;

            if (usedTypes.Add(candidate.itemType))
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("有効な注文候補がありません。ManagerでItem Typeを設定してください。", this);
            return;
        }

        int minKinds = Mathf.Clamp(
            Mathf.Min(settings.minKinds, settings.maxKinds), 1, candidates.Count);
        int maxKinds = Mathf.Clamp(
            Mathf.Max(settings.minKinds, settings.maxKinds), 1, candidates.Count);
        int kindCount = UnityEngine.Random.Range(minKinds, maxKinds + 1);

        // 候補を重複なしでランダム選択する（Fisher-Yatesの部分シャッフル）。
        for (int i = 0; i < kindCount; i++)
        {
            int selectedIndex = UnityEngine.Random.Range(i, candidates.Count);
            OrderCandidate temp = candidates[i];
            candidates[i] = candidates[selectedIndex];
            candidates[selectedIndex] = temp;

            OrderCandidate selected = candidates[i];
            int min = Mathf.Max(1, Mathf.Min(selected.minCount, selected.maxCount));
            int max = Mathf.Max(1, Mathf.Max(selected.minCount, selected.maxCount));

            // Random.Range(int, int) の上限は含まれない。
            max = Mathf.Min(max, int.MaxValue - 1);
            min = Mathf.Min(min, max);

            int count = UnityEngine.Random.Range(min, max + 1);
            currentOrder.Add(new OrderItem(selected.itemType, count));
        }

        long total = 0;
        var descriptions = new List<string>();
        foreach (OrderItem item in currentOrder)
        {
            total += item.requiredCount;
            descriptions.Add(item.itemType + " × " + item.requiredCount);
        }

        currentRandomNumber = (int)Math.Min(total, int.MaxValue);
        currentOrderSummary = string.Join(" / ", descriptions);

        Debug.Log(
            $"注文生成：Lv.{level} / {currentOrderSummary}", this);
    }

    // アイテムを受け取る。実際に受け取れた個数を返す（在庫は渡す側で減らす）。
    public int TryDeliverItem(ItemType itemType, int count)
    {
        if (!IsWaitingAtB || hasReceivedItem || count <= 0)
            return 0;

        foreach (OrderItem item in currentOrder)
        {
            if (item.itemType != itemType)
                continue;

            int accepted = Mathf.Min(count, item.RemainingCount);
            if (accepted <= 0) return 0;

            item.receivedCount += accepted;

            // 全種類の必要数が揃ったら、既存のboolをONにしてCへ進ませる。
            bool completed = currentOrder.Count > 0;
            foreach (OrderItem entry in currentOrder)
            {
                if (entry.RemainingCount > 0)
                {
                    completed = false;
                    break;
                }
            }

            if (completed)
                hasReceivedItem = true;

            return accepted;
        }

        return 0;
    }

    private void RestartAtA()
    {
        transform.position = pointA.position;
        hasReceivedItem = false;
        QueueProgress = 0f;
        GenerateOrder();
        BeginMoveToB();
    }

    private void BeginMoveToB()
    {
        state = MoveState.ToB;
        currentState = "A → B に移動中";
        currentWaypoints = waypointsAToB;
        destination = pointB;
        waypointIndex = 0;
    }

    private void BeginMoveToC()
    {
        state = MoveState.ToC;
        currentState = "B → C に移動中";
        currentWaypoints = waypointsBToC;
        destination = pointC;
        waypointIndex = 0;
    }

    /// <summary>マネージャーが列の位置を決めて呼び出します。</summary>
    public void SetQueuePosition(Vector3 nextPosition, float routeProgress, bool reachedB)
    {
        if (!managedByQueue || state != MoveState.ToB) return;

        RotateToward(nextPosition - transform.position);
        transform.position = nextPosition;
        QueueProgress = routeProgress;

        if (reachedB)
        {
            state = MoveState.WaitingAtB;
            currentState = "B で受取待ち";
        }
    }

    /// <summary>列の先頭が受取完了したとき、マネージャーから呼ばれます。</summary>
    public void LeaveQueueForC()
    {
        if (managedByQueue && state == MoveState.WaitingAtB)
            BeginMoveToC();
    }

    /// <summary>CからAに戻るとき、空きができたらマネージャーから呼ばれます。</summary>
    public void ReturnToQueueAtA()
    {
        if (managedByQueue && state == MoveState.WaitingToReturn)
            RestartAtA();
    }

    private void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return;

        float offset = managedByQueue && queueManager != null
            ? queueManager.RotationOffsetY : rotationOffsetY;
        float turnSpeed = managedByQueue && queueManager != null
            ? queueManager.RotationSpeed : rotationSpeed;

        Quaternion targetRotation = Quaternion.LookRotation(direction)
            * Quaternion.Euler(0f, offset, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    // 単独テストのA→Bと、すべてのB→Cで使用。
    private void MoveAlongRoute()
    {
        Transform target = null;
        while (currentWaypoints != null && waypointIndex < currentWaypoints.Length)
        {
            if (currentWaypoints[waypointIndex] != null)
            {
                target = currentWaypoints[waypointIndex];
                break;
            }
            waypointIndex++;
        }
        if (target == null) target = destination;
        if (target == null) return;

        RotateToward(target.position - transform.position);
        transform.position = Vector3.MoveTowards(
            transform.position, target.position, MoveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) > 0.01f)
            return;

        if (target != destination)
        {
            waypointIndex++;
            return;
        }

        if (state == MoveState.ToB)
        {
            state = MoveState.WaitingAtB;
            currentState = "B で受取待ち";
        }
        else if (state == MoveState.ToC)
        {
            currentState = "C に到着";

            bool shouldReturn = managedByQueue
                ? queueManager != null && queueManager.ReturnCustomersToA
                : returnToAAfterC;

            if (shouldReturn)
            {
                if (managedByQueue)
                {
                    state = MoveState.WaitingToReturn;
                    currentState = "C でAの空きを待機中";
                    queueManager.RequestReturn(this);
                }
                else
                {
                    RestartAtA();
                }
            }
            else
            {
                state = MoveState.Finished;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pointA == null || pointB == null || pointC == null) return;
        DrawRoute(pointA, waypointsAToB, pointB, Color.cyan);
        DrawRoute(pointB, waypointsBToC, pointC, Color.yellow);
    }

    private static void DrawRoute(Transform start, Transform[] waypoints, Transform end, Color color)
    {
        Gizmos.color = color;
        Vector3 previous = start.position;
        if (waypoints != null)
        {
            foreach (Transform waypoint in waypoints)
            {
                if (waypoint == null) continue;
                Gizmos.DrawLine(previous, waypoint.position);
                Gizmos.DrawSphere(waypoint.position, 0.1f);
                previous = waypoint.position;
            }
        }
        Gizmos.DrawLine(previous, end.position);
        Gizmos.DrawSphere(end.position, 0.15f);
    }
}
