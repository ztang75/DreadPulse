using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostAI : MonoBehaviour
{
    public enum GhostState
    {
        Patrolling,
        Chasing,
        Searching
    }
    
    [Header("AI设置")]
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float runSpeed = 3.5f;
    [SerializeField] private float detectPlayerRadius = 10f;
    [SerializeField] private float chaseRadius = 15f;
    [SerializeField] private float catchDistance = 1.5f;
    [SerializeField] private float searchDuration = 5f;
    
    [Header("动态调整")]
    [SerializeField] private float maxSpeedIncrease = 1.0f;
    [SerializeField] private float maxDetectionIncrease = 5.0f;
    
    private Transform player;
    private NavMeshAgent agent;
    private GhostState currentState;
    private Animator animator;
    private Vector3 lastKnownPlayerPosition;
    private GhostPatrol patrolBehavior;
    
    // 保存原始值用于动态调整
    private float baseDetectionRadius;
    private float baseWalkSpeed;
    private float baseRunSpeed;
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        patrolBehavior = GetComponent<GhostPatrol>();
        
        // 保存基础值以便后续动态调整
        baseDetectionRadius = detectPlayerRadius;
        baseWalkSpeed = walkSpeed;
        baseRunSpeed = runSpeed;
    }
    
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        currentState = GhostState.Patrolling;
        
        // 设置初始移动速度
        agent.speed = walkSpeed;
        
        // 监听巡逻行为中的玩家检测事件
        if (patrolBehavior != null)
        {
            patrolBehavior.onPlayerDetected += OnPlayerDetectedByPatrol;
        }
    }
    
    private void Update()
    {
        if (GameManager.Instance.gameOver || GameManager.Instance.gameWon) return;
        
        // 如果处于巡逻状态，巡逻行为由 GhostPatrol 脚本处理
        if (currentState == GhostState.Patrolling)
            return;
        
        // 获取与玩家的距离
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // 检查是否抓住玩家
        if (distanceToPlayer < catchDistance)
        {
            CatchPlayer();
            return;
        }
        
        // 向适应性系统报告接近度
        if (distanceToPlayer < detectPlayerRadius * 1.5f)
        {
            float proximity = Mathf.Clamp01(distanceToPlayer / (detectPlayerRadius * 1.5f));
            AdaptiveSystem.Instance.OnGhostNearby(proximity);
        }
        
        // AI状态机逻辑
        switch (currentState)
        {
            case GhostState.Chasing:
                HandleChasing(distanceToPlayer);
                break;
                
            case GhostState.Searching:
                HandleSearching();
                break;
        }
    }
    
    private void OnPlayerDetectedByPatrol()
    {
        // 从巡逻模式转换到追逐模式
        currentState = GhostState.Chasing;
        agent.speed = runSpeed;
        
        // 播放发现玩家的音效
        AudioManager.Instance?.PlayGhostDetectSound(transform.position);
        AudioManager.Instance?.PlayHeartBeatSound(true);
        
        // 更新动画
        UpdateAnimation();
    }
    
    private void HandleChasing(float distanceToPlayer)
    {
        // 追逐玩家
        agent.SetDestination(player.position);

        // 添加这一行来确保鬼魂始终面向玩家
        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));
        transform.Rotate(0, 180, 0); // 添加这行，旋转180度
        
        // 记录玩家最后位置，用于可能的搜索
        lastKnownPlayerPosition = player.position;
        
        // 如果玩家跑出追逐范围，切换到搜索状态
        if (distanceToPlayer > chaseRadius)
        {
            StartSearching();
        }
        
        // 检查是否能看到玩家
        if (!CanSeePlayer())
        {
            // 已失去视线，但继续向最后已知位置移动
            // 如果已接近最后位置但看不到玩家，切换到搜索状态
            float distanceToLastPos = Vector3.Distance(transform.position, lastKnownPlayerPosition);
            if (distanceToLastPos < 2f)
            {
                StartSearching();
            }
        }
    }
    
    private void HandleSearching()
    {
        // 搜索逻辑由搜索协程处理
        // 这个方法可以用于添加其他搜索行为，如环顾四周等
    }
    
    private void StartSearching()
    {
        if (currentState == GhostState.Searching) return;
        
        currentState = GhostState.Searching;
        agent.speed = walkSpeed;
        
        // 更新动画
        UpdateAnimation();
        
        // 停止所有协程，以防有正在运行的
        StopAllCoroutines();
        
        // 启动搜索协程
        StartCoroutine(SearchForPlayer());
    }
    
    private IEnumerator SearchForPlayer()
    {
        // 移动到玩家最后已知位置
        agent.SetDestination(lastKnownPlayerPosition);
        
        // 等待到达目的地
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            // 在搜索过程中，随时检查是否能重新发现玩家
            if (CanSeePlayer())
            {
                currentState = GhostState.Chasing;
                agent.speed = runSpeed;
                AudioManager.Instance?.PlayHeartBeatSound(true);
                UpdateAnimation();
                yield break;
            }
            yield return null;
        }
        
        // 搜索若干秒
        float searchTimeLeft = searchDuration;
        while (searchTimeLeft > 0)
        {
            // 在原地搜索时旋转，环顾四周
            transform.Rotate(0, 120 * Time.deltaTime, 0);
            
            // 检查是否重新发现玩家
            if (CanSeePlayer())
            {
                currentState = GhostState.Chasing;
                agent.speed = runSpeed;
                UpdateAnimation();
                yield break;
            }
            
            searchTimeLeft -= Time.deltaTime;
            yield return null;
        }
        
        // 搜索结束，未发现玩家，返回巡逻状态
        ReturnToPatrol();
    }
    
    private void ReturnToPatrol()
    {
        currentState = GhostState.Patrolling;
        AudioManager.Instance?.PlayHeartBeatSound(false);
        
        // 通知巡逻行为组件恢复巡逻
        if (patrolBehavior != null)
        {
            patrolBehavior.ReturnToPatrol();
        }
        
        // 更新动画
        UpdateAnimation();
    }
    
    private bool CanSeePlayer()
    {
        if (player == null) return false;
    
    float distanceToPlayer = Vector3.Distance(transform.position, player.position);
    
    // 检查距离
    if (distanceToPlayer > detectPlayerRadius)
        return false;
        
    // 检查视线方向（放宽角度限制）
    Vector3 directionToPlayer = (player.position - transform.position).normalized;
    float angle = Vector3.Angle(transform.forward, directionToPlayer);
    if (angle > 120f) // 从90度增加到120度，提供更宽的视野
        return false;
    
    // 近距离检测 - 如果非常近，忽略角度检查
    if (distanceToPlayer < detectPlayerRadius * 0.3f)
    {
        // 使用多高度射线检测
        return CheckPlayerVisibilityWithMultipleRays(directionToPlayer, distanceToPlayer);
    }
    
    // 正常视线检测 - 使用多高度射线
    return CheckPlayerVisibilityWithMultipleRays(directionToPlayer, distanceToPlayer);
}

