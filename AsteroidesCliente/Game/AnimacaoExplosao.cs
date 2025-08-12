using Microsoft.Xna.Framework;

namespace AsteroidesCliente.Game;

/// <summary>
/// Representa uma animação de explosão quando um meteoro é destruído
/// </summary>
public class AnimacaoExplosao
{
    public Vector2 Posicao { get; set; }
    public int TempoVida { get; set; }
    public int TempoVidaMaximo { get; set; }
    public List<ParticulaExplosao> Particulas { get; set; }
    public float Raio { get; set; }

    public AnimacaoExplosao(Vector2 posicao, float raio)
    {
        Posicao = posicao;
        Raio = raio;
        TempoVidaMaximo = 60; // 1 segundo a 60 FPS
        TempoVida = TempoVidaMaximo;
        Particulas = new List<ParticulaExplosao>();

        // Cria partículas da explosão
        CriarParticulas();
    }

    private void CriarParticulas()
    {
        Random rnd = new Random();
        int numParticulas = (int)(Raio * 0.8f); // Mais partículas para meteoros maiores

        for (int i = 0; i < numParticulas; i++)
        {
            float angulo = (float)(rnd.NextDouble() * Math.PI * 2);
            float velocidade = (float)(rnd.NextDouble() * 3 + 1);
            Vector2 direcao = new Vector2((float)Math.Cos(angulo), (float)Math.Sin(angulo));
            
            var particula = new ParticulaExplosao
            {
                Posicao = Posicao + new Vector2(
                    (float)(rnd.NextDouble() - 0.5) * Raio * 0.5f,
                    (float)(rnd.NextDouble() - 0.5) * Raio * 0.5f
                ),
                Velocidade = direcao * velocidade,
                Cor = ObterCorParticula(rnd),
                Tamanho = (float)(rnd.NextDouble() * 3 + 1),
                VidaRestante = (int)(rnd.NextDouble() * 40 + 20)
            };

            Particulas.Add(particula);
        }
    }

    private Color ObterCorParticula(Random rnd)
    {
        // Cores típicas de explosão: laranja, vermelho, amarelo, branco
        Color[] cores = {
            Color.Orange,
            Color.Red,
            Color.Yellow,
            Color.White,
            Color.OrangeRed,
            Color.Gold
        };

        return cores[rnd.Next(cores.Length)];
    }

    public void Atualizar()
    {
        TempoVida--;

        // Atualiza todas as partículas
        for (int i = Particulas.Count - 1; i >= 0; i--)
        {
            var particula = Particulas[i];
            particula.Atualizar();

            // Remove partículas mortas
            if (particula.VidaRestante <= 0)
            {
                Particulas.RemoveAt(i);
            }
        }
    }

    public bool EstaViva()
    {
        return TempoVida > 0 && Particulas.Count > 0;
    }

    public void Desenhar(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, 
                        Microsoft.Xna.Framework.Graphics.Texture2D pixelTexture)
    {
        foreach (var particula in Particulas)
        {
            particula.Desenhar(spriteBatch, pixelTexture);
        }
    }
}

/// <summary>
/// Representa uma partícula individual da explosão
/// </summary>
public class ParticulaExplosao
{
    public Vector2 Posicao { get; set; }
    public Vector2 Velocidade { get; set; }
    public Color Cor { get; set; }
    public float Tamanho { get; set; }
    public int VidaRestante { get; set; }

    public void Atualizar()
    {
        // Move a partícula
        Posicao += Velocidade;
        
        // Aplica resistência do ar
        Velocidade *= 0.98f;
        
        // Diminui a vida
        VidaRestante--;
        
        // Fade out baseado na vida restante
        float fatorVida = VidaRestante / 60f;
        if (fatorVida < 1f)
        {
            Cor = Color.FromNonPremultiplied(
                Cor.R,
                Cor.G,
                Cor.B,
                (int)(255 * Math.Max(0, fatorVida))
            );
        }
    }

    public void Desenhar(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, 
                        Microsoft.Xna.Framework.Graphics.Texture2D pixelTexture)
    {
        int x = (int)Posicao.X;
        int y = (int)Posicao.Y;
        int tamanho = Math.Max(1, (int)Tamanho);

        var rect = new Rectangle(x - tamanho/2, y - tamanho/2, tamanho, tamanho);
        spriteBatch.Draw(pixelTexture, rect, Cor);

        // Adiciona brilho para partículas maiores
        if (Tamanho > 2)
        {
            var brilhoRect = new Rectangle(x - 1, y - 1, 2, 2);
            spriteBatch.Draw(pixelTexture, brilhoRect, Color.White * 0.8f);
        }
    }
}