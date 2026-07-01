// enemyLogic.cs
// Máquina de estados completa del enemigo para Backrooms.
// El enemigo comienza DESACTIVADO en la escena (SetActive false desde el editor).
// El enemySpawner lo activa cuando el NavMesh está listo.

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(enemyVision))]
[RequireComponent(typeof(enemyAudio))]
public class enemyLogic : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Referencias")]
    public Transform player;

    [Header("Nodos de patrulla (opcional)")]
    [SerializeField] private Transform[] patrolNodes;

    [Header("Velocidades")]
    [SerializeField] private float patrolSpeed      = 2.5f;
    [SerializeField] private float chaseSpeed       = 5f;
    [SerializeField] private float investigateSpeed = 3f;

    [Header("Tiempos")]
    [SerializeField] private float searchDuration  = 10f;
    [SerializeField] private float stalkDuration   = 4f;
    [SerializeField] private float chaseDurationMax= 20f;
    [SerializeField] private float vanishDuration  = 1.2f;
    [SerializeField] private float spawnDuration   = 1.2f;

    [Header("Agresividad")]
    [SerializeField] private float aggressionRate = 0.005f;
    [SerializeField] private float aggressionMax  = 3f;

    // ── Estado público ───────────────────────────────────────────────────────
    public enemyState CurrentState { get; private set; } = enemyState.Dormant;
    public float      Aggression   { get; private set; } = 0f;

    // ── Componentes ──────────────────────────────────────────────────────────
    private NavMeshAgent  agent;
    private enemyVision   vision;
    private enemyAudio    audio;
    private Renderer[]    renderers;

    // ── Variables internas ───────────────────────────────────────────────────
    private float   stateTimer;
    private float   chaseTimer;
    private Vector3 investigateTarget;
    private Vector3 lastKnownPlayerPos;
    private int     patrolIndex = 0;
    private EnemyNoiseLevel playerNoise = EnemyNoiseLevel.Walking;

    // ── Awake / OnEnable ─────────────────────────────────────────────────────

    private void Awake()
    {
        agent     = GetComponent<NavMeshAgent>();
        vision    = GetComponent<enemyVision>();
        audio     = GetComponent<enemyAudio>();
        renderers = GetComponentsInChildren<Renderer>();

        // El agente empieza desactivado; lo activa el spawner una vez haya NavMesh
        agent.enabled = false;
    }

    // ── Update principal ─────────────────────────────────────────────────────

    private void Update()
    {
        if (!agent.enabled) return;

        Aggression = Mathf.Min(Aggression + aggressionRate * Time.deltaTime, aggressionMax);

        switch (CurrentState)
        {
            case enemyState.Patrol:      UpdatePatrol();      break;
            case enemyState.Stalk:       UpdateStalk();       break;
            case enemyState.Investigate: UpdateInvestigate(); break;
            case enemyState.Chase:       UpdateChase();       break;
            case enemyState.Search:      UpdateSearch();      break;
            case enemyState.Spawn:       UpdateSpawn();       break;
        }
    }

    // ── Estados ──────────────────────────────────────────────────────────────

    private void UpdateSpawn()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) SetState(enemyState.Patrol);
    }

    private void UpdatePatrol()
    {
        agent.speed = patrolSpeed;

        // ¿Ve al jugador? → perseguir
        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        // ¿Escucha al jugador? → investigar
        if (audio.CanHearPlayer(player, playerNoise))
        {
            investigateTarget = player.position;
            SetState(enemyState.Investigate);
            return;
        }

        // ¿Jugador en rango de acecho? → pequeña probabilidad de acechar
        if (vision.IsPlayerInStalkRange(player) && Random.value < 0.002f)
        {
            SetState(enemyState.Stalk);
            return;
        }

        MoveToNextPatrolNode();
    }

    private void UpdateStalk()
    {
        // Gira lentamente hacia el jugador sin moverse
        if (player != null)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), 1.5f * Time.deltaTime);
        }

        stateTimer -= Time.deltaTime;

        // Si ve al jugador directamente → perseguir
        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        // Si el jugador lo mira fijamente → desaparecer (mecánica Backrooms)
        if (IsPlayerLookingAtEnemy())
        {
            SetState(enemyState.Vanish);
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

        // Llegó al destino → patrullar
        if (!agent.pathPending && agent.remainingDistance < 1.2f)
            SetState(enemyState.Patrol);
    }

    private void UpdateChase()
    {
        agent.speed = chaseSpeed + Aggression * 0.4f;

        if (player != null)
        {
            agent.SetDestination(player.position);
            lastKnownPlayerPos = player.position;
        }

        chaseTimer += Time.deltaTime;

        bool lostSight = !vision.CanSeePlayer(player);
        bool tooFar    = vision.DistanceTo(player) > 30f;
        bool timedOut  = chaseTimer >= chaseDurationMax;

        if (lostSight || tooFar || timedOut)
        {
            chaseTimer = 0f;
            // Va a la última posición conocida y busca
            investigateTarget = lastKnownPlayerPos;
            SetState(enemyState.Search);
        }
    }

    private void UpdateSearch()
    {
        agent.speed = investigateSpeed;
        stateTimer -= Time.deltaTime;

        // ¿Ve al jugador durante la búsqueda? → volver a perseguir
        if (vision.CanSeePlayer(player))
        {
            lastKnownPlayerPos = player.position;
            SetState(enemyState.Chase);
            return;
        }

        if (stateTimer <= 0f)
        {
            float roll = Random.value;
            if (roll < 0.5f)      SetState(enemyState.Patrol);
            else if (roll < 0.75f) SetState(enemyState.Investigate);
            else                   SetState(enemyState.Vanish);
        }
    }

    // ── Transición de estados ────────────────────────────────────────────────

    public void SetState(enemyState newState)
    {
        CurrentState = newState;
        stateTimer   = 0f;
        audio.StopAudio();

        switch (newState)
        {
            case enemyState.Dormant:
                agent.enabled = false;
                gameObject.SetActive(false);
                break;

            case enemyState.Spawn:
                stateTimer = spawnDuration;
                agent.isStopped = false;
                StartCoroutine(FadeRenderers(0f, 1f, spawnDuration));
                audio.PlaySpawn();
                break;

            case enemyState.Patrol:
                agent.isStopped = false;
                audio.PlayPatrol();
                // Si hay nodos asignados, ir al primero; si no, deambular
                if (patrolNodes != null && patrolNodes.Length > 0)
                    agent.SetDestination(patrolNodes[patrolIndex].position);
                else
                    WanderNearby();
                break;

            case enemyState.Stalk:
                agent.isStopped = true;
                stateTimer = stalkDuration + Random.Range(-1f, 2f);
                audio.PlayStalk();
                break;

            case enemyState.Investigate:
                agent.isStopped = false;
                agent.SetDestination(investigateTarget);
                break;

            case enemyState.Chase:
                agent.isStopped = false;
                chaseTimer = 0f;
                audio.PlayChase();
                break;

            case enemyState.Search:
                agent.isStopped = false;
                stateTimer = searchDuration;
                agent.SetDestination(lastKnownPlayerPos);
                break;

            case enemyState.Vanish:
                StartCoroutine(VanishCoroutine());
                break;
        }
    }

    // ── Helpers de movimiento ────────────────────────────────────────────────

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

    /// <summary>Busca un punto aleatorio en el NavMesh cercano para deambular.</summary>
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

    // ── Coroutines ───────────────────────────────────────────────────────────

    private IEnumerator VanishCoroutine()
    {
        agent.isStopped = true;
        audio.PlayVanish();
        yield return StartCoroutine(FadeRenderers(1f, 0f, vanishDuration));
        agent.isStopped = false;
        SetState(enemyState.Dormant);
    }

    private IEnumerator FadeRenderers(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / duration);
            SetAlpha(alpha);
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Color c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>Llamado por enemySpawner después de posicionar al enemigo.</summary>
    public void ActivateFromSpawn()
    {
        gameObject.SetActive(true);
        agent.enabled = true;
        SetState(enemyState.Spawn);
    }

    public void ForceChase()  => SetState(enemyState.Chase);
    public void ForceVanish() => SetState(enemyState.Vanish);
    public void ForcePatrol() => SetState(enemyState.Patrol);

    public void SetPlayerNoise(EnemyNoiseLevel level) => playerNoise = level;
    public void AddAggression(float amount) =>
        Aggression = Mathf.Min(Aggression + amount, aggressionMax);
}