using UnityEngine;

public class GOMIPLAYER : MonoBehaviour
{
    // Playerの動きの 制御を行うスクリプト
    // 上押すと プレイヤーが 上に向き 上に 動く
    // 下押すと プレイヤーが 下に向き 下に 動く
    // 左押すと プレイヤーが 左に向き 左に 動く
    // 右押すと プレイヤーが 右に向き 右に 動く
    // 斜め方向も同様に プレイヤーが向きを変え 動く

    public float moveSpeed = 5f; // プレイヤーの移動速度

    private Vector3 moveDirection; // 移動方向

    Animator animator; // プレイヤーのアニメーター

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }


    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector3(horizontal, 0, vertical).normalized;

        if (moveDirection != Vector3.zero)
        {
            transform.forward = moveDirection; // プレイヤーの向きを移動方向に合わせる
        }

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    
        if (moveDirection != Vector3.zero)
        {
            animator.SetBool("Running", true);
        }
        else
        {
            animator.SetBool("Running", false);
        }
    }
}
