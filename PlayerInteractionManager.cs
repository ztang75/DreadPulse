using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlayerInteractionManager : MonoBehaviour
{
    [SerializeField] private Transform headTransform; // VR相机位置
    [SerializeField] private CharacterController characterController; // 引用已有的CharacterController
    [SerializeField] private float colliderHeightOffset = 0.1f; // 微调碰撞器高度
    
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private float interactionDistance = 2.0f;
    
    private XRDirectInteractor leftHandInteractor;
    private XRDirectInteractor rightHandInteractor;
    
    private void Start()
    {
        // 获取左右手交互器引用
        XRDirectInteractor[] interactors = GetComponentsInChildren<XRDirectInteractor>();
        foreach (var interactor in interactors)
        {
            if (interactor.name.ToLower().Contains("left"))
            {
                leftHandInteractor = interactor;
            }
            else if (interactor.name.ToLower().Contains("right"))
            {
                rightHandInteractor = interactor;
            }
        }
        
        // 如果未手动分配，自动获取XR Origin上的CharacterController
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        
        if (headTransform == null)
        {
            // 通常是XR Origin下的Camera Offset/Main Camera
            headTransform = GetComponentInChildren<Camera>().transform;
        }
        
        // 初始化时调整一次CharacterController
        UpdateCharacterController();
    }
    
    private void Update()
    {
        // 持续更新CharacterController以匹配玩家VR高度
        UpdateCharacterController();
        
        // 检测可交互物体
        DetectInteractables();
    }
    
    private void UpdateCharacterController()
    {
        if (characterController == null || headTransform == null) return;
        
        // 获取头部位置（VR相机位置）
        Vector3 headPosition = headTransform.position;
        
        // 计算人物高度 - 从地面到头部
        float height = Mathf.Max(0.2f, headPosition.y - transform.position.y);
        
        // 设置CharacterController参数
        characterController.height = height + colliderHeightOffset;
        characterController.center = new Vector3(0, height / 2, 0); // 中心点在角色中间
        characterController.radius = characterController.height * 0.3f; // 半径适配身高
    }
    
    private void DetectInteractables()
    {
        // 使用射线检测前方的可交互物体
        RaycastHit hit;
        if (Physics.Raycast(headTransform.position, headTransform.forward, out hit, interactionDistance, interactableLayer))
        {
            // 处理可交互物体的UI提示或高亮
            InteractableItem item = hit.collider.GetComponent<InteractableItem>();
            if (item != null)
            {
                
            }
        }
    }
    
    // 注意: CharacterController不会触发OnTriggerEnter，
    // 而是会触发OnControllerColliderHit事件
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // 这里可以处理与非触发器物体的碰撞
        // 例如墙壁、地面等
    }
    
    // 对于想要检测与触发器的交互，我们需要在触发器对象上处理
    // 或使用额外的触发器碰撞体附加到玩家上
}