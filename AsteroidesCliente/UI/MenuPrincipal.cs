using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using AsteroidesCliente.Game;

namespace AsteroidesCliente.UI;

/// <summary>
/// Tela de menu principal do jogo
/// </summary>
public class MenuPrincipal
{
    private readonly SpriteFont _font;
    public enum EstadoMenu
    {
        MenuInicial,
        Configuracao,
        Conectando,
        Conectado,
        Erro
    }

    public EstadoMenu Estado { get; private set; } = EstadoMenu.MenuInicial;
    public string EnderecoServidor { get; private set; } = "localhost";
    public int PortaServidor { get; private set; } = 8890;
    public string NomeJogador { get; private set; } = "";
    public NivelDificuldade DificuldadeSelecionada { get; private set; } = NivelDificuldade.Medio;

    private int _opcaoSelecionada = 0;
    private readonly string[] _opcoesMenu = { "Jogo Online", "Configuracoes", "Personalizacao", "Sair" };
    private readonly string[] _opcoesConfig = { "Endereco: ", "Porta: ", "Nome: ", "Dificuldade: ", "Voltar" };
    
    private string _mensagemErro = "";
    private string _inputAtual = "";
    private int _campoEditando = -1;
    private KeyboardState _estadoTecladoAnterior;
    private bool _inicializado = false;
    private int _frameCount = 0;

    public event Action? IniciarJogo;
    public event Action? SairJogo;
    public event Action? AbrirPersonalizacao;
    public event Action? VoltarAoJogo;
    public event Action? DesconectarDoServidor;

    // Propriedades para contexto gráfico
    public GraphicsDevice? GraphicsDevice { get; set; }
    public ContentManager? Content { get; set; }

    public MenuPrincipal(SpriteFont font)
    {
        _font = font;
    }

    public void Setup()
    {
        try
        {
            NomeJogador = "";
            _estadoTecladoAnterior = Keyboard.GetState();
            _inicializado = true;
            Console.WriteLine("Menu principal inicializado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro na inicializacao do menu: {ex.Message}");
            throw;
        }
    }

