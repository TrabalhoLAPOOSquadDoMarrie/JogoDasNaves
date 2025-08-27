using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AsteroidesCliente.UI;

/// <summary>
/// Menu para personalizacao das opcoes visuais do jogo
/// </summary>
public class MenuPersonalizacao
{
    private SpriteFont _font;
    private Texture2D _pixelTexture;
    private Game.PersonalizacaoJogador _personalizacao;
    
    private KeyboardState _estadoTecladoAnterior;
    private int _opcaoSelecionada = 0;
    private int _subOpcaoSelecionada = 0;
    private bool _modoSelecao = false;

    // Opcoes do menu
    private readonly string[] _opcoes = {
        "Cor da Nave",
        "Modelo da Nave", 
        "Cor do Missil",
        "Tipo de Meteoro",
        "Cor do Fundo",
        "Tipo de Fundo",
        "Salvar e Voltar"
    };

    public bool MenuAtivo { get; set; } = false;
    public bool VoltarParaMenuPrincipal { get; set; } = false;
    
    /// <summary>
    /// Reseta o estado do menu para o inicial
    /// </summary>
    public void ResetarMenu()
    {
        _opcaoSelecionada = 0;
        _subOpcaoSelecionada = 0;
        _modoSelecao = false;
        VoltarParaMenuPrincipal = false;
    }

    public MenuPersonalizacao(SpriteFont font, Texture2D pixelTexture, 
                             Game.PersonalizacaoJogador personalizacao)
    {
        _font = font;
        _pixelTexture = pixelTexture;
        _personalizacao = personalizacao;
    }

    public void Atualizar()
    {
        if (!MenuAtivo) return;

        var estadoTeclado = Keyboard.GetState();

        // Navegação vertical
        if (estadoTeclado.IsKeyDown(Keys.Up) && !_estadoTecladoAnterior.IsKeyDown(Keys.Up))
        {
            if (_modoSelecao)
            {
                _subOpcaoSelecionada = Math.Max(0, _subOpcaoSelecionada - 1);
            }
            else
            {
                _opcaoSelecionada = Math.Max(0, _opcaoSelecionada - 1);
            }
        }

        if (estadoTeclado.IsKeyDown(Keys.Down) && !_estadoTecladoAnterior.IsKeyDown(Keys.Down))
        {
            if (_modoSelecao)
            {
                int maxSubOpcoes = ObterMaximoSubOpcoes();
                _subOpcaoSelecionada = Math.Min(maxSubOpcoes - 1, _subOpcaoSelecionada + 1);
            }
            else
            {
                _opcaoSelecionada = Math.Min(_opcoes.Length - 1, _opcaoSelecionada + 1);
            }
        }

        // Seleção
        if (estadoTeclado.IsKeyDown(Keys.Enter) && !_estadoTecladoAnterior.IsKeyDown(Keys.Enter))
        {
            if (_modoSelecao)
            {
                AplicarSelecao();
                _modoSelecao = false;
                _subOpcaoSelecionada = 0;
            }
            else
            {
                if (_opcaoSelecionada == _opcoes.Length - 1) // Salvar e Voltar
                {
                    _personalizacao.Salvar();
                    VoltarParaMenuPrincipal = true;
                    MenuAtivo = false;
                }
                else
                {
                    _modoSelecao = true;
                    _subOpcaoSelecionada = 0;
                }
            }
        }

        // Voltar
        if (estadoTeclado.IsKeyDown(Keys.Escape) && !_estadoTecladoAnterior.IsKeyDown(Keys.Escape))
        {
            if (_modoSelecao)
            {
                _modoSelecao = false;
                _subOpcaoSelecionada = 0;
            }
            else
            {
                VoltarParaMenuPrincipal = true;
                MenuAtivo = false;
            }
        }

        _estadoTecladoAnterior = estadoTeclado;
    }

    private int ObterMaximoSubOpcoes()
    {
        return _opcaoSelecionada switch
        {
            0 => Game.PersonalizacaoJogador.CoresDisponiveis.Length, // Cor da Nave
            1 => Enum.GetValues<Game.PersonalizacaoJogador.TipoNave>().Length, // Modelo da Nave
            2 => Game.PersonalizacaoJogador.CoresDisponiveis.Length, // Cor do Míssil
            3 => Enum.GetValues<Game.PersonalizacaoJogador.TipoMeteoro>().Length, // Tipo de Meteoro
            4 => Game.PersonalizacaoJogador.CoresFundo.Length, // Cor do Fundo
            5 => Enum.GetValues<Game.PersonalizacaoJogador.TipoFundo>().Length, // Tipo de Fundo
            _ => 1
        };
    }

