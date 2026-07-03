using UnityEngine;
using System.Reflection;

/// <summary>
/// ExitSpawner: coloca por código la LLAVE, el PORTAL de salida y el HUD
/// (Parte 5 - Mecánicas).
///
/// Sirve para cualquier nivel de CUADRÍCULA (Nivel 1 y Nivel 2). Lee el tamaño
/// (width/height/cellSize/wallHeight) automáticamente del generador que esté en
/// el mismo objeto, así que NO hay que sincronizar los números a mano.
///
/// USO: pon este script en el MISMO objeto que tiene el generador del nivel.
/// </summary>
public class ExitSpawner : MonoBehaviour
{
    [Header("Tamaño de la cuadrícula (se lee solo del generador)")]
    public int width = 42;
    public int height = 42;
    public float cellSize = 4f;
    public float wallHeight = 3f;

    [Header("Colores")]
    [Tooltip("Color de la llave (brilla con el bloom de la escena).")]
    public Color colorLlave = new Color(1f, 0.85f, 0.1f);    // dorado

    [Tooltip("Color del portal de salida (brilla con el bloom).")]
    public Color colorPortal = new Color(0.2f, 1f, 0.4f);    // verde

    [Tooltip("Segundos de espera antes de colocar todo (deja terminar la generación).")]
    public float retraso = 0.5f;

    private void Start()
    {
        // El HUD se crea de una vez para que esté listo desde el inicio.
        new GameObject("GameHUD").AddComponent<GameHUD>();

        Invoke(nameof(ColocarTodo), retraso);
    }

    private void ColocarTodo()
    {
        LeerTamañoDelGenerador(); // copia width/height/cellSize del generador del nivel

        ColocarPortal(width - 1, height - 1); // salida: esquina lejana
        ColocarLlave(0, height - 1);          // llave: otra esquina
    }

    // Lee el tamaño de la cuadrícula del generador que esté en el mismo objeto
    // (MazeGenerator, Level2Generator...), buscando sus campos públicos por nombre.
    // Así el portal y la llave siempre quedan dentro del laberinto real.
    private void LeerTamañoDelGenerador()
    {
        foreach (var comp in GetComponents<MonoBehaviour>())
        {
            if (comp == null || comp == this) continue;

            var t = comp.GetType();
            FieldInfo fw = t.GetField("width");
            FieldInfo fh = t.GetField("height");
            FieldInfo fc = t.GetField("cellSize");
            if (fw == null || fh == null || fc == null) continue;
            if (fw.FieldType != typeof(int) || fh.FieldType != typeof(int) || fc.FieldType != typeof(float)) continue;

            width    = (int)fw.GetValue(comp);
            height   = (int)fh.GetValue(comp);
            cellSize = (float)fc.GetValue(comp);

            FieldInfo fwh = t.GetField("wallHeight");
            if (fwh != null && fwh.FieldType == typeof(float))
                wallHeight = (float)fwh.GetValue(comp);

            Debug.Log($"[ExitSpawner] Tamaño leído del generador {t.Name}: {width}x{height}, celda {cellSize}.");
            return;
        }
    }

    // Centro de una celda, misma fórmula que usan los generadores.
    private Vector3 CentroCelda(int x, int y) =>
        transform.position + new Vector3(x * cellSize, 0f, y * cellSize);

    // --------------------------------------------------------------- PORTAL
    private void ColocarPortal(int cx, int cy)
    {
        Vector3 centro = CentroCelda(cx, cy);

        // Portal verde brillante que llena la celda. Es SÓLIDO (su collider
        // bloquea): sin llave no se puede pasar. Con llave, el ExitDoor lo deja
        // avanzar al tocarlo.
        var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portal.name = "PortalSalida";
        portal.transform.position = centro + Vector3.up * (wallHeight / 2f);
        portal.transform.localScale = new Vector3(cellSize * 0.92f, wallHeight, cellSize * 0.92f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", colorPortal * 3f); // x3 para que brille con el bloom
        portal.GetComponent<Renderer>().sharedMaterial = mat;

        // Trigger un poco más grande que la celda para detectar al jugador.
        var trigger = new GameObject("TriggerPortal");
        trigger.transform.SetParent(portal.transform, false);
        var box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.7f, 1f, 1.7f); // en espacio local (el portal está escalado)

        trigger.AddComponent<ExitDoor>();

        Debug.Log($"Portal de salida colocado en la celda ({cx},{cy}).");
    }

    // ---------------------------------------------------------------- LLAVE
    private void ColocarLlave(int cx, int cy)
    {
        Vector3 centro = CentroCelda(cx, cy);

        // Raíz: gira y tiene el área de recogida.
        var raiz = new GameObject("Llave");
        raiz.transform.position = centro + Vector3.up * 1.2f;
        var box = raiz.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2f, 3f, 2f); // área de recogida generosa
        raiz.AddComponent<KeyPickup>();

        // Material dorado brillante para todas las piezas.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", colorLlave * 2.5f);

        ConstruirFormaLlave(raiz.transform, mat);

        Debug.Log($"Llave colocada en la celda ({cx},{cy}).");
    }

    // Construye una llave con cubos: cuerpo + anillo (cabeza) + dientes.
    private void ConstruirFormaLlave(Transform parent, Material mat)
    {
        Pieza(parent, mat, new Vector3(0.05f, 0, 0), new Vector3(0.55f, 0.07f, 0.07f)); // cuerpo

        int segmentos = 12;                 // anillo de la cabeza
        float radio = 0.13f;
        Vector3 centroAnillo = new Vector3(-0.30f, 0, 0);
        for (int i = 0; i < segmentos; i++)
        {
            float a = (i / (float)segmentos) * Mathf.PI * 2f;
            Vector3 p = centroAnillo + new Vector3(0, Mathf.Sin(a) * radio, Mathf.Cos(a) * radio);
            Pieza(parent, mat, p, new Vector3(0.05f, 0.06f, 0.06f));
        }

        Pieza(parent, mat, new Vector3(0.28f, -0.08f, 0), new Vector3(0.06f, 0.12f, 0.07f)); // dientes
        Pieza(parent, mat, new Vector3(0.36f, -0.11f, 0), new Vector3(0.06f, 0.18f, 0.07f));
    }

    private void Pieza(Transform parent, Material mat, Vector3 localPos, Vector3 localScale)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        Object.Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
