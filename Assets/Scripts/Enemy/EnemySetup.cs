using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Script de utilidad para configurar el enemigo con el modelo de Captain Clark.
/// Uso: Attach este script a un GameObject vacío, asigna el modelo, y presiona "Setup Enemy" en el Inspector.
/// </summary>
public class EnemySetup : MonoBehaviour
{
    [Header("Modelo del Enemigo")]
    [Tooltip("Arrastra el modelo de Captain Clark desde Assets/Resources/captain-clark/source/Captain clark")]
    public GameObject enemyModel;

    [Header("Configuración de Vision")]
    public float visionRange = 18f;
    public float visionAngle = 90f;
    public float stalkRange = 25f;
    public float eyeHeight = 1.6f;

    [Header("Configuración de Audio")]
    public AudioClip clipPatrol;
    public AudioClip clipChase;
    public AudioClip clipStalk;
    public AudioClip clipSpawn;
    public AudioClip clipVanish;
    public AudioClip clipDistantScream;

    [Header("Configuración de NavMeshAgent")]
    public float agentRadius = 0.5f;
    public float agentHeight = 2f;

#if UNITY_EDITOR
    [ContextMenu("Setup Enemy")]
    public void SetupEnemy()
    {
        if (enemyModel == null)
        {
            Debug.LogError("[EnemySetup] Debes asignar el modelo del enemigo primero!");
            return;
        }

        // Limpiar hijos existentes
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }

        // Instanciar el modelo como hijo
        GameObject modelInstance = Instantiate(enemyModel, transform);
        modelInstance.name = "EnemyModel";
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        // Configurar NavMeshAgent
        NavMeshAgent agent = gameObject.GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;

        // Configurar CapsuleCollider
        CapsuleCollider capsule = gameObject.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.radius = agentRadius;
        capsule.height = agentHeight;
        capsule.center = new Vector3(0, agentHeight / 2f, 0);

        // Agregar componentes de enemigo
        enemyLogic logic = gameObject.GetComponent<enemyLogic>();
        if (logic == null) logic = gameObject.AddComponent<enemyLogic>();

        enemyVision vision = gameObject.GetComponent<enemyVision>();
        if (vision == null) vision = gameObject.AddComponent<enemyVision>();
        
        // Configurar vision usando SerializedObject para campos privados
        SerializedObject serializedVision = new SerializedObject(vision);
        serializedVision.FindProperty("visionRange").floatValue = visionRange;
        serializedVision.FindProperty("visionAngle").floatValue = visionAngle;
        serializedVision.FindProperty("stalkRange").floatValue = stalkRange;
        serializedVision.FindProperty("eyeHeight").floatValue = eyeHeight;
        serializedVision.ApplyModifiedProperties();

        enemyAudio audioComp = gameObject.GetComponent<enemyAudio>();
        if (audioComp == null) audioComp = gameObject.AddComponent<enemyAudio>();

        // Configurar audio clips usando SerializedObject
        SerializedObject serializedAudio = new SerializedObject(audioComp);
        serializedAudio.FindProperty("clipPatrol").objectReferenceValue = clipPatrol;
        serializedAudio.FindProperty("clipChase").objectReferenceValue = clipChase;
        serializedAudio.FindProperty("clipStalk").objectReferenceValue = clipStalk;
        serializedAudio.FindProperty("clipSpawn").objectReferenceValue = clipSpawn;
        serializedAudio.FindProperty("clipVanish").objectReferenceValue = clipVanish;
        serializedAudio.FindProperty("clipDistantScream").objectReferenceValue = clipDistantScream;
        serializedAudio.ApplyModifiedProperties();

        // Agregar AudioSource
        AudioSource audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 50f;

        // Crear punto de vision (ojo)
        GameObject eyePoint = new GameObject("EyePoint");
        eyePoint.transform.SetParent(transform);
        eyePoint.transform.localPosition = new Vector3(0, eyeHeight, 0);

        // Agregar enemySpawner como hijo
        GameObject spawnerObj = new GameObject("Spawner");
        spawnerObj.transform.SetParent(transform);
        spawnerObj.transform.localPosition = Vector3.zero;
        enemySpawner spawner = spawnerObj.AddComponent<enemySpawner>();

        Debug.Log("[EnemySetup] ✅ Enemigo configurado exitosamente!");
        Debug.Log("[EnemySetup] Siguiente paso: Asigna el Transform del jugador en enemySpawner y enemyLogic.");
        
        // Marcar como sucio para guardar cambios
        EditorUtility.SetDirty(gameObject);
    }
#endif
}
