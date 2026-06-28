// EnemyState.cs
// Define los estados posibles del enemigo.
// Este archivo es compartido por todos los scripts del enemigo.

public enum enemyState
{
    Dormant,      // No existe en escena o está oculto
    Spawn,        // Acaba de aparecer, transición inicial
    Patrol,       // Recorre nodos del mapa
    Stalk,        // Observa al jugador sin atacar
    Investigate,  // Va hacia un ruido o punto de interés
    Chase,        // Persecución activa
    Search,       // Perdió al jugador, busca en la zona
    Vanish        // Desaparece y vuelve a Dormant
}