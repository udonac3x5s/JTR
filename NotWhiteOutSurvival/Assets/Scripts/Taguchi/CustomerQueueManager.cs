
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerQueueManager : MonoBehaviour
{
    [Header("商人NPC")]
    [SerializeField] private CustomerMoveTest customerPrefab;
    [SerializeField, Min(1)] private int spawnCount = 5;
    [SerializeField, Min(0.01f)] private float spawnInterval = 1f;

    [Header("木こりNPC")]
    [SerializeField] private GameObject woodcutterPrefab;
    [SerializeField, Min(0)] private int woodcutterCount = 0;
    [SerializeField, Min(0.01f)] private float spawnIntervalW = 1f;
    [SerializeField] private Transform woodcutterSpawnPoint;
    [SerializeField, Min(0f)] private float woodcutterSpawnSpacing = 1f;

    [Header("地点")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform pointC;

    private Transform[] waypointsAToB;
    private Transform[] waypointsBToC;

    [Header("順番待ちの間隔")]
    [SerializeField, Min(0.1f)] private float queueSpacing = 1.5f;

    [Header("C地点からA地点に戻す")]
    [SerializeField] private bool returnCustomersToA = true;
    public bool ReturnCustomersToA => returnCustomersToA;

    [Header("取引完了")]
    public bool serveFirstCustomer = false;

    private int createdCount;
    private int queueCount;
    private float nextWoodcutterSpawnTime;

    private readonly List<CustomerMoveTest> waitingLine =
        new List<CustomerMoveTest>();

    private readonly Queue<CustomerMoveTest> waitingToReturn =
        new Queue<CustomerMoveTest>();

    private readonly List<GameObject> spawnedWoodcutters =
        new List<GameObject>();

    private Vector3[] routeAToB;
    private float routeLengthAToB;

    private void Start()
    {
        if (customerPrefab == null ||
            pointA == null || pointB == null || pointC == null)
        {
            Debug.LogError("CustomerQueueManager: 参照が未設定です。", this);
            enabled = false;
            return;
        }

        routeAToB = BuildRoute(pointA, waypointsAToB, pointB);
        routeLengthAToB = GetRouteLength(routeAToB);

        if (routeLengthAToB < 0.01f)
        {
            Debug.LogError("AからBまでの経路が短すぎます。", this);
            enabled = false;
            return;
        }

        StartCoroutine(SpawnCustomers());
    }

    private IEnumerator SpawnCustomers()
    {
        while (createdCount < spawnCount)
        {
            if (waitingToReturn.Count > 0 || !CanEnterAtA())
            {
                yield return null;
                continue;
            }

            CustomerMoveTest customer = Instantiate(
                customerPrefab, pointA.position, pointA.rotation);

            customer.InitializeForQueue(
                this, pointA, pointB, pointC,
                waypointsAToB, waypointsBToC);

            waitingLine.Add(customer);
            createdCount++;

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void Update()
    {
        // 木こりの生成数を管理
        UpdateWoodcutters();

        waitingLine.RemoveAll(customer => customer == null);

        // 先頭NPCの取引完了
        if (serveFirstCustomer && waitingLine.Count > 0 &&
            waitingLine[0].IsWaitingAtB)
        {
            waitingLine[0].hasReceivedItem = true;
            serveFirstCustomer = false;
        }

        // 取引完了した先頭NPCをCへ移動
        if (waitingLine.Count > 0 &&
            waitingLine[0].IsWaitingAtB &&
            waitingLine[0].hasReceivedItem)
        {
            CustomerMoveTest first = waitingLine[0];
            waitingLine.RemoveAt(0);
            first.LeaveQueueForC();
        }

        // AからBまでの順番待ち
        for (int i = 0; i < waitingLine.Count; i++)
        {
            CustomerMoveTest customer = waitingLine[i];

            float maxProgress = i == 0
                ? routeLengthAToB
                : waitingLine[i - 1].QueueProgress - queueSpacing;

            maxProgress = Mathf.Clamp(
                maxProgress, 0f, routeLengthAToB);

            float nextProgress = Mathf.MoveTowards(
                customer.QueueProgress,
                Mathf.Max(customer.QueueProgress, maxProgress),
                customer.MoveSpeed * Time.deltaTime);

            Vector3 position = GetPointOnRoute(
                routeAToB, nextProgress);

            customer.SetQueuePosition(
                position,
                nextProgress,
                nextProgress >= routeLengthAToB - 0.001f);
        }

        // Cから戻ってきたNPCをAへ再配置
        while (waitingToReturn.Count > 0 && CanEnterAtA())
        {
            CustomerMoveTest customer = waitingToReturn.Dequeue();

            if (customer == null) continue;

            customer.ReturnToQueueAtA();
            waitingLine.Add(customer);
        }

        queueCount = waitingLine.Count;
    }

    // 木こりの生成数を管理する
    private void UpdateWoodcutters()
    {
        spawnedWoodcutters.RemoveAll(worker => worker == null);

        int targetCount = Mathf.Max(0, woodcutterCount);

        // 指定数より多ければ削除
        while (spawnedWoodcutters.Count > targetCount)
        {
            int last = spawnedWoodcutters.Count - 1;

            Destroy(spawnedWoodcutters[last]);
            spawnedWoodcutters.RemoveAt(last);
        }

        // 指定数まで達していたら生成しない
        if (spawnedWoodcutters.Count >= targetCount)
            return;

        if (woodcutterPrefab == null || woodcutterSpawnPoint == null)
            return;

        // 生成間隔を管理
        if (Time.time < nextWoodcutterSpawnTime)
            return;

        Vector3 position = woodcutterSpawnPoint.position
            + woodcutterSpawnPoint.right
            * woodcutterSpawnSpacing
            * spawnedWoodcutters.Count;

        GameObject worker = Instantiate(
            woodcutterPrefab,
            position,
            woodcutterSpawnPoint.rotation);

        spawnedWoodcutters.Add(worker);

        nextWoodcutterSpawnTime =
            Time.time + Mathf.Max(0.01f, spawnIntervalW);
    }

    // 他のスクリプトから木こりの人数を変更
    public void SetWoodcutterCount(int count)
    {
        woodcutterCount = Mathf.Max(0, count);
    }

    private bool CanEnterAtA()
    {
        if (waitingLine.Count == 0) return true;

        CustomerMoveTest last =
            waitingLine[waitingLine.Count - 1];

        return last != null &&
               last.QueueProgress >= queueSpacing;
    }

    public void RequestReturn(CustomerMoveTest customer)
    {
        if (customer != null)
            waitingToReturn.Enqueue(customer);
    }

    [ContextMenu("テスト：Bの先頭NPCを完了にする")]
    public void ServeFirstNow()
    {
        if (waitingLine.Count == 0) return;

        CustomerMoveTest first = waitingLine[0];

        if (first != null && first.IsWaitingAtB)
            first.hasReceivedItem = true;
    }

    private static Vector3[] BuildRoute(
        Transform start, Transform[] waypoints, Transform end)
    {
        List<Vector3> positions = new List<Vector3>();
        positions.Add(start.position);

        if (waypoints != null)
        {
            foreach (Transform waypoint in waypoints)
            {
                if (waypoint != null)
                    positions.Add(waypoint.position);
            }
        }

        positions.Add(end.position);
        return positions.ToArray();
    }

    private static float GetRouteLength(Vector3[] points)
    {
        float length = 0f;

        for (int i = 1; i < points.Length; i++)
            length += Vector3.Distance(points[i - 1], points[i]);

        return length;
    }

    private static Vector3 GetPointOnRoute(
        Vector3[] points, float distance)
    {
        for (int i = 1; i < points.Length; i++)
        {
            float segmentLength =
                Vector3.Distance(points[i - 1], points[i]);

            if (segmentLength < 0.00001f) continue;

            if (distance <= segmentLength)
                return Vector3.Lerp(
                    points[i - 1], points[i],
                    distance / segmentLength);

            distance -= segmentLength;
        }

        return points[points.Length - 1];
    }

    private void OnDrawGizmosSelected()
    {
        if (pointA == null || pointB == null || pointC == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pointA.position, pointB.position);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pointB.position, pointC.position);
    }
}
