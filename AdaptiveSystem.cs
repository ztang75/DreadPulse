using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.Android;

public class AdaptiveSystem : MonoBehaviour
{
    public static AdaptiveSystem Instance { get; private set; }
    
    [Header("难度设置")]
    [Range(1f, 3f)]
    [SerializeField] private float difficultyLevel = 1f; // 1-3级难度
    [SerializeField] private float difficultyChangeRate = 0.05f; // 难度变化率
    [SerializeField] private float difficultyDecayRate = 0.025f; // 难度自然衰减率

    [Header("监测参数")]
    [SerializeField] private float headRotationThreshold = 30f; // 每秒钟头部旋转角度阈值
    [SerializeField] private float triggerThreshold = 0.7f;     // 扳机按压力度阈值
    [SerializeField] private float movementThreshold = 0.5f;    // 移动速度阈值

    [Header("事件影响系数")]
    [SerializeField] private float suddenMovementImpact = 0.1f; // 突然移动对难度的影响
    [SerializeField] private float fastRotationImpact = 0.1f;   // 快速旋转对难度的影响
    [SerializeField] private float triggerGripImpact = 0.05f;   // 紧握扳机对难度的影响
    
     [Header("数据记录")]
    [SerializeField] private bool enableDataLogging = true;     // 是否启用数据记录
    [SerializeField] private float dataLogInterval = 0.5f;      // 数据记录间隔（秒）
    [SerializeField] private string logFileName = "adaptive_system_data.csv"; // 日志文件基本名称
    [SerializeField] private bool saveToDesktop = true;         // 是否保存到桌面而非默认位置
    
    private string logFilePath;  // 完整的日志文件路径
    
    // 组件引用
    private AudioManager audioManager;
    private HapticsManager hapticsManager;
    private List<GhostAI> ghosts = new List<GhostAI>();
    
    // 头部旋转跟踪
    private Quaternion lastHeadRotation;
    private Vector3 lastHeadPosition;
    private float rotationAccumulator = 0f;
    private float movementAccumulator = 0f;
    private float timeSinceLastCheck = 0f;
    private float timeSinceLastLog = 0f;
    private const float CHECK_INTERVAL = 0.5f;
    
    // 数据记录
    private List<PlayerBehaviorData> behaviorDataLog = new List<PlayerBehaviorData>();
    private float currentTriggerForce = 0f;
    private float currentRotationSpeed = 0f;
    private float currentMovementSpeed = 0f;
    
    // 玩家行为数据结构
    [System.Serializable]
    public class PlayerBehaviorData
    {
        public float timeStamp;
        public float difficultyLevel;
        public float headRotationSpeed; // 度/秒
        public float movementSpeed;     // 米/秒
        public float triggerForce;      // 0-1
        public Vector3 playerPosition;
        public Quaternion headRotation;

        public override string ToString()
        {
            return $"{timeStamp},{difficultyLevel},{headRotationSpeed},{movementSpeed},{triggerForce}," +
                   $"{playerPosition.x},{playerPosition.y},{playerPosition.z}," +
                   $"{headRotation.x},{headRotation.y},{headRotation.z},{headRotation.w}";
        }

        public static string GetHeaderString()
        {
            return "TimeStamp,DifficultyLevel,HeadRotationSpeed,MovementSpeed,TriggerForce," +
                   "PositionX,PositionY,PositionZ," +
                   "RotationX,RotationY,RotationZ,RotationW";
        }
    }
    
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
        
        // 获取其他系统引用
        audioManager = FindFirstObjectByType<AudioManager>();
        hapticsManager = FindFirstObjectByType<HapticsManager>();
        
        // 找到场景中的所有鬼魂AI
        ghosts.AddRange(FindObjectsByType<GhostAI>(FindObjectsSortMode.None, FindObjectsInactive.Exclude));
        
