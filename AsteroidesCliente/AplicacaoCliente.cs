using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AsteroidesCliente.Network;
using AsteroidesCliente.UI;
using AsteroidesCliente.Game;
using Microsoft.Xna.Framework.Audio;

namespace AsteroidesCliente;

public class AplicacaoCliente : Microsoft.Xna.Framework.Game
{
    public enum EstadoAplicacao
    {
        Menu,
        Jogo,
        Personalizacao
    }

    private GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;

    private SoundEffect? _somTiro;
    private SoundEffect? _somExplosao;
    private SoundEffect? _somClick;

    private EstadoAplicacao _estadoAtual = EstadoAplicacao.Menu;
    private MenuPrincipal? _menuPrincipal;
    private TelaJogo? _telaJogo;
    private MenuPersonalizacao? _menuPersonalizacao;
    private PersonalizacaoJogador? _personalizacao;
    private ClienteRede? _clienteRede;
    private bool _inicializado = false;

    // Novo: rastrear nome/ID do jogador atual para repassar à tela e restaurar controle
    private string? _nomeJogadorAtual;
    private int _meuJogadorId = -1;

    public AplicacaoCliente()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Inicializar com resolução padrão - será atualizada no LoadContent
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
    }

    protected override void Initialize()
    {
        base.Initialize();
        Console.WriteLine("Cliente Asteroides Multiplayer inicializado");
    }

    protected override void LoadContent()
    {
        try
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = Content.Load<SpriteFont>("font");

            _somTiro = Content.Load<SoundEffect>("Sounds/tiro");
            _somExplosao = Content.Load<SoundEffect>("Sounds/explosao");
            _somClick = Content.Load<SoundEffect>("Sounds/click");

            _menuPrincipal = new MenuPrincipal(_font, _somClick);
            _menuPrincipal.GraphicsDevice = GraphicsDevice;
            _menuPrincipal.Content = Content;
            _menuPrincipal.IniciarJogo += IniciarJogo;
            _menuPrincipal.SairJogo += SairJogo;
            _menuPrincipal.AbrirPersonalizacao += AbrirPersonalizacao;
            _menuPrincipal.VoltarAoJogo += VoltarAoJogo;
            _menuPrincipal.DesconectarDoServidor += DesconectarDoServidor;
            _menuPrincipal.Setup();

            // Inicializar sistema de personalização
            _personalizacao = new PersonalizacaoJogador();
            _personalizacao.Carregar();

            // Criar textura pixel para o menu de personalização
            var pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            _menuPersonalizacao = new MenuPersonalizacao(_font, pixelTexture, _personalizacao);

            // Aplicar resolução salva
            var (largura, altura) = _personalizacao.ObterDimensoesResolucao();
            AplicarResolucao(largura, altura);

            _inicializado = true;
            
            // Conexão automática removida - agora o usuário tem controle total
            // _ = ConectarAutomaticamente();
            
            Console.WriteLine("Cliente carregado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar conteudo: {ex.Message}");
            _inicializado = false;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        try
        {
            if (!_inicializado) return;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                (Keyboard.GetState().IsKeyDown(Keys.Escape) && _estadoAtual == EstadoAplicacao.Menu))
                Exit();

            switch (_estadoAtual)
            {
                case EstadoAplicacao.Menu:
                    _menuPrincipal?.Update();
                    break;
                case EstadoAplicacao.Jogo:
                    _telaJogo?.Update(gameTime);
                    if (_telaJogo?.Sair == true)
                    {
                        _clienteRede?.Desconectar();
                        // Se a origem foi o menu de Game Over (Sair do Jogo), encerra a aplicação
                        if (_telaJogo?.SairDoJogo == true)
                        {
                            Exit();
                            return;
                        }
                        _estadoAtual = EstadoAplicacao.Menu;
                        _telaJogo = null;
                        // Evita que a tecla Enter mantida acione a primeira opção do menu
                        _menuPrincipal?.ResetarInput();
                    }
                    else if (_telaJogo?.VoltarAoMenu == true)
                    {
                        // Voltar ao menu principal: desconectar e ir para o menu inicial
                        DesconectarDoServidor();
                        _estadoAtual = EstadoAplicacao.Menu;
                        _telaJogo = null;
                        // Evita leak de Enter ao voltar ao menu
                        _menuPrincipal?.ResetarInput();
                    }
                    break;
                case EstadoAplicacao.Personalizacao:
                    _menuPersonalizacao?.Atualizar();
                    if (_menuPersonalizacao?.VoltarParaMenuPrincipal == true)
                    {
                        _estadoAtual = EstadoAplicacao.Menu;
                        // Opcional: evita entradas persistentes ao voltar
                        _menuPrincipal?.ResetarInput();
                    }
                    break;
            }

            base.Update(gameTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no Update: {ex.Message}");
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        bool spriteBatchIniciado = false;
        try
        {
            if (!_inicializado || _spriteBatch == null) return;

            GraphicsDevice.Clear(_personalizacao?.CorFundo ?? new Color(0, 20, 40));

            _spriteBatch.Begin();
            spriteBatchIniciado = true;

            switch (_estadoAtual)
            {
                case EstadoAplicacao.Menu:
                    if (_font != null)
                    {
                        _menuPrincipal?.Draw(_spriteBatch,
                            GraphicsDevice.Viewport.Width,
                            GraphicsDevice.Viewport.Height);
                    }
                    break;
                case EstadoAplicacao.Jogo:
                    _telaJogo?.Draw(_spriteBatch);
                    break;
                case EstadoAplicacao.Personalizacao:
                    if (_font != null)
                    {
                        _menuPersonalizacao?.Desenhar(_spriteBatch, 
                            GraphicsDevice.Viewport.Width, 
                            GraphicsDevice.Viewport.Height);
                    }
                    break;
            }

            base.Draw(gameTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no Draw: {ex.Message}");
        }
        finally
        {
            if (spriteBatchIniciado)
            {
                try
                {
                    _spriteBatch.End();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao finalizar SpriteBatch: {ex.Message}");
                }
            }
        }
    }

    private async void IniciarJogo()
    {
        try
        {
            if (_menuPrincipal == null) return;

            _clienteRede = new ClienteRede();
            _clienteRede.Desconectado += OnDesconectado;

            bool conectado = await _clienteRede.ConectarAsync(
                _menuPrincipal.EnderecoServidor,
                _menuPrincipal.PortaServidor
            );

            if (!conectado)
            {
                _menuPrincipal.DefinirEstado(
                    MenuPrincipal.EstadoMenu.Erro,
                    $"Nao foi possivel conectar ao servidor {_menuPrincipal.EnderecoServidor}:{_menuPrincipal.PortaServidor}"
                );
                return;
            }

            // Registrar handler ANTES de enviar a mensagem de conexão para não perder JogadorConectado
            _clienteRede.MensagemRecebida += OnMensagemRecebida;

            // Garante um nome válido/único se não informado
            var nome = string.IsNullOrWhiteSpace(_menuPrincipal.NomeJogador)
                ? $"Jogador_{Environment.MachineName}_{DateTime.Now:HHmmss}"
                : _menuPrincipal.NomeJogador.Trim();
            if (string.IsNullOrWhiteSpace(_menuPrincipal.NomeJogador) || _menuPrincipal.NomeJogador != nome)
            {
                _menuPrincipal.DefinirNomeJogador(nome);
            }

            // Armazena nome atual e reseta ID
            _nomeJogadorAtual = nome;
            _meuJogadorId = -1;

            await _clienteRede.EnviarMensagemAsync(new MensagemConectarJogador
            {
                NomeJogador = _nomeJogadorAtual
            });

            _menuPrincipal.DefinirEstado(MenuPrincipal.EstadoMenu.Conectado);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao iniciar jogo: {ex.Message}");
            _menuPrincipal?.DefinirEstado(
                MenuPrincipal.EstadoMenu.Erro,
                $"Erro inesperado: {ex.Message}"
            );
        }
    }

    private void OnMensagemRecebida(MensagemBase mensagem)
    {
        // Trata identificação do jogador assim que o servidor confirmar
        if (mensagem is MensagemJogadorConectado conectadoMsg)
        {
            if (!string.IsNullOrEmpty(_nomeJogadorAtual) && conectadoMsg.NomeJogador == _nomeJogadorAtual)
            {
                _meuJogadorId = conectadoMsg.JogadorId;
                Console.WriteLine($"Meu JogadorId definido: {_meuJogadorId}");
                _telaJogo?.DefinirMeuNome(_nomeJogadorAtual);
                _telaJogo?.DefinirMeuJogadorId(_meuJogadorId);
            }
        }

        if (mensagem is MensagemEstadoJogo)
        {
            // Cria a tela do jogo na primeira vez que receber estado
            if (_telaJogo == null)
            {
                IniciarTelaJogo();
            }
        }
    }

    private void IniciarTelaJogo()
    {
        if (_clienteRede == null || _spriteBatch == null || _font == null || _menuPrincipal == null) return;

        _telaJogo = new TelaJogo(_clienteRede, _personalizacao, _spriteBatch, _graphics, _font, _menuPrincipal.DificuldadeSelecionada, _somTiro, _somExplosao, _somClick);
        // Propaga nome e (se já conhecido) o ID do jogador para a tela
        if (!string.IsNullOrEmpty(_nomeJogadorAtual))
        {
            _telaJogo.DefinirMeuNome(_nomeJogadorAtual);
        }
        if (_meuJogadorId != -1)
        {
            _telaJogo.DefinirMeuJogadorId(_meuJogadorId);
        }
        _estadoAtual = EstadoAplicacao.Jogo;
    }

    private void OnDesconectado()
    {
        _estadoAtual = EstadoAplicacao.Menu;
        // Garante limpeza da tela e handlers ao perder conexão
        _telaJogo?.Fechar();
        _telaJogo = null;
        _meuJogadorId = -1;
        _nomeJogadorAtual = null;
        _menuPrincipal?.DefinirEstado(MenuPrincipal.EstadoMenu.Erro, "Desconectado do servidor");
        // Evita que uma tecla mantida dispare ações no menu
        _menuPrincipal?.ResetarInput();
    }

    private async Task ConectarAutomaticamente()
    {
        try
        {
            // Aguardar um pouco para garantir que o menu foi inicializado
            await Task.Delay(1000);
            
            if (_menuPrincipal == null) return;

            Console.WriteLine("Tentando conectar automaticamente ao servidor...");
            _menuPrincipal.DefinirEstado(MenuPrincipal.EstadoMenu.Conectando);

            _clienteRede = new ClienteRede();
            _clienteRede.Desconectado += OnDesconectado;

            bool conectado = await _clienteRede.ConectarAsync(
                _menuPrincipal.EnderecoServidor,
                _menuPrincipal.PortaServidor
            );

            if (!conectado)
            {
                Console.WriteLine($"Falha na conexao com {_menuPrincipal.EnderecoServidor}:{_menuPrincipal.PortaServidor}");
                _menuPrincipal.DefinirEstado(
                    MenuPrincipal.EstadoMenu.Erro,
                    $"Nao foi possivel conectar ao servidor {_menuPrincipal.EnderecoServidor}:{_menuPrincipal.PortaServidor}"
                );
                return;
            }

            // Registrar handler antes de enviar a conexão aqui também
            _clienteRede.MensagemRecebida += OnMensagemRecebida;

            // Gerar nome automático baseado no timestamp
            var nomeJogador = $"Jogador_{DateTime.Now:HHmmss}";
            _menuPrincipal.DefinirNomeJogador(nomeJogador);
            _nomeJogadorAtual = nomeJogador;
            _meuJogadorId = -1;

            await _clienteRede.EnviarMensagemAsync(new MensagemConectarJogador
            {
                NomeJogador = nomeJogador
            });

            Console.WriteLine($"Conectado como {nomeJogador}");
            _menuPrincipal.DefinirEstado(MenuPrincipal.EstadoMenu.Conectado);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro na conexao automatica: {ex.Message}");
            _menuPrincipal?.DefinirEstado(
                MenuPrincipal.EstadoMenu.Erro,
                $"Erro inesperado: {ex.Message}"
            );
        }
    }

    private void SairJogo()
    {
        Exit();
    }

    private void AbrirPersonalizacao()
    {
        _estadoAtual = EstadoAplicacao.Personalizacao;
        if (_menuPersonalizacao != null)
        {
            _menuPersonalizacao.MenuAtivo = true;
            // Reset da propriedade para permitir que o menu funcione corretamente
            _menuPersonalizacao.VoltarParaMenuPrincipal = false;
        }
    }

    private void VoltarAoJogo()
    {
        // Se ainda estamos conectados, reinicia o jogo no servidor e volta para o jogo
        if (_clienteRede != null && _menuPrincipal != null)
        {
            ReiniciarJogo();
        }
    }

    private void DesconectarDoServidor()
    {
        try
        {
            // Remove handlers da tela antes de descartar
            _telaJogo?.Fechar();

            if (_clienteRede != null)
            {
                // Desinscreve eventos para evitar múltiplas assinaturas ao reconectar
                _clienteRede.MensagemRecebida -= OnMensagemRecebida;
                _clienteRede.Desconectado -= OnDesconectado;
                _clienteRede.Desconectar();
            }

            _clienteRede = null;
            _telaJogo = null;
            _meuJogadorId = -1;
            _nomeJogadorAtual = null;
            _menuPrincipal?.DefinirEstado(MenuPrincipal.EstadoMenu.MenuInicial);
            // Evita que Enter mantido acione o menu ao voltar
            _menuPrincipal?.ResetarInput();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao desconectar: {ex.Message}");
        }
    }

    private void VoltarAoMenu()
    {
        _estadoAtual = EstadoAplicacao.Menu;
    }

    private async void ReiniciarJogo()
    {
        try
        {
            if (_clienteRede == null || _menuPrincipal == null) return;

            Console.WriteLine("Iniciando reinicio do jogo...");

            // Envia mensagem de reinício para o servidor
            await _clienteRede.EnviarMensagemAsync(new MensagemReiniciarJogo());

            // Aguarda um pouco para o servidor processar
            await Task.Delay(500);

            // Recria a tela do jogo para resetar o estado local
            if (_spriteBatch != null && _font != null)
            {
                // Desconecta os eventos da tela anterior se existir
                if (_telaJogo != null)
                {
                    // Remove event handlers para evitar vazamentos de memória
                    _telaJogo.Fechar();
                    _telaJogo = null;
                }

                // Cria nova instância da tela do jogo
                _telaJogo = new TelaJogo(_clienteRede, _personalizacao, _spriteBatch, _graphics, _font, _menuPrincipal.DificuldadeSelecionada, _somTiro, _somExplosao, _somClick);
                // Propaga nome/ID novamente
                if (!string.IsNullOrEmpty(_nomeJogadorAtual))
                {
                    _telaJogo.DefinirMeuNome(_nomeJogadorAtual);
                }
                if (_meuJogadorId != -1)
                {
                    _telaJogo.DefinirMeuJogadorId(_meuJogadorId);
                }
                _estadoAtual = EstadoAplicacao.Jogo;
                
                Console.WriteLine("Nova tela de jogo criada com sucesso!");
            }

            Console.WriteLine("Jogo reiniciado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao reiniciar jogo: {ex.Message}");
            // Em caso de erro, volta ao menu
            _estadoAtual = EstadoAplicacao.Menu;
            _menuPrincipal?.DefinirEstado(MenuPrincipal.EstadoMenu.Erro, "Erro ao reiniciar o jogo");
            _menuPrincipal?.ResetarInput();
        }
    }

    private void AplicarResolucao(int largura, int altura)
    {
        _graphics.PreferredBackBufferWidth = largura;
        _graphics.PreferredBackBufferHeight = altura;
        _graphics.ApplyChanges();
        
        Console.WriteLine($"Resolucao aplicada: {largura}x{altura}");
    }
}
