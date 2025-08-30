using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AsteroidesCliente.Game;

/// <summary>
/// Gerencia as personalizacoes visuais do jogador
/// </summary>
public class PersonalizacaoJogador
{
    public enum TipoNave
    {
        Nave1,
        Nave2,
        Nave3,
        Nave4
    }

    public enum TipoMeteoro
    {
        Rochoso,
        Metalico,
        Cristalino,
        Gelado
    }

    public enum TipoFundo
    {
        EspacoProfundo,
        Nebulosa,
        CampoEstelar,
        GalaxiaEspiral
    }

    public enum TipoMusica
    {
        Desabilitada,
        MantisLords,
        Medley,
        Metallica,
        Aleatoria
    }

    // Propriedades de personalizacao
    public Color CorNave { get; set; } = Color.CornflowerBlue;
    public Color CorDetalhesNave { get; set; } = Color.LightBlue;
    public Color CorMissil { get; set; } = Color.Yellow;
    public Color CorFundo { get; set; } = Color.Black;
    public TipoNave ModeloNave { get; set; } = TipoNave.Nave1;
    public TipoMeteoro ModeloMeteoro { get; set; } = TipoMeteoro.Rochoso;
    public TipoFundo ModeloFundo { get; set; } = TipoFundo.EspacoProfundo;
    public TipoMusica MusicaFundo { get; set; } = TipoMusica.MantisLords;

    // Texturas das naves
    public static Texture2D[]? TexturasNave { get; set; }
    public static Texture2D[]? TexturasAsteroide { get; set; }

    // Cores predefinidas para selecao rapida
    public static readonly Color[] CoresDisponiveis = {
        Color.CornflowerBlue,
        Color.Red,
        Color.Green,
        Color.Purple,
        Color.Orange,
        Color.Cyan,
        Color.Yellow,
        Color.Pink,
        Color.LimeGreen,
        Color.Magenta
    };

    // Cores de fundo predefinidas
    public static readonly Color[] CoresFundo = {
        Color.Black,
        Color.DarkBlue,
        Color.DarkRed,
        Color.DarkGreen,
        Color.DarkSlateGray,
        Color.MidnightBlue
    };

    /// <summary>
    /// Obtem a cor de detalhes baseada na cor principal da nave
    /// </summary>
    public Color ObterCorDetalhes()
    {
        // Calcula uma cor mais clara baseada na cor principal
        return Color.FromNonPremultiplied(
            Math.Min(255, CorNave.R + 50),
            Math.Min(255, CorNave.G + 50),
            Math.Min(255, CorNave.B + 50),
            255
        );
    }

    /// <summary>
    /// Obtem as cores do meteoro baseadas no tipo selecionado
    /// </summary>
    public (Color corPrincipal, Color corDetalhes) ObterCoresMeteoro()
    {
        return ModeloMeteoro switch
        {
            TipoMeteoro.Rochoso => (Color.SaddleBrown, Color.Brown),
            TipoMeteoro.Metalico => (Color.Gray, Color.Silver),
            TipoMeteoro.Cristalino => (Color.LightBlue, Color.Cyan),
            TipoMeteoro.Gelado => (Color.LightCyan, Color.White),
            _ => (Color.SaddleBrown, Color.Brown)
        };
    }

    /// <summary>
    /// Obtem as dimensoes da resolucao (fixo em HD)
    /// </summary>
    public (int largura, int altura) ObterDimensoesResolucao()
    {
        return (1280, 720);
    }

    /// <summary>
    /// Salva as personalizacoes em um arquivo
    /// </summary>
    public void Salvar(string caminho = "personalizacao.txt")
    {
        try
        {
            var linhas = new[]
            {
                $"CorNave={CorNave.R},{CorNave.G},{CorNave.B}",
                $"CorMissil={CorMissil.R},{CorMissil.G},{CorMissil.B}",
                $"CorFundo={CorFundo.R},{CorFundo.G},{CorFundo.B}",
                $"ModeloNave={ModeloNave}",
                $"ModeloMeteoro={ModeloMeteoro}",
                $"ModeloFundo={ModeloFundo}",
                $"MusicaFundo={MusicaFundo}"
            };
            File.WriteAllLines(caminho, linhas);
        }
        catch
        {
            // Ignora erros de salvamento
        }
    }

