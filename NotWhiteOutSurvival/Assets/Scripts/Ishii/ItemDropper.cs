using UnityEngine;

/// <summary>
/// アイテムを周りにばらまく共通処理。木こり・狩りなどどこからでも使える。
/// </summary>
public static class ItemDropper
{
    /// <param name="prefab">落とすアイテムのPrefab</param>
    /// <param name="count">個数</param>
    /// <param name="origin">飛び出す位置(幹の途中など)</param>
    /// <param name="groundCenter">散らばる中心(地面の高さ)</param>
    /// <param name="minRadius">中心からの最小距離</param>
    /// <param name="maxRadius">中心からの最大距離</param>
    public static void Drop(ItemPickup prefab, int count, Vector3 origin, Vector3 groundCenter,
                            float minRadius, float maxRadius)
    {
        if (prefab == null || count <= 0) return;

        // 角度を均等に割って少しずらす → きれいに周りに散らばる
        float baseAngle = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + 360f / count * i + Random.Range(-20f, 20f);
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 landing = groundCenter + dir * radius;

            ItemPickup item = Object.Instantiate(prefab, origin, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            item.Launch(origin, landing);
        }
    }
}