
using UnityEngine;

public class BearRespawnController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ChoppableTree bearHealth;
    [SerializeField] private BearChaseAttack bearAI;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private Vector3 initialAILocalPosition;
    private Quaternion initialAILocalRotation;

    private Rigidbody rb;

    private void Awake()
    {
        if (bearHealth == null)
            bearHealth = GetComponent<ChoppableTree>();

        if (bearAI == null)
            bearAI = GetComponent<BearChaseAttack>();

        rb = GetComponent<Rigidbody>();

        // �ŏ��̈ʒu�Ɗp�x��ۑ�
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (bearAI != null)
        {
            initialAILocalPosition = bearAI.transform.localPosition;
            initialAILocalRotation = bearAI.transform.localRotation;
        }
    }

    private void OnEnable()
    {
        if (bearHealth == null) return;

        bearHealth.onChopped.AddListener(OnBearDead);
        bearHealth.onRespawn.AddListener(OnBearRespawn);
    }

    private void OnDisable()
    {
        if (bearHealth == null) return;

        bearHealth.onChopped.RemoveListener(OnBearDead);
        bearHealth.onRespawn.RemoveListener(OnBearRespawn);
    }

    // HP��0�ɂȂ����Ƃ�
    private void OnBearDead()
    {
        if (bearAI != null)
            bearAI.enabled = false;

        StopRigidbody();
    }

    // ���������Ƃ�
    private void OnBearRespawn()
    {
        StopRigidbody();

        // �ŏ��̈ʒu�ɖ߂�
        transform.SetPositionAndRotation(
            initialPosition, initialRotation);

        // AI���q�I�u�W�F�N�g�ɂ���ꍇ���ʒu��߂�
        if (bearAI != null && bearAI.transform != transform)
        {
            bearAI.transform.localPosition = initialAILocalPosition;
            bearAI.transform.localRotation = initialAILocalRotation;
        }

        Physics.SyncTransforms();

        // �ǐՂ��ĊJ
        if (bearAI != null)
            bearAI.enabled = true;
    }

    private void StopRigidbody()
    {
        if (rb == null || rb.isKinematic) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
