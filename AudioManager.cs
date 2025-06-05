using System.Collections;
using System.Collections.Generic;
using Oculus.Haptics;
using UnityEngine;
using UnityEngine.Audio;


public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("音频混合器")]
    [SerializeField] private AudioMixer mainMixer;
    
    [Header("环境音效")]
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource heartbeatSource;
    

    [Header("音效预设")]
    [SerializeField] private AudioClip[] itemPickupSounds;
    [SerializeField] private AudioClip[] ghostDetectSounds;
    [SerializeField] private AudioClip[] doorSounds;
    [SerializeField] private AudioClip[] jumpscaresSounds;
    [SerializeField] private AudioClip[] heartbeatSounds;
    
    
    [Header("动态音频设置")]
    // 已移除心跳相关设置
    [SerializeField] private float lowStressMusicPitch = 0.8f;
    [SerializeField] private float highStressMusicPitch = 1.2f;
    
    // 已移除心跳相关变量
    private float baseAmbientVolume;

    [Header("触觉反馈")]
    [SerializeField] private HapticClip roarHaptic; // 鬼发现玩家时触觉反馈
    [SerializeField] private HapticClip doorHaptic; // 门打开时触觉反馈

    private HapticClipPlayer roarplayer;
    private HapticClipPlayer doorplayer;
    
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
        
        // 保存基础音频设置
        // 已移除心跳相关代码
        
        if (ambientSource != null)
        {
            baseAmbientVolume = ambientSource.volume;
        }

        doorplayer = new HapticClipPlayer(doorHaptic);
        roarplayer = new HapticClipPlayer(roarHaptic);
        
        // 检查并记录声音数组状态
        Debug.Log($"音效数组状态 - 拾取: {itemPickupSounds?.Length ?? 0}, 鬼声: {ghostDetectSounds?.Length ?? 0}, 门: {doorSounds?.Length ?? 0}");
    }
    
    public void UpdateAudioParameters(float playerStressLevel)
    {
        // 已移除心跳相关代码
        
        // 调整音乐音调和效果
        if (musicSource != null && musicSource.clip != null)
        {
            // 调整音调 - 压力大时音乐音调升高
            musicSource.pitch = Mathf.Lerp(lowStressMusicPitch, highStressMusicPitch, playerStressLevel);
        }
        
        // 调整混音器效果
        if (mainMixer != null)
        {
            mainMixer.SetFloat("LowPassCutoff", Mathf.Lerp(22000, 5000, playerStressLevel * 0.7f));
            mainMixer.SetFloat("Reverb", Mathf.Lerp(0, 1500, playerStressLevel * 0.5f));
        }
        
        // 调整环境音量
        if (ambientSource != null)
        {
            // 压力大时环境声音变得更明显
            ambientSource.volume = Mathf.Lerp(baseAmbientVolume, baseAmbientVolume * 1.3f, playerStressLevel);
        }
    }
    
    public void PlayItemPickupSound(Vector3 position)
    {
        if (itemPickupSounds != null && itemPickupSounds.Length > 0)
        {
            PlayRandomClipAtPoint(itemPickupSounds, position);
            Debug.Log("播放物品拾取音效");
        }
        else
        {
            Debug.LogWarning("物品拾取音效未设置！");
        }
    }

    public void PlayHeartBeatSound(bool isChasing)
{
    if (heartbeatSource != null && heartbeatSounds != null && heartbeatSounds.Length > 0)
    {
        if (isChasing)
        {
            // Start heartbeat if not already playing
            if (!heartbeatSource.isPlaying)
            {
                heartbeatSource.clip = heartbeatSounds[Random.Range(0, heartbeatSounds.Length)];
                heartbeatSource.loop = true;
                heartbeatSource.volume = 0.8f; // 固定的音量
                heartbeatSource.pitch = 1.2f;  // 固定的音调
                heartbeatSource.Play();
                Debug.Log("开始播放追逐心跳音效");
            }
        }
        else
        {
            // Stop heartbeat if ghost is no longer chasing
            if (heartbeatSource.isPlaying)
            {
                heartbeatSource.Stop();
                Debug.Log("停止心跳音效");
            }
        }
    }
    else
    {
        Debug.LogWarning("心跳音效或音源未设置！");
    }
}

    public void PlayGhostDetectSound(Vector3 position)
    {
        Debug.Log("尝试播放鬼发现声音");

        if (ghostDetectSounds != null && ghostDetectSounds.Length > 0)
        {
            // 选择一个声音并直接播放（不使用随机方法，确保声音播放）
            AudioClip clip = ghostDetectSounds[0];
            AudioSource.PlayClipAtPoint(clip, position, 1.0f);
            Debug.Log($"播放鬼发现音效: {clip.name}");

            // 播放触觉反馈
            if (roarHaptic != null)
            {
                roarplayer.clip = roarHaptic;
                roarplayer.Play(Controller.Both);
            }
            else
            {
                Debug.LogWarning("鬼的触觉反馈未设置！");
            }
        }
        else
        {
            Debug.LogWarning("鬼发现音效未设置！");
        }
    }
    
    public void PlayDoorSound(Vector3 position)
    {
        if (doorSounds != null && doorSounds.Length > 0)
        {
            PlayRandomClipAtPoint(doorSounds, position);
            Debug.Log("播放门声音");
            
            if (doorHaptic != null)
            {
                Debug.Log("触发门打开的触觉反馈");
                doorplayer.clip = doorHaptic;
                doorplayer.Play(Controller.Both);
            }
            else
            {
                Debug.LogWarning("门的触觉反馈未设置！");
            }
        }
        else
        {
            Debug.LogWarning("门音效未设置！");
        }
    }
    
    public void PlayJumpscareSound()
    {
        if (jumpscaresSounds != null && jumpscaresSounds.Length > 0)
        {
            AudioClip clip = jumpscaresSounds[Random.Range(0, jumpscaresSounds.Length)];
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, 1.0f);
            Debug.Log("播放惊吓音效");
            
            // 暂停其他音乐
            if (musicSource != null) musicSource.Pause();
            if (ambientSource != null) ambientSource.volume *= 0.3f;
            
            // 几秒后恢复音乐
            StartCoroutine(ResumeAudioAfterJumpscare(3.0f));
        }
        else
        {
            Debug.LogWarning("惊吓音效未设置！");
        }
    }
    
    private IEnumerator ResumeAudioAfterJumpscare(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (musicSource != null) musicSource.UnPause();
        if (ambientSource != null) ambientSource.volume = baseAmbientVolume;
    }
    
    private void PlayRandomClipAtPoint(AudioClip[] clips, Vector3 position, float volume = 1.0f)
    {
        if (clips == null || clips.Length == 0) return;
        
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}