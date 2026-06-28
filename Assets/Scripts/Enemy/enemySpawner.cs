
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class enemySpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject  enemyPrefab;
    [SerializeField] private Transform   player;
    [SerializeField] private Camera      playerCamera;

    [Header("Parámetros de spawn")]
    [SerializeField] private float spawnRadiusMin   = 20f;
    [SerializeField] private float spawnRadiusMax   = 40f;
    [SerializeField] private int   candidatePoints  = 20;   // Puntos evaluados por intento
    [SerializeField] private float spawnIntervalMin = 30f;
    [SerializeField] private float spawnIntervalMax = 90f;

    // Pesos de tipo de spawn (deben sumar 100)
    // 0: esquina  | 1: cuarto cercano | 2: final pasillo
    // 3: detrás jugador | 4: muy cerca (susto)
    private readonly float[] spawnWeights = { 50f, 20f, 15f, 10f, 5f };

    private GameObject activeenemy;
    private float      spawnTimer;

    // ──────────────────────────────────────────────────────────────────────────
    // Init & Loop
    // ──────────────────────────────────────────────────────────────────────────

    private void Start() => ResetTimer();

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
    /// </summary>
    private bool FindValidSpawnPosition(out Vector3 result)
    {
        result = Vector3.zero;
        var validPoints = new List<Vector3>();

        for (int i = 0; i < candidatePoints; i++)
        {
            float   angle   = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   radius  = Random.Range(spawnRadiusMin, spawnRadiusMax);
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
}