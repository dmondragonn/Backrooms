// enemyLogic.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(enemyVision))]
[RequireComponent(typeof(enemyAudio))]
public class enemyLogic : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;

    [Header("Nodos de patrulla (opcional)")]
    [SerializeField] private Transform[] patrolNodes;

    [Header("Velocidades")]
    [SerializeField] private float patrolSpeed      = 2.5f;
    [SerializeField] private float chaseSpeed       = 3.8f; // Ligeramente menor que moveSpeed del jugador (4f)
    [SerializeField] private float investigateSpeed = 3f;

    [Header("Tiempos")]
    [SerializeField] private float searchDuration  = 10f;
    [SerializeField] private float stalkDuration   = 4f;
    [SerializeField] private float chaseDurationMax= 20f;
    [SerializeField] private float spawnDuration   = 1.2f;

    [Header("Agresividad")]
    [SerializeField] private float aggressionRate = 0.005f;
    [SerializeField] private float aggressionMax  = 3f;

    public enemyState CurrentState { get; private set; } = enemyState.Dormant;
    public float      Aggression   { get; private set; } = 0f;

    private NavMeshAgent agent;
    private enemyVision  vision;
    private enemyAudio   audioComp;

    private float   stateTimer;
    private float   chaseTimer;
    private Vector3 investigateTarget;
    private Vector3 lastKnownPlayerPos;
    private int     patrolIndex = 0;
    private EnemyNoiseLevel playerNoise = EnemyNoiseLevel.Walking;

    private void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        vision = GetComponent<enemyVision>();
        audioComp = GetComponent<enemyAudio>();
        agent.enabled = false;
    }

    private void Update()
    {
        if (!agent.enabled) return;

        Aggression = Mathf.Min(Aggression + aggressionRate * Time.deltaTime, aggressionMax);

        switch (CurrentState)
        {
            case enemyState.Spawn:       UpdateSpawn();       break;
            case enemyState.Patrol:      UpdatePatrol();      break;
            case enemyState.Stalk:       UpdateStalk();       break;
            case enemyState.Investigate: UpdateInvestigate(); break;
            case enemyState.Chase:       UpdateChase();       break;
            case enemyState.Search:      UpdateSearch();      break;
        }

        audioComp.UpdateFootsteps(agent.velocity.magnitude);
    }

    private void UpdateSpawn()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) SetState(enemyState.Patrol);
    }

    private void UpdatePatrol()
    {
        agent.speed = patrolSpeed;

        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        if (audioComp.CanHearPlayer(player, playerNoise))
        {
            investigateTarget = player.position;
            SetState(enemyState.Investigate);
            return;
        }

        if (vision.IsPlayerInStalkRange(player) && Random.value < 0.002f)
        {
            SetState(enemyState.Stalk);
            return;
        }

        MoveToNextPatrolNode();
    }

    private void UpdateStalk()
    {
        if (player != null)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), 1.5f * Time.deltaTime);
        }

        stateTimer -= Time.deltaTime;

        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        // Mecánica Backrooms: si el jugador lo mira → se teletransporta
        if (IsPlayerLookingAtEnemy())
        {
            TeleportNearPlayer();
            SetState(enemyState.Patrol);
            return;
        }

        if (stateTimer <= 0f) SetState(enemyState.Patrol);
    }

    private void UpdateInvestigate()
    {
        agent.speed = investigateSpeed;

        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 1.2f)
            SetState(enemyState.Patrol);
    }

    private void UpdateChase()
    {
        agent.speed = chaseSpeed + Aggression * 0.3f;

        if (player != null)
        {
            agent.SetDestination(player.position);
            lastKnownPlayerPos = player.position;
        }

        chaseTimer += Time.deltaTime;
        if (chaseTimer >= chaseDurationMax)
        {
            chaseTimer = 0f;
            investigateTarget = lastKnownPlayerPos;
            SetState(enemyState.Search);
        }
    }

    private void UpdateSearch()
    {
        agent.speed = investigateSpeed;
        stateTimer -= Time.deltaTime;

        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        if (stateTimer <= 0f)
        {
            // Nunca desaparece: siempre vuelve a patrullar o investigar
            if (Random.value < 0.6f) SetState(enemyState.Patrol);
            else                     SetState(enemyState.Investigate);
        }
    }

    public void SetState(enemyState newState)
    {
        CurrentState = newState;
        stateTimer   = 0f;

        switch (newState)
        {
            case enemyState.Dormant:
                // Solo se usa en el primer frame antes del spawn
                if (agent.enabled) agent.isStopped = true;
                break;

            case enemyState.Spawn:
                stateTimer = spawnDuration;
                agent.isStopped = false;
                break;

            case enemyState.Patrol:
                agent.isStopped = false;
                if (patrolNodes != null && patrolNodes.Length > 0)
                    agent.SetDestination(patrolNodes[patrolIndex].position);
                else
                    WanderNearby();
                break;

            case enemyState.Stalk:
                agent.isStopped = true;
                stateTimer = stalkDuration + Random.Range(-1f, 2f);
                break;

            case enemyState.Investigate:
                agent.isStopped = false;
                agent.SetDestination(investigateTarget);
                break;

            case enemyState.Chase:
                agent.isStopped = false;
                chaseTimer = 0f;
                break;

            case enemyState.Search:
                agent.isStopped = false;
                stateTimer = searchDuration;
                agent.SetDestination(lastKnownPlayerPos);
                break;

            case enemyState.Vanish:
                // En lugar de desaparecer para siempre, se teletransporta y sigue
                TeleportNearPlayer();
                SetState(enemyState.Patrol);
                break;
        }
    }

    private void MoveToNextPatrolNode()
    {
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            if (patrolNodes != null && patrolNodes.Length > 0)
            {
                patrolIndex = Random.Range(0, patrolNodes.Length);
                agent.SetDestination(patrolNodes[patrolIndex].position);
            }
            else
            {
                WanderNearby();
            }
        }
    }

    private void WanderNearby()
    {
        Vector3 randomDir = Random.insideUnitSphere * 15f;
        randomDir += transform.position;
        randomDir.y = transform.position.y;
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private bool IsPlayerLookingAtEnemy()
    {
        if (player == null) return false;
        Vector3 toEnemy = (transform.position - player.position).normalized;
        return Vector3.Dot(player.forward, toEnemy) > 0.85f;
    }

    /// <summary>Teletransporta al enemigo a un punto fuera del FOV del jugador.</summary>
    private void TeleportNearPlayer()
    {
        if (player == null) return;

        for (int i = 0; i < 30; i++)
        {
            float   angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   dist  = Random.Range(12f, 28f);
            Vector3 cand  = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 6f, NavMesh.AllAreas)) continue;

            // Evitar spawnear justo delante del jugador
            Vector3 toPoint = (hit.position - player.position).normalized;
            if (Vector3.Dot(player.forward, toPoint) > 0.4f) continue;

            agent.enabled = false;
            transform.position = hit.position;
            agent.enabled = true;
            return;
        }

        // Fallback sin restricción de ángulo
        for (int i = 0; i < 10; i++)
        {
            float   angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   dist  = Random.Range(12f, 28f);
            Vector3 cand  = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 8f, NavMesh.AllAreas)) continue;
            agent.enabled = false;
            transform.position = hit.position;
            agent.enabled = true;
            return;
        }
    }

    public void ActivateFromSpawn()
    {
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("[enemyLogic] No se pudo activar el enemigo porque no está sobre un NavMesh válido.");
            return;
        }
        agent.isStopped = false;
        SetState(enemyState.Spawn);
    }

    public void ForceChase()  => SetState(enemyState.Chase);
    public void ForceVanish() => SetState(enemyState.Vanish);
    public void ForcePatrol() => SetState(enemyState.Patrol);

    public void SetPlayerNoise(EnemyNoiseLevel level) => playerNoise = level;
    public void AddAggression(float amount) =>
        Aggression = Mathf.Min(Aggression + amount, aggressionMax);
}