using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;   // só para comparar com Keys.*
using Monogame.Processing;

namespace Asteroides;

class Nave
{
    public Vector2 Posicao;
    public float Rotacao; // Ângulo de rotação em radianos
    const float Vel = 4f;
    const float VelRotacao = 0.1f; // Velocidade de rotação
    const float HalfW = 10, HalfH = 10;

    public Nave(Vector2 start) 
    { 
        Posicao = start;
        Rotacao = 0f; // Inicia apontando para cima
    }

    public void Atualizar(bool left, bool right, bool up, bool down, int w, int h)
    {
        // Rotação da nave
        if (left) Rotacao -= VelRotacao;
        if (right) Rotacao += VelRotacao;

        // Movimento baseado na orientação atual
        Vector2 dir = Vector2.Zero;
        if (up)
        {
            // Move na direção que a nave está apontando
            dir.X = (float)Math.Sin(Rotacao);
            dir.Y = -(float)Math.Cos(Rotacao); // Negativo porque Y cresce para baixo
        }
        if (down)
        {
            // Move na direção oposta
            dir.X = -(float)Math.Sin(Rotacao);
            dir.Y = (float)Math.Cos(Rotacao);
        }

        if (dir != Vector2.Zero) dir.Normalize();
        Posicao += dir * Vel;

        /* mantém dentro da tela */
        Posicao.X = Math.Clamp(Posicao.X, HalfW, w - HalfW);
        Posicao.Y = Math.Clamp(Posicao.Y, HalfH, h - HalfH);
    }

    public void Desenhar(Processing g)
    {
        g.stroke(0);
        g.fill(100, 100, 255);
        
        // Calcula os pontos do triângulo rotacionado
        float cos = (float)Math.Cos(Rotacao);
        float sin = (float)Math.Sin(Rotacao);
        
        // Pontos originais do triângulo (relativos ao centro)
        Vector2[] pontosOriginais = {
            new Vector2(0, -10),   // Ponta superior
            new Vector2(-8, 10),   // Base esquerda
            new Vector2(8, 10)     // Base direita
        };
        
        // Aplica rotação e translação
        Vector2[] pontosRotacionados = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            float x = pontosOriginais[i].X;
            float y = pontosOriginais[i].Y;
            
            pontosRotacionados[i] = new Vector2(
                Posicao.X + (x * cos - y * sin),
                Posicao.Y + (x * sin + y * cos)
            );
        }
        
        g.triangle(pontosRotacionados[0].X, pontosRotacionados[0].Y,
                   pontosRotacionados[1].X, pontosRotacionados[1].Y,
                   pontosRotacionados[2].X, pontosRotacionados[2].Y);
    }

    public Tiro Atirar() 
    {
        // Calcula a direção do tiro baseada na rotação da nave
        Vector2 direcaoTiro = new Vector2(
            (float)Math.Sin(Rotacao),
            -(float)Math.Cos(Rotacao)
        );
        
        // Posição inicial do tiro (na ponta da nave)
        Vector2 posicaoTiro = Posicao + direcaoTiro * 12;
        
        // Velocidade do tiro na direção da nave
        Vector2 velocidadeTiro = direcaoTiro * 8;
        
        return new Tiro(posicaoTiro, velocidadeTiro);
    }
}
