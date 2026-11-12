using UnityEngine;
using Common;

/// <summary>
/// 地面に落ちているアイテム。アイテムごとのPrefab(薪・肉など)に付ける。
/// 必要:Collider(Is Trigger ON 推奨)、レイヤーは PlayerCollector の itemLayer に合わせる。
///
/// 流れ: 飛び出す(放物線) → 着地 → 少し待つ → プレイヤーが近づくと吸い寄せ → 回収
/// </summary>
public class ItemPickup : MonoBehaviour
{
    private enum State { Launching, Waiting, Ready, Attracting, Collected }

    [Header("アイテム")]
    [SerializeField] private ItemType itemType = ItemType.Wood;
    [SerializeField] private int amount = 1;

    [Header("飛び出し")]
    [SerializeField] private float launchDuration = 0.5f;
    [SerializeField] private float launchHeight = 1.5f;

    [Header("回収")]
    [Tooltip("着地してから回収できるまでの時間")]
    [SerializeField] private float collectDelay = 0.3f;
    [SerializeField] private float attractStartSpeed = 4f;
    [SerializeField] private float attractAcceleration = 40f;
    [Tooltip("この距離まで近づいたら回収")]
    [SerializeField] private float collectDistance = 0.3f;

    [Header("待機中の演出")]
    [SerializeField] private float spinSpeed = 90f;

    public ItemType ItemType => itemType;
    public int Amount => amount;
    public bool CanAttract => state == State.Ready;

    private State state = State.Ready;
    private Vector3 launchFrom, launchTo;
    private float timer;
    private PlayerCollector collector;
    private float attractSpeed;

    /// <summary>from から to へ放物線で飛ばす</summary>
    public void Launch(Vector3 from, Vector3 to)
    {
        launchFrom = from;
        launchTo = to;
        transform.position = from;
        timer = 0f;
        state = State.Launching;
    }

    /// <summary>PlayerCollector から呼ばれる</summary>
    public void StartAttract(PlayerCollector by)
    {
        if (state != State.Ready) return;
        collector = by;
        attractSpeed = attractStartSpeed;
        state = State.Attracting;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Launching:
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / launchDuration);
                Vector3 p = Vector3.Lerp(launchFrom, launchTo, t);
                p.y += 4f * launchHeight * t * (1f - t); // 放物線
                transform.position = p;
                if (t >= 1f)
                {
                    timer = 0f;
                    state = State.Waiting;
                }
                break;

            case State.Waiting:
                timer += Time.deltaTime;
                if (timer >= collectDelay) state = State.Ready;
                break;

            case State.Attracting:
                if (collector == null) { state = State.Ready; break; }

                attractSpeed += attractAcceleration * Time.deltaTime;
                Vector3 goal = collector.CollectPoint;
                transform.position = Vector3.MoveTowards(transform.position, goal, attractSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, goal) <= collectDistance)
                {
                    state = State.Collected;
                    collector.Collect(this);
                    Destroy(gameObject);
                }
                break;
        }

        if (state != State.Attracting)
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}