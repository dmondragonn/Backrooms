// horrorDirector.cs
// Director externo de terror. Controla el ritmo del miedo.
// Coloca este script en un GameObject vacío llamado "HorrorDirector" en la escena.
// Se auto-encuentra el enemigo y el spawner, no necesita referencias manuales.

using System.Collections;
using UnityEngine;

public class horrorDirector : MonoBehaviour
{
    [Header("Referencias (se buscan automáticamente)")]
    private enemyLogic   enemy;
    private enemySpawner spawner;
    private Transform    player;

    [Header("Luces de escena (opcional)")]
    [SerializeField] private Light[] sceneLights;

    [Header("Umbrales")]
    [SerializeField] private float calmThreshold    = 90f;  // Segundos sin encuentro antes de forzar spawn
    [SerializeField] private float encounterDistance = 15f; // Distancia que cuenta como encuentro
    [SerializeField] private float restPeriodMin     = 15f;
    [SerializeField] private float restPeriodMax     = 30f;

    private float timeSinceLastEncounter = 0f;
    private float restTimer              = 0f;
    private bool  inRestPeriod           = false;
    private bool  ready                  = false;

    // ── Init ─────────────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        // Esperar a que el NavMesh esté listo
        while (!MazeGenerator.NavMeshReady)
            yield return new WaitForSeconds(0.5f);

        // Buscar referencias automáticamente
        enemy   = FindFirstObjectByType<enemyLogic>();
        spawner = FindFirstObjectByType<enemySpawner>();

        // Esperar al jugador
        while (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
            yield return new WaitForSeconds(0.5f);
        }

        ready = true;
        Debug.Log("[horrorDirector] Listo. Controlando ritmo de terror.");
        StartCoroutine(DirectorLoop());
    }

    // ── Loop principal (evalúa cada 5 segundos) ───────────────────────────────

    private IEnumerator DirectorLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            if (ready) EvaluateSituation();
        }
    }

    private void Update()
    {
        if (!ready || enemy == null || player == null) return;

        TrackEncounterTime();

        if (inRestPeriod)
        {
            restTimer -= Time.deltaTime;
            if (restTimer <= 0f) EndRestPeriod();
        }
    }

    // ── Evaluación ────────────────────────────────────────────────────────────

    private void EvaluateSituation()
    {
        if (inRestPeriod || enemy == null) return;

        enemyState state = enemy.CurrentState;
        float dist = player != null
            ? Vector3.Distance(player.position, enemy.transform.position)
            : float.MaxValue;

        // Demasiado tiempo sin que pase nada → forzar spawn
        if (timeSinceLastEncounter >= calmThreshold &&
           (state == enemyState.Dormant || !enemy.gameObject.activeSelf))
        {
            Debug.Log("[horrorDirector] Silencio excesivo. Forzando aparición.");
            enemy.AddAggression(0.3f);
            spawner?.ForceSpawn();
            timeSinceLastEncounter = 0f;
            return;
        }

        // Persecución muy larga → dar respiro
        if (state == enemyState.Chase && timeSinceLastEncounter > 25f)
        {
            Debug.Log("[horrorDirector] Persecución larga. Dando respiro.");
            enemy.ForceVanish();
            StartRestPeriod();
            return;
        }

        // Enemigo cerca patrullando → forzar acecho para generar tensión
        if (state == enemyState.Patrol && dist < encounterDistance * 1.5f)
        {
            if (Random.value < 0.25f)
            {
                Debug.Log("[horrorDirector] Forzando Stalk para tensión.");
                enemy.SetState(enemyState.Stalk);
            }
        }

        // Evento raro (2% cada evaluación)
        if (Random.value < 0.02f)
            TriggerRareEvent();
    }

    // ── Rastreo de encuentros ─────────────────────────────────────────────────

    private void TrackEncounterTime()
    {
        if (enemy == null || player == null) return;

        float dist = Vector3.Distance(player.position, enemy.transform.position);
        bool isEncounter = enemy.gameObject.activeSelf &&
                           dist < encounterDistance &&
                           enemy.CurrentState != enemyState.Dormant &&
                           enemy.CurrentState != enemyState.Vanish;

        if (isEncounter) timeSinceLastEncounter = 0f;
        else             timeSinceLastEncounter += Time.deltaTime;
    }

    // ── Periodos de respiro ───────────────────────────────────────────────────

    private void StartRestPeriod()
    {
        inRestPeriod = true;
        restTimer    = Random.Range(restPeriodMin, restPeriodMax);
        Debug.Log($"[horrorDirector] Respiro de {restTimer:F0}s.");
    }

    private void EndRestPeriod()
    {
        inRestPeriod = false;
        Debug.Log("[horrorDirector] Fin del respiro. El terror regresa.");
    }

    // ── Eventos raros ─────────────────────────────────────────────────────────

    private void TriggerRareEvent()
    {
        int pick = Random.Range(0, 4);
        switch (pick)
        {
            case 0:
                StartCoroutine(FlickerLights());
                Debug.Log("[horrorDirector] Evento raro: luces parpadeando.");
                break;
            case 1:
                if (enemy != null) enemy.AddAggression(0.5f);
                Debug.Log("[horrorDirector] Evento raro: pico de agresividad.");
                break;
            case 2:
                spawner?.ForceSpawn();
                Debug.Log("[horrorDirector] Evento raro: spawn forzado.");
                break;
            case 3:
                Debug.Log("[horrorDirector] Evento raro: silencio prolongado.");
                StartRestPeriod();
                break;
        }
    }

    // ── Parpadeo de luces ─────────────────────────────────────────────────────

    private IEnumerator FlickerLights()
    {
        if (sceneLights == null || sceneLights.Length == 0) yield break;

        int flickers = Random.Range(3, 8);
        for (int i = 0; i < flickers; i++)
        {
            foreach (var l in sceneLights) if (l != null) l.enabled = false;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            foreach (var l in sceneLights) if (l != null) l.enabled = true;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
        }
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Un sonido fue emitido en worldPos. El director puede ordenar investigar.</summary>
    public void ReportNoise(Vector3 worldPos)
    {
        if (enemy == null) return;
        if (enemy.CurrentState == enemyState.Patrol ||
            enemy.CurrentState == enemyState.Stalk)
        {
            enemy.SetState(enemyState.Investigate);
        }
    }
}