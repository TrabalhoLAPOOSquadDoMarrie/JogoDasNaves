using Microsoft.Xna.Framework;

namespace AsteroidesServidor.Models;

/// <summary>
/// Representa um asteroide no jogo
/// </summary>
public class Asteroide
{
    public Vector2 Posicao { get; set; }
    public Vector2 Velocidade { get; set; }
    public float Raio { get; set; }
    public int Id { get; set; }

    public Asteroide(int id, Vector2 posicao, Vector2 velocidade, float raio)
    {
        Id = id;
        Posicao = posicao;
        Velocidade = velocidade;
        Raio = raio;
    }

    /// <summary>
    /// Atualiza a posição do asteroide
    /// </summary>
    public void Atualizar()
    {
        Posicao += Velocidade;
    }

    /// <summary>
    /// Verifica colisão com um tiro
    /// </summary>
    public bool Colide(Tiro tiro)
    {
        return Vector2.Distance(tiro.Posicao, Posicao) < Raio;
    }

    /// <summary>
    /// Verifica colisão com uma nave
    /// </summary>
    public bool Colide(Nave nave)
    {
        // Raio base da nave é 8, escalado pelo tamanho
        float raioNave = 8 * nave.Tamanho;
        return Vector2.Distance(nave.Posicao, Posicao) < Raio + raioNave;
    }

    /// <summary>
    /// Verifica se o asteroide está fora da tela
    /// </summary>
    public bool ForaDaTela(int altura)
    {
        return Posicao.Y > altura + Raio;
    }
}