// enemyState.cs
// Enum compartido por todos los scripts del enemigo.

public enum enemyState
{
    Dormant,     // Inactivo, esperando spawn
    Spawn,       // Acaba de aparecer, fade in
    Patrol,      // Patrullando nodos del mapa
    Stalk,       // Observando al jugador sin atacar
    Investigate, // Investigando un ruido o posición
    Chase,       // Persecución activa por NavMesh
    Search,      // Perdió al jugador, busca en la zona
    Vanish       // Desapareciendo, vuelve a Dormant
}