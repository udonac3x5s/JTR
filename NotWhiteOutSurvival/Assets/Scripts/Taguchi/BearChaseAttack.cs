using UnityEngine;

/// <summary>
/// 指定したLayerの相手を探し、追いかけて攻撃する熊のAI。
/// BearAnimator.controller（Run Forward / Attack1）に対応。
/// </summary>
[DisallowMultipleComponent]
public class BearChaseAttack : MonoBehaviour
{
    [Header("索敵")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0.1f)] private float detectionRange = 8f;
    [SerializeField, Min(0.05f)] private float searchInterval = 0.2f;

    [Header("移動")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;

    [Header("攻撃")]
    [Tooltip("熊の中心から相手のCollider表面までの距離。接触して押してしまう場合は大きくする")]
    [SerializeField, Min(0.1f)] private float attackRange = 1.8f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.8f;
    [SerializeField, Min(0f)] private float attackHitDelay = 0.45f;
    [SerializeField, Min(0.1f)] private float attackDuration = 1.1f;
    [SerializeField, Min(0)] private int attackDamage = 1;

    [Header("アニメーション")]
    [SerializeField] private Animator animator;

    private Collider target;
    private Collider attackTarget;
    private float nextSearchTime;
    private float nextAttackTime;
    private float attackStartTime;
    private bool isRunning;
    private bool isAttacking;
    private bool hasHit;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError(
                "BearChaseAttack: Animatorが見つかりません。熊のAnimatorが付いたオブジェクトにスクリプトを付けるか、InspectorでAnimatorを指定してください。",
                this);
        }
    }

    private void Update()
    {
        // 攻撃中は移動せず、指定したタイミングで1回だけ当たり判定を行う。
        if (isAttacking)
        {
            if (!hasHit && Time.time >= attackStartTime + attackHitDelay)
            {
                hasHit = true;
                HitTarget();
            }

            if (Time.time < attackStartTime + attackDuration)
            {
                return;
            }

            isAttacking = false;
            attackTarget = null;
        }

        if (Time.time >= nextSearchTime)
        {
            SearchTarget();
            nextSearchTime = Time.time + searchInterval;
        }

        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy)
        {
            target = null;
            SetRunning(false);
            return;
        }

        // Colliderの中心ではなく、熊に最も近い表面を基準にする。
        // これにより相手の向きやColliderの大きさで攻撃判定がずれにくくなる。
        Vector3 direction = GetSurfaceDirection(target);
        float distance = direction.magnitude;

        if (distance > detectionRange)
        {
            target = null;
            SetRunning(false);
            return;
        }

        // 向く方向は相手の中心を使用（すでにColliderと重なっていても向ける）。
        Vector3 lookDirection = target.bounds.center - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }

        if (distance > attackRange)
        {
            // Colliderに接触する前に止まる。
            float step = Mathf.Min(moveSpeed * Time.deltaTime, distance - attackRange);
            transform.position += direction.normalized * step;
            SetRunning(true);
        }
        else
        {
            SetRunning(false);

            if (Time.time >= nextAttackTime)
            {
                StartAttack();
            }
        }
    }

    private void SearchTarget()
    {
        Collider[] found = Physics.OverlapSphere(
            transform.position, detectionRange, targetLayers,
            QueryTriggerInteraction.Ignore);

        Collider nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider candidate in found)
        {
            if (candidate == null || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            Vector3 direction = GetSurfaceDirection(candidate);
            float sqrDistance = direction.sqrMagnitude;

            if (sqrDistance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = sqrDistance;
            }
        }

        target = nearest;
    }

    private Vector3 GetSurfaceDirection(Collider candidate)
    {
        Vector3 closestPoint = candidate.ClosestPoint(transform.position);
        Vector3 direction = closestPoint - transform.position;
        direction.y = 0f;
        return direction;
    }

    private void StartAttack()
    {
        if (target == null)
        {
            return;
        }

        isAttacking = true;
        hasHit = false;
        attackTarget = target;
        attackStartTime = Time.time;
        nextAttackTime = Time.time + attackInterval;

        if (animator != null)
        {
            animator.SetTrigger("Attack1");
        }
    }

    private void HitTarget()
    {
        if (attackTarget == null || !attackTarget.enabled ||
            !attackTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 direction = GetSurfaceDirection(attackTarget);

        if (direction.magnitude <= attackRange)
        {
            // 相手に TakeDamage(int) があればダメージを渡す。
            // メソッドが未実装でも攻撃アニメーションは動作する。
            attackTarget.SendMessageUpwards(
                "TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void SetRunning(bool running)
    {
        if (isRunning == running)
        {
            return;
        }

        isRunning = running;

        if (animator == null)
        {
            return;
        }

        animator.SetBool("Run Forward", running);

        if (running)
        {
            // Controllerの遷移設定に依存せず、走るStateに切り替える。
            animator.CrossFadeInFixedTime("Base Layer.RunForward", 0.15f);
        }
        else
        {
            // この素材のControllerには RunForward -> 待機の条件がないため、
            // 走るのをやめたら明示的に Combat Idle に戻す。
            animator.CrossFadeInFixedTime("Base Layer.Combat Idle", 0.15f);
        }
    }

    private void OnDisable()
    {
        if (animator != null)
        {
            animator.SetBool("Run Forward", false);
        }

        isRunning = false;
        isAttacking = false;
        target = null;
        attackTarget = null;
    }

    private void OnValidate()
    {
        attackRange = Mathf.Min(attackRange, detectionRange);
        attackDuration = Mathf.Max(attackDuration, attackHitDelay);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
