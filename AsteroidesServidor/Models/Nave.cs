using Microsoft.Xna.Framework;
using AsteroidesServidor.Network;

namespace AsteroidesServidor.Models;

/// <summary>
/// Representa uma nave de jogador
/// </summary>
public class Nave
{
    public Vector2 Posicao { get; set; }
    public float Rotacao { get; set; } // Ângulo de rotação em radianos
    public int JogadorId { get; set; }
    public bool Viva { get; set; }
    public int Pontuacao { get; set; }
    public float Tamanho { get; private set; } = 1.0f; // Tamanho base da nave
    public int ModeloNave { get; set; } = 0; // Modelo da nave (0-3)
    
    // Velocidades independentes de framerate (pixels por segundo)
    private const float VelocidadePorSegundo = 300f; // 300 pixels por segundo
    private const float VelocidadeRotacaoPorSegundo = 3.0f; // 3 radianos por segundo
    private const float HalfW = 10, HalfH = 10;
    private const int PontosParaCrescimento = 200; // A cada 200 pontos a nave cresce
    private const float IncrementoTamanho = 0.1f; // Incremento de 10% no tamanho


    public Nave(int jogadorId, Vector2 posicaoInicial)
    {
        JogadorId = jogadorId;
        Posicao = posicaoInicial;
        Rotacao = 0f; // Inicia apontando para cima
        Viva = true;
        Pontuacao = 0;
        Tamanho = 1.0f;
    }

    /// <summary>
    /// Atualiza a posição da nave baseada nos comandos de movimento
    /// </summary>
    /// <param name="deltaTime">Tempo decorrido desde o último frame em segundos</param>
    public void Atualizar(bool esquerda, bool direita, bool cima, bool baixo, int largura, int altura, float deltaTime)
    {
        // Rotação da nave (independente de framerate)
        if (esquerda) Rotacao -= VelocidadeRotacaoPorSegundo * deltaTime;
        if (direita) Rotacao += VelocidadeRotacaoPorSegundo * deltaTime;

        // Movimento baseado na orientação atual
        Vector2 direcao = Vector2.Zero;
        if (cima)
        {
            // Move na direção que a nave está apontando
            direcao.X = (float)Math.Sin(Rotacao);
            direcao.Y = -(float)Math.Cos(Rotacao); // Negativo porque Y cresce para baixo
        }
        if (baixo)
        {
            // Move na direção oposta
            direcao.X = -(float)Math.Sin(Rotacao);
            direcao.Y = (float)Math.Cos(Rotacao);
        }

        if (direcao != Vector2.Zero)
        {
            direcao.Normalize();
            Posicao += direcao * VelocidadePorSegundo * deltaTime;
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
        // Calcula a direção do tiro baseada na rotação da nave
        Vector2 direcaoTiro = new Vector2(
            (float)Math.Sin(Rotacao),
            -(float)Math.Cos(Rotacao)
        );

        // Posição inicial do tiro (na ponta da nave)
        Vector2 posicaoTiro = Posicao + direcaoTiro * 12;

        // Velocidade do tiro na direção da nave (pixels por segundo)
        Vector2 velocidadeTiro = direcaoTiro * 600f; // 600 pixels por segundo

        return new Tiro(tiroId, JogadorId, posicaoTiro, velocidadeTiro);
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
        Rotacao = 0f; // Reseta a rotação para apontar para cima
        Viva = true;
        Pontuacao = 0;
        Tamanho = 1.0f;
    }
}