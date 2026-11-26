using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// プレイヤーのHP。クマの SendMessageUpwards("TakeDamage", int) を受けて減り、
/// 0になったらゲームオーバーにする。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 3;
    [Tooltip("ダメージを受けたあとの無敵時間(秒)。連続ヒット対策")]
    [SerializeField] private float invincibleTime = 0.5f;

    [Header("ゲームオーバー時")]
    [Tooltip("死んだら止めたいスクリプト(移動・PlayerAxe・PlayerCollector など)")]
    [SerializeField] private Behaviour[] disableOnDeath;
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTrigger = "Dead";

    [Header("イベント")]
    [Tooltip("ダメージを受けたとき(現在HP)。HPバーの更新などに")]
    public UnityEvent<int> onHpChanged;
    public UnityEvent onDamaged;
    public UnityEvent onGameOver;

    /// <summary>コードから受け取りたいとき用</summary>
    public event Action<int, int> OnHpChanged; // (現在HP, 最大HP)
    public event Action OnGameOver;

    public int Hp { get; private set; }
    public int MaxHp => maxHp;
    public bool IsDead { get; private set; }

    private float invincibleUntil;

    private void Awake()
    {
        Hp = maxHp;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 初期表示用
        OnHpChanged?.Invoke(Hp, maxHp);
        onHpChanged?.Invoke(Hp);
    }

    /// <summary>
    /// クマの SendMessageUpwards("TakeDamage", int) から呼ばれる。
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0) return;
        if (Time.time < invincibleUntil) return;

        // Debugを出す
        Debug.Log($"ダメージ {damage} : 現在HP: {Hp}/{maxHp}");


        invincibleUntil = Time.time + invincibleTime;
        Hp = Mathf.Max(0, Hp - damage);

        OnHpChanged?.Invoke(Hp, maxHp);
        onHpChanged?.Invoke(Hp);
        onDamaged?.Invoke();

        if (Hp <= 0) Die();
    }

    /// <summary>回復用(任意)</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Hp = Mathf.Min(maxHp, Hp + amount);
        OnHpChanged?.Invoke(Hp, maxHp);
        onHpChanged?.Invoke(Hp);
    }

    private void Die()
    {
        IsDead = true;

        foreach (var b in disableOnDeath)
            if (b != null) b.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        OnGameOver?.Invoke();
        onGameOver?.Invoke();
    }
}