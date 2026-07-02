using UnityEngine;
using UnityEditor;

public class EnemySceneFixer : EditorWindow
{
    [MenuItem("Tools/Fix Enemy Setup")]
    public static void ShowWindow()
    {
        GetWindow<EnemySceneFixer>("Fix Enemy Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Enemy Scene Fixer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Fix All Issues", GUILayout.Height(40)))
            FixAll();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Este script corrige:\n" +
            "1. Mantiene Enemy ACTIVO (ya no usa SetActive false)\n" +
            "2. Corrige posicion de la Capsula hijo a (0,0,0)\n" +
            "3. Agrega enemySpawner al hijo EnemySpawner si falta\n" +
            "4. Sincroniza valores de enemyLogic y enemyAudio",
            MessageType.Info);
    }

    private static void FixAll()
    {
        GameObject enemy = GameObject.Find("Enemy");
        if (enemy == null)
        {
            Debug.LogError("[EnemySceneFixer] No se encontro el GameObject 'Enemy' en la escena.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(enemy, "Fix Enemy Setup");

        // 1. El Enemy debe estar ACTIVO — el enemigo se oculta via renderers, no SetActive
        enemy.SetActive(true);
        Debug.Log("[EnemySceneFixer] Enemy activo.");

        // 2. Corregir posicion del hijo visual (Capsule o modelo)
        Transform capsule = enemy.transform.Find("Capsule");
        if (capsule != null)
        {
            capsule.localPosition = Vector3.zero;
            capsule.localRotation = Quaternion.identity;
            Debug.Log("[EnemySceneFixer] Posicion de Capsule corregida a (0,0,0).");
        }

        // 3. Agregar enemySpawner al hijo EnemySpawner si no lo tiene
        Transform spawnerObj = enemy.transform.Find("EnemySpawner");
        if (spawnerObj != null)
        {
            if (spawnerObj.GetComponent<enemySpawner>() == null)
            {
                spawnerObj.gameObject.AddComponent<enemySpawner>();
                Debug.Log("[EnemySceneFixer] Componente enemySpawner agregado.");
            }
            else
            {
                Debug.Log("[EnemySceneFixer] EnemySpawner ya tenia el componente.");
            }
        }
        else
        {
            var newSpawner = new GameObject("EnemySpawner");
            newSpawner.transform.SetParent(enemy.transform);
            newSpawner.transform.localPosition = Vector3.zero;
            newSpawner.AddComponent<enemySpawner>();
            Debug.Log("[EnemySceneFixer] Hijo EnemySpawner creado con componente enemySpawner.");
        }

        // 4. Sincronizar valores de enemyLogic
        enemyLogic logic = enemy.GetComponent<enemyLogic>();
        if (logic != null)
        {
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("patrolSpeed").floatValue      = 2.5f;
            so.FindProperty("chaseSpeed").floatValue       = 3.8f;
            so.FindProperty("investigateSpeed").floatValue = 3f;
            so.FindProperty("searchDuration").floatValue   = 10f;
            so.FindProperty("stalkDuration").floatValue    = 4f;
            so.FindProperty("chaseDurationMax").floatValue = 20f;
            so.FindProperty("spawnDuration").floatValue    = 1.2f;
            so.FindProperty("aggressionRate").floatValue   = 0.005f;
            so.FindProperty("aggressionMax").floatValue    = 3f;
            so.ApplyModifiedProperties();
            Debug.Log("[EnemySceneFixer] Valores de enemyLogic sincronizados.");
        }
        else
        {
            Debug.LogWarning("[EnemySceneFixer] No se encontro componente enemyLogic en Enemy.");
        }

        // 5. Sincronizar valores de enemyAudio
        enemyAudio audio = enemy.GetComponent<enemyAudio>();
        if (audio != null)
        {
            SerializedObject so = new SerializedObject(audio);
            so.FindProperty("hearingRun").floatValue  = 25f;
            so.FindProperty("hearingWalk").floatValue = 10f;
            so.FindProperty("hearingIdle").floatValue = 2f;
            so.ApplyModifiedProperties();
            Debug.Log("[EnemySceneFixer] Valores de enemyAudio sincronizados.");
        }
        else
        {
            Debug.LogWarning("[EnemySceneFixer] No se encontro componente enemyAudio en Enemy.");
        }

        EditorUtility.SetDirty(enemy);
        Debug.Log("[EnemySceneFixer] Listo. Guarda la escena con Ctrl+S.");
    }
}
