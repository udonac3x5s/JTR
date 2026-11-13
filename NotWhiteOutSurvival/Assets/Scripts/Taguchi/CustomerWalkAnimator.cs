
using UnityEngine;

public class CustomerWalkAnimator : MonoBehaviour
{
    [Header("éQè∆")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform movementRoot;

    [Header("ï‡çsîªíË")]
    [SerializeField] private float minMoveSpeed = 0.02f;
    [SerializeField] private float teleportDistance = 3f;

    private Vector3 previousPosition;

    private void Awake()
    {
        if (movementRoot == null)
            movementRoot = transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

        previousPosition = movementRoot.position;
    }

    private void OnEnable()
    {
        if (movementRoot == null)
            movementRoot = transform;

        previousPosition = movementRoot.position;
    }

    private void LateUpdate()
    {
        if (animator == null || movementRoot == null)
            return;

        Vector3 currentPosition = movementRoot.position;
        Vector3 movement = currentPosition - previousPosition;
        movement.y = 0f;

        float speed = movement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        bool isWalking =
            speed > minMoveSpeed &&
            movement.magnitude < teleportDistance;

        animator.SetBool("IsWalking", isWalking);


        previousPosition = currentPosition;
    }
}
