
using UnityEngine;
using UnityEngine.UI;

public class BearHealthBar : MonoBehaviour
{
    [Header("éQè∆")]
    [SerializeField] private ChoppableTree bearHealth;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private RectTransform hpCanvas;
    [SerializeField] private Camera targetCamera;

    private void Awake()
    {
        if (bearHealth == null)
            bearHealth = GetComponent<ChoppableTree>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (bearHealth != null)
        {
            bearHealth.onHit.AddListener(UpdateHP);
            bearHealth.onChopped.AddListener(HideHP);
            bearHealth.onRespawn.AddListener(ShowHP);
        }

        UpdateHP();
    }

    private void Start()
    {
        UpdateHP();
    }

    private void OnDisable()
    {
        if (bearHealth != null)
        {
            bearHealth.onHit.RemoveListener(UpdateHP);
            bearHealth.onChopped.RemoveListener(HideHP);
            bearHealth.onRespawn.RemoveListener(ShowHP);
        }
    }

    private void LateUpdate()
    {
        if (hpCanvas == null) return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        // ÉJÉÅÉâÇÃï˚å¸Çå¸Ç≠
        if (targetCamera != null)
            hpCanvas.rotation = targetCamera.transform.rotation;
    }

    private void UpdateHP()
    {
        if (bearHealth == null || hpSlider == null)
            return;

        hpSlider.minValue = 0f;
        hpSlider.maxValue = 1f;

        hpSlider.value =
            (float)bearHealth.CurrentHP / bearHealth.MaxHP;
    }

    private void HideHP()
    {
        UpdateHP();

        if (hpCanvas != null)
            hpCanvas.gameObject.SetActive(false);
    }

    private void ShowHP()
    {
        if (hpCanvas != null)
            hpCanvas.gameObject.SetActive(true);

        UpdateHP();
    }
}

