using UnityEngine;

/// <summary>
/// ExitZone: zona de salida "llega y ganas" (Parte 5 - Mecánicas).
///
/// Para niveles que NO son de cuadrícula (como el Nivel 3, un mapa real).
/// Cuando el jugador entra en esta zona, completa el nivel. Como el Nivel 3 es
/// el último, el GameManager mostrará la victoria.
///
/// USO: crea un Cube, ponlo donde está la meta del mapa, y agrégale este script.
/// El script lo convierte solo en un portal verde atravesable.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    private bool activado = false;

    private void Start()
    {
        // Zona INVISIBLE en el juego (la puerta del mapa es la pista visual).
        // En el editor el cubo sí se ve, para poder colocarlo sobre la puerta.
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        // El collider debe ser trigger para poder atravesarlo/tocarlo.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activado) return;
        if (other.GetComponentInParent<SimplePlayer>() == null) return;

        activado = true;
        Debug.Log("🏁 ¡Llegaste a la salida del Nivel 3!");
        if (GameManager.Instance != null)
            GameManager.Instance.CompletarNivel(); // último nivel -> victoria
        else
            Debug.LogWarning("No hay GameManager en la escena.");
    }
}