    /// <summary>
    /// Carrega as personalizacoes de um arquivo
    /// </summary>
    public void Carregar(string caminho = "personalizacao.txt")
    {
        try
        {
            if (!File.Exists(caminho)) return;

            var linhas = File.ReadAllLines(caminho);
            foreach (var linha in linhas)
            {
                var partes = linha.Split('=');
                if (partes.Length != 2) continue;

                var chave = partes[0];
                var valor = partes[1];

                switch (chave)
                {
                    case "CorNave":
                        var coresNave = valor.Split(',');
                        if (coresNave.Length == 3)
                            CorNave = new Color(int.Parse(coresNave[0]), int.Parse(coresNave[1]), int.Parse(coresNave[2]));
                        break;
                    case "CorMissil":
                        var coresMissil = valor.Split(',');
                        if (coresMissil.Length == 3)
                            CorMissil = new Color(int.Parse(coresMissil[0]), int.Parse(coresMissil[1]), int.Parse(coresMissil[2]));
                        break;
                    case "CorFundo":
                        var coresFundo = valor.Split(',');
                        if (coresFundo.Length == 3)
                            CorFundo = new Color(int.Parse(coresFundo[0]), int.Parse(coresFundo[1]), int.Parse(coresFundo[2]));
                        break;
                    case "ModeloNave":
                        if (Enum.TryParse<TipoNave>(valor, out var tipoNave))
                            ModeloNave = tipoNave;
                        break;
                    case "ModeloMeteoro":
                        if (Enum.TryParse<TipoMeteoro>(valor, out var tipoMeteoro))
                            ModeloMeteoro = tipoMeteoro;
                        break;
                    case "ModeloFundo":
                        if (Enum.TryParse<TipoFundo>(valor, out var tipoFundo))
                            ModeloFundo = tipoFundo;
                        break;
                    case "MusicaFundo":
                        if (Enum.TryParse<TipoMusica>(valor, out var tipoMusica))
                            MusicaFundo = tipoMusica;
                        break;
                }
            }
        }
        catch
        {
            // Ignora erros de carregamento
        }
    }

    /// <summary>
    /// Obtém o nome do arquivo da música baseado na seleção
    /// </summary>
    public string? ObterNomeArquivoMusica()
    {
        return MusicaFundo switch
        {
            TipoMusica.Desabilitada => null,
            TipoMusica.MantisLords => "Sounds/Mantis-Lords",
            TipoMusica.Medley => "Sounds/Medley", 
            TipoMusica.Metallica => "Sounds/Metallica-Enter-Sandman",
            TipoMusica.Aleatoria => ObterMusicaAleatoria(),
            _ => null
        };
    }

    /// <summary>
    /// Obtém o nome amigável da música selecionada
    /// </summary>
    public string ObterNomeAmigavelMusica()
    {
        return MusicaFundo switch
        {
            TipoMusica.Desabilitada => "Desabilitada",
            TipoMusica.MantisLords => "Mantis Lords",
            TipoMusica.Medley => "Medley Clássico",
            TipoMusica.Metallica => "Enter Sandman",
            TipoMusica.Aleatoria => "Aleatória",
            _ => "Desconhecida"
        };
    }

    /// <summary>
    /// Seleciona uma música aleatória (exceto Desabilitada e Aleatoria)
    /// </summary>
    private string ObterMusicaAleatoria()
    {
        var musicas = new[] 
        {
            "Sounds/Mantis-Lords",
            "Sounds/Medley", 
            "Sounds/Metallica-Enter-Sandman"
        };
        var random = new Random();
        return musicas[random.Next(musicas.Length)];
    }
}