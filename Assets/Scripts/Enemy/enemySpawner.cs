
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class enemySpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject  enemyPrefab;
    [SerializeField] public Transform   player;
    [SerializeField] public Camera      playerCamera;

    [Header("Parámetros de spawn")]
    [SerializeField] private float spawnRadiusMin   = 20f;
    [SerializeField] private float spawnRadiusMax   = 40f;
    [SerializeField] private int   candidatePoints  = 20;   // Puntos evaluados por intento
    [SerializeField] private float spawnIntervalMin = 30f;
    [SerializeField] private float spawnIntervalMax = 90f;
    [SerializeField] private bool  showDebugGizmos  = false;

    // Pesos de tipo de spawn (deben sumar 100)
    // 0: esquina  | 1: cuarto cercano | 2: final pasillo
    // 3: detrás jugador | 4: muy cerca (susto)
    private readonly float[] spawnWeights = { 50f, 20f, 15f, 10f, 5f };

    private GameObject activeenemy;
    private float      spawnTimer;

    // ──────────────────────────────────────────────────────────────────────────
    // Init & Loop
    // ──────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        ResetTimer();
        spawnTimer = 5f; // Primer spawn a los 5 segundos
        Debug.Log("[enemySpawner] Iniciado. Primer spawn en 5 segundos");
    }

    private void Update()
    {
        GameObject enemy = transform.parent.gameObject;
        if (enemy.activeSelf && 
            enemy.GetComponent<enemyLogic>().CurrentState != enemyState.Dormant) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            TrySpawn();
            ResetTimer();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Spawn
    // ──────────────────────────────────────────────────────────────────────────

    private void TrySpawn()
    {
        Vector3 spawnPos;
        if (!FindValidSpawnPosition(out spawnPos)) return;

        // El enemigo es el padre de este GameObject
        GameObject enemy = transform.parent.gameObject;
        enemy.transform.position = spawnPos;
        enemy.GetComponent<enemyLogic>().ActivateFromSpawn();
    }   

    /// <summary>
    /// Genera candidatePoints posiciones aleatorias alrededor del jugador,
    /// descarta las visibles y elige una al azar entre las válidas.
    /// Aplica pesos según el tipo de spawn.
    /// </summary>
    private bool FindValidSpawnPosition(out Vector3 result)
    {
        result = Vector3.zero;

        // Decide tipo de spawn según pesos
        int spawnType = ChooseSpawnType();

        switch (spawnType)
        {
            case 0: // Esquina (50%)
                return FindCornerSpawn(out result);
            case 1: // Cuarto cercano (20%)
                return FindRoomSpawn(out result);
            case 2: // Final de pasillo (15%)
                return FindHallwayEndSpawn(out result);
            case 3: // Detrás del jugador (10%)
                return FindBehindPlayerSpawn(out result);
            case 4: // Muy cerca - susto (5%)
                return FindJumpscareSpawn(out result);
            default:
                return FindCornerSpawn(out result);
        }
    }

    private int ChooseSpawnType()
    {
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        for (int i = 0; i < spawnWeights.Length; i++)
        {
            cumulative += spawnWeights[i];
            if (roll < cumulative) return i;
        }

        return 0; // Por defecto: esquina
    }

    // Spawn tipo 0: Esquina
    private bool FindCornerSpawn(out Vector3 result)
    {
        return FindGenericSpawn(out result, spawnRadiusMin, spawnRadiusMax);
    }

    // Spawn tipo 1: Cuarto cercano (más lejos)
    private bool FindRoomSpawn(out Vector3 result)
    {
        return FindGenericSpawn(out result, spawnRadiusMax * 0.7f, spawnRadiusMax * 1.2f);
    }

    // Spawn tipo 2: Final de pasillo (usa dirección del jugador)
    private bool FindHallwayEndSpawn(out Vector3 result)
    {
        result = Vector3.zero;
        
        // Intenta spawnear en dirección hacia donde mira el jugador, pero lejos
        Vector3 forward = player.forward;
        float distance = Random.Range(spawnRadiusMax * 0.8f, spawnRadiusMax * 1.5f);
        
        Vector3 candidate = player.position + forward * distance;
        
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            if (!IsVisibleToCamera(hit.position))
            {
                result = hit.position;
                return true;
            }
        }
        
        // Fallback a spawn genérico
        return FindCornerSpawn(out result);
    }

    // Spawn tipo 3: Detrás del jugador
    private bool FindBehindPlayerSpawn(out Vector3 result)
    {
        result = Vector3.zero;
        
        Vector3 behind = player.position - player.forward * Random.Range(8f, 15f);
        
        if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            if (!IsVisibleToCamera(hit.position))
            {
                result = hit.position;
                return true;
            }
        }
        
        return FindCornerSpawn(out result);
    }

    // Spawn tipo 4: Jumpscare (muy cerca)
    private bool FindJumpscareSpawn(out Vector3 result)
    {
        return FindGenericSpawn(out result, 5f, 12f);
    }

    // Método genérico de spawn
    private bool FindGenericSpawn(out Vector3 result, float minRadius, float maxRadius)
    {
        result = Vector3.zero;
        var validPoints = new List<Vector3>();

        for (int i = 0; i < candidatePoints; i++)
        {
            float   angle   = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   radius  = Random.Range(minRadius, maxRadius);
            Vector3 offset  = new Vector3(Mathf.Cos(angle) * radius, 0f,
                                          Mathf.Sin(angle) * radius);
            Vector3 candidate = player.position + offset;

            // Punto válido en NavMesh?
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            // Visible desde la cámara del jugador?
            if (IsVisibleToCamera(hit.position)) continue;

            validPoints.Add(hit.position);
        }

        if (validPoints.Count == 0) return false;

        result = validPoints[Random.Range(0, validPoints.Count)];
        return true;
    }

    private bool IsVisibleToCamera(Vector3 worldPos)
    {
        if (playerCamera == null) return false;

        Vector3 viewport = playerCamera.WorldToViewportPoint(worldPos);

        // Fuera del frustum
        if (viewport.z < 0 ||
            viewport.x < 0 || viewport.x > 1 ||
            viewport.y < 0 || viewport.y > 1) return false;

        // Hay pared entre la cámara y el punto?
        Vector3 dir  = worldPos - playerCamera.transform.position;
        float   dist = dir.magnitude;
        if (Physics.Raycast(playerCamera.transform.position, dir.normalized, dist))
            return false; // Tapado por geometría → no es visible

        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // API pública — HorrorDirector puede llamar esto directamente
    // ──────────────────────────────────────────────────────────────────────────

    public void ForceSpawn()
    {
        ResetTimer();
        TrySpawn();
    }

    public void Despawnenemy()
    {
        if (activeenemy != null) activeenemy.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void ResetTimer()
    {
        spawnTimer = Random.Range(spawnIntervalMin, spawnIntervalMax);
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || player == null) return;
        
        // Radio mínimo
        Gizmos.color = Color.yellow;
        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.1f);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, spawnRadiusMin);
        
        // Radio máximo
        Gizmos.color = Color.red;
        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, spawnRadiusMax);
    }
#endif
}