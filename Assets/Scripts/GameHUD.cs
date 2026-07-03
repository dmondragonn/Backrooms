using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameHUD: los mensajes en pantalla durante el juego (Parte 5 - Mecánicas).
///
/// Muestra arriba el objetivo actual ("busca la llave" / "busca el portal de salida") y
/// puede mostrar mensajes temporales (ej: "el portal está cerrado").
///
/// Lo crea automáticamente el ExitSpawner; no hay que ponerlo a mano.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    private Text objetivo;     // mensaje permanente (qué hacer ahora)
    private Text mensaje;      // mensaje temporal (avisos)
    private float mensajeHasta; // tiempo hasta el que se muestra el aviso

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        // --- Canvas (lámina de interfaz) ---
        var canvasGO = new GameObject("CanvasHUD");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Objetivo: barra arriba al centro.
        objetivo = NuevoTexto(canvasGO.transform, "", 34, Color.white,
                              new Vector2(0.5f, 0.93f), new Vector2(1400, 80));

        // Aviso temporal: centro de la pantalla, en rojo.
        mensaje = NuevoTexto(canvasGO.transform, "", 40, new Color(1f, 0.4f, 0.3f),
                             new Vector2(0.5f, 0.62f), new Vector2(1400, 100));
        mensaje.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Actualiza el objetivo según si el jugador ya tiene la llave.
        bool tiene = GameManager.Instance != null && GameManager.Instance.tieneLlave;
        objetivo.text = tiene
            ? "¡Tienes la llave! Busca el portal de SALIDA"
            : "Objetivo: encuentra la LLAVE dorada";

        // Oculta el aviso temporal cuando se acaba su tiempo.
        if (mensaje.gameObject.activeSelf && Time.time > mensajeHasta)
            mensaje.gameObject.SetActive(false);
    }

    /// <summary>Muestra un aviso temporal en el centro de la pantalla.</summary>
    public void MostrarAviso(string texto, float duracion = 2.5f)
    {
        mensaje.text = texto;
        mensaje.gameObject.SetActive(true);
        mensajeHasta = Time.time + duracion;
    }

    private Text NuevoTexto(Transform parent, string txt, int tam, Color color,
                            Vector2 anclaje, Vector2 tamCaja)
    {
        var go = new GameObject("Texto");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tam;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anclaje;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = tamCaja;
        return t;
    }
}
