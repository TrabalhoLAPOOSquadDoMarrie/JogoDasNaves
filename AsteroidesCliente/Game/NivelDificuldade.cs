namespace AsteroidesCliente.Game
{
    /// <summary>
    /// Enum que define os niveis de dificuldade do jogo
    /// </summary>
    public enum NivelDificuldade
    {
        /// <summary>
        /// Facil: Velocidade constante durante todo o jogo
        /// </summary>
        Facil,
        
        /// <summary>
        /// Medio: Comeca devagar, acelera ate velocidade media e mantem
        /// </summary>
        Medio,
        
        /// <summary>
        /// Dificil: Comeca facil, passa para medio e depois fica rapido/dificil
        /// </summary>
        Dificil
    }
}