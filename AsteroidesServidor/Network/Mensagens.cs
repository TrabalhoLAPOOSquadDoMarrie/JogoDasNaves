using Microsoft.Xna.Framework;

namespace AsteroidesServidor.Network;

/// <summary>
/// Tipos de mensagens trocadas entre cliente e servidor
/// </summary>
public enum TipoMensagem
{
    // Cliente para Servidor
    ConectarJogador,
    MovimentoJogador,
    AtirarTiro,
    DesconectarJogador,
    ReiniciarJogo,
    
    // Servidor para Cliente
    EstadoJogo,
    JogadorConectado,
    JogadorDesconectado,
    GameOver,
    ErroConexao
}

/// <summary>
/// Classe base para todas as mensagens
/// </summary>
public abstract class MensagemBase
{
    public TipoMensagem Tipo { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Mensagem de conexão de jogador
/// </summary>
public class MensagemConectarJogador : MensagemBase
{
    public string NomeJogador { get; set; } = "";
    
    public MensagemConectarJogador()
    {
        Tipo = TipoMensagem.ConectarJogador;
    }
}

/// <summary>
/// Mensagem de movimento do jogador
/// </summary>
public class MensagemMovimentoJogador : MensagemBase
{
    public int JogadorId { get; set; }
    public bool Esquerda { get; set; }
    public bool Direita { get; set; }
    public bool Cima { get; set; }
    public bool Baixo { get; set; }
    
    public MensagemMovimentoJogador()
    {
        Tipo = TipoMensagem.MovimentoJogador;
    }
}

/// <summary>
/// Mensagem de tiro
/// </summary>
public class MensagemAtirarTiro : MensagemBase
{
    public int JogadorId { get; set; }
    
    public MensagemAtirarTiro()
    {
        Tipo = TipoMensagem.AtirarTiro;
    }
}

/// <summary>
/// Estado completo do jogo enviado aos clientes
/// </summary>
public class MensagemEstadoJogo : MensagemBase
{
    public List<DadosNave> Naves { get; set; } = new();
    public List<DadosTiro> Tiros { get; set; } = new();
    public List<DadosAsteroide> Asteroides { get; set; } = new();
    public bool JogoAtivo { get; set; } = true;
    
    public MensagemEstadoJogo()
    {
        Tipo = TipoMensagem.EstadoJogo;
    }
}

/// <summary>
/// Dados da nave para transmissão
/// </summary>
public class DadosNave
{
    public int JogadorId { get; set; }
    public Vector2 Posicao { get; set; }
    public bool Viva { get; set; }
    public int Pontuacao { get; set; }
    public float Tamanho { get; set; } = 1.0f;
}

/// <summary>
/// Dados do tiro para transmissão
/// </summary>
public class DadosTiro
{
    public int Id { get; set; }
    public int JogadorId { get; set; }
    public Vector2 Posicao { get; set; }
}

/// <summary>
/// Dados do asteroide para transmissão
/// </summary>
public class DadosAsteroide
{
    public int Id { get; set; }
    public Vector2 Posicao { get; set; }
    public float Raio { get; set; }
}

/// <summary>
/// Mensagem de jogador conectado
/// </summary>
public class MensagemJogadorConectado : MensagemBase
{
    public int JogadorId { get; set; }
    public string NomeJogador { get; set; } = "";
    
    public MensagemJogadorConectado()
    {
        Tipo = TipoMensagem.JogadorConectado;
    }
}

/// <summary>
/// Mensagem de jogador desconectado
/// </summary>
public class MensagemJogadorDesconectado : MensagemBase
{
    public int JogadorId { get; set; }
    
    public MensagemJogadorDesconectado()
    {
        Tipo = TipoMensagem.JogadorDesconectado;
    }
}

/// <summary>
/// Mensagem de game over
/// </summary>
public class MensagemGameOver : MensagemBase
{
    public string Motivo { get; set; } = "";
    public List<DadosNave> PontuacaoFinal { get; set; } = new();
    
    public MensagemGameOver()
    {
        Tipo = TipoMensagem.GameOver;
    }
}

/// <summary>
/// Mensagem de reinício do jogo
/// </summary>
public class MensagemReiniciarJogo : MensagemBase
{
    public MensagemReiniciarJogo()
    {
        Tipo = TipoMensagem.ReiniciarJogo;
    }
}