        // 初始化数据日志
        if (enableDataLogging)
        {
            InitializeDataLog();
        }
    }

    private IEnumerable<T> FindObjectsByType<T>(FindObjectsSortMode none, FindObjectsInactive exclude)
    {
        throw new NotImplementedException();
    }

    private void Start()
    {
        // 获取初始头部旋转和位置数据
        if (Camera.main != null)
        {
            lastHeadRotation = Camera.main.transform.rotation;
            lastHeadPosition = Camera.main.transform.position;
        }
    }
    
    private void Update()
    {
        // 监控玩家行为
        MonitorPlayerBehavior();
        
        // 每隔一段时间检查并应用难度变化
        timeSinceLastCheck += Time.deltaTime;
        if (timeSinceLastCheck >= CHECK_INTERVAL)
        {
            ApplyDifficultyChanges();
            timeSinceLastCheck = 0f;
        }
        
        // 难度随时间自然下降（缓慢回归到基础难度）
        if (difficultyLevel > 1f)
        {
            difficultyLevel = Mathf.Max(1f, difficultyLevel - (difficultyDecayRate * Time.deltaTime));
            AdjustGameParameters();
        }
        
        // 记录数据
        if (enableDataLogging)
        {
            timeSinceLastLog += Time.deltaTime;
            if (timeSinceLastLog >= dataLogInterval)
            {
                LogPlayerData();
                timeSinceLastLog = 0f;
            }
        }
    }
    
    private void MonitorPlayerBehavior()
    {
        if (Camera.main == null) return;
        
        // 1. 监测头部旋转
        Quaternion currentRotation = Camera.main.transform.rotation;
        float rotationDiff = Quaternion.Angle(lastHeadRotation, currentRotation);
        
        rotationAccumulator += rotationDiff;
        lastHeadRotation = currentRotation;
        
        // 计算当前旋转速度（度/秒）
        currentRotationSpeed = rotationDiff / Time.deltaTime;
        
        // 2. 监测头部移动
        Vector3 currentPosition = Camera.main.transform.position;
        float movementDiff = Vector3.Distance(lastHeadPosition, currentPosition);
        
        movementAccumulator += movementDiff;
        lastHeadPosition = currentPosition;
        
        // 计算当前移动速度（米/秒）
        currentMovementSpeed = movementDiff / Time.deltaTime;
        
        // 3. 监测扳机力度
        CheckTriggerForce();
    }
    
    private void CheckTriggerForce()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        
        float maxTriggerValue = 0f;
        
        foreach (var device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
            {
                // 记录最大触发力度
                if (triggerValue > maxTriggerValue)
                {
                    maxTriggerValue = triggerValue;
                }
                
                if (triggerValue > triggerThreshold)
                {
                    float intensity = Mathf.Min(1.0f, triggerValue / triggerThreshold);
                    IncreaseDifficulty(triggerGripImpact * intensity);
                    
                    // 当玩家紧握扳机时触发触觉反馈
                    if (hapticsManager != null)
                    {
                        hapticsManager.TriggerHapticPulse(device, 0.2f * intensity, 0.1f);
                    }
                }
            }
        }
        
        // 更新当前触发器力度
        currentTriggerForce = maxTriggerValue;
    }
    
    private void ApplyDifficultyChanges()
    {
        // 检测是否存在突然的头部旋转
        if (rotationAccumulator > headRotationThreshold)
        {
            float intensity = Mathf.Min(1.0f, rotationAccumulator / (headRotationThreshold * 2));
            IncreaseDifficulty(fastRotationImpact * intensity);
            
            Debug.Log($"检测到快速头部旋转: {rotationAccumulator:F1}度，难度增加: {fastRotationImpact * intensity:F2}");
        }
        
        // 检测是否存在快速移动
        if (movementAccumulator > movementThreshold)
        {
            float intensity = Mathf.Min(1.0f, movementAccumulator / (movementThreshold * 2));
            IncreaseDifficulty(suddenMovementImpact * intensity);
            
            Debug.Log($"检测到快速移动: {movementAccumulator:F2}米，难度增加: {suddenMovementImpact * intensity:F2}");
        }
        
        // 重置计数器
        rotationAccumulator = 0f;
        movementAccumulator = 0f;
        
        // 应用当前难度到游戏参数
        AdjustGameParameters();
    }
    
    private void AdjustGameParameters()
    {
        // 将难度级别（1-3）缩放到0-1范围，以与其他系统兼容
        float normalizedDifficulty = (difficultyLevel - 1f) / 2f;  // 1->0, 3->1
        
        // 1. 通知音频管理器
        if (audioManager != null)
        {
            audioManager.UpdateAudioParameters(normalizedDifficulty);
        }
        
        // 2. 调整鬼魂AI行为
        foreach (var ghost in ghosts)
        {
            if (ghost != null)
            {
                ghost.AdjustBehavior(normalizedDifficulty);
            }
        }
        
        // 3. 更新触觉反馈系统的基础强度
        if (hapticsManager != null)
        {
            hapticsManager.UpdateBaseIntensity(normalizedDifficulty);
        }
        
        // 日志记录
        Debug.Log($"调整游戏难度: {difficultyLevel:F2}/3.0 (归一化: {normalizedDifficulty:F2})");
    }
    
    public void IncreaseDifficulty(float amount)
    {
        float oldLevel = difficultyLevel;
        difficultyLevel = Mathf.Min(3.0f, difficultyLevel + (amount * difficultyChangeRate));
        
        // 只有当难度实际变化时才记录日志
        if (difficultyLevel > oldLevel + 0.01f)
        {
            Debug.Log($"难度提高: {oldLevel:F2} -> {difficultyLevel:F2}");
        }
    }
    
    public void OnItemCollected()
    {
        // 收集物品时短暂增加难度
        IncreaseDifficulty(0.15f);
    }
    
    public void OnGhostNearby(float proximity)
    {
        // 鬼接近时增加难度
        float impact = Mathf.Lerp(0.05f, 0.15f, 1f - proximity);
        IncreaseDifficulty(impact * Time.deltaTime);
    }
    
    public float GetDifficultyLevel()
    {
        return difficultyLevel;
    }
    
    #region 数据记录功能

    
    
    // 添加此方法，确保在应用退出前保存数据
    private void OnApplicationQuit()
    {
        // 确保在应用退出前保存所有数据
        if (enableDataLogging)
        {
        Debug.Log("应用退出前保存数据...");
        SafeFlushDataToFile();
    }
    }

