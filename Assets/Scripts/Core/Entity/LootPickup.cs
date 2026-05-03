using System;
using UnityEngine;

/// <summary>
///     掉落物拾取实体 —— 可被玩家拾取的掉落物。
/// </summary>
public class LootPickup : MonoBehaviour
{
    /// <summary>物品ID</summary>
    public int ItemID;

    /// <summary>数量</summary>
    public int Quantity;

    /// <summary>拾取时的音效</summary>
    [SerializeField] private AudioClip _pickupSFX;

    /// <summary>拾取动画</summary>
    [SerializeField] private AnimationClip _pickupAnimation;

    /// <summary>拾取范围</summary>
    [SerializeField] private float _pickupRadius = 1.5f;

    /// <summary>
    ///     初始化。
    /// </summary>
    private void Awake()
    {
        // 添加碰撞体用于拾取检测
        var collider = gameObject.AddComponent<SphereCollider>();
        collider.radius = _pickupRadius;
        collider.isTrigger = true;
    }

    /// <summary>
    ///     碰撞触发。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Pickup(other.transform);
        }
    }

    /// <summary>
    ///     拾取掉落物。
    /// </summary>
    public void Pickup(Transform playerTransform)
    {
        if (playerTransform == null) return;

        // 播放拾取音效
        if (_pickupSFX != null)
        {
            AudioSource.PlayClipAtPoint(_pickupSFX, transform.position);
        }

        // 播放拾取动画
        if (_pickupAnimation != null)
        {
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play(_pickupAnimation.name);
            }
        }

        // 添加到玩家背包
        var inventory = playerTransform.GetComponent<InventoryComponent>();
        if (inventory != null)
        {
            inventory.AddItem(ItemID, Quantity);
        }

        // 播放粒子特效
        PlayPickupVFX();

        // 销毁掉落物
        Destroy(gameObject);
    }

    /// <summary>
    ///     播放拾取粒子特效。
    /// </summary>
    private void PlayPickupVFX()
    {
        // 查找并播放拾取特效
        var vfxPrefab = Resources.Load<GameObject>("VFX/LootPickup");
        if (vfxPrefab != null)
        {
            var vfxInstance = GameObject.Instantiate(vfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfxInstance, 2f);
        }
    }
}