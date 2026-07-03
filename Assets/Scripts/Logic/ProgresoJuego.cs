namespace Backrooms.Logic
{
    /// <summary>
    /// ProgresoJuego: las REGLAS puras del juego (Parte 5 - Mecánicas).
    ///
    /// No depende de Unity (ni escenas, ni MonoBehaviour), por eso es fácil de
    /// probar con pruebas unitarias. El GameManager usa esta clase para llevar
    /// el estado de la llave y el nivel actual.
    /// </summary>
    public class ProgresoJuego
    {
        /// <summary>Nivel en el que va el jugador (1 = primer laberinto).</summary>
        public int NivelActual { get; private set; } = 1;

        /// <summary>¿El jugador tiene la llave del nivel actual?</summary>
        public bool TieneLlave { get; private set; } = false;

        /// <summary>Marca que el jugador recogió la llave.</summary>
        public void RecogerLlave()
        {
            TieneLlave = true;
        }

        /// <summary>El portal solo se puede abrir si se tiene la llave.</summary>
        public bool PuedeAbrirPortal()
        {
            return TieneLlave;
        }

        /// <summary>
        /// Pasa al siguiente nivel. Cada laberinto tiene su propia llave, así que
        /// la llave se reinicia.
        /// </summary>
        public void PasarASiguienteNivel()
        {
            NivelActual++;
            TieneLlave = false;
        }

        /// <summary>
        /// Reinicia solo el nivel actual: se pierde la llave, pero NO se cambia
        /// de nivel (para reintentar tras ser atrapado por el enemigo).
        /// </summary>
        public void ReiniciarNivel()
        {
            TieneLlave = false;
        }

        /// <summary>Vuelve todo al inicio (nivel 1, sin llave).</summary>
        public void Reiniciar()
        {
            NivelActual = 1;
            TieneLlave = false;
        }
    }
}
