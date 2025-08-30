using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AsteroidesCliente.Network;
using AsteroidesCliente.UI;
using AsteroidesCliente.Game;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

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
    private Song? _musicaAtual;
    private Dictionary<string, Song> _musicasCarregadas = new();

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

        // Configurações para alta performance
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 144.0); // 144 FPS target
        IsFixedTimeStep = false; // Permite framerate variável para melhor performance
        _graphics.SynchronizeWithVerticalRetrace = false; // Desabilita VSync para máxima performance

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
            
            // Carrega todas as músicas disponíveis
            CarregarMusicas();

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

            PersonalizacaoJogador.TexturasNave = new Texture2D[4];
            PersonalizacaoJogador.TexturasNave[0] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Nave1.png");
            PersonalizacaoJogador.TexturasNave[1] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Nave2.png");
            PersonalizacaoJogador.TexturasNave[2] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Nave3.png");
            PersonalizacaoJogador.TexturasNave[3] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Nave4.png");
            // --- FIM DA CORREÇÃO ---

            // Carrega as texturas dos asteroides
            PersonalizacaoJogador.TexturasAsteroide = new Texture2D[3];
            PersonalizacaoJogador.TexturasAsteroide[0] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Asteroide1.png");
            PersonalizacaoJogador.TexturasAsteroide[1] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Asteroide2.png");
            PersonalizacaoJogador.TexturasAsteroide[2] = Texture2D.FromFile(GraphicsDevice, "AsteroidesCliente/Content/Sprites/Asteroide3.png");
        

            // Criar textura pixel para o menu de personalização
            var pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            _menuPersonalizacao = new MenuPersonalizacao(_font, pixelTexture, _personalizacao);
            
            // Conectar evento para aplicar música quando alterada
            _menuPersonalizacao.MusicaAlterada += AplicarMusicaSelecionada;

            // Aplicar resolução salva
            var (largura, altura) = _personalizacao.ObterDimensoesResolucao();
            AplicarResolucao(largura, altura);

            // Aplicar música selecionada
            AplicarMusicaSelecionada();

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
                    _spriteBatch?.End();
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

        _telaJogo = new TelaJogo(_clienteRede, _personalizacao, _spriteBatch, _graphics, _font, _menuPrincipal.DificuldadeSelecionada, _somTiro, _somExplosao, _somClick);
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

    private void AplicarResolucao(int largura, int altura)
    {
        _graphics.PreferredBackBufferWidth = largura;
        _graphics.PreferredBackBufferHeight = altura;
        _graphics.ApplyChanges();
        
        Console.WriteLine($"Resolucao aplicada: {largura}x{altura}");
    }

    /// <summary>
    /// Carrega todas as músicas disponíveis
    /// </summary>
    private void CarregarMusicas()
    {
        try
        {
            // Lista das músicas disponíveis
            var musicasDisponiveis = new Dictionary<string, string>
            {
                { "Sounds/Mantis-Lords", "Mantis Lords" },
                { "Sounds/Medley", "Medley Clássico" },
                { "Sounds/Metallica-Enter-Sandman", "Enter Sandman" }
            };

            foreach (var musica in musicasDisponiveis)
            {
                try
                {
                    var song = Content.Load<Song>(musica.Key);
                    _musicasCarregadas[musica.Key] = song;
                    Console.WriteLine($"Música carregada: {musica.Value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao carregar música '{musica.Value}': {ex.Message}");
                }
            }

            // Configura o MediaPlayer
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0.3f;

            Console.WriteLine($"Sistema de música inicializado com {_musicasCarregadas.Count} músicas disponíveis");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao inicializar sistema de música: {ex.Message}");
        }
    }

    /// <summary>
    /// Aplica a música selecionada nas configurações
    /// </summary>
    private void AplicarMusicaSelecionada()
    {
        if (_personalizacao == null) return;

        var nomeArquivo = _personalizacao.ObterNomeArquivoMusica();
        
        if (nomeArquivo == null)
        {
            // Música desabilitada
            MediaPlayer.Stop();
            _musicaAtual = null;
            Console.WriteLine("Música desabilitada");
            return;
        }

        if (_musicasCarregadas.TryGetValue(nomeArquivo, out var novaMusica))
        {
            // Só troca se for diferente da atual
            if (_musicaAtual != novaMusica)
            {
                _musicaAtual = novaMusica;
                MediaPlayer.Play(_musicaAtual);
                Console.WriteLine($"Tocando: {_personalizacao.ObterNomeAmigavelMusica()}");
            }
        }
        else
        {
            Console.WriteLine($"Música não encontrada: {nomeArquivo}");
        }
    }
}