private bool CheckPlayerVisibilityWithMultipleRays(Vector3 direction, float distance)
{
    // 从不同高度发射射线
    float[] heightOffsets = { 0.1f, 0.5f, 1.0f, 1.5f, 2.0f };
    
    foreach (float heightOffset in heightOffsets)
    {
        Vector3 rayStart = transform.position + new Vector3(0, heightOffset, 0);
        Debug.DrawRay(rayStart, direction * distance, Color.blue, 0.1f);
        
        RaycastHit hit;
        if (Physics.Raycast(rayStart, direction, out hit, distance))
        {
            if (hit.transform == player)
            {
                return true;
            }
        }
    }
    
    return false;
    }
    
    private void CatchPlayer()
    {
        // 抓住玩家，游戏结束
        GameManager.Instance.GameOver();
        
        // 停止移动
        agent.isStopped = true;
        
        // 播放抓到玩家的动画
        if (animator != null)
        {
            animator.SetTrigger("Catch");
        }
    }
    
    private void UpdateAnimation()
    {
        if (animator == null) return;
        
        switch (currentState)
        {
            case GhostState.Patrolling:
                animator.SetBool("IsMoving", true);
                animator.SetBool("IsChasing", false);
                break;
                
            case GhostState.Chasing:
                animator.SetBool("IsMoving", false);
                animator.SetBool("IsChasing", true);
                break;
                
            case GhostState.Searching:
                animator.SetBool("IsMoving", true);
                animator.SetBool("IsChasing", false);
                break;
        }
    }
    
    public void AdjustBehavior(float playerStressLevel)
    {
        // 基于玩家压力水平调整AI参数
        detectPlayerRadius = baseDetectionRadius + (maxDetectionIncrease * playerStressLevel);
        walkSpeed = baseWalkSpeed + (maxSpeedIncrease * 0.5f * playerStressLevel);
        runSpeed = baseRunSpeed + (maxSpeedIncrease * playerStressLevel);
        
        // 更新NavMeshAgent的速度
        if (agent != null)
        {
            if (currentState == GhostState.Chasing)
            {
                agent.speed = runSpeed;
            }
            else
            {
                agent.speed = walkSpeed;
            }
        }
    }
    
    // 用于调试可视化
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectPlayerRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
        
        // 显示最后已知的玩家位置
        if (lastKnownPlayerPosition != Vector3.zero && currentState == GhostState.Searching)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(lastKnownPlayerPosition, 0.5f);
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
        }
    }
}