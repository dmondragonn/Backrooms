// enemySpawner.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class enemySpawner : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Camera    playerCamera;

    [Header("Distancias de spawn")]
    [SerializeField] private float spawnDistanceMin = 5f;
    [SerializeField] private float spawnDistanceMax = 25f;
    [SerializeField] private int   candidatePoints  = 40;

    [Header("Tiempos")]
    [SerializeField] private float firstSpawnDelay = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;

    private enemyLogic logic;

    private void Start()
    {
        logic = GetComponentInParent<enemyLogic>();
        StartCoroutine(FirstSpawn());
    }

    private IEnumerator FirstSpawn()
    {
        // Esperar NavMesh
        while (!MazeGenerator.NavMeshReady)
            yield return new WaitForSeconds(0.5f);

        yield return new WaitForSeconds(firstSpawnDelay);

        // Reintentar hasta encontrar al jugador (se crea en runtime por MazeGenerator)
        for (int i = 0; i < 20; i++)
        {
            if (player == null)
            {
                var p = GameObject.Find("Player");
                if (p != null)
                {
                    player       = p.transform;
                    playerCamera = p.GetComponentInChildren<Camera>();
                }
            }
            if (player != null) break;
            yield return new WaitForSeconds(0.5f);
        }

        if (player == null)
        {
            Debug.LogWarning("[enemySpawner] No se encontró al jugador tras varios intentos.");
            yield break;
        }

        if (!FindSpawnPosition(out Vector3 pos))
        {
            if (!TryGetAnyNavMeshPoint(out pos))
            {
                Debug.LogWarning("[enemySpawner] No se encontró posición de spawn y no hay NavMesh utilizable. Abortando spawn.");
                yield break;
            }

            Debug.LogWarning("[enemySpawner] No se encontró posición de spawn cerca del jugador. Usando otro punto válido del NavMesh.");
        }

        NavMeshAgent agent = transform.parent.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        transform.parent.position = pos;
        if (agent != null) agent.enabled = true;

        if (logic != null)
        {
            logic.player = player;
            logic.ActivateFromSpawn();
            Debug.Log($"[enemySpawner] Primer spawn en {pos}");
        }
    }

    private bool FindSpawnPosition(out Vector3 result)
    {
        result = Vector3.zero;
        var candidates = new List<Vector3>();

        for (int i = 0; i < candidatePoints; i++)
        {
            float   angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   dist  = Random.Range(spawnDistanceMin, spawnDistanceMax);
            Vector3 cand  = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            if (!NavMesh.SamplePosition(cand, out NavMeshHit hit, 6f, NavMesh.AllAreas)) continue;
            if (playerCamera != null && IsInFrustum(hit.position)) continue;

            candidates.Add(hit.position);
        }

        if (candidates.Count > 0)
        {
            result = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        // Fallback sin restricción de frustum
        for (int i = 0; i < 15; i++)
        {
            float   angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   dist  = Random.Range(spawnDistanceMin, spawnDistanceMax);
            Vector3 cand  = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            if (NavMesh.SamplePosition(cand, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        return false;
    }

    private bool TryGetAnyNavMeshPoint(out Vector3 result)
    {
        result = Vector3.zero;
        var triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices == null || triangulation.indices == null || triangulation.indices.Length < 3)
            return false;

        int triangleCount = triangulation.indices.Length / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            int i0 = triangulation.indices[i * 3];
            int i1 = triangulation.indices[i * 3 + 1];
            int i2 = triangulation.indices[i * 3 + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0) continue;

            Vector3 centroid = (triangulation.vertices[i0] + triangulation.vertices[i1] + triangulation.vertices[i2]) / 3f;
            if (playerCamera != null && IsInFrustum(centroid)) continue;

            result = centroid;
            return true;
        }

        return false;
    }

    private bool IsInFrustum(Vector3 worldPos)
    {
        if (playerCamera == null) return false;
        Vector3 vp = playerCamera.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f;
    }

    public void ForceSpawn()
    {
        StopAllCoroutines();
        StartCoroutine(FirstSpawn());
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || player == null) return;
        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, spawnDistanceMin);
        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(player.position, Vector3.up, spawnDistanceMax);
    }
#endif
}
