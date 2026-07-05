using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CatchDetector: detecta cuando el enemigo atrapa al jugador (Parte 5 - Mecánicas).
///
/// Cada frame mide la distancia entre el jugador y CADA enemigo de la escena. Si
/// alguno se acerca lo suficiente, avisa al GameManager para mostrar la derrota.
///
/// Busca al jugador por nombre ("Player") y a los enemigos por componente
/// (enemyLogic), así soporta cualquier cantidad de enemigos en la escena.
/// </summary>
public class CatchDetector : MonoBehaviour
{
    [Tooltip("Distancia (en metros) a la que el enemigo te atrapa.")]
    public float distanciaCaptura = 1.6f;

    private Transform jugador;
    private List<enemyLogic> enemigos = new List<enemyLogic>();
    private bool atrapado = false;

    private void Update()
    {
        if (atrapado) return;

        // Busca al jugador (aparece después de generarse el nivel).
        if (jugador == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) jugador = p.transform;
        }

        // Busca a todos los enemigos de la escena (puede haber más de uno).
        if (enemigos.Count == 0)
        {
            enemigos.AddRange(FindObjectsByType<enemyLogic>(FindObjectsSortMode.None));
            if (enemigos.Count == 0) return; // todavía no se generaron
        }

        if (jugador == null) return;

        foreach (var logicaEnemigo in enemigos)
        {
            if (logicaEnemigo == null) continue; // por si algún enemigo fue destruido

            // El enemigo solo es una amenaza cuando ya despertó (se teletransportó
            // lejos y anda cazando). Mientras está Dormant/Spawn, lo ignoramos para
            // no "atrapar" al jugador al inicio, dentro del ascensor.
            bool enemigoActivo = logicaEnemigo.CurrentState != enemyState.Dormant
                && logicaEnemigo.CurrentState != enemyState.Spawn;
            if (!enemigoActivo) continue;

            if (Vector3.Distance(jugador.position, logicaEnemigo.transform.position) <= distanciaCaptura)
            {
                atrapado = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.PerderNivel();
                return;
            }
        }
    }
}