// enemyAudio.cs
// Controla sonidos del enemigo y sistema de audición.
// Adjunta al mismo GameObject que enemyLogic.

using UnityEngine;

public enum EnemyNoiseLevel { Idle, Walking, Running }

public class enemyAudio : MonoBehaviour
{
    [Header("Clips de audio (opcionales)")]
    [SerializeField] private AudioClip clipPatrol;
    [SerializeField] private AudioClip clipChase;
    [SerializeField] private AudioClip clipStalk;
    [SerializeField] private AudioClip clipSpawn;
    [SerializeField] private AudioClip clipVanish;
    [SerializeField] private AudioClip clipDistantScream;

    [Header("Radios de audición")]
    [SerializeField] private float hearingRun  = 25f;
    [SerializeField] private float hearingWalk = 10f;
    [SerializeField] private float hearingIdle =  2f;

    [Header("Pasos (Footsteps)")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float baseStepsPerSecond = 2f;   // pasos por segundo A referenceSpeed
    [SerializeField] private float referenceSpeed = 2.5f;      // usa tu patrolSpeed como referencia
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.6f;

    private float stepTimer;

    private AudioSource src;

    private void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Linear;
        src.maxDistance  = 50f;
    }

    // ── Audición ─────────────────────────────────────────────────────────────

    public bool CanHearPlayer(Transform player, EnemyNoiseLevel noise)
    {
        if (player == null) return false;
        float dist = Vector3.Distance(transform.position, player.position);
        float radius = noise switch
        {
            EnemyNoiseLevel.Running => hearingRun,
            EnemyNoiseLevel.Walking => hearingWalk,
            _                       => hearingIdle
        };
        return dist <= radius;
    }

    // ── Reproducción ─────────────────────────────────────────────────────────

    public void PlayPatrol()       => PlayLooped(clipPatrol);
    public void PlayChase()        => PlayLooped(clipChase);
    public void PlayStalk()        => PlayLooped(clipStalk);
    public void PlaySpawn()        => PlayOneShot(clipSpawn);
    public void PlayVanish()       => PlayOneShot(clipVanish);
    public void PlayDistantScream()=> PlayOneShot(clipDistantScream);
    

    public void StopAudio()
    {
        if (src.isPlaying) src.Stop();
    }

    private void PlayLooped(AudioClip clip)
    {
        if (clip == null) return;
        if (src.clip == clip && src.isPlaying) return;
        src.clip = clip;
        src.loop = true;
        src.Play();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        src.PlayOneShot(clip);
    }

    public void UpdateFootsteps(float currentSpeed)
    {
        if (currentSpeed < 0.1f)
        {
            stepTimer = 0f;
            return;
        }

        float stepsPerSecond = baseStepsPerSecond * (currentSpeed / referenceSpeed);
        float interval = 1f / Mathf.Max(stepsPerSecond, 0.01f);

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = interval;
        }
    }

    private void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        src.PlayOneShot(clip, footstepVolume);
    }
}