// 添加这个更安全的数据保存方法
    public void SafeFlushDataToFile()
    {
    if (!enableDataLogging || string.IsNullOrEmpty(logFilePath)) return;
    
    try
    {
        // 确保目录存在
        string directory = Path.GetDirectoryName(logFilePath);
        if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // 在Quest Link模式下使用一个更可靠的路径
        string tempPath = logFilePath;
        if (Application.platform == RuntimePlatform.Android)
        {
            tempPath = Path.Combine(Application.temporaryCachePath, Path.GetFileName(logFilePath));
            Debug.Log($"Quest模式数据保存路径: {tempPath}");
        }
        
        using (StreamWriter writer = new StreamWriter(tempPath, true))
        {
            foreach (var data in behaviorDataLog)
            {
                writer.WriteLine(data.ToString());
            }
            writer.Flush(); // 确保写入磁盘
        }
        
        behaviorDataLog.Clear();
        Debug.Log($"数据已成功写入文件: {tempPath}");
    }
    catch (System.Exception e)
    {
        // 更详细的错误信息
        Debug.LogError($"写入数据文件失败: {e.Message}\n路径: {logFilePath}\n堆栈: {e.StackTrace}");
        
        // 备用保存方案 - 保存到临时缓存
        try
        {
            string backupPath = Path.Combine(Application.temporaryCachePath, "backup_" + Path.GetFileName(logFilePath));
            using (StreamWriter writer = new StreamWriter(backupPath, true))
            {
                foreach (var data in behaviorDataLog)
                {
                    writer.WriteLine(data.ToString());
                }
                writer.Flush();
            }
            Debug.Log($"备用数据已保存到: {backupPath}");
        }
        catch (System.Exception backupEx)
        {
            Debug.LogError($"备用保存也失败: {backupEx.Message}");
        }
    }
}

