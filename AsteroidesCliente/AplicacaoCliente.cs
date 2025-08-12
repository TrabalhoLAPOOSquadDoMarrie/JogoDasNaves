using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AsteroidesCliente.Network;
using AsteroidesCliente.UI;
using AsteroidesCliente.Game;

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

    private EstadoAplicacao _estadoAtual = EstadoAplicacao.Menu;
    private MenuPrincipal? _menuPrincipal;
    private TelaJogo? _telaJogo;
    private MenuPersonalizacao? _menuPersonalizacao;
    private PersonalizacaoJogador? _personalizacao;
    private ClienteRede? _clienteRede;
    private bool _inicializado = false;

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

            _menuPrincipal = new MenuPrincipal(_font);
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
                        _estadoAtual = EstadoAplicacao.Menu;
                        _telaJogo = null;
                    }
                    else if (_telaJogo?.VoltarAoMenu == true)
                    {
                        // Volta ao menu sem desconectar - mantém a conexão ativa
                        _estadoAtual = EstadoAplicacao.Menu;
                        _telaJogo = null;
                        // Redefine o estado do menu para conectado já que mantemos a conexão
                        _menuPrincipal?.DefinirEstado(MenuPrincipal.EstadoMenu.Conectado);
                    }
                    else if (_telaJogo?.ReiniciarJogo == true)
                    {
                        // Reinicia o jogo enviando mensagem para o servidor
                        ReiniciarJogo();
                    }
                    break;
                case EstadoAplicacao.Personalizacao:
                    _menuPersonalizacao?.Atualizar();
                    if (_menuPersonalizacao?.VoltarParaMenuPrincipal == true)
                    {
                        _estadoAtual = EstadoAplicacao.Menu;
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

            await _clienteRede.EnviarMensagemAsync(new MensagemConectarJogador
            {
                NomeJogador = _menuPrincipal.NomeJogador
            });

            _menuPrincipal.DefinirEstado(MenuPrincipal.EstadoMenu.Conectado);
            _clienteRede.MensagemRecebida += OnMensagemRecebida;
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
        if (mensagem is MensagemEstadoJogo)
        {
            if (_clienteRede != null) _clienteRede.MensagemRecebida -= OnMensagemRecebida;
            IniciarTelaJogo();
        }
    }

    private void IniciarTelaJogo()
    {
        if (_clienteRede == null || _spriteBatch == null || _font == null || _menuPrincipal == null) return;

        _telaJogo = new TelaJogo(_clienteRede, _personalizacao, _spriteBatch, _graphics, _font, _menuPrincipal.DificuldadeSelecionada);
        _estadoAtual = EstadoAplicacao.Jogo;
    }

    private void OnDesconectado()
    {
        _estadoAtual = EstadoAplicacao.Menu;
        _menuPrincipal?.DefinirEstado(MenuPrincipal.EstadoMenu.Erro, "Desconectado do servidor");
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

            // Gerar nome automático baseado no timestamp
            var nomeJogador = $"Jogador_{DateTime.Now:HHmmss}";
            _menuPrincipal.DefinirNomeJogador(nomeJogador);

            await _clienteRede.EnviarMensagemAsync(new MensagemConectarJogador
            {
                NomeJogador = nomeJogador
            });

            Console.WriteLine($"Conectado como {nomeJogador}");
            _menuPrincipal.DefinirEstado(MenuPrincipal.EstadoMenu.Conectado);
            _clienteRede.MensagemRecebida += OnMensagemRecebida;
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
        if (_telaJogo != null && _clienteRede != null)
        {
            _estadoAtual = EstadoAplicacao.Jogo;
        }
    }

    private void DesconectarDoServidor()
    {
        try
        {
            _clienteRede?.Desconectar();
            _clienteRede = null;
            _telaJogo = null;
            _menuPrincipal?.DefinirEstado(MenuPrincipal.EstadoMenu.MenuInicial);
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
                    _telaJogo = null;
                }

                // Cria nova instância da tela do jogo
                _telaJogo = new TelaJogo(_clienteRede, _personalizacao, _spriteBatch, _graphics, _font, _menuPrincipal.DificuldadeSelecionada);
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