
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(enemyVision))]
[RequireComponent(typeof(enemyAudio))]
public class enemyLogic : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Referencias")]
    [SerializeField] public Transform player;
    [SerializeField] private Transform[] patrolNodes;

    [Header("Velocidades")]
    [SerializeField] private float patrolSpeed     = 2f;
    [SerializeField] private float chaseSpeed      = 5.5f;
    [SerializeField] private float stalkSpeed      = 0f;    // Se queda quieto
    [SerializeField] private float investigateSpeed= 3f;

    [Header("Tiempos")]
    [SerializeField] private float searchDuration   = 8f;
    [SerializeField] private float stalkDuration    = 5f;
    [SerializeField] private float spawnFadeDuration= 1f;   // Fade in al aparecer
    [SerializeField] private float vanishDuration   = 1.5f; // Fade out al desaparecer
    [SerializeField] private float chaseDurationMax = 25f;  // Persecución máxima

    [Header("Nivel de agresividad")]
    [Tooltip("Empieza en 0 y sube con el tiempo. Afecta velocidad y frecuencia.")]
    [SerializeField] private float aggression       = 0f;
    [SerializeField] private float aggressionRate   = 0.01f;
    [SerializeField] private float aggressionMax    = 3f;

    [Header("Eventos raros")]
    [SerializeField, Range(0f, 1f)]
    private float rareEventChance = 0.02f;            // 2 % por evaluación
    [SerializeField] private float rareEventInterval  = 30f;

    // ──────────────────────────────────────────────────────────────────────────
    // Estado público (leído por HorrorDirector)
    // ──────────────────────────────────────────────────────────────────────────
    public enemyState CurrentState { get; private set; } = enemyState.Dormant;
    public float      Aggression   => aggression;

    // ──────────────────────────────────────────────────────────────────────────
    // Componentes privados
    // ──────────────────────────────────────────────────────────────────────────
    private NavMeshAgent   agent;
    private enemyVision    vision;
    private enemyAudio     enemyAudio;
    private Renderer[]     renderers;

    // ──────────────────────────────────────────────────────────────────────────
    // Variables de estado internas
    // ──────────────────────────────────────────────────────────────────────────
    private int   patrolIndex        = 0;
    private float stateTimer         = 0f;
    private float chaseTimer         = 0f;
    private float rareEventTimer     = 0f;
    private Vector3 investigateTarget;

    // Nivel de ruido del jugador (el jugador debe llamar a SetPlayerNoise)
    private enemyAudio.PlayerNoiseLevel playerNoise = enemyAudio.PlayerNoiseLevel.Idle;

    // ──────────────────────────────────────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        agent      = GetComponent<NavMeshAgent>();
        vision     = GetComponent<enemyVision>();
        enemyAudio = GetComponent<enemyAudio>();
        renderers  = GetComponentsInChildren<Renderer>();
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        vision = GetComponent<enemyVision>();
        enemyAudio = GetComponent<enemyAudio>();
        renderers = GetComponentsInChildren<Renderer>();

        // Desactivar hasta que el NavMesh esté listo
        agent.enabled = false;
        StartCoroutine(WaitForNavMesh());
    }

    private IEnumerator WaitForNavMesh()
    {
        // Espera hasta que haya un NavMesh válido en la posición del enemigo
        while (!UnityEngine.AI.NavMesh.SamplePosition(
            transform.position, out _, 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            yield return new WaitForSeconds(0.5f);
        }

        // NavMesh listo, activar agente
        agent.enabled = true;
        Debug.Log("[EnemyLogic] NavMesh detectado. Enemigo activado.");
        CurrentState = enemyState.Patrol;
        enemyAudio.PlayPatrol();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update principal
    // ──────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        aggression = Mathf.Min(aggression + aggressionRate * Time.deltaTime, aggressionMax);

        // Eventos raros
        rareEventTimer += Time.deltaTime;
        if (rareEventTimer >= rareEventInterval)
        {
            rareEventTimer = 0f;
            if (Random.value <= rareEventChance) TriggerRareEvent();
        }

        switch (CurrentState)
        {
            case enemyState.Dormant:      UpdateDormant();      break;
            case enemyState.Spawn:        UpdateSpawn();        break;
            case enemyState.Patrol:       UpdatePatrol();       break;
            case enemyState.Stalk:        UpdateStalk();        break;
            case enemyState.Investigate:  UpdateInvestigate();  break;
            case enemyState.Chase:        UpdateChase();        break;
            case enemyState.Search:       UpdateSearch();       break;
            case enemyState.Vanish:                             break; // Coroutine
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Estados
    // ──────────────────────────────────────────────────────────────────────────

    // ── DORMANT ──
    private void UpdateDormant()
    {
        // HorrorDirector o enemySpawner nos activa con ActivateFromSpawn()
    }

    // ── SPAWN ──
    private void UpdateSpawn()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) SetState(enemyState.Patrol);
    }

    // ── PATROL ──
    private void UpdatePatrol()
    {
        agent.speed = patrolSpeed + aggression * 0.3f;

        if (vision.CanSeePlayer(player))     { SetState(enemyState.Chase);   return; }
        if (enemyAudio.CanHearPlayer(player, playerNoise))
                                             { InvestigateLastKnownPosition(); return; }
        if (vision.IsPlayerInStalkRange(player) && Random.value < 0.003f)
                                             { SetState(enemyState.Stalk);    return; }

        MoveToNextPatrolNode();
    }

    // ── STALK ──
    private void UpdateStalk()
    {
        agent.speed = stalkSpeed;
        agent.isStopped = true;

        // Gira lentamente hacia el jugador
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), 2f * Time.deltaTime);

        stateTimer -= Time.deltaTime;

        if (vision.CanSeePlayer(player))  { agent.isStopped = false; SetState(enemyState.Chase); return; }

        // Si el jugador lo mira fijamente, desaparece (mecánica de Backrooms)
        if (IsPlayerLookingAtenemy() && stateTimer > 0f)
        {
            agent.isStopped = false;
            SetState(enemyState.Vanish);
            return;
        }

        if (stateTimer <= 0f) { agent.isStopped = false; SetState(enemyState.Patrol); }
    }

    // ── INVESTIGATE ──
    private void UpdateInvestigate()
    {
        agent.speed = investigateSpeed;

        if (vision.CanSeePlayer(player)) { SetState(enemyState.Chase); return; }

        if (!agent.pathPending && agent.remainingDistance < 1f)
            SetState(enemyState.Patrol);
    }

    // ── CHASE ──
    private void UpdateChase()
    {
        agent.speed = chaseSpeed + aggression * 0.5f;
        agent.SetDestination(player.position);
        chaseTimer += Time.deltaTime;

        bool lostSight = !vision.CanSeePlayer(player);
        bool tooFar    = vision.DistanceTo(player) > 28f + aggression * 2f;
        bool timedOut  = chaseTimer >= chaseDurationMax;

        if (lostSight || tooFar || timedOut)
        {
            chaseTimer = 0f;
            ChoosePostChaseState();
        }
    }

    // ── SEARCH ──
    private void UpdateSearch()
    {
        agent.speed = investigateSpeed;
        stateTimer -= Time.deltaTime;

        if (vision.CanSeePlayer(player)) { SetState(enemyState.Chase); return; }

        if (stateTimer <= 0f)
        {
            // Al terminar búsqueda: patrullar o desaparecer
            float rand = Random.value;
            if (rand < 0.6f) SetState(enemyState.Patrol);
            else             SetState(enemyState.Vanish);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Transición de estados
    // ──────────────────────────────────────────────────────────────────────────

    public void SetState(enemyState newState)
    {
        CurrentState = newState;
        stateTimer   = 0f;

        enemyAudio.StopAudio();

        switch (newState)
        {
            case enemyState.Dormant:
                gameObject.SetActive(false);
                break;

            case enemyState.Spawn:
                gameObject.SetActive(true);
                stateTimer = spawnFadeDuration;
                StartCoroutine(FadeRenderers(0f, 1f, spawnFadeDuration));
                enemyAudio.PlaySpawn();
                break;

            case enemyState.Patrol:
                agent.isStopped = false;
                enemyAudio.PlayPatrol();
                break;

            case enemyState.Stalk:
                stateTimer = stalkDuration + Random.Range(-1f, 2f);
                enemyAudio.PlayStalk();
                break;

            case enemyState.Investigate:
                agent.isStopped = false;
                agent.SetDestination(investigateTarget);
                break;

            case enemyState.Chase:
                agent.isStopped = false;
                chaseTimer = 0f;
                enemyAudio.PlayChase();
                break;

            case enemyState.Search:
                stateTimer = searchDuration;
                // Camina hacia la última posición conocida y luego busca alrededor
                agent.SetDestination(player.position);
                break;

            case enemyState.Vanish:
                StartCoroutine(VanishCoroutine());
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers de estado
    // ──────────────────────────────────────────────────────────────────────────

    private void MoveToNextPatrolNode()
    {
        if (patrolNodes == null || patrolNodes.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.8f)
        {
            // Recorre nodos en orden aleatorio
            patrolIndex = Random.Range(0, patrolNodes.Length);
            agent.SetDestination(patrolNodes[patrolIndex].position);
        }
    }

    private void InvestigateLastKnownPosition()
    {
        investigateTarget = player.position;
        SetState(enemyState.Investigate);
    }

    private void ChoosePostChaseState()
    {
        float roll = Random.value;

        if (roll < 0.40f) SetState(enemyState.Search);
        else if (roll < 0.70f) SetState(enemyState.Patrol);
        else                    SetState(enemyState.Vanish);
    }

    /// <summary>¿El jugador está mirando directamente al enemigo?</summary>
    private bool IsPlayerLookingAtenemy()
    {
        if (player == null) return false;
        Vector3 toenemy = (transform.position - player.position).normalized;
        float   dot     = Vector3.Dot(player.forward, toenemy);
        return dot > 0.85f; // ~30° de tolerancia
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Eventos raros
    // ──────────────────────────────────────────────────────────────────────────

    private void TriggerRareEvent()
    {
        int pick = Random.Range(0, 5);
        switch (pick)
        {
            case 0: // Aparece detrás del jugador por un segundo y desaparece
                StartCoroutine(BriefAppearanceBehindPlayer());
                break;
            case 1: // Se queda mirando una pared
                StartCoroutine(StareAtWall());
                break;
            case 2: // Cruza un pasillo rápidamente
                StartCoroutine(DashAcrossHallway());
                break;
            case 3: // Grito lejano
                enemyAudio.PlayDistantScream();
                break;
            case 4: // No aparece durante mucho tiempo (resetea timer de spawn)
                if (CurrentState == enemyState.Dormant)
                    Debug.Log("[enemyLogic] Evento raro: silencio prolongado.");
                break;
        }
    }

    private IEnumerator BriefAppearanceBehindPlayer()
    {
        Vector3 behindPlayer = player.position - player.forward * 2f;
        if (!NavMesh.SamplePosition(behindPlayer, out NavMeshHit hit, 3f, NavMesh.AllAreas)) yield break;

        Vector3 originalPos = transform.position;
        bool    wasActive   = gameObject.activeSelf;

        gameObject.SetActive(true);
        transform.position = hit.position;
        yield return new WaitForSeconds(0.8f);

        transform.position = originalPos;
        if (!wasActive) gameObject.SetActive(false);
    }

    private IEnumerator StareAtWall()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(Random.Range(3f, 7f));
        agent.isStopped = false;
    }

    private IEnumerator DashAcrossHallway()
    {
        float savedSpeed = agent.speed;
        agent.speed = chaseSpeed * 2f;
        yield return new WaitForSeconds(1.5f);
        agent.speed = savedSpeed;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fade de renderizadores
    // ──────────────────────────────────────────────────────────────────────────

    private IEnumerator FadeRenderers(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / duration);
            SetRenderersAlpha(alpha);
            yield return null;
        }
        SetRenderersAlpha(to);
    }

    private IEnumerator VanishCoroutine()
    {
        agent.isStopped = true;
        enemyAudio.PlayVanish();
        yield return FadeRenderers(1f, 0f, vanishDuration);
        agent.isStopped = false;
        SetState(enemyState.Dormant);
    }

    private void SetRenderersAlpha(float alpha)
    {
        foreach (var r in renderers)
        {
            Color c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // API pública — HorrorDirector llama estos métodos
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Activa al enemigo desde posición ya definida por enemySpawner.</summary>
    public void ActivateFromSpawn() => SetState(enemyState.Spawn);

    /// <summary>Fuerza desaparición inmediata.</summary>
    public void ForceVanish() => SetState(enemyState.Vanish);

    /// <summary>Fuerza persecución (director lo ordena).</summary>
    public void ForceChase()  => SetState(enemyState.Chase);

    /// <summary>Informa el nivel de ruido actual del jugador.</summary>
    public void SetPlayerNoise(enemyAudio.PlayerNoiseLevel level) => playerNoise = level;

    /// <summary>Sube el nivel de agresividad manualmente (director).</summary>
    public void AddAggression(float amount) =>
        aggression = Mathf.Min(aggression + amount, aggressionMax);
}