    private void AplicarSelecao()
    {
        switch (_opcaoSelecionada)
        {
            case 0: // Cor da Nave
                _personalizacao.CorNave = Game.PersonalizacaoJogador.CoresDisponiveis[_subOpcaoSelecionada];
                _personalizacao.CorDetalhesNave = _personalizacao.ObterCorDetalhes();
                break;
            case 1: // Modelo da Nave
                var tiposNave = Enum.GetValues<Game.PersonalizacaoJogador.TipoNave>();
                _personalizacao.ModeloNave = tiposNave[_subOpcaoSelecionada];
                break;
            case 2: // Cor do Míssil
                _personalizacao.CorMissil = Game.PersonalizacaoJogador.CoresDisponiveis[_subOpcaoSelecionada];
                break;
            case 3: // Tipo de Meteoro
                var tiposMeteoro = Enum.GetValues<Game.PersonalizacaoJogador.TipoMeteoro>();
                _personalizacao.ModeloMeteoro = tiposMeteoro[_subOpcaoSelecionada];
                break;
            case 4: // Cor do Fundo
                _personalizacao.CorFundo = Game.PersonalizacaoJogador.CoresFundo[_subOpcaoSelecionada];
                break;
            case 5: // Tipo de Fundo
                var tiposFundo = Enum.GetValues<Game.PersonalizacaoJogador.TipoFundo>();
                _personalizacao.ModeloFundo = tiposFundo[_subOpcaoSelecionada];
                break;
        }
        
        // CORREÇÃO: Salvar as personalizações após aplicar a seleção
        _personalizacao.Salvar();
    }

    public void Desenhar(SpriteBatch spriteBatch, int largura, int altura)
    {
        if (!MenuAtivo) return;

        // Fundo semi-transparente
        var fundoRect = new Rectangle(0, 0, largura, altura);
        spriteBatch.Draw(_pixelTexture, fundoRect, Color.Black * 0.8f);

        // Título
        string titulo = "PERSONALIZACAO";
        var tamanhoTitulo = _font.MeasureString(titulo);
        var posicaoTitulo = new Vector2((largura - tamanhoTitulo.X) / 2, 50);
        DesenharTextoComSombra(spriteBatch, titulo, posicaoTitulo, Color.Cyan, Color.Black, new Vector2(2, 2));

        // Menu principal
        if (!_modoSelecao)
        {
            DesenharMenuPrincipal(spriteBatch, largura, altura);
        }
        else
        {
            DesenharSubMenu(spriteBatch, largura, altura);
        }

        // Preview da personalização
        DesenharPreview(spriteBatch, largura - 200, 150);

        // Instruções
        string instrucoes = _modoSelecao ? 
            "Cima/Baixo: Navegar | ENTER: Selecionar | ESC: Voltar" :
            "Cima/Baixo: Navegar | ENTER: Entrar | ESC: Sair";
        var posicaoInstrucoes = new Vector2(20, altura - 30);
        spriteBatch.DrawString(_font, instrucoes, posicaoInstrucoes, Color.White);
    }

    private void DesenharMenuPrincipal(SpriteBatch spriteBatch, int largura, int altura)
    {
        for (int i = 0; i < _opcoes.Length; i++)
        {
            Color cor = i == _opcaoSelecionada ? Color.Yellow : Color.White;
            string prefixo = i == _opcaoSelecionada ? "> " : "  ";
            
            string valorAtual = ObterValorAtual(i);
            string texto = $"{prefixo}{_opcoes[i]}: {valorAtual}";
            
            var posicao = new Vector2(50, 150 + i * 40);
            spriteBatch.DrawString(_font, texto, posicao, cor);
        }
    }

    private void DesenharSubMenu(SpriteBatch spriteBatch, int largura, int altura)
    {
        string tituloSubMenu = $"Selecionar {_opcoes[_opcaoSelecionada]}";
        var posicaoSubTitulo = new Vector2(50, 120);
        spriteBatch.DrawString(_font, tituloSubMenu, posicaoSubTitulo, Color.Cyan);

        var opcoes = ObterOpcoesSubMenu();
        for (int i = 0; i < opcoes.Length; i++)
        {
            Color cor = i == _subOpcaoSelecionada ? Color.Yellow : Color.White;
            string prefixo = i == _subOpcaoSelecionada ? "> " : "  ";
            string texto = $"{prefixo}{opcoes[i]}";
            
            var posicao = new Vector2(70, 160 + i * 30);
            spriteBatch.DrawString(_font, texto, posicao, cor);

            // Mostra preview da cor se aplicável
            if ((_opcaoSelecionada == 0 || _opcaoSelecionada == 2 || _opcaoSelecionada == 4) && i == _subOpcaoSelecionada)
            {
                Color corPreview = ObterCorPreview(i);
                var rectPreview = new Rectangle((int)posicao.X + 200, (int)posicao.Y, 20, 20);
                spriteBatch.Draw(_pixelTexture, rectPreview, corPreview);
            }
        }
    }

