using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// WinScreen: pantalla de victoria por código (Parte 5 - Mecánicas).
///
/// Construye una pantalla con el texto "¡GANASTE!" y un botón para volver a
/// jugar. No hay que armar nada a mano: solo pon este script en un objeto
/// vacío dentro de una escena llamada "Victoria".
/// </summary>
public class WinScreen : MonoBehaviour
{
    private void Start()
    {
        // Liberamos el cursor para poder hacer clic en el botón.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // La escena de victoria no tiene cámara: creamos una para que Unity no
        // muestre el aviso "No cameras rendering" y pinte un fondo limpio.
        if (FindFirstObjectByType<Camera>() == null)
        {
            var camGO = new GameObject("Camara");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            camGO.AddComponent<AudioListener>();
        }

        // El EventSystem es lo que hace que los botones reciban clics.
        // Usamos InputSystemUIInputModule porque el proyecto usa el Input System nuevo
        // (el módulo viejo StandaloneInputModule da error con este paquete).
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // --- Canvas (la "lámina" donde se dibuja la interfaz) ---
        var canvasGO = new GameObject("CanvasVictoria");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // --- Fondo oscuro que cubre toda la pantalla ---
        var fondo = NuevoPanel(canvasGO.transform, new Color(0.04f, 0.04f, 0.05f, 1f));
        EstirarTodo(fondo);

        // --- Título "¡GANASTE!" ---
        var titulo = NuevoTexto(canvasGO.transform, "¡GANASTE!", 110, new Color(0.3f, 1f, 0.5f));
        ColocarCentrado(titulo.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1000, 250));

        // --- Subtítulo ---
        var sub = NuevoTexto(canvasGO.transform, "Escapaste de los Backrooms", 40, Color.white);
        ColocarCentrado(sub.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(1000, 100));

        // --- Botón "Jugar de nuevo" ---
        CrearBoton(canvasGO.transform, "Jugar de nuevo", new Vector2(0.5f, 0.32f), () =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReiniciarJuego();
            else
                SceneManager.LoadScene(0);
        });
    }

    // --- Helpers para construir la interfaz ---

    private Image NuevoPanel(Transform parent, Color color)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Text NuevoTexto(Transform parent, string texto, int tam, Color color)
    {
        var go = new GameObject("Texto");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = texto;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tam;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        return t;
    }

    private void CrearBoton(Transform parent, string texto, Vector2 anclaje, UnityEngine.Events.UnityAction alHacerClic)
    {
        var go = new GameObject("Boton");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.55f, 0.3f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(alHacerClic);
        ColocarCentrado(go.GetComponent<RectTransform>(), anclaje, new Vector2(380, 90));

        var t = NuevoTexto(go.transform, texto, 34, Color.white);
        EstirarTodo(t); // el texto llena el botón
    }

    // Ancla un elemento a un punto de la pantalla (0-1) con un tamaño fijo.
    private void ColocarCentrado(RectTransform rt, Vector2 anclaje, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = anclaje;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = tam;
    }

    // Hace que un elemento ocupe TODO su contenedor.
    private void EstirarTodo(Graphic g)
    {
        var rt = g.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
