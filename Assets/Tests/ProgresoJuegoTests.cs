using NUnit.Framework;
using Backrooms.Logic;

/// <summary>
/// Pruebas unitarias de las reglas del juego (Parte 6 - Pruebas).
///
/// Cada método [Test] comprueba UNA regla. Se ejecutan desde:
///   Window -> General -> Test Runner -> pestaña "EditMode" -> Run All.
/// </summary>
public class ProgresoJuegoTests
{
    [Test]
    public void AlInicio_EstaEnNivel1_YSinLlave()
    {
        var progreso = new ProgresoJuego();

        Assert.AreEqual(1, progreso.NivelActual);
        Assert.IsFalse(progreso.TieneLlave);
    }

    [Test]
    public void RecogerLlave_PoneTieneLlaveEnTrue()
    {
        var progreso = new ProgresoJuego();

        progreso.RecogerLlave();

        Assert.IsTrue(progreso.TieneLlave);
    }

    [Test]
    public void SinLlave_NoSePuedeAbrirElPortal()
    {
        var progreso = new ProgresoJuego();

        Assert.IsFalse(progreso.PuedeAbrirPortal());
    }

    [Test]
    public void ConLlave_SiSePuedeAbrirElPortal()
    {
        var progreso = new ProgresoJuego();

        progreso.RecogerLlave();

        Assert.IsTrue(progreso.PuedeAbrirPortal());
    }

    [Test]
    public void PasarDeNivel_SubeElNivel_YReiniciaLaLlave()
    {
        var progreso = new ProgresoJuego();
        progreso.RecogerLlave(); // tenía la llave del nivel 1

        progreso.PasarASiguienteNivel();

        Assert.AreEqual(2, progreso.NivelActual);
        Assert.IsFalse(progreso.TieneLlave, "Cada nivel debe empezar sin llave.");
    }

    [Test]
    public void PasarDeNivel_VariasVeces_LlevaLaCuentaCorrecta()
    {
        var progreso = new ProgresoJuego();

        progreso.PasarASiguienteNivel();
        progreso.PasarASiguienteNivel();

        Assert.AreEqual(3, progreso.NivelActual);
    }

    [Test]
    public void ReiniciarNivel_QuitaLaLlave_PeroMantieneElNivel()
    {
        var progreso = new ProgresoJuego();
        progreso.PasarASiguienteNivel(); // ahora en nivel 2
        progreso.RecogerLlave();

        progreso.ReiniciarNivel(); // como al ser atrapada por el enemigo

        Assert.AreEqual(2, progreso.NivelActual, "Reintentar NO debe cambiar de nivel.");
        Assert.IsFalse(progreso.TieneLlave, "Al reintentar se pierde la llave.");
    }

    [Test]
    public void Reiniciar_VuelveANivel1_YSinLlave()
    {
        var progreso = new ProgresoJuego();
        progreso.RecogerLlave();
        progreso.PasarASiguienteNivel();

        progreso.Reiniciar();

        Assert.AreEqual(1, progreso.NivelActual);
        Assert.IsFalse(progreso.TieneLlave);
    }
}
