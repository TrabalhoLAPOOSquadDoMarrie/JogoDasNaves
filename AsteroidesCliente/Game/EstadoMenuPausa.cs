namespace AsteroidesCliente.Game;

/// <summary>
/// Estados possíveis do menu de pausa
/// </summary>
public enum EstadoMenuPausa
{
    Fechado,
    Aberto,
    Configuracoes,
    Recordes
}

/// <summary>
/// Opções disponíveis no menu de pausa
/// </summary>
public enum OpcaoMenuPausa
{
    Retomar,
    Configuracoes,
    Recordes,
    VoltarMenu,
    Sair
}