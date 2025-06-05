using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostPatrol : MonoBehaviour
{
    [Header("巡逻设置")]
    [SerializeField] private float minWanderDistance = 20f;  // 增大最小随机巡逻距离
    [SerializeField] private float maxWanderDistance = 50f;  // 增大最大随机巡逻距离
    [SerializeField] private float waitAtPointTime = 2f;     // 到达目标点后等待时间
    [SerializeField] private float maxSearchTime = 30f;      // 增加最大搜索时间
    [SerializeField] private bool usePreferredDirection = true;  // 使用偏向性方向
    [SerializeField] private bool restrictToLevelY = true;   // 限制在当前Y平面上寻找点
    
    [Header("玩家检测")]
    [SerializeField] private float detectionRadius = 10f;   // 玩家检测半径
    [SerializeField] private float fieldOfViewAngle = 90f;  // 视野角度
    [SerializeField] private Transform playerTransform;     // 玩家位置引用
    [SerializeField] private LayerMask detectionLayers;     // 检测层（包括玩家和障碍物）
    
    [Header("调试")]
    [SerializeField] private bool showGizmos = true;       // 显示调试可视化
    [SerializeField] private bool logNavMeshInfo = true;   // 是否记录导航网格信息
    
    // 内部状态
    private NavMeshAgent navAgent;
    private Vector3 currentDestination;
    private bool isWaiting = false;
    private bool isChasing = false;
    private float searchTimer = 0f;
    private Vector3 lastDirection = Vector3.zero;  // 上一次的移动方向
    private int failedAttemptsCounter = 0;         // 连续失败尝试计数
    private float yLevelOffset = 0.1f;             // Y轴偏移量，避免卡在地形上

    // 事件 - 可用于触发其他系统（如音效）
    public System.Action onPlayerDetected;
    public System.Action onReachedPatrolPoint;
    
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }
    
    private void Start()
    {
        // 自动获取玩家引用（如果未指定）
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
        
        // 检查NavMesh状态
        CheckNavMeshStatus();
        
        // 开始第一次巡逻
        SetRandomDestination();
    }
    
    private void CheckNavMeshStatus()
    {
        if (!logNavMeshInfo) return;
        
        NavMeshHit hit;
        bool onNavMesh = NavMesh.SamplePosition(transform.position, out hit, 1.0f, NavMesh.AllAreas);
        
        if (!onNavMesh)
        {
            Debug.LogWarning($"Ghost ({gameObject.name}) 没有放置在NavMesh上! 当前位置: {transform.position}");
        }
        else
        {
            Debug.Log($"Ghost ({gameObject.name}) 位于NavMesh上，区域类型: {hit.mask}");
        }
        
        NavMeshPath path = new NavMeshPath();
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * 30f;
        NavMesh.SamplePosition(randomPoint, out hit, 30f, NavMesh.AllAreas);
        
        bool canFindPath = NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path);
        Debug.Log($"NavMesh路径测试: {(canFindPath ? "成功" : "失败")}, 路径状态: {path.status}, 路径点数: {path.corners.Length}");
    }
    
    private void Update()
    {
        if (isChasing)
    {
        // 已进入追逐模式 - 这部分将由 GhostAI 脚本处理
        return;
    }
    
    // 检测玩家
    CheckForPlayer();
    
    // 管理巡逻行为
    ManagePatrolState();
    
    // 添加这部分代码修正朝向问题
    if (navAgent.velocity.magnitude > 0.1f)
    {
        // 在有速度时，手动调整朝向
        Vector3 direction = navAgent.velocity.normalized;
        
        // 可能需要反转方向，取决于您的模型
        direction = -direction; // 如果模型是倒着走，添加这行；如果模型正着走但您想让它倒着走，删除这行
        
        // 计算目标旋转
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
    }
    
    private void ManagePatrolState()
    {
        // 如果正在等待，不需要检查是否到达
        if (isWaiting)
            return;
            
        // 检查是否到达当前目标点或者是否无法到达目的地
        if (!navAgent.pathPending && 
            (navAgent.remainingDistance <= navAgent.stoppingDistance || 
             searchTimer >= maxSearchTime))
        {
            // 重置搜索定时器
            searchTimer = 0f;
            
            // 检查是否达到目标点或超时
            if (navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                // 成功到达
                failedAttemptsCounter = 0;
                
                // 触发到达事件
                onReachedPatrolPoint?.Invoke();
                
                // 到达目标点，在新目标前等待
                StartCoroutine(WaitAtPoint());
            }
            else
            {
                // 搜索超时，直接尝试新的目标点
                failedAttemptsCounter++;
                
                if (failedAttemptsCounter > 3)
                {
                    Debug.LogWarning($"Ghost ({gameObject.name}) 连续多次无法到达目标点，可能NavMesh有问题");
                    failedAttemptsCounter = 0;
                }
                
                SetRandomDestination();
            }
        }
        else
        {
            // 更新搜索时间
            searchTimer += Time.deltaTime;
        }
    }
    
    private IEnumerator WaitAtPoint()
    {
        isWaiting = true;
        
        // 停止移动
        navAgent.isStopped = true;
        
        // 等待指定时间
        yield return new WaitForSeconds(waitAtPointTime);
        
        // 继续移动并设置新的目标点
        navAgent.isStopped = false;
        isWaiting = false;
        
        // 设置新的随机目标
        SetRandomDestination();
    }
    
    private void SetRandomDestination()
    {
        // 尝试找到有效的随机位置
        int attempts = 0;
        const int maxAttempts = 30;  // 增加尝试次数
        
        while (attempts < maxAttempts)
        {
            // 生成随机方向
            Vector3 randomDirection;
            
            if (usePreferredDirection && lastDirection != Vector3.zero && Random.Range(0f, 1f) < 0.7f)
            {
                // 70%概率沿着之前的大致方向继续，但加入一些随机偏移
                float randomAngle = Random.Range(-90f, 90f);
                randomDirection = Quaternion.Euler(0, randomAngle, 0) * lastDirection;
            }
            else
            {
                // 完全随机方向
                randomDirection = new Vector3(
                    Random.Range(-1f, 1f),
                    0f,  // Y坐标设置为0，避免上下随机
                    Random.Range(-1f, 1f)
                ).normalized;
            }
            
            // 应用随机距离
            float distance = Random.Range(minWanderDistance, maxWanderDistance);
            Vector3 randomPoint = transform.position + randomDirection * distance;
            
            // 如果需要限制在当前Y平面，保持Y值
            if (restrictToLevelY)
            {
                randomPoint.y = transform.position.y + yLevelOffset;
            }
            
            // 尝试在导航网格上找到最近的点
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, distance, NavMesh.AllAreas))
            {
                // 找到有效位置，检查是否可以到达
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        // 找到有效的完整路径
                        currentDestination = hit.position;
                        navAgent.SetDestination(currentDestination);
                        
                        // 记录方向用于下次寻路
                        lastDirection = (currentDestination - transform.position).normalized;
                        lastDirection.y = 0; // 确保y方向为0
                        
                        if (logNavMeshInfo)
                        {
                            Debug.Log($"Ghost设置新目标点: {hit.position}, 距离: {Vector3.Distance(transform.position, hit.position):F1}m");
                        }
                        return;
                    }
                }
            }
            
            attempts++;
        }
        
        // 如果经过多次尝试仍然找不到有效路径，尝试周围近距离随机点
        Debug.LogWarning($"Ghost ({gameObject.name}) 在 {maxAttempts} 次尝试后无法找到有效导航路径，尝试短距离点");
        
        // 尝试在小范围内寻找点
        for (int i = 0; i < 10; i++)
        {
            Vector3 closePoint = transform.position + Random.insideUnitSphere * (minWanderDistance * 0.5f);
            closePoint.y = transform.position.y + yLevelOffset; // 保持在同一平面
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(closePoint, out hit, minWanderDistance, NavMesh.AllAreas))
            {
                currentDestination = hit.position;
                navAgent.SetDestination(currentDestination);
                Debug.Log($"Ghost使用应急短距离点: {hit.position}");
                return;
            }
        }
        
        // 最后手段：尝试原地不动几秒，然后再次尝试
        Debug.LogError($"Ghost ({gameObject.name}) 无法找到任何有效路径点，可能是NavMesh配置问题");
        StartCoroutine(EmergencyWait());
    }
    
    private IEnumerator EmergencyWait()
    {
        yield return new WaitForSeconds(3f);
        SetRandomDestination();
    }

    private void CheckForPlayer()
    {
        if (playerTransform == null) return;

    Vector3 directionToPlayer = playerTransform.position - transform.position;
    float distanceToPlayer = directionToPlayer.magnitude;

    // 基本距离检查
    if (distanceToPlayer <= detectionRadius)
    {
        // 水平角度检查
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle <= fieldOfViewAngle * 0.5f)
        {
            // 使用多条射线进行检测，覆盖不同高度
            bool playerDetected = CheckRaycastAtMultipleHeights(directionToPlayer, distanceToPlayer);
            
            if (playerDetected)
            {
                OnPlayerDetected();
            }
        }
        // 增加一个近距离全方位检测（当玩家非常近时，不考虑视角限制）
        else if (distanceToPlayer < detectionRadius * 0.3f) // 比如在检测半径的30%内
        {
            // 近距离检测，使用多射线但不考虑角度
            bool playerDetected = CheckRaycastAtMultipleHeights(directionToPlayer, distanceToPlayer);
            
            if (playerDetected)
            {
                Debug.Log("Ghost 感知到非常近的玩家！");
                OnPlayerDetected();
            }
        }
    }
    }

    private bool CheckRaycastAtMultipleHeights(Vector3 direction, float distance)
{
    // 射线起点高度列表（相对于鬼魂位置）
    float[] heightOffsets = { 0.1f, 0.5f, 1.0f, 1.5f, 2.0f };
    
    foreach (float heightOffset in heightOffsets)
    {
        Vector3 rayStart = transform.position + new Vector3(0, heightOffset, 0);
        Debug.DrawRay(rayStart, direction.normalized * distance, Color.red, 0.1f); // 在场景中显示射线，便于调试
        
        RaycastHit hit;
        if (Physics.Raycast(rayStart, direction.normalized, out hit, distance, detectionLayers))
        {
            if (hit.transform == playerTransform)
            {
                return true; // 任何一条射线检测到玩家都返回true
            }
        }
    }
    
    return false; // 所有射线都未检测到玩家
}

    private void OnPlayerDetected()
    {
        // [保留原代码，没有变化]
        if (isChasing) return;
        
        isChasing = true;
        Debug.Log("Ghost 发现玩家！开始追逐...");
        
        StopAllCoroutines();
        onPlayerDetected?.Invoke();
    }
    
    public void ReturnToPatrol()
    {
        isChasing = false;
        SetRandomDestination();
    }
    
    // [保留原始的Gizmos代码，没有变化]
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = Color.red;
        Vector3 rightDir = Quaternion.Euler(0, fieldOfViewAngle * 0.5f, 0) * transform.forward;
        Vector3 leftDir = Quaternion.Euler(0, -fieldOfViewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, rightDir * detectionRadius);
        Gizmos.DrawRay(transform.position, leftDir * detectionRadius);
        
        if (currentDestination != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(currentDestination, 0.5f);
            Gizmos.DrawLine(transform.position, currentDestination);
        }
    }
}