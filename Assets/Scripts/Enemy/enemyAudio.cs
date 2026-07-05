// enemyAudio.cs
// Controla sonidos del enemigo (gruñidos ambientales) y sistema de audición.
// Adjunta al mismo GameObject que enemyLogic.

using UnityEngine;

public enum EnemyNoiseLevel { Idle, Walking, Running }

public class enemyAudio : MonoBehaviour
{
    [Header("Gruñidos")]
    [SerializeField] private AudioClip[] growlClips;
    [SerializeField] private float minGrowlDelay = 4f;   // silencio mínimo entre gruñidos
    [SerializeField] private float maxGrowlDelay = 10f;  // silencio máximo (= min para un valor fijo)
    [Range(0f, 1f)] [SerializeField] private float growlVolume = 0.8f;

    [Header("Pasos (Footsteps)")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float baseStepsPerSecond = 2f;   // pasos por segundo A referenceSpeed
    [SerializeField] private float referenceSpeed = 2.5f;      // usa tu patrolSpeed como referencia
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.6f;

    [Header("Radios de audición")]
    [SerializeField] private float hearingRun  = 25f;
    [SerializeField] private float hearingWalk = 10f;
    [SerializeField] private float hearingIdle =  2f;

    private AudioSource src;
    private float stepTimer;
    private float growlTimer;

    private void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Linear;
        src.maxDistance  = 50f;

        growlTimer = Random.Range(minGrowlDelay, maxGrowlDelay);
    }

    private void Update()
    {
        growlTimer -= Time.deltaTime;
        if (growlTimer <= 0f)
        {
            PlayGrowl();
            growlTimer = Random.Range(minGrowlDelay, maxGrowlDelay);
        }
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

    // ── Gruñidos ─────────────────────────────────────────────────────────────

    private void PlayGrowl()
    {
        if (growlClips == null || growlClips.Length == 0) return;
        AudioClip clip = growlClips[Random.Range(0, growlClips.Length)];
        src.PlayOneShot(clip, growlVolume);
    }

    // ── Pasos ────────────────────────────────────────────────────────────────

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