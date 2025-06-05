using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HapticsManager : MonoBehaviour
{
    [SerializeField] private float basePulseIntensity = 0.2f;
    [SerializeField] private float basePulseDuration = 0.1f;
    
    [SerializeField] private float ghostNearbyIntensity = 0.5f;
    [SerializeField] private float itemPickupIntensity = 0.3f;
    [SerializeField] private float doorInteractionIntensity = 0.2f;
    [SerializeField] private float jumpscareIntensity = 1.0f;
    
    private float currentBaseIntensity;
    
    private void Start()
    {
        currentBaseIntensity = basePulseIntensity;
    }
    
    // 基于玩家紧张度更新基础触觉反馈强度
    public void UpdateBaseIntensity(float playerStressLevel)
    {
        currentBaseIntensity = Mathf.Lerp(basePulseIntensity, basePulseIntensity * 2f, playerStressLevel);
    }
    
    // 触发单次触觉脉冲
    public void TriggerHapticPulse(InputDevice device, float intensity, float duration)
    {
        if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) 
            && capabilities.supportsImpulse)
        {
            device.SendHapticImpulse(0, intensity, duration);
        }
    }
    
    // 触发双手触觉反馈
    public void TriggerHapticPulseOnBothHands(float intensity, float duration)
    {
        List<InputDevice> leftHandDevices = new List<InputDevice>();
        List<InputDevice> rightHandDevices = new List<InputDevice>();
        
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, 
            leftHandDevices);
            
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, 
            rightHandDevices);
            
        foreach (var device in leftHandDevices)
        {
            TriggerHapticPulse(device, intensity, duration);
        }
        
        foreach (var device in rightHandDevices)
        {
            TriggerHapticPulse(device, intensity, duration);
        }
    }
    
    // 物品拾取触觉反馈
    public void OnItemPickup()
    {
        TriggerHapticPulseOnBothHands(itemPickupIntensity * currentBaseIntensity, basePulseDuration);
    }
    
    // 门交互触觉反馈
    public void OnDoorInteraction(bool isLeft)
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDeviceCharacteristics handChar = isLeft ? 
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left : 
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right;
            
        InputDevices.GetDevicesWithCharacteristics(handChar, devices);
        
        foreach (var device in devices)
        {
            TriggerHapticPulse(device, doorInteractionIntensity * currentBaseIntensity, basePulseDuration * 1.5f);
        }
    }
    
    // 鬼接近触觉反馈 - 随距离变化
    public void OnGhostNearby(float proximity)
    {
        float intensity = Mathf.Lerp(ghostNearbyIntensity, ghostNearbyIntensity * 0.3f, proximity);
        float duration = basePulseDuration * (1.0f - proximity * 0.5f);
        
        TriggerHapticPulseOnBothHands(intensity * currentBaseIntensity, duration);
    }
    
    // 吓一跳效果的强烈触觉反馈
    public void OnJumpscare()
    {
        StartCoroutine(JumpscareHapticSequence());
    }
    
    private IEnumerator JumpscareHapticSequence()
    {
        // 短而强烈的双手触觉序列
        for (int i = 0; i < 3; i++)
        {
            TriggerHapticPulseOnBothHands(jumpscareIntensity, 0.15f);
            yield return new WaitForSeconds(0.07f);
        }
        
        yield return new WaitForSeconds(0.2f);
        TriggerHapticPulseOnBothHands(jumpscareIntensity * 0.7f, 0.3f);
    }
    
    // 低频率的"心跳"触觉反馈
    public void StartHeartbeatHaptics(float intensity)
    {
        StartCoroutine(HeartbeatHapticSequence(intensity));
    }
    
    private IEnumerator HeartbeatHapticSequence(float intensity)
    {
        // 模拟心跳的双脉冲
        TriggerHapticPulseOnBothHands(intensity * 0.7f, 0.1f);
        yield return new WaitForSeconds(0.15f);
        TriggerHapticPulseOnBothHands(intensity, 0.15f);
    }
}