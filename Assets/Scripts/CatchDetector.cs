using UnityEngine;

/// <summary>
/// CatchDetector: detecta cuando el enemigo atrapa al jugador (Parte 5 - Mecánicas).
///
/// Cada frame mide la distancia entre el jugador y el enemigo. Si el enemigo se
/// acerca lo suficiente, avisa al GameManager para mostrar la pantalla de derrota.
///
/// Busca los objetos por nombre ("Player" y "Enemy"), así que no depende del
/// código interno de tus compañeros. Lo crea el ExitSpawner automáticamente.
/// </summary>
public class CatchDetector : MonoBehaviour
{
    [Tooltip("Distancia (en metros) a la que el enemigo te atrapa.")]
    public float distanciaCaptura = 1.6f;

    private Transform jugador;
    private Transform enemigo;
    private enemyLogic logicaEnemigo; // para saber si el enemigo ya "despertó"
    private bool atrapado = false;

    private void Update()
    {
        if (atrapado) return;

        // Busca al jugador y al enemigo (aparecen después de generarse el nivel).
        if (jugador == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) jugador = p.transform;
        }
        if (enemigo == null)
        {
            var e = GameObject.Find("Enemy");
            if (e != null)
            {
                enemigo = e.transform;
                logicaEnemigo = e.GetComponent<enemyLogic>();
            }
        }

        // Si todavía no existe alguno, esperamos al siguiente frame.
        if (jugador == null || enemigo == null) return;

        // El enemigo solo es una amenaza cuando ya despertó (se teletransportó
        // lejos y anda cazando). Mientras está Dormant/Spawn, lo ignoramos para
        // no "atrapar" al jugador al inicio, dentro del ascensor.
        bool enemigoActivo = logicaEnemigo != null
            && logicaEnemigo.CurrentState != enemyState.Dormant
            && logicaEnemigo.CurrentState != enemyState.Spawn;
        if (!enemigoActivo) return;

        if (Vector3.Distance(jugador.position, enemigo.position) <= distanciaCaptura)
        {
            atrapado = true;
            if (GameManager.Instance != null)
                GameManager.Instance.PerderNivel();
        }
    }
}
