using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Level3EnemySetup: activa al enemigo (Captain Clark) en el Nivel 3 (Parte 5/6).
///
/// El Nivel 3 es un mapa real que NO prepara el NavMesh ni conecta al enemigo.
/// Este script, unos segundos después de que el mapa se genera:
///   1. Bakea el NavMesh sobre la geometría (para que el enemigo pueda moverse).
///   2. Avisa que el NavMesh está listo (MazeGenerator.NavMeshReady).
///   3. Conecta al jugador con el enemigo (enemySpawner y enemyLogic).
///   4. Crea el detector de captura (la derrota).
///
/// USO: pon este script en el MISMO objeto que tiene el Level3Generator.
/// (Se le agrega solo un NavMeshSurface, que es lo que bakea el NavMesh.)
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class Level3EnemySetup : MonoBehaviour
{
    [Tooltip("Segundos de espera para que el mapa y el jugador terminen de crearse.")]
    public float retraso = 2f;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(retraso);

        // 1 y 2. Bakear el NavMesh y avisar que está listo.
        var surface = GetComponent<NavMeshSurface>();

        // El mapa del Nivel 3 es GIGANTE (>400 m). Con celdas finas el bake falla
        // por "excessive number of tiles". Subimos el tamaño de voxel y de tile
        // para que el NavMesh se genere bien sobre todo el mapa.
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // pisos/paredes sólidos, no las mallas
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.4f;   // celdas más grandes = menos tiles
        surface.overrideTileSize = true;
        surface.tileSize = 512;     // tiles más grandes = muchos menos tiles

        surface.BuildNavMesh();
        MazeGenerator.NavMeshReady = true; // el enemigo espera este flag
        Debug.Log("[Level3] NavMesh bakeado. El enemigo puede activarse.");

        // 3. Conectar al jugador con el enemigo.
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var cam = player.GetComponentInChildren<Camera>();

            var spawner = FindFirstObjectByType<enemySpawner>();
            if (spawner != null)
            {
                spawner.player = player.transform;
                spawner.playerCamera = cam;
            }

            var logic = FindFirstObjectByType<enemyLogic>();
            if (logic != null) logic.player = player.transform;

            Debug.Log("[Level3] Jugador conectado al enemigo.");
        }
        else
        {
            Debug.LogWarning("[Level3] No se encontró al 'Player'.");
        }

        // 4. Crear el detector de captura (derrota -> pantalla y reintentar nivel).
        new GameObject("CatchDetector").AddComponent<CatchDetector>();
    }
}
