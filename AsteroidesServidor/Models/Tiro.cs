using Microsoft.Xna.Framework;

namespace AsteroidesServidor.Models;

/// <summary>
/// Representa um tiro disparado por uma nave
/// </summary>
public class Tiro
{
    public Vector2 Posicao { get; set; }
    public Vector2 Velocidade { get; set; }
    public int Id { get; set; }
    public int JogadorId { get; set; }

    public Tiro(int id, int jogadorId, Vector2 posicao, Vector2 velocidade)
    {
        Id = id;
        JogadorId = jogadorId;
        Posicao = posicao;
        Velocidade = velocidade;
    }

    /// <summary>
    /// Atualiza a posição do tiro
    /// </summary>
    /// <param name="deltaTime">Tempo decorrido desde o último frame em segundos</param>
    public void Atualizar(float deltaTime)
    {
        Posicao += Velocidade * deltaTime;
    }

    /// <summary>
    /// Verifica se o tiro está fora da tela
    /// </summary>
    public bool ForaDaTela(int altura)
    {
        return Posicao.Y < -5;
    }
}