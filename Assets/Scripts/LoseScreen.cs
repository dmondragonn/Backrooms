using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// LoseScreen: pantalla de derrota "TE ATRAPARON" (Parte 5 - Mecánicas).
///
/// Se muestra encima del juego (no es una escena aparte) cuando el enemigo te
/// atrapa. Tiene un botón "Reintentar" que reinicia el nivel actual.
/// La crea el GameManager automáticamente al perder.
/// </summary>
public class LoseScreen : MonoBehaviour
{
    private void Start()
    {
        // Libera el cursor para poder hacer clic.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // EventSystem para que el botón reciba clics (Input System nuevo).
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // Canvas por encima de todo lo demás (HUD, barra de estamina...).
        var canvasGO = new GameObject("CanvasDerrota");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Fondo rojo oscuro semitransparente.
        var fondo = NuevoPanel(canvasGO.transform, new Color(0.12f, 0f, 0f, 0.85f));
        EstirarTodo(fondo);

        // Título.
        var titulo = NuevoTexto(canvasGO.transform, "TE ATRAPARON", 110, new Color(1f, 0.2f, 0.2f));
        ColocarCentrado(titulo.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(1200, 250));

        // Botón "Reintentar".
        CrearBoton(canvasGO.transform, "Reintentar", new Vector2(0.5f, 0.38f), () =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReintentarNivel();
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
        img.color = new Color(0.55f, 0.2f, 0.2f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(alHacerClic);
        ColocarCentrado(go.GetComponent<RectTransform>(), anclaje, new Vector2(380, 90));

        var t = NuevoTexto(go.transform, texto, 34, Color.white);
        EstirarTodo(t);
    }

    private void ColocarCentrado(RectTransform rt, Vector2 anclaje, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = anclaje;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = tam;
    }

    private void EstirarTodo(Graphic g)
    {
        var rt = g.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
