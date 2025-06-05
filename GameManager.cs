using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private int totalItemsToCollect = 5;
    private int collectedItems = 0;
    
    public bool gameOver = false;
    public bool gameWon = false;
    
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform exitDoor;
    [SerializeField] private float exitProximity = 2f;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Update()
    {
        // 检查是否可以离开学校（所有物品已收集）
        if (collectedItems >= totalItemsToCollect && !gameWon)
        {
            if (Vector3.Distance(playerTransform.position, exitDoor.position) < exitProximity)
            {
                WinGame();
            }
        }
    }
    
    public void ItemCollected()
    {
        collectedItems++;
        Debug.Log($"物品已收集: {collectedItems}/{totalItemsToCollect}");
        
        // 通知适应性系统物品收集事件
        AdaptiveSystem.Instance.OnItemCollected();
    }

    public void GameOver()
    {
        if (gameOver) return;

        gameOver = true;
        Debug.Log("游戏结束 - 被鬼抓住了!");

        // 保存当前收集的物品数量到PlayerPrefs，以便在游戏恢复时使用
        PlayerPrefs.SetInt("CollectedItems", collectedItems);
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();

        // 加载游戏结束场景
        SceneManager.LoadScene("GameOver");
    }

    public void WinGame()
    {
        if (gameWon) return;
        
        gameWon = true;
        Debug.Log("游戏胜利 - 成功逃脱!");
        
        // 显示胜利UI
        StartCoroutine(RestartGame(5f));
    }
    
    private IEnumerator RestartGame(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public int GetCollectedItemCount()
    {
        return collectedItems;
    }

    public int GetTotalItemsToCollect()
    {
        return totalItemsToCollect;
    }
}