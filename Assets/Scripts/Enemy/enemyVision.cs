// enemyVision.cs
// Sistema de visión del enemigo: cono de visión + raycast para detectar paredes.

using UnityEngine;

public class enemyVision : MonoBehaviour
{
    [Header("Visión")]
    [SerializeField] public float viewRadius = 20f;
    [SerializeField, Range(0f, 360f)] public float viewAngle = 100f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Acecho")]
    [SerializeField] public float stalkRadius = 30f;

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>¿El jugador está dentro del cono de visión sin obstáculos?</summary>
    public bool CanSeePlayer(Transform player)
    {
        if (player == null) return false;

        Vector3 dir  = (player.position - transform.position).normalized;
        float   dist = Vector3.Distance(transform.position, player.position);

        if (dist > viewRadius) return false;
        if (Vector3.Angle(transform.forward, dir) > viewAngle * 0.5f) return false;

        // Raycast — ¿hay pared en medio?
        if (Physics.Raycast(transform.position + Vector3.up * 1f,
                            dir, dist, obstacleMask)) return false;

        return true;
    }

    /// <summary>¿El jugador está dentro del radio de acecho (sin ángulo)?</summary>
    public bool IsPlayerInStalkRange(Transform player)
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= stalkRadius;
    }

    /// <summary>Distancia actual al jugador.</summary>
    public float DistanceTo(Transform player)
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 left  = DirFromAngle(-viewAngle * 0.5f);
        Vector3 right = DirFromAngle( viewAngle * 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + left  * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * viewRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
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