// 修改InitializeDataLog方法，替换现有方法
private void InitializeDataLog()
{
    behaviorDataLog = new List<PlayerBehaviorData>();
    
    // 记录开始时间
    string startTime = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    string fileName = $"adaptive_system_data_{startTime}.csv";
    
    // 针对Quest Link模式优化路径选择
    if (Application.platform == RuntimePlatform.Android)
    {
        // Quest设备上使用缓存路径
        logFilePath = Path.Combine(Application.temporaryCachePath, fileName);
    }
    else if (saveToDesktop && (Application.platform == RuntimePlatform.WindowsPlayer || 
             Application.platform == RuntimePlatform.WindowsEditor))
    {
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string gameFolderPath = Path.Combine(desktopPath, "VR_Horror_Data");
        
        if (!Directory.Exists(gameFolderPath))
        {
            Directory.CreateDirectory(gameFolderPath);
        }
        
        logFilePath = Path.Combine(gameFolderPath, fileName);
    }
    else
    {
        logFilePath = Path.Combine(Application.persistentDataPath, fileName);
    }
    
    // 添加定期自动保存 (每30秒)
    StartCoroutine(AutoSaveRoutine(30f));
    
    // 创建并写入标题行
    try 
    {
        using (StreamWriter writer = new StreamWriter(logFilePath, false))
        {
            writer.WriteLine(PlayerBehaviorData.GetHeaderString());
            writer.Flush();
        }
        Debug.Log($"数据记录已初始化，日志文件保存在: {logFilePath}");
    }
    catch (System.Exception e) 
    {
        Debug.LogError($"初始化数据日志失败: {e.Message}");
    }
}

// 添加自动保存协程
private IEnumerator AutoSaveRoutine(float interval)
{
    while (enableDataLogging)
    {
        yield return new WaitForSeconds(interval);
        if (behaviorDataLog.Count > 0)
        {
            Debug.Log($"执行自动保存，记录 {behaviorDataLog.Count} 条数据...");
            SafeFlushDataToFile();
        }
    }
}

// 修改OnDestroy方法，使用安全的写入方法
private void OnDestroy()
{
    if (enableDataLogging)
    {
        Debug.Log("组件销毁，保存剩余数据...");
        SafeFlushDataToFile();
    }
}
    
    private void LogPlayerData()
    {
        if (!enableDataLogging || Camera.main == null) return;
        
        // 创建新的数据记录
        PlayerBehaviorData data = new PlayerBehaviorData
        {
            timeStamp = Time.time,
            difficultyLevel = difficultyLevel,
            headRotationSpeed = currentRotationSpeed,
            movementSpeed = currentMovementSpeed,
            triggerForce = currentTriggerForce,
            playerPosition = Camera.main.transform.position,
            headRotation = Camera.main.transform.rotation
        };
        
        // 添加到内存日志
        behaviorDataLog.Add(data);
        
        // 立即写入文件
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine(data.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"写入数据文件失败: {e.Message}");
        }
    }
    
    // 获取记录的数据（可供其他系统访问）
    public List<PlayerBehaviorData> GetBehaviorData()
    {
        return new List<PlayerBehaviorData>(behaviorDataLog);
    }
    
    // 强制将内存中的数据写入文件
    public void FlushDataToFile()
    {
        if (!enableDataLogging) return;
        
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                foreach (var data in behaviorDataLog)
                {
                    writer.WriteLine(data.ToString());
                }
            }
            
            // 清除内存中的日志，以避免重复写入
            behaviorDataLog.Clear();
            
            Debug.Log($"数据已成功写入文件: {logFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"写入数据文件失败: {e.Message}");
        }
    }
    
    
    
    // 获取数据文件路径（用于外部访问）
    public string GetDataFilePath()
    {
        return logFilePath;
    }

    
    
    #endregion
    
    #region 开发者UI和调试功能
    
    // 可以添加用于测试的方法，例如手动设置难度
    public void SetDifficultyLevel(float level)
    {
        difficultyLevel = Mathf.Clamp(level, 1f, 3f);
        AdjustGameParameters();
        
        Debug.Log($"手动设置难度为: {difficultyLevel:F2}/3.0");
    }
    
    // 打印当前监控数据到控制台
    public void PrintCurrentMonitoringData()
    {
        Debug.Log($"===== 玩家行为监控数据 =====");
        Debug.Log($"难度级别: {difficultyLevel:F2}/3.0");
        Debug.Log($"头部旋转速度: {currentRotationSpeed:F2} 度/秒");
        Debug.Log($"移动速度: {currentMovementSpeed:F2} 米/秒");
        Debug.Log($"触发器压力: {currentTriggerForce:F2}");
        Debug.Log($"记录的数据点总数: {behaviorDataLog.Count}");
    }
    
    #endregion
}