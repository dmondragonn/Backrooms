
using System.Collections;
using UnityEngine;

public class HorrorDirector : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Referencias")]
    [SerializeField] private enemyLogic    enemy;
    [SerializeField] private enemySpawner  spawner;
    [SerializeField] private Transform     player;

    [Header("Luces ambientales (opcionales)")]
    [SerializeField] private Light[] sceneLights;

    [Header("Umbrales de estrés")]
    [Tooltip("Tiempo sin amenaza antes de forzar un spawn")]
    [SerializeField] private float calmThreshold         = 120f; // 2 minutos
    [Tooltip("Tiempo mínimo de respiro entre encuentros")]
    [SerializeField] private float restPeriodMin         = 20f;
    [SerializeField] private float restPeriodMax         = 45f;
    [Tooltip("Distancia máxima al enemigo que cuenta como encuentro cercano")]
    [SerializeField] private float encounterDistance     = 18f;

    [Header("Agresividad del director")]
    [SerializeField] private float aggressionBoostOnCalmEnd = 0.5f;

    // ──────────────────────────────────────────────────────────────────────────
    // Estado interno
    // ──────────────────────────────────────────────────────────────────────────

    private float timeSinceLastEncounter = 0f;
    private float restTimer              = 0f;
    private bool  inRestPeriod           = false;

    // ──────────────────────────────────────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        StartCoroutine(DirectorLoop());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Loop principal del director (evalúa cada 5 segundos)
    // ──────────────────────────────────────────────────────────────────────────

    private IEnumerator DirectorLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            EvaluateSituation();
        }
    }

    private void Update()
    {
        TrackEncounterTime();

        if (inRestPeriod)
        {
            restTimer -= Time.deltaTime;
            if (restTimer <= 0f) EndRestPeriod();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Evaluación de situación
    // ──────────────────────────────────────────────────────────────────────────

    private void EvaluateSituation()
    {
        if (inRestPeriod) return;

        enemyState state = enemy.CurrentState;
        float dist       = Vector3.Distance(player.position, enemy.transform.position);

        // ── El jugador lleva demasiado tiempo tranquilo → forzar aparición ──
        if (timeSinceLastEncounter >= calmThreshold &&
            state == enemyState.Dormant)
        {
            Debug.Log("[HorrorDirector] Calma excesiva. Forzando spawn.");
            enemy.AddAggression(aggressionBoostOnCalmEnd);
            spawner.ForceSpawn();
            timeSinceLastEncounter = 0f;
            return;
        }

        // ── Persecución muy larga → ordenar desaparecer para dar respiro ──
        if (state == enemyState.Chase && timeSinceLastEncounter > 30f)
        {
            Debug.Log("[HorrorDirector] Persecución muy larga. Forzando desaparición.");
            enemy.ForceVanish();
            StartRestPeriod();
            return;
        }

        // ── Jugador en rango pero enemigo en patrulla → forzar acecho ──
        if (state == enemyState.Patrol && dist < encounterDistance * 1.5f)
        {
            if (Random.value < 0.3f)
            {
                Debug.Log("[HorrorDirector] Forzando estado Stalk para generar tensión.");
                enemy.SetState(enemyState.Stalk);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Seguimiento de encuentros
    // ──────────────────────────────────────────────────────────────────────────

    private void TrackEncounterTime()
    {
        if (enemy == null) return;

        float dist       = Vector3.Distance(player.position, enemy.transform.position);
        bool  isEncounter = dist < encounterDistance &&
                            enemy.CurrentState != enemyState.Dormant &&
                            enemy.CurrentState != enemyState.Vanish;

        if (isEncounter)
            timeSinceLastEncounter = 0f;
        else
            timeSinceLastEncounter += Time.deltaTime;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Periodo de respiro
    // ──────────────────────────────────────────────────────────────────────────

    private void StartRestPeriod()
    {
        inRestPeriod = true;
        restTimer    = Random.Range(restPeriodMin, restPeriodMax);
        Debug.Log($"[HorrorDirector] Periodo de respiro: {restTimer:F0}s");
    }

    private void EndRestPeriod()
    {
        inRestPeriod = false;
        Debug.Log("[HorrorDirector] Fin del respiro. El terror regresa.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Eventos ambientales — llama esto desde enemyLogic o externamente
    // ──────────────────────────────────────────────────────────────────────────

    public void TriggerLightFlicker()
    {
        if (sceneLights == null || sceneLights.Length == 0) return;
        StartCoroutine(FlickerLights());
    }

    private IEnumerator FlickerLights()
    {
        int flickers = Random.Range(3, 8);
        for (int i = 0; i < flickers; i++)
        {
            foreach (var l in sceneLights) l.enabled = false;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            foreach (var l in sceneLights) l.enabled = true;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.3f));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // API pública — accesible desde otros sistemas (puertas, trampas, triggers)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Un sonido externo fue emitido en worldPos. Informa al enemigo.</summary>
    public void ReportNoise(Vector3 worldPos)
    {
        if (enemy.CurrentState == enemyState.Patrol ||
            enemy.CurrentState == enemyState.Stalk)
        {
            // Hacemos que el enemigo investigue el sonido
            enemy.SetState(enemyState.Investigate);
        }
    }
}