    private string[] ObterOpcoesSubMenu()
    {
        return _opcaoSelecionada switch
        {
            0 => new[] { "Azul", "Vermelho", "Verde", "Roxo", "Laranja", "Ciano", "Amarelo", "Rosa", "Verde-Limao", "Magenta" },
            1 => Enum.GetNames<Game.PersonalizacaoJogador.TipoNave>(),
            2 => new[] { "Azul", "Vermelho", "Verde", "Roxo", "Laranja", "Ciano", "Amarelo", "Rosa", "Verde-Limao", "Magenta" },
            3 => new[] { "Rochoso", "Metalico", "Cristalino", "Gelado" },
            4 => new[] { "Preto", "Azul Escuro", "Vermelho Escuro", "Verde Escuro", "Cinza Escuro", "Azul Meia-Noite" },
            5 => new[] { "Espaco Profundo", "Nebulosa", "Campo Estelar", "Galaxia Espiral" },
            _ => new[] { "" }
        };
    }

    private Color ObterCorPreview(int indice)
    {
        return _opcaoSelecionada switch
        {
            0 => Game.PersonalizacaoJogador.CoresDisponiveis[indice],
            2 => Game.PersonalizacaoJogador.CoresDisponiveis[indice],
            4 => Game.PersonalizacaoJogador.CoresFundo[indice],
            _ => Color.White
        };
    }

    private string ObterValorAtual(int opcao)
    {
        return opcao switch
        {
            0 => "Personalizada",
            1 => _personalizacao.ModeloNave.ToString(),
            2 => "Personalizada",
            3 => _personalizacao.ModeloMeteoro.ToString(),
            4 => "Personalizada",
            5 => _personalizacao.ModeloFundo.ToString(),
            _ => ""
        };
    }

    private void DesenharPreview(SpriteBatch spriteBatch, int x, int y)
    {
        // Desenha um preview da nave personalizada
        var posicaoNave = new Vector2(x, y);
        DesenharNavePreview(spriteBatch, posicaoNave, _personalizacao.CorNave, _personalizacao.ObterCorDetalhes());

        // Desenha um preview do míssil
        var posicaoMissil = new Vector2(x, y + 50);
        DesenharMissilPreview(spriteBatch, posicaoMissil, _personalizacao.CorMissil);

        // Desenha um preview do meteoro
        var posicaoMeteoro = new Vector2(x, y + 100);
        var (corMeteoro, detalheMeteoro) = _personalizacao.ObterCoresMeteoro();
        DesenharMeteoroPreview(spriteBatch, posicaoMeteoro, corMeteoro, detalheMeteoro);
    }

    private void DesenharNavePreview(SpriteBatch spriteBatch, Vector2 posicao, Color corPrincipal, Color corDetalhes)
    {
        var textura = Game.PersonalizacaoJogador.TexturasNave[(int)_personalizacao.ModeloNave];
        spriteBatch.Draw(textura, posicao, null, corPrincipal, 0f, new Vector2(textura.Width / 2, textura.Height / 2), 1.0f, SpriteEffects.None, 0f);
    }

    private void DesenharMissilPreview(SpriteBatch spriteBatch, Vector2 posicao, Color cor)
    {
        var rect = new Rectangle((int)posicao.X - 2, (int)posicao.Y - 2, 4, 4);
        spriteBatch.Draw(_pixelTexture, rect, cor);
    }

    private void DesenharMeteoroPreview(SpriteBatch spriteBatch, Vector2 posicao, Color corPrincipal, Color corDetalhes)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        
        var rect1 = new Rectangle(x - 4, y - 4, 8, 8);
        spriteBatch.Draw(_pixelTexture, rect1, corPrincipal);
        
        var rect2 = new Rectangle(x - 2, y - 2, 4, 4);
        spriteBatch.Draw(_pixelTexture, rect2, corDetalhes);
    }

    private void DesenharTextoComSombra(SpriteBatch spriteBatch, string texto, Vector2 posicao, Color corTexto, Color corSombra, Vector2 offsetSombra)
    {
        spriteBatch.DrawString(_font, texto, posicao + offsetSombra, corSombra);
        spriteBatch.DrawString(_font, texto, posicao, corTexto);
    }
}