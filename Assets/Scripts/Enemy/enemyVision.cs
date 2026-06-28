// enemyVision.cs
// Sistema de visión del enemigo: cono de visión + raycast para detectar paredes.
// Requiere que el jugador esté en la capa "Player".

using UnityEngine;

public class enemyVision : MonoBehaviour
{
    [Header("Parámetros de visión")]
    [SerializeField] private float viewRadius      = 25f;
    [SerializeField, Range(0f, 360f)]
    private float             viewAngle       = 90f;
    [SerializeField] private LayerMask        playerMask;
    [SerializeField] private LayerMask        obstacleMask;

    [Header("Acecho — rango ampliado sin ángulo")]
    [SerializeField] private float stalkRadius = 40f;   // Detecta al jugador para acechar

    // ──────────────────────────────────────────────────────────────────────────
    // API pública
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>¿El jugador está dentro del cono de visión Y sin obstáculos?</summary>
    public bool CanSeePlayer(Transform player)
    {
        if (player == null) return false;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float   distToPlayer = Vector3.Distance(transform.position, player.position);

        if (distToPlayer > viewRadius)           return false;
        if (Vector3.Angle(transform.forward, dirToPlayer) > viewAngle * 0.5f) return false;

        // Raycast para paredes
        if (Physics.Raycast(transform.position, dirToPlayer, distToPlayer, obstacleMask))
            return false;

        return true;
    }

    /// <summary>¿El jugador está dentro del radio de acecho (sin ángulo)?</summary>
    public bool IsPlayerInStalkRange(Transform player)
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= stalkRadius;
    }

    /// <summary>Distancia actual al jugador (-1 si no existe).</summary>
    public float DistanceTo(Transform player)
    {
        if (player == null) return -1f;
        return Vector3.Distance(transform.position, player.position);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Debug — dibuja el cono en el editor
    // ──────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 leftBoundary  = DirFromAngle(-viewAngle * 0.5f);
        Vector3 rightBoundary = DirFromAngle( viewAngle * 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary  * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, stalkRadius);
    }

    private Vector3 DirFromAngle(float angleDeg)
    {
        angleDeg += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleDeg * Mathf.Deg2Rad), 0,
                           Mathf.Cos(angleDeg * Mathf.Deg2Rad));
    }
#endif
}