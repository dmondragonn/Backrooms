// enemyAudio.cs
// Controla los sonidos del enemigo y expone el sistema de audición.
// Adjunta este script al mismo GameObject que enemyLogic.

using UnityEngine;

public class enemyAudio : MonoBehaviour
{
    [Header("Clips de audio")]
    [SerializeField] private AudioClip clipPatrol;       // Pasos lentos
    [SerializeField] private AudioClip clipChase;        // Pasos rápidos / respiración
    [SerializeField] private AudioClip clipStalk;        // Silencio tenso / leve murmullo
    [SerializeField] private AudioClip clipSpawn;        // Sonido al aparecer
    [SerializeField] private AudioClip clipVanish;       // Sonido al desaparecer
    [SerializeField] private AudioClip clipDistantScream;// Evento raro — grito lejano

    [Header("Audición del enemigo")]
    [Tooltip("Radio en que escucha al jugador corriendo")]
    [SerializeField] private float hearingRadiusRun   = 30f;
    [Tooltip("Radio en que escucha al jugador caminando")]
    [SerializeField] private float hearingRadiusWalk  = 12f;
    [Tooltip("Radio en que escucha al jugador quieto")]
    [SerializeField] private float hearingRadiusIdle  =  3f;

    private AudioSource audioSource;

    // ──────────────────────────────────────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1f; // Sonido 3D
        audioSource.rolloffMode  = AudioRolloffMode.Linear;
        audioSource.maxDistance  = 60f;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Audición pública
    // ──────────────────────────────────────────────────────────────────────────

    public enum PlayerNoiseLevel { Idle, Walking, Running }

    /// <summary>
    /// Devuelve true si el enemigo puede escuchar al jugador según
    /// su nivel de ruido y la distancia actual.
    /// </summary>
    public bool CanHearPlayer(Transform player, PlayerNoiseLevel noise)
    {
        if (player == null) return false;

        float dist = Vector3.Distance(transform.position, player.position);

        float radius = noise switch
        {
            PlayerNoiseLevel.Running => hearingRadiusRun,
            PlayerNoiseLevel.Walking => hearingRadiusWalk,
            _                        => hearingRadiusIdle
        };

        return dist <= radius;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Reproducción
    // ──────────────────────────────────────────────────────────────────────────

    public void PlayPatrol()      => PlayLooped(clipPatrol);
    public void PlayChase()       => PlayLooped(clipChase);
    public void PlayStalk()       => PlayLooped(clipStalk);
    public void PlaySpawn()       => PlayOneShot(clipSpawn);
    public void PlayVanish()      => PlayOneShot(clipVanish);
    public void PlayDistantScream() => PlayOneShot(clipDistantScream);

    public void StopAudio()
    {
        if (audioSource.isPlaying) audioSource.Stop();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ──────────────────────────────────────────────────────────────────────────

    private void PlayLooped(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        audioSource.clip   = clip;
        audioSource.loop   = true;
        audioSource.Play();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}