using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CustomerMoveTestを付けたPrefabを指定数生成し、A→Bで追い越さずに並ばせます。
/// 先頭がBで受取完了したらB→Cへ進み、後ろのNPCが1つずつ詰めます。
/// </summary>
public class CustomerQueueManager : MonoBehaviour
{
    [Header("生成するNPC")]
    [SerializeField] private CustomerMoveTest customerPrefab;
    [SerializeField, Min(1)] private int spawnCount = 5;
    [SerializeField, Min(0.01f)] private float spawnInterval = 1f;

    [Header("地点")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform pointC;

    private Transform[] waypointsAToB;
    private Transform[] waypointsBToC;

    [Header("順番待ちの間隔")]
    [SerializeField, Min(0.1f)] private float queueSpacing = 1.5f;

    [Header("取引完了")]
    public bool serveFirstCustomer = false;
    private int createdCount;
    private int queueCount;

    // Listの先頭がBに一番近いNPC。
    private readonly List<CustomerMoveTest> waitingLine = new List<CustomerMoveTest>();
    private readonly Queue<CustomerMoveTest> waitingToReturn = new Queue<CustomerMoveTest>();
    private Vector3[] routeAToB;
    private float routeLengthAToB;

    private void Start()
    {
        if (customerPrefab == null || pointA == null || pointB == null || pointC == null)
        {
            Debug.LogError("CustomerQueueManager: Customer Prefab と Point A / B / C を設定してください。", this);
            enabled = false;
            return;
        }

        routeAToB = BuildRoute(pointA, waypointsAToB, pointB);
        routeLengthAToB = GetRouteLength(routeAToB);
        if (routeLengthAToB < 0.01f)
        {
            Debug.LogError("CustomerQueueManager: AからBまでの経路長が短すぎます。", this);
            enabled = false;
            return;
        }

        StartCoroutine(SpawnCustomers());
    }

    private IEnumerator SpawnCustomers()
    {
        while (createdCount < spawnCount)
        {
            // 地点Aが空くまで待つ。後からCへ行ったNPCが戻るときはそちらを優先。
            if (waitingToReturn.Count > 0 || !CanEnterAtA())
            {
                yield return null;
                continue;
            }

            CustomerMoveTest customer = Instantiate(
                customerPrefab, pointA.position, pointA.rotation);
            customer.InitializeForQueue(
                this, pointA, pointB, pointC, waypointsAToB, waypointsBToC);
            waitingLine.Add(customer);
            createdCount++;
            queueCount = waitingLine.Count;

            yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));
        }
    }

    private void Update()
    {
        // Inspector上でチェックすると、先頭がBにいるときに1回だけ完了にする。
        if (serveFirstCustomer && waitingLine.Count > 0 &&
            waitingLine[0] != null && waitingLine[0].IsWaitingAtB)
        {
            waitingLine[0].hasReceivedItem = true;
            serveFirstCustomer = false;
        }

        // 先頭以外のHas Received ItemをONにしても、前を追い越さない。
        if (waitingLine.Count > 0 && waitingLine[0] != null &&
            waitingLine[0].IsWaitingAtB && waitingLine[0].hasReceivedItem)
        {
            CustomerMoveTest first = waitingLine[0];
            waitingLine.RemoveAt(0);
            first.LeaveQueueForC();
        }

        // A→Bの経路に沿った距離で順序と間隔を管理する。
        for (int i = 0; i < waitingLine.Count; i++)
        {
            CustomerMoveTest customer = waitingLine[i];
            if (customer == null) continue;

            float maxProgress = (i == 0)
                ? routeLengthAToB
                : waitingLine[i - 1].QueueProgress - queueSpacing;
            maxProgress = Mathf.Clamp(maxProgress, 0f, routeLengthAToB);

            float nextProgress = Mathf.MoveTowards(
                customer.QueueProgress,
                Mathf.Max(customer.QueueProgress, maxProgress),
                customer.MoveSpeed * Time.deltaTime);

            Vector3 position = GetPointOnRoute(routeAToB, nextProgress);
            customer.SetQueuePosition(position, nextProgress,
                nextProgress >= routeLengthAToB - 0.001f);
        }

        // Cまで到達したNPCは、最後尾が離れたらAに戻して列の後ろに加える。
        while (waitingToReturn.Count > 0 && CanEnterAtA())
        {
            CustomerMoveTest customer = waitingToReturn.Dequeue();
            if (customer == null) continue;
            customer.ReturnToQueueAtA();
            waitingLine.Add(customer);
        }

        queueCount = waitingLine.Count;
    }

    private bool CanEnterAtA()
    {
        if (waitingLine.Count == 0) return true;
        CustomerMoveTest last = waitingLine[waitingLine.Count - 1];
        return last != null && last.QueueProgress >= queueSpacing;
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

    private static Vector3[] BuildRoute(Transform start, Transform[] waypoints, Transform end)
    {
        List<Vector3> positions = new List<Vector3>();
        positions.Add(start.position);
        if (waypoints != null)
        {
            foreach (Transform waypoint in waypoints)
                if (waypoint != null) positions.Add(waypoint.position);
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

    private static Vector3 GetPointOnRoute(Vector3[] points, float distance)
    {
        for (int i = 1; i < points.Length; i++)
        {
            float segmentLength = Vector3.Distance(points[i - 1], points[i]);
            if (segmentLength < 0.00001f) continue;
            if (distance <= segmentLength)
                return Vector3.Lerp(points[i - 1], points[i], distance / segmentLength);
            distance -= segmentLength;
        }
        return points[points.Length - 1];
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
