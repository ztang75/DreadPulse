using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class InteractableItem : MonoBehaviour
{
    [SerializeField] private string itemName = "Item";
    [SerializeField] private bool isCollectible = true;
    [SerializeField] private bool destroyOnCollect = true;
    [SerializeField] private string playerTag = "Player"; // 确保在Unity中设置了正确的玩家标签
    
    private bool isCollected = false;
    
    private void Awake()
    {
        // 确保物品的碰撞器设置为触发器，允许玩家穿过
        Collider itemCollider = GetComponent<Collider>();
        if (itemCollider != null && isCollectible)
        {
            itemCollider.isTrigger = true;
        }
        else if (itemCollider == null)
        {
            Debug.LogWarning($"物品 {gameObject.name} 缺少碰撞器组件，可能无法被收集");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 检查碰撞对象是否为玩家
        if (other.CompareTag(playerTag) || other.CompareTag("MainCamera") || 
            (other.transform.parent != null && other.transform.parent.CompareTag(playerTag)))
        {
            CollectItem();
        }
    }
    
    // 收集物品逻辑
    private void CollectItem()
    {
        if (isCollected || !isCollectible) return;
        
        Debug.Log($"物品 {gameObject.name} 被收集");
        
        // 通知游戏管理器物品被收集
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ItemCollected();
        }
        
        // 播放收集音效
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayItemPickupSound(transform.position);
        }
        
        // 触发触觉反馈
        HapticsManager hapticsManager = FindFirstObjectByType<HapticsManager>();
        if (hapticsManager != null)
        {
            hapticsManager.OnItemPickup();
        }
        
        isCollected = true;
        
        // 如果设置为收集后销毁，则销毁物体（稍微延迟以确保音效播放）
        if (destroyOnCollect)
        {
            StartCoroutine(DestroyAfterDelay(0.1f));
        }
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}