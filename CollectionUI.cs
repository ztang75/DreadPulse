using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CollectionUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI collectionCountText;
    // 如果使用普通UI Text而非TextMeshPro，请改用以下行:
    // [SerializeField] private Text collectionCountText;
    
    private GameManager gameManager;
    
    private void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("无法找到GameManager!");
        }
        
        UpdateUI();
    }
    
    private void Update()
    {
        // 每帧更新UI - 也可以改为事件驱动方式(见下文建议)
        UpdateUI();
    }
    
    public void UpdateUI()
    {
        if (gameManager != null && collectionCountText != null)
        {
            int collected = gameManager.GetCollectedItemCount();
            int total = gameManager.GetTotalItemsToCollect();
            collectionCountText.text = $"{collected}/{total}";
        }
    }
}