    public void Update()
    {
        if (!_inicializado) return;
        
        _frameCount++;
        ProcessarInput();
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        try
        {
            if (!_inicializado) return;

            switch (Estado)
            {
                case EstadoMenu.MenuInicial:
                    DesenharMenuInicial(spriteBatch, screenWidth, screenHeight);
                    break;
                case EstadoMenu.Configuracao:
                    DesenharMenuConfiguracao(spriteBatch, screenWidth, screenHeight);
                    break;
                case EstadoMenu.Conectando:
                    DesenharTelaConectando(spriteBatch, screenWidth, screenHeight);
                    break;
                case EstadoMenu.Conectado:
                    DesenharTelaConectado(spriteBatch, screenWidth, screenHeight);
                    break;
                case EstadoMenu.Erro:
                    DesenharTelaErro(spriteBatch, screenWidth, screenHeight);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no Draw do menu: {ex.Message}");
        }
    }

    private void DesenharMenuInicial(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        // Título
        var titulo = "ASTEROIDES MULTIPLAYER";
        var tamanhoTitulo = _font.MeasureString(titulo);
        spriteBatch.DrawString(_font, titulo, new Vector2(screenWidth / 2 - tamanhoTitulo.X / 2, 150), Color.Yellow);

        var subtitulo = "Trabalho de TPOO";
        var tamanhoSubtitulo = _font.MeasureString(subtitulo);
        spriteBatch.DrawString(_font, subtitulo, new Vector2(screenWidth / 2 - tamanhoSubtitulo.X / 2, 200), Color.LightGray);

        // Menu
        for (int i = 0; i < _opcoesMenu.Length; i++)
        {
            var cor = i == _opcaoSelecionada ? Color.Yellow : Color.White;
            var tamanhoOpcao = _font.MeasureString(_opcoesMenu[i]);
            spriteBatch.DrawString(_font, _opcoesMenu[i], new Vector2(screenWidth / 2 - tamanhoOpcao.X / 2, 350 + i * 50), cor);
        }

        // Instruções
        var instrucoes = "Use as setas para navegar e Enter para selecionar";
        var tamanhoInstrucoes = _font.MeasureString(instrucoes);
        spriteBatch.DrawString(_font, instrucoes, new Vector2(screenWidth / 2 - tamanhoInstrucoes.X / 2, 550), Color.Gray);
    }

    private void DesenharMenuConfiguracao(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        // Título
        var titulo = "Configuracoes";
        var tamanhoTitulo = _font.MeasureString(titulo);
        spriteBatch.DrawString(_font, titulo, new Vector2(screenWidth / 2 - tamanhoTitulo.X / 2, 100), Color.Yellow);

        // Opções de configuração
        for (int i = 0; i < _opcoesConfig.Length; i++)
        {
            float y = 200 + i * 60;
            
            var cor = i == _opcaoSelecionada ? Color.Yellow : Color.White;
            var textoOpcao = _opcoesConfig[i];
            var valor = "";
            switch (i)
            {
                case 0: valor = EnderecoServidor; break;
                case 1: valor = PortaServidor.ToString(); break;
                case 2: valor = NomeJogador; break;
                case 3: valor = ObterNomeDificuldade(DificuldadeSelecionada); break;
            }

            if (_campoEditando == i && i != 3) // Dificuldade não é editável por texto
            {
                valor = _inputAtual + ( (_frameCount / 30) % 2 == 0 ? "_" : "" );
                cor = Color.LightGreen;
            }
            else if (i == 3 && _opcaoSelecionada == 3) // Destaque especial para dificuldade
            {
                cor = Color.Cyan;
            }

            var textoCompleto = textoOpcao + valor;
            var tamanhoTexto = _font.MeasureString(textoCompleto);
            spriteBatch.DrawString(_font, textoCompleto, new Vector2(screenWidth / 2 - tamanhoTexto.X / 2, y), cor);
            
            // Adiciona descrição da dificuldade
            if (i == 3)
            {
                string descricao = ObterDescricaoDificuldade(DificuldadeSelecionada);
                var tamanhoDescricao = _font.MeasureString(descricao);
                spriteBatch.DrawString(_font, descricao, new Vector2(screenWidth / 2 - tamanhoDescricao.X / 2, y + 25), Color.Gray);
            }
        }

        // Instruções
        var instrucoes = "Setas: navegar | Enter: editar/salvar | Esquerda/Direita: mudar dificuldade | Esc: voltar";
        var tamanhoInstrucoes = _font.MeasureString(instrucoes);
        spriteBatch.DrawString(_font, instrucoes, new Vector2(screenWidth / 2 - tamanhoInstrucoes.X / 2, 550), Color.Gray);
    }

    private string ObterNomeDificuldade(NivelDificuldade nivel)
    {
        return nivel switch
        {
            NivelDificuldade.Facil => "Facil",
            NivelDificuldade.Medio => "Medio",
            NivelDificuldade.Dificil => "Dificil",
            _ => "Desconhecido"
        };
    }

    private string ObterDescricaoDificuldade(NivelDificuldade nivel)
    {
        return nivel switch
        {
            NivelDificuldade.Facil => "Velocidade constante",
            NivelDificuldade.Medio => "Acelera ate velocidade media",
            NivelDificuldade.Dificil => "Tres fases de dificuldade crescente",
            _ => ""
        };
    }

    private void DesenharTelaConectando(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var texto = "Conectando...";
        var tamanhoTexto = _font.MeasureString(texto);
        spriteBatch.DrawString(_font, texto, new Vector2(screenWidth / 2 - tamanhoTexto.X / 2, screenHeight / 2 - 20), Color.Yellow);

        var textoServidor = $"ao servidor {EnderecoServidor}:{PortaServidor}";
        var tamanhoServidor = _font.MeasureString(textoServidor);
        spriteBatch.DrawString(_font, textoServidor, new Vector2(screenWidth / 2 - tamanhoServidor.X / 2, screenHeight / 2 + 20), Color.White);
    }

    private void DesenharTelaConectado(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var texto = "Conectado com sucesso!";
        var tamanhoTexto = _font.MeasureString(texto);
        spriteBatch.DrawString(_font, texto, new Vector2(screenWidth / 2 - tamanhoTexto.X / 2, screenHeight / 2 - 60), Color.LightGreen);

        var textoJogador = $"Bem-vindo, {NomeJogador}!";
        var tamanhoJogador = _font.MeasureString(textoJogador);
        spriteBatch.DrawString(_font, textoJogador, new Vector2(screenWidth / 2 - tamanhoJogador.X / 2, screenHeight / 2 - 20), Color.White);

        var textoAguardando = "Aguardando outros jogadores...";
        var tamanhoAguardando = _font.MeasureString(textoAguardando);
        spriteBatch.DrawString(_font, textoAguardando, new Vector2(screenWidth / 2 - tamanhoAguardando.X / 2, screenHeight / 2 + 20), Color.White);

        // Instruções de controle
        var instrucoes = "Enter: Voltar ao jogo | Esc: Desconectar";
        var tamanhoInstrucoes = _font.MeasureString(instrucoes);
        spriteBatch.DrawString(_font, instrucoes, new Vector2(screenWidth / 2 - tamanhoInstrucoes.X / 2, screenHeight / 2 + 60), Color.Yellow);
    }

    private void DesenharTelaErro(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var texto = "Erro de Conexao";
        var tamanhoTexto = _font.MeasureString(texto);
        spriteBatch.DrawString(_font, texto, new Vector2(screenWidth / 2 - tamanhoTexto.X / 2, screenHeight / 2 - 60), Color.Red);

        var tamanhoErro = _font.MeasureString(_mensagemErro);
        spriteBatch.DrawString(_font, _mensagemErro, new Vector2(screenWidth / 2 - tamanhoErro.X / 2, screenHeight / 2 - 20), Color.White);

        var instrucao = "Pressione Enter para voltar ao menu";
        var tamanhoInstrucao = _font.MeasureString(instrucao);
        spriteBatch.DrawString(_font, instrucao, new Vector2(screenWidth / 2 - tamanhoInstrucao.X / 2, screenHeight / 2 + 40), Color.Yellow);
    }

    private void ProcessarInput()
    {
        var estadoTeclado = Keyboard.GetState();
        var teclasPressionadas = estadoTeclado.GetPressedKeys();
        var teclasAnteriores = _estadoTecladoAnterior.GetPressedKeys();

        // Verifica se alguma tecla foi pressionada (não estava pressionada antes)
        foreach (var tecla in teclasPressionadas)
        {
            if (!teclasAnteriores.Contains(tecla))
            {
                ProcessarTecla(tecla);
            }
        }

        _estadoTecladoAnterior = estadoTeclado;
    }

    private void ProcessarTecla(Keys tecla)
    {
        switch (Estado)
        {
            case EstadoMenu.MenuInicial:
                ProcessarInputMenuInicial(tecla);
                break;
            case EstadoMenu.Configuracao:
                ProcessarInputConfiguracao(tecla);
                break;
            case EstadoMenu.Conectado:
                if (tecla == Keys.Enter)
                {
                    // Volta ao jogo se já estiver conectado
                    VoltarAoJogo?.Invoke();
                }
                else if (tecla == Keys.Escape)
                {
                    // Desconecta e volta ao menu inicial
                    DesconectarDoServidor?.Invoke();
                    Estado = EstadoMenu.MenuInicial;
                }
                break;
            case EstadoMenu.Erro:
                if (tecla == Keys.Enter)
                {
                    Estado = EstadoMenu.MenuInicial;
                    _mensagemErro = "";
                }
                break;
        }
    }

    private void ProcessarInputMenuInicial(Keys tecla)
    {
        switch (tecla)
        {
            case Keys.Up:
                _opcaoSelecionada = (_opcaoSelecionada - 1 + _opcoesMenu.Length) % _opcoesMenu.Length;
                break;
            case Keys.Down:
                _opcaoSelecionada = (_opcaoSelecionada + 1) % _opcoesMenu.Length;
                break;
            case Keys.Enter:
                switch (_opcaoSelecionada)
                {
                    case 0: // Jogo Online
                        Estado = EstadoMenu.Conectando;
                        IniciarJogo?.Invoke();
                        break;
                    case 1: // Configurações
                        Estado = EstadoMenu.Configuracao;
                        _opcaoSelecionada = 0;
                        break;
                    case 2: // Personalização
                        AbrirPersonalizacao?.Invoke();
                        break;
                    case 3: // Sair
                        SairJogo?.Invoke();
                        break;
                }
                break;
        }
    }

    private void ProcessarInputConfiguracao(Keys tecla)
    {
        if (_campoEditando >= 0)
        {
            // Modo de edição
            switch (tecla)
            {
                case Keys.Enter:
                    // Confirma a edição
                    switch (_campoEditando)
                    {
                        case 0:
                            if (!string.IsNullOrWhiteSpace(_inputAtual))
                                EnderecoServidor = _inputAtual;
                            break;
                        case 1:
                            if (int.TryParse(_inputAtual, out int porta) && porta > 0 && porta <= 65535)
                                PortaServidor = porta;
                            break;
                        case 2:
                            if (!string.IsNullOrWhiteSpace(_inputAtual))
                                NomeJogador = _inputAtual;
                            break;
                    }
                    _campoEditando = -1;
                    _inputAtual = "";
                    break;
                case Keys.Escape:
                    // Cancela a edição
                    _campoEditando = -1;
                    _inputAtual = "";
                    break;
                case Keys.Back:
                    // Remove último caractere
                    if (_inputAtual.Length > 0)
                        _inputAtual = _inputAtual.Substring(0, _inputAtual.Length - 1);
                    break;
                default:
                    // Adiciona caractere - permite maiúsculas e minúsculas
                    string caractere = ObterCaractereDigitado(tecla);
                    if (!string.IsNullOrEmpty(caractere))
                    {
                        _inputAtual += caractere;
                    }
                    break;
            }
        }
        else
        {
            // Modo de navegação
            switch (tecla)
            {
                case Keys.Up:
                    _opcaoSelecionada = (_opcaoSelecionada - 1 + _opcoesConfig.Length) % _opcoesConfig.Length;
                    break;
                case Keys.Down:
                    _opcaoSelecionada = (_opcaoSelecionada + 1) % _opcoesConfig.Length;
                    break;
                case Keys.Left:
                    // Navega dificuldade para a esquerda
                    if (_opcaoSelecionada == 3)
                    {
                        DificuldadeSelecionada = DificuldadeSelecionada switch
                        {
                            NivelDificuldade.Medio => NivelDificuldade.Facil,
                            NivelDificuldade.Dificil => NivelDificuldade.Medio,
                            _ => NivelDificuldade.Dificil
                        };
                    }
                    break;
                case Keys.Right:
                    // Navega dificuldade para a direita
                    if (_opcaoSelecionada == 3)
                    {
                        DificuldadeSelecionada = DificuldadeSelecionada switch
                        {
                            NivelDificuldade.Facil => NivelDificuldade.Medio,
                            NivelDificuldade.Medio => NivelDificuldade.Dificil,
                            _ => NivelDificuldade.Facil
                        };
                    }
                    break;
                case Keys.Enter:
                    if (_opcaoSelecionada == 4) // Voltar (agora é índice 4)
                    {
                        Estado = EstadoMenu.MenuInicial;
                        _opcaoSelecionada = 0;
                    }
                    else if (_opcaoSelecionada == 3) // Dificuldade - navega com setas esquerda/direita
                    {
                        // Não faz nada, usa as setas para navegar
                    }
                    else
                    {
                        // Inicia edição do campo
                        _campoEditando = _opcaoSelecionada;
                        switch (_opcaoSelecionada)
                        {
                            case 0: _inputAtual = EnderecoServidor; break;
                            case 1: _inputAtual = PortaServidor.ToString(); break;
                            case 2: _inputAtual = NomeJogador; break;
                        }
                    }
                    break;
                case Keys.Escape:
                    Estado = EstadoMenu.MenuInicial;
                    _opcaoSelecionada = 0;
                    break;
            }
        }
    }

    private string ObterCaractereDigitado(Keys tecla)
    {
        var estadoTeclado = Keyboard.GetState();
        bool shiftPressionado = estadoTeclado.IsKeyDown(Keys.LeftShift) || estadoTeclado.IsKeyDown(Keys.RightShift);
        
        // Verifica CapsLock de forma segura (apenas no Windows)
        bool capsLockAtivo = false;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                capsLockAtivo = Console.CapsLock;
            }
        }
        catch
        {
            // Ignora erro se não conseguir acessar CapsLock
        }

        // Determina se deve usar maiúscula
        bool usarMaiuscula = (shiftPressionado && !capsLockAtivo) || (!shiftPressionado && capsLockAtivo);

        // Mapeia as teclas de letras
        if (tecla >= Keys.A && tecla <= Keys.Z)
        {
            char letra = (char)('a' + (tecla - Keys.A));
            return usarMaiuscula ? letra.ToString().ToUpper() : letra.ToString();
        }

        // Mapeia as teclas de números (apenas números, sem caracteres especiais)
        if (tecla >= Keys.D0 && tecla <= Keys.D9)
        {
            return ((char)('0' + (tecla - Keys.D0))).ToString();
        }

        // Mapeia teclas do teclado numérico
        if (tecla >= Keys.NumPad0 && tecla <= Keys.NumPad9)
        {
            return ((char)('0' + (tecla - Keys.NumPad0))).ToString();
        }

        // Mapeia apenas caracteres básicos seguros
        return tecla switch
        {
            Keys.Space => " ",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            _ => ""
        };
    }

    public void DefinirEstado(EstadoMenu novoEstado, string mensagemErro = "")
    {
        Estado = novoEstado;
        _mensagemErro = mensagemErro;
    }

    public void DefinirNomeJogador(string nome)
    {
        NomeJogador = nome;
    }
}