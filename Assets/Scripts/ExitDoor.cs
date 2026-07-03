using UnityEngine;

/// <summary>
/// ExitDoor: controla el portal de salida sellado con llave (Parte 5 - Mecánicas).
///
/// Es un trigger alrededor del portal. Cuando el jugador lo toca:
///   - Si tiene la llave -> avanza al siguiente nivel.
///   - Si NO tiene la llave -> muestra un aviso. El portal es sólido, así que
///     no puede pasar hasta conseguir la llave.
///
/// Lo coloca automáticamente el ExitSpawner; no hay que ponerlo a mano.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitDoor : MonoBehaviour
{
    private bool yaActivado = false;

    private void OnTriggerEnter(Collider other)
    {
        if (yaActivado) return;

        // ¿Fue el jugador quien tocó el portal?
        if (other.GetComponentInParent<SimplePlayer>() == null) return;

        if (GameManager.Instance != null && GameManager.Instance.PuedeAbrirPortal())
        {
            yaActivado = true;
            Debug.Log("🔑 ¡Portal activado! Avanzando al siguiente nivel...");
            GameManager.Instance.CompletarNivel();
        }
        else
        {
            Debug.Log("🔒 El portal está sellado. Busca la llave dorada.");
            if (GameHUD.Instance != null)
                GameHUD.Instance.MostrarAviso("El portal esta sellado. Necesitas la llave.");
        }
    }
}
