using UnityEngine;

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

    [Header("移動設定")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField] private float rotationOffsetY = 0f;
    [SerializeField] private bool returnToAAfterC = false;

    [Header("乱数設定（レベルは1から。配列の0番がレベル1）")]
    [SerializeField, Min(1)] private int randomLevel = 1;

    [System.Serializable]
    private class RandomRange
    {
        [Min(1)] public int min = 1;
        [Min(1)] public int max = 3;

        public RandomRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }
    }

    [SerializeField]
    private RandomRange[] rangesByLevel =
    {
        new RandomRange(1, 3),
        new RandomRange(1, 5),
        new RandomRange(1, 8)
    };

    [Header("今回生成された乱数")]
    [SerializeField] private int currentRandomNumber;
    public int CurrentRandomNumber => currentRandomNumber;

    [Header("購入成功フラグ（テスト用）")]
    public bool hasReceivedItem = false;

    [Header("現在の動作（確認用）")]
    [SerializeField] private string currentState = "未開始";

    public float MoveSpeed => moveSpeed;
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

    private void GenerateRandomNumber()
    {
        if (rangesByLevel == null || rangesByLevel.Length == 0)
        {
            currentRandomNumber = 1;
            Debug.LogWarning("乱数の範囲が未設定のため、1を使用します。", this);
            return;
        }

        int index = Mathf.Clamp(randomLevel - 1, 0, rangesByLevel.Length - 1);
        RandomRange range = rangesByLevel[index];
        if (range == null)
        {
            currentRandomNumber = 1;
            Debug.LogWarning("選択したレベルの範囲が未設定のため、1を使用します。", this);
            return;
        }

        int min = Mathf.Max(1, Mathf.Min(range.min, range.max));
        int max = Mathf.Max(1, Mathf.Max(range.min, range.max));
        max = Mathf.Min(max, int.MaxValue - 1);
        min = Mathf.Min(min, max);
        currentRandomNumber = Random.Range(min, max + 1);
    }

    private void RestartAtA()
    {
        transform.position = pointA.position;
        hasReceivedItem = false;
        QueueProgress = 0f;
        GenerateRandomNumber();
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

        Quaternion targetRotation = Quaternion.LookRotation(direction)
            * Quaternion.Euler(0f, rotationOffsetY, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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
            transform.position, target.position, moveSpeed * Time.deltaTime);

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
