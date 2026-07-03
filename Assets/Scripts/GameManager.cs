using UnityEngine;
using UnityEngine.SceneManagement;
using Backrooms.Logic;

/// <summary>
/// GameManager: el "cerebro" del juego (Parte 5 - Mecánicas).
///
/// Lleva la cuenta del nivel actual y controla el paso de un laberinto al
/// siguiente. Como cada laberinto es una ESCENA distinta (Nivel1, Nivel2,
/// Nivel3), avanzar de nivel = cargar la siguiente escena de la lista de
/// "Build Settings". Cuando ya no hay más escenas -> el jugador ganó.
///
/// Las REGLAS (llave, nivel) viven en la clase ProgresoJuego, que es fácil de
/// probar con pruebas unitarias. Aquí solo conectamos esas reglas con Unity.
///
/// Es un SINGLETON: existe UNA sola copia que sobrevive al cambiar de escena.
/// </summary>
public class GameManager : MonoBehaviour
{
    // Acceso global: cualquier otro script puede usar GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Configuración")]
    [Tooltip("Nombre de la escena de victoria (opcional). Déjala vacía si aún no la tienes.")]
    public string escenaVictoria = "";

    // El estado real del juego (reglas puras, sin Unity).
    private ProgresoJuego progreso = new ProgresoJuego();

    // Atajos de solo lectura para que otros scripts (HUD, portal) consulten el estado.
    public bool tieneLlave => progreso.TieneLlave;
    public int nivelActual => progreso.NivelActual;

    private bool nivelPerdido = false; // evita mostrar la pantalla de derrota dos veces

    private void Awake()
    {
        // Patrón Singleton: si ya existe un GameManager, este duplicado se destruye.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // No se destruye al cargar otra escena -> sobrevive entre laberintos.
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>¿El jugador puede abrir el portal de salida? (necesita la llave)</summary>
    public bool PuedeAbrirPortal() => progreso.PuedeAbrirPortal();

    /// <summary>
    /// Se llama cuando el jugador atraviesa el portal de salida.
    /// Carga el siguiente laberinto, o gana el juego si era el último.
    /// </summary>
    public void CompletarNivel()
    {
        int indiceActual = SceneManager.GetActiveScene().buildIndex;
        int indiceSiguiente = indiceActual + 1;

        // ¿Hay un siguiente laberinto en la lista de escenas (Build Settings)?
        if (indiceSiguiente < SceneManager.sceneCountInBuildSettings)
        {
            progreso.PasarASiguienteNivel(); // sube de nivel y reinicia la llave
            Debug.Log($"¡Nivel completado! Cargando el laberinto {progreso.NivelActual}...");
            SceneManager.LoadScene(indiceSiguiente);
        }
        else
        {
            // No hay más laberintos -> el jugador ganó el juego.
            GanarJuego();
        }
    }

    private void GanarJuego()
    {
        Debug.Log("¡GANASTE EL JUEGO! 🎉");

        // Si configuraste una escena de victoria y está en Build Settings, la carga.
        if (!string.IsNullOrEmpty(escenaVictoria) &&
            Application.CanStreamedLevelBeLoaded(escenaVictoria))
        {
            SceneManager.LoadScene(escenaVictoria);
        }
    }

    /// <summary>
    /// Reinicia el juego desde el primer nivel (útil para el botón de "jugar de nuevo").
    /// </summary>
    public void ReiniciarJuego()
    {
        Time.timeScale = 1f;   // por si veníamos de una pantalla que pausó el juego
        nivelPerdido = false;
        progreso.Reiniciar();
        SceneManager.LoadScene(0); // la primera escena de la lista
    }

    /// <summary>
    /// Se llama cuando el enemigo atrapa al jugador. Pausa el juego y muestra la
    /// pantalla de derrota.
    /// </summary>
    public void PerderNivel()
    {
        if (nivelPerdido) return;
        nivelPerdido = true;

        Debug.Log("💀 ¡El enemigo te atrapó!");
        Time.timeScale = 0f;   // congela el juego
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Crea la pantalla de derrota (con botón de reintentar).
        new GameObject("LoseScreen").AddComponent<LoseScreen>();
    }

    /// <summary>
    /// Reinicia SOLO el nivel actual (para el botón "Reintentar" de la derrota).
    /// </summary>
    public void ReintentarNivel()
    {
        Time.timeScale = 1f;
        nivelPerdido = false;
        progreso.ReiniciarNivel(); // se pierde la llave, pero seguimos en el mismo nivel
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Se llama cuando el jugador recoge la llave del nivel.
    /// </summary>
    public void RecogerLlave()
    {
        progreso.RecogerLlave();
        Debug.Log("🔑 ¡Recogiste la llave! Ahora busca el portal de salida.");
        if (GameHUD.Instance != null)
            GameHUD.Instance.MostrarAviso("¡Llave conseguida!");
    }
}
