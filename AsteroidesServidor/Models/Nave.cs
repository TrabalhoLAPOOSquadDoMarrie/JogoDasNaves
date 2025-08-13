using Microsoft.Xna.Framework;

namespace AsteroidesServidor.Models;

/// <summary>
/// Representa uma nave de jogador
/// </summary>
public class Nave
{
    public Vector2 Posicao { get; set; }
    public int JogadorId { get; set; }
    public bool Viva { get; set; }
    public int Pontuacao { get; set; }
    public float Tamanho { get; private set; } = 1.0f; // Tamanho base da nave

    private const float Velocidade = 5f;
    private const float HalfW = 10, HalfH = 10;
    private const int PontosParaCrescimento = 200; // A cada 200 pontos a nave cresce
    private const float IncrementoTamanho = 0.1f; // Incremento de 10% no tamanho
    

    public Nave(int jogadorId, Vector2 posicaoInicial)
    {
        JogadorId = jogadorId;
        Posicao = posicaoInicial;
        Viva = true;
        Pontuacao = 0;
        Tamanho = 1.0f;
    }

    /// <summary>
    /// Atualiza a posição da nave baseada nos comandos de movimento
    /// </summary>
    public void Atualizar(bool esquerda, bool direita, bool cima, bool baixo, int largura, int altura)
    {
        Vector2 direcao = Vector2.Zero;
        
        if (esquerda) direcao.X -= 1;
        if (direita) direcao.X += 1;
        if (cima) direcao.Y -= 1;
        if (baixo) direcao.Y += 1;

        if (direcao != Vector2.Zero)
        {
            direcao.Normalize();
            Posicao += direcao * Velocidade;
        }

        // Mantém a nave dentro da tela
        Posicao = new Vector2(
            Math.Clamp(Posicao.X, HalfW, largura - HalfW),
            Math.Clamp(Posicao.Y, HalfH, altura - HalfH)
        );
    }

    /// <summary>
    /// Cria um novo tiro na posição da nave
    /// </summary>
    public Tiro Atirar(int tiroId)
    {
        return new Tiro(tiroId, JogadorId, Posicao + new Vector2(0, -12), new Vector2(0, -8));
    }

    /// <summary>
    /// Mata a nave (colisão com asteroide)
    /// </summary>
    public void Morrer()
    {
        Viva = false;
    }

    /// <summary>
    /// Adiciona pontos à pontuação do jogador e atualiza o tamanho da nave
    /// </summary>
    public void AdicionarPontos(int pontos)
    {
        int pontuacaoAnterior = Pontuacao;
        Pontuacao += pontos;
        
        // Calcula o novo tamanho baseado na pontuação
        int nivelAnterior = pontuacaoAnterior / PontosParaCrescimento;
        int nivelAtual = Pontuacao / PontosParaCrescimento;
        
        // Se subiu de nível, aumenta o tamanho
        if (nivelAtual > nivelAnterior)
        {
            Tamanho = 1.0f + (nivelAtual * IncrementoTamanho);
            Console.WriteLine($"Nave do jogador {JogadorId} cresceu! Novo tamanho: {Tamanho:F2}x");
        }
    }
    
    /// <summary>
    /// Reseta a nave para o estado inicial
    /// </summary>
    public void Resetar(Vector2 novaPosicao)
    {
        Posicao = novaPosicao;
        Viva = true;
        Pontuacao = 0;
        Tamanho = 1.0f;
    }
}