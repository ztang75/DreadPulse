using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Door : MonoBehaviour
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;
    [SerializeField] private bool isLocked = true; // 默认为锁住状态
    [SerializeField] private bool destroyWhenUnlocked = true; // 解锁后是否销毁
    [SerializeField] private GameObject destroyEffect; // 可选的销毁特效
    
    private XRSimpleInteractable doorHandle;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen = false;
    private bool isMoving = false;
    
    // 检查门状态的计时器
    private float checkLockStatusTimer = 0f;
    private float checkInterval = 0.5f; // 每0.5秒检查一次
    
    private void Awake()
    {
        doorHandle = GetComponentInChildren<XRSimpleInteractable>();
        
        if (doorHandle != null)
        {
            doorHandle.selectEntered.AddListener(OnDoorHandleGrabbed);
        }
        
        if (doorPivot != null)
        {
            closedRotation = doorPivot.rotation;
            openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
        }
    }
    
    private void Start()
    {
        // 初次检查门锁状态
        CheckLockStatus();
    }
    
    private void Update()
    {
        if (isLocked)
        {
            // 定期检查是否应该解锁门
            checkLockStatusTimer += Time.deltaTime;
            if (checkLockStatusTimer >= checkInterval)
            {
                checkLockStatusTimer = 0f;
                CheckLockStatus();
            }
        }
    }
    
    private void OnDoorHandleGrabbed(SelectEnterEventArgs args)
    {
        // 检查门是否被锁住
        if (isLocked)
        {
            // 再次检查是否已收集所有物品
            CheckLockStatus();
            
            // 如果仍然锁住，播放锁住提示音效
            if (isLocked)
            {
                // 播放锁住的门的音效
                AudioManager.Instance?.PlayDoorSound(transform.position);
                
                // 触发触觉反馈表示门锁住了
                XRBaseInteractor interactor = args.interactorObject as XRBaseInteractor;
                if (interactor != null)
                {
                    bool isLeft = interactor.name.ToLower().Contains("left");
                    FindFirstObjectByType<HapticsManager>()?.OnDoorInteraction(isLeft);
                }
                
                // 可以添加UI提示，告诉玩家需要收集所有物品
                Debug.Log("门仍然锁住。需要收集所有物品才能解锁。");
                return;
            }
        }
        
        // 如果门没有锁住且没在移动中，则切换开/关状态
        if (!isMoving)
        {
            isOpen = !isOpen;
            StartCoroutine(MoveDoor(isOpen));
            
            // 播放门开关音效
            AudioManager.Instance?.PlayDoorSound(transform.position);
            
            // 触发触觉反馈
            XRBaseInteractor interactor = args.interactorObject as XRBaseInteractor;
            if (interactor != null)
            {
                bool isLeft = interactor.name.ToLower().Contains("left");
                FindFirstObjectByType<HapticsManager>()?.OnDoorInteraction(isLeft);
            }
        }
    }
    
    private IEnumerator MoveDoor(bool open)
    {
        isMoving = true;
        float elapsedTime = 0;
        
        Quaternion startRotation = doorPivot.rotation;
        Quaternion targetRotation = open ? openRotation : closedRotation;
        
        while (elapsedTime < 1f)
        {
            doorPivot.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime * openSpeed);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 确保门完全到达目标位置
        doorPivot.rotation = targetRotation;
        isMoving = false;
    }
    
    // 检查是否已收集所有物品
    private void CheckLockStatus()
    {
        if (GameManager.Instance == null) return;
        
        // 检查是否已收集所有需要的物品
        int collectedItems = GameManager.Instance.GetCollectedItemCount();
        int totalItemsNeeded = GameManager.Instance.GetTotalItemsToCollect();
        
        if (collectedItems >= totalItemsNeeded)
        {
            // 已收集所有物品，解锁门
            Unlock();
        }
    }
    
    // 解锁门
    private void Unlock()
    {
        if (!isLocked) return; // 已经解锁了
        
        isLocked = false;
        Debug.Log("门已解锁！所有物品已收集。");
        
        // 播放解锁音效
        AudioManager.Instance?.PlayDoorSound(transform.position);
        
        // 如果设置为解锁后销毁，则销毁门
        if (destroyWhenUnlocked)
        {
            // 播放销毁特效（如果有）
            if (destroyEffect != null)
            {
                Instantiate(destroyEffect, transform.position, transform.rotation);
            }
            
            // 销毁门对象
            Destroy(gameObject);
        }
        else
        {
            // 如果不销毁，可以改变门的外观或添加其他视觉提示
            // 例如改变材质、添加粒子效果等
        }
    }
}