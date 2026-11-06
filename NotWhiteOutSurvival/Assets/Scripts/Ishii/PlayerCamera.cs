using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤー追従カメラ(角度固定・ズームなし)
/// 注視点は「カメラ移動エリア」の中だけを動く。
/// プレイヤーがエリアの外に出たら、エリア内でいちばん近い点で止まる
/// → 右に出ても上下に余裕があれば上下は追従する。
/// </summary>
public class PlayerCamera : MonoBehaviour
{
    [Serializable]
    public class CameraArea
    {
        public string name = "Area";

        [Tooltip("X方向を制限する(OFFならX方向は無限)")]
        public bool limitX = true;
        public float minX = -10f;
        public float maxX = 10f;

        [Tooltip("Z方向を制限する(OFFならZ方向は無限)")]
        public bool limitZ = true;
        public float minZ = -10f;
        public float maxZ = 10f;

        // エリア内でいちばん近い点に丸める
        public Vector2 ClosestPoint(float x, float z)
        {
            if (limitX) x = Mathf.Clamp(x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            if (limitZ) z = Mathf.Clamp(z, Mathf.Min(minZ, maxZ), Mathf.Max(minZ, maxZ));
            return new Vector2(x, z);
        }
    }

    [Header("追従対象")]
    [SerializeField] private Transform target;

    [Header("カメラ位置")]
    [Tooltip("注視点からのカメラのオフセット(ワールド座標)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
    [Tooltip("0なら即時追従")]
    [SerializeField] private float smoothTime = 0.1f;

    [Header("カメラが動けるエリア(複数あれば合体した範囲になる / 空なら制限なし)")]
    [SerializeField] private List<CameraArea> areas = new List<CameraArea>();

    private Vector3 focus;
    private Vector3 velocity;

    private void Start()
    {
        if (target == null) return;
        focus = Constrain(target.position);
        transform.position = focus + offset;
        transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        focus = Constrain(target.position);

        Vector3 goal = focus + offset;
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime)
            : goal;
    }

    /// <summary>全エリアの中で、プレイヤーにいちばん近い点を注視点にする</summary>
    private Vector3 Constrain(Vector3 p)
    {
        if (areas == null || areas.Count == 0) return p;

        Vector2 best = new Vector2(p.x, p.z);
        float bestDist = float.MaxValue;

        foreach (var a in areas)
        {
            Vector2 c = a.ClosestPoint(p.x, p.z);
            float d = (c.x - p.x) * (c.x - p.x) + (c.y - p.z) * (c.y - p.z);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
                if (d <= 0f) break; // エリアの中にいる
            }
        }
        return new Vector3(best.x, p.y, best.y);
    }

    /// <summary>ワープ時などに即座に位置を合わせる</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        focus = Constrain(target.position);
        velocity = Vector3.zero;
        transform.position = focus + offset;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        const float inf = 500f;
        float y = target != null ? target.position.y : 0f;

        foreach (var a in areas)
        {
            float x0 = a.limitX ? a.minX : -inf, x1 = a.limitX ? a.maxX : inf;
            float z0 = a.limitZ ? a.minZ : -inf, z1 = a.limitZ ? a.maxZ : inf;
            var center = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
            var size = new Vector3(Mathf.Abs(x1 - x0), 0.05f, Mathf.Abs(z1 - z0));

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.15f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(focus, 0.3f);
        }
    }
#endif
}