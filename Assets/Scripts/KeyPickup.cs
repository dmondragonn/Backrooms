using UnityEngine;

/// <summary>
/// KeyPickup: la llave que el jugador debe recoger (Parte 5 - Mecánicas).
///
/// Gira para llamar la atención. Cuando el jugador la toca, le avisa al
/// GameManager (tieneLlave = true) y desaparece.
///
/// La coloca automáticamente el ExitSpawner; no hay que ponerla a mano.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    [Tooltip("Velocidad de giro de la llave (grados por segundo).")]
    public float velocidadGiro = 90f;

    private void Update()
    {
        // Gira sobre su eje para que se vea "recogible".
        transform.Rotate(Vector3.up, velocidadGiro * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // ¿Fue el jugador quien la tocó?
        if (other.GetComponentInParent<SimplePlayer>() == null) return;

        if (GameManager.Instance != null)
            GameManager.Instance.RecogerLlave();

        Destroy(gameObject); // la llave desaparece al recogerla
    }
}
