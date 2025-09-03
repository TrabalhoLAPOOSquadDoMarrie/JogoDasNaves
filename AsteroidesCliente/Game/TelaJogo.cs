using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AsteroidesCliente.Network;
using Microsoft.Xna.Framework.Audio;
using AsteroidesCliente.UI;

namespace AsteroidesCliente.Game;

/// <summary>
/// Estados do menu de game over
/// </summary>
public enum EstadoGameOver
{
    Fechado,
    Aberto
}

/// <summary>
/// Opções do menu de game over
/// </summary>
public enum OpcaoGameOver
{
    Reiniciar,
    VoltarMenu,
    Sair
}

/// <summary>
/// Tela principal do jogo multiplayer
/// </summary>
public class TelaJogo
{
    private readonly ClienteRede _clienteRede;
    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly SpriteFont _fonte;
    private readonly SoundEffect? _somTiro;
    private readonly SoundEffect? _somExplosao;
    private readonly SoundEffect? _somClick;
    private readonly GraphicsDeviceManager _graphics;
    private readonly PersonalizacaoJogador? _personalizacao;
    private readonly GerenciadorDificuldade _gerenciadorDificuldade;
    private readonly GerenciadorRecordes _gerenciadorRecordes;
    private readonly MenuPersonalizacao _menuPersonalizacao;
    private MensagemEstadoJogo? _estadoJogo;
    private int _meuJogadorId = -1;
    private bool _jogoAtivo = true;
    private int _frameCount = 0;
    private int _pontuacao = 0;
    private string _dificuldadeAtual = "Medio";

    // Contador de FPS para otimização
    private int _fpsCount = 0;
    private float _fpsTimer = 0f;
    private int _currentFps = 0;

    // Constantes de otimização para 144 FPS
    private const int MAX_PARTICULAS = 150; // Drasticamente reduzido para 144 FPS
    private const int MAX_EXPLOSOES = 8; // Reduzido para 144 FPS
    private const int UPDATE_NETWORK_INTERVAL = 3; // Envia dados de rede a cada 3 frames (48 Hz)
    private const int UPDATE_EFFECTS_INTERVAL = 2; // Atualiza efeitos visuais a cada 2 frames
    private const int UPDATE_HUD_INTERVAL = 10; // Atualiza cálculos do HUD a cada 10 frames
    private const int MAX_PARTICULAS_RENDER = 75; // Máximo de partículas desenhadas por frame
    private const int MAX_STARS_COUNT = 150; // Número fixo de estrelas para evitar cálculo em tempo real
    private int _networkUpdateCounter = 0;
    private int _effectsUpdateCounter = 0;
    private int _hudUpdateCounter = 0;

    // Cache para otimizações
    private Vector2[]? _starPositions = null;
    private Color[]? _starColors = null;
    private int[]? _starSizes = null;
    private bool _starsInitialized = false;

    // Estado do input - Controles únicos (WASD + Espaço)
    private bool _esquerda, _direita, _cima, _baixo;
    private bool _espacoAnterior = false;
    private KeyboardState _estadoTecladoAnterior;

    private bool _jogoPausado = false;
    private List<DadosAsteroide> _asteroidesEmPausa = new List<DadosAsteroide>();
    
    // Menu de pausa
    private EstadoMenuPausa _estadoMenuPausa = EstadoMenuPausa.Fechado;
    private int _opcaoSelecionadaPausa = 0;
    private readonly string[] _opcoesMenuPausa = { "Retomar", "Configuracoes", "Recordes", "Voltar ao Menu", "Sair" };

    // Menu de game over
    private EstadoGameOver _estadoGameOver = EstadoGameOver.Fechado;
    private int _opcaoSelecionadaGameOver = 0;
    private readonly string[] _opcoesGameOver = { "Reiniciar Jogo", "Voltar ao Menu", "Sair do Jogo" };
    
    // Rastreamento de votação para reinício
    private int _votosReinicioAtuais = 0;
    private int _votosReinicioNecessarios = 0;

    // Efeitos visuais
    private readonly List<Particula> _particulas = new();
    private readonly List<AnimacaoExplosao> _explosoes = new();
    private readonly Random _random = new();

    private readonly Texture2D _pixelTexture;

    // Rastreamento de pausa global (consenso)
    private readonly Dictionary<int, string> _nomesJogadores = new();
    private readonly HashSet<int> _pausaPendentes = new();
    private readonly HashSet<int> _pausaConfirmados = new();
    private int _pausaTotal = 0;
    private bool _pausaEmAndamentoUI = false;

    public bool Sair { get; private set; }
    public bool VoltarAoMenu { get; private set; }
    public bool SairDoJogo { get; private set; }

    // Indica se este cliente já confirmou o retorno durante a pausa global
    private bool ConfirmadoLocal => _meuJogadorId != -1 && _pausaConfirmados.Contains(_meuJogadorId);

    public TelaJogo(ClienteRede clienteRede, PersonalizacaoJogador? personalizacao, SpriteBatch spriteBatch, GraphicsDeviceManager graphics, SpriteFont font, NivelDificuldade dificuldade = NivelDificuldade.Medio, SoundEffect? somTiro = null, SoundEffect? somExplosao = null, SoundEffect? somClick = null)
    {
        _clienteRede = clienteRede;
        _personalizacao = personalizacao;
        _spriteBatch = spriteBatch;
        _font = font;
        _fonte = font; // Usa a mesma fonte para ambas as variáveis
        _somTiro = somTiro;
        _somExplosao = somExplosao;
        _somClick = somClick;
        _graphics = graphics;
        _gerenciadorDificuldade = new GerenciadorDificuldade(dificuldade);
        _gerenciadorRecordes = new GerenciadorRecordes();
        // Inicializa textura antes de criar menus que a usam
        _pixelTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
        _menuPersonalizacao = new MenuPersonalizacao(font, _pixelTexture, personalizacao ?? new PersonalizacaoJogador());
        _clienteRede.MensagemRecebida += ProcessarMensagem;
        _dificuldadeAtual = dificuldade.ToString();
    }
    

    public void Update(GameTime gameTime)
    {
        // Atualizar contador de FPS
        _fpsTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _fpsCount++;
        if (_fpsTimer >= 1.0f)
        {
            _currentFps = _fpsCount;
            _fpsCount = 0;
            _fpsTimer = 0f;
        }

        var estadoTeclado = Keyboard.GetState();
        
        // Verifica se o menu de pausa foi ativado/desativado (tecla M)
        if (estadoTeclado.IsKeyDown(Keys.M) && !_estadoTecladoAnterior.IsKeyDown(Keys.M))
        {
            // Se já confirmamos retorno, ignorar interações com M
            if (_estadoMenuPausa == EstadoMenuPausa.Aberto && _pausaEmAndamentoUI && ConfirmadoLocal)
            {
                _estadoTecladoAnterior = estadoTeclado;
                return;
            }
            
            if (_estadoMenuPausa == EstadoMenuPausa.Fechado)
            {
                _estadoMenuPausa = EstadoMenuPausa.Aberto;
                _jogoPausado = true;
                _somClick?.Play(); 
                CapturarAsteroidesParaPausa();
                // Informa o servidor para PAUSAR a simulação (abre pausa global)
                _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = true, JogadorId = _meuJogadorId });
            }
            else if (_estadoMenuPausa == EstadoMenuPausa.Aberto)
            {
                // Se estamos em pausa global (consenso), não feche o menu ao tentar retornar.
                if (_pausaEmAndamentoUI || _jogoPausado)
                {
                    // Apenas confirma retorno e mantém o menu aberto aguardando os demais.
                    _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = false, JogadorId = _meuJogadorId });
                    // Marca localmente nossa confirmação (caso ainda não tenha sido refletida pelo broadcast)
                    if (_pausaPendentes.Contains(_meuJogadorId))
                    {
                        _pausaPendentes.Remove(_meuJogadorId);
                        _pausaConfirmados.Add(_meuJogadorId);
                    }
                }
                else
                {
                    // Sem consenso ativo, pode fechar normalmente
                    _estadoMenuPausa = EstadoMenuPausa.Fechado;
                    _jogoPausado = false;
                    _asteroidesEmPausa.Clear();
                    _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = false, JogadorId = _meuJogadorId });
                }
            }
        }
        
        // Se o menu de pausa está aberto, processa apenas input do menu
        if (_estadoMenuPausa != EstadoMenuPausa.Fechado)
        {
            // Se já confirmamos retorno, não aceita navegação no menu; apenas aguarda
            if (_pausaEmAndamentoUI && ConfirmadoLocal)
            {
                _estadoTecladoAnterior = estadoTeclado;
                return;
            }

            ProcessarInputMenuPausa(estadoTeclado);
            
            // Atualiza menu de personalização se estiver ativo
            if (_estadoMenuPausa == EstadoMenuPausa.Configuracoes)
            {
                _menuPersonalizacao.Atualizar();
                if (_menuPersonalizacao.VoltarParaMenuPrincipal)
                {
                    _estadoMenuPausa = EstadoMenuPausa.Aberto;
                    _menuPersonalizacao.VoltarParaMenuPrincipal = false;
                }
            }
            
            _estadoTecladoAnterior = estadoTeclado;
            return;
        }
        
        _frameCount++;
        
        // Atualiza o gerenciador de dificuldade
        _gerenciadorDificuldade.Atualizar();
        
        if (!_jogoAtivo)
        {
            // Se o jogo não está ativo, processa apenas input do menu de game over
            if (_estadoGameOver == EstadoGameOver.Fechado)
            {
                _estadoGameOver = EstadoGameOver.Aberto;
            }
            
            if (_estadoGameOver == EstadoGameOver.Aberto)
            {
                ProcessarInputGameOver(estadoTeclado);
            }
            
            _estadoTecladoAnterior = estadoTeclado;
            return;
        }
        
        if (_estadoJogo == null)
        {
            _estadoTecladoAnterior = estadoTeclado;
            return;
        }

        ProcessarInput();
        
        // Otimização agressiva: Atualizar efeitos visuais em intervalos maiores para 144 FPS
        _effectsUpdateCounter++;
        bool updateEffects = _effectsUpdateCounter >= UPDATE_EFFECTS_INTERVAL;
        
        if (updateEffects)
        {
            _effectsUpdateCounter = 0;
            
            // Atualização ultra-eficiente de partículas
            for (int i = _particulas.Count - 1; i >= 0; i--)
            {
                var particula = _particulas[i];
                particula.Atualizar();
                if (particula.Morta)
                {
                    _particulas.RemoveAt(i);
                }
            }
            
            // Limpeza agressiva se ultrapassar o limite
            if (_particulas.Count > MAX_PARTICULAS)
            {
                int excessoParticulas = _particulas.Count - (int)(MAX_PARTICULAS * 0.7f);
                _particulas.RemoveRange(0, excessoParticulas);
            }

            // Atualização ultra-eficiente de explosões
            for (int i = _explosoes.Count - 1; i >= 0; i--)
            {
                var explosao = _explosoes[i];
                explosao.Atualizar();
                if (!explosao.EstaViva())
                {
                    _explosoes.RemoveAt(i);
                }
            }
            
            // Limpeza agressiva de explosões
            if (_explosoes.Count > MAX_EXPLOSOES)
            {
                int excessoExplosoes = _explosoes.Count - (int)(MAX_EXPLOSOES * 0.6f);
                _explosoes.RemoveRange(0, excessoExplosoes);
            }
        }
        
        _estadoTecladoAnterior = estadoTeclado;
    }

    private void CapturarAsteroidesParaPausa()
    {
        _asteroidesEmPausa.Clear();
        if (_estadoJogo?.Asteroides == null) return;

        foreach (var a in _estadoJogo.Asteroides)
        {
            _asteroidesEmPausa.Add(new DadosAsteroide
            {
                Id = a.Id,
                Posicao = a.Posicao,
                Raio = a.Raio
            });
        }
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        if (_estadoJogo == null)
        {
            DesenharTelaCarregamento();
            
            // Desenha menu de pausa se estiver ativo (mesmo sem estado do jogo)
            if (_estadoMenuPausa != EstadoMenuPausa.Fechado)
            {
                DesenharMenuPausa();
            }
            return;
        }

        if (!_jogoAtivo)
        {
            // Se o menu de Game Over está aberto, só desenha o menu
            if (_estadoGameOver == EstadoGameOver.Aberto)
            {
                DesenharMenuGameOver();
            }
            else
            {
                // Se não há menu ativo, desenha a tela de Game Over padrão
                DesenharTelaGameOver();
            }
            
            // Desenha menu de pausa se estiver ativo (mesmo com jogo inativo)
            if (_estadoMenuPausa != EstadoMenuPausa.Fechado)
            {
                DesenharMenuPausa();
            }
            return;
        }

        DesenharEstrelas();
        DesenharAsteroides();
        DesenharTiros();
        DesenharNaves();
        DesenharParticulas();
        DesenharExplosoes();
        DesenharHUD();
        
        // Desenha menu de pausa se estiver ativo
        if (_estadoMenuPausa != EstadoMenuPausa.Fechado)
        {
            DesenharMenuPausa();
        }
    }

    private void DesenharExplosoes()
    {
        // Desenha animações de explosão usando o método da própria classe
        foreach (var explosao in _explosoes)
        {
            explosao.Desenhar(_spriteBatch, _pixelTexture);
        }
    }

    private async void ProcessarInput()
    {
        var estadoTeclado = Keyboard.GetState();
        var kState = Keyboard.GetState();
        
        // Verificar tecla ESC para abrir/confirmar no menu de pausa
        if (estadoTeclado.IsKeyDown(Keys.Escape) && !_estadoTecladoAnterior.IsKeyDown(Keys.Escape))
        {
            if (!_jogoPausado && _estadoMenuPausa == EstadoMenuPausa.Fechado)
            {
                _estadoMenuPausa = EstadoMenuPausa.Aberto;
                _jogoPausado = true;
                
                // Enviar mensagem para o servidor pausar o jogo para todos os jogadores
                await _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo
                {
                    Pausado = true,
                    JogadorId = _meuJogadorId
                });
                
                Console.WriteLine($"Menu de pausa aberto e mensagem enviada ao servidor");
            }
            else if (_estadoMenuPausa == EstadoMenuPausa.Aberto)
            {
                // Em consenso de pausa, confirma retorno mas mantém o menu aberto
                await _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo
                {
                    Pausado = false,
                    JogadorId = _meuJogadorId
                });
                
                if (_pausaPendentes.Contains(_meuJogadorId))
                {
                    _pausaPendentes.Remove(_meuJogadorId);
                    _pausaConfirmados.Add(_meuJogadorId);
                }
                
                Console.WriteLine("Confirmação de retorno enviada ao servidor (aguardando demais)");
            }
        }

        // ===== CONTROLES DUPLOS - WASD + Setas + Espaço =====
        bool novoEsquerda = kState.IsKeyDown(Keys.A) || kState.IsKeyDown(Keys.Left);
        bool novoDireita = kState.IsKeyDown(Keys.D) || kState.IsKeyDown(Keys.Right);
        bool novoCima = kState.IsKeyDown(Keys.W) || kState.IsKeyDown(Keys.Up);
        bool novoBaixo = kState.IsKeyDown(Keys.S) || kState.IsKeyDown(Keys.Down);
        bool novoEspaco = kState.IsKeyDown(Keys.Space);

        // Atualiza o estado local
        bool estadoMudou = novoEsquerda != _esquerda || novoDireita != _direita ||
                           novoCima != _cima || novoBaixo != _baixo;
    
        _esquerda = novoEsquerda;
        _direita = novoDireita;
        _cima = novoCima;
        _baixo = novoBaixo;
    
        // Otimização de rede: Envia movimento a cada N frames ou quando há mudanças significativas
        _networkUpdateCounter++;
        bool forceNetworkUpdate = _networkUpdateCounter >= UPDATE_NETWORK_INTERVAL;
        
        if (forceNetworkUpdate)
            _networkUpdateCounter = 0;
    
        // Envia o estado quando há mudanças ou no intervalo de atualização da rede
        if (_meuJogadorId != -1 && 
            (estadoMudou || (forceNetworkUpdate && (_esquerda || _direita || _cima || _baixo))))
        {
            _ = _clienteRede.EnviarMensagemAsync(new MensagemMovimentoJogador
            {
                JogadorId = _meuJogadorId,
                Esquerda = _esquerda,
                Direita = _direita,
                Cima = _cima,
                Baixo = _baixo
            });
        }
    

        // Envia tiro se espaço foi pressionado
        if (novoEspaco && !_espacoAnterior && _meuJogadorId != -1)
        {
            _somTiro?.Play();
            _ = _clienteRede.EnviarMensagemAsync(new MensagemAtirarTiro
            {
                JogadorId = _meuJogadorId
            });

            AdicionarEfeitoTiro();
        }
        _espacoAnterior = novoEspaco;

        // Sair do jogo
        if (kState.IsKeyDown(Keys.Escape))
        {
            Sair = true;
        }
    }

    private void DesenharTelaCarregamento()
    {
        // Obtém as dimensões atuais da tela
        int largura = _spriteBatch.GraphicsDevice.Viewport.Width;
        int altura = _spriteBatch.GraphicsDevice.Viewport.Height;
        
        var texto = "Aguardando estado do jogo...";
        var tamanho = _font.MeasureString(texto);
        var posicao = new Vector2(largura / 2f - tamanho.X / 2, altura / 2f - tamanho.Y / 2);
        _spriteBatch.DrawString(_font, texto, posicao, Color.White);
    }

    private void DesenharTelaGameOver()
    {
        // Obtém as dimensões da tela
        int largura = _spriteBatch.GraphicsDevice.Viewport.Width;
        int altura = _spriteBatch.GraphicsDevice.Viewport.Height;
        
        // Fundo semi-transparente
        var fundoRect = new Rectangle(0, 0, largura, altura);
        _spriteBatch.Draw(_pixelTexture, fundoRect, Color.FromNonPremultiplied(0, 0, 0, 180));
        
        // Título Game Over
        var textoGameOver = "GAME OVER";
        var tamanhoGameOver = _font.MeasureString(textoGameOver);
        var posicaoGameOver = new Vector2(largura / 2f - tamanhoGameOver.X / 2, altura * 0.15f);
        DesenharTextoComSombra(textoGameOver, posicaoGameOver, Color.Red, Color.Black, new Vector2(2, 2));

        // Informações de dificuldade
        string infoDificuldade = _gerenciadorDificuldade.ObterInfoDetalhada();
        var tamanhoInfo = _font.MeasureString(infoDificuldade);
        var posicaoInfo = new Vector2(largura / 2f - tamanhoInfo.X / 2, altura * 0.25f);
        DesenharTextoComSombra(infoDificuldade, posicaoInfo, Color.Cyan, Color.Black, new Vector2(1, 1));

        // Pontuações finais
        if (_estadoJogo != null)
        {
            var textoPontuacao = "PONTUACAO FINAL:";
            var tamanhoPontuacao = _font.MeasureString(textoPontuacao);
            var posicaoPontuacao = new Vector2(largura / 2f - tamanhoPontuacao.X / 2, altura * 0.35f);
            DesenharTextoComSombra(textoPontuacao, posicaoPontuacao, Color.White, Color.Black, new Vector2(1, 1));

            float y = altura * 0.45f;
            var navesOrdenadas = _estadoJogo.Naves.OrderByDescending(n => n.Pontuacao).ToList();
            
            foreach (var nave in navesOrdenadas)
            {
                string nome = nave.JogadorId == _meuJogadorId ? "VOCE" : $"Jogador {nave.JogadorId}";
                var cor = nave.JogadorId == _meuJogadorId ? Color.Yellow : Color.White;
                var textoNave = $"{nome}: {nave.Pontuacao} pontos";
                var tamanhoNave = _font.MeasureString(textoNave);
                var posicaoNave = new Vector2(largura / 2f - tamanhoNave.X / 2, y);
                DesenharTextoComSombra(textoNave, posicaoNave, cor, Color.Black, new Vector2(1, 1));
                
                // Verifica se é um novo recorde para o jogador atual
                if (nave.JogadorId == _meuJogadorId)
                {
                    bool novoRecorde = _gerenciadorRecordes.AdicionarRecorde(
                        "Jogador", nave.Pontuacao, _gerenciadorDificuldade.NivelAtual);
                    
                    if (novoRecorde)
                    {
                        var textoRecorde = "★ NOVO RECORDE! ★";
                        var tamanhoRecorde = _font.MeasureString(textoRecorde);
                        var posicaoRecorde = new Vector2(largura / 2f - tamanhoRecorde.X / 2, y + 25);
                        DesenharTextoComSombra(textoRecorde, posicaoRecorde, Color.Gold, Color.Black, new Vector2(1, 1));
                    }
                }
                
                y += 35;
            }
        }

        // Melhor recorde atual
        var melhorRecorde = _gerenciadorRecordes.ObterMelhorRecorde(_gerenciadorDificuldade.NivelAtual);
        if (melhorRecorde != null)
        {
            var textoMelhorRecorde = $"Melhor Recorde ({_gerenciadorDificuldade.NivelAtual}): {melhorRecorde.Pontuacao} pts";
            var tamanhoMelhorRecorde = _font.MeasureString(textoMelhorRecorde);
            var posicaoMelhorRecorde = new Vector2(largura / 2f - tamanhoMelhorRecorde.X / 2, altura * 0.7f);
            DesenharTextoComSombra(textoMelhorRecorde, posicaoMelhorRecorde, Color.LightGreen, Color.Black, new Vector2(1, 1));
        }

        // Instruções
        var textoInstrucoes = "Use W/S ou ↑/↓ para navegar, Enter para selecionar";
        var tamanhoInstrucoes = _font.MeasureString(textoInstrucoes);
        var posicaoInstrucoes = new Vector2(largura / 2f - tamanhoInstrucoes.X / 2, altura * 0.8f);
        DesenharTextoComSombra(textoInstrucoes, posicaoInstrucoes, Color.Yellow, Color.Black, new Vector2(1, 1));
        
        var textoControles = "M: Menu de Pausa | ESC: Sair";
        var tamanhoControles = _font.MeasureString(textoControles);
        var posicaoControles = new Vector2(largura / 2f - tamanhoControles.X / 2, altura * 0.9f);
        DesenharTextoComSombra(textoControles, posicaoControles, Color.LightGray, Color.Black, new Vector2(1, 1));
    }

    private void DesenharEstrelas()
    {
        // Inicializa as estrelas uma única vez para melhor performance
        if (!_starsInitialized)
        {
            InicializarEstrelas();
        }

        // Desenha estrelas usando cache pré-computado
        for (int i = 0; i < MAX_STARS_COUNT; i++)
        {
            if (_starPositions != null && _starColors != null && _starSizes != null)
            {
                var posicao = _starPositions[i];
                var cor = _starColors[i];
                var tamanho = _starSizes[i];

                // Efeito de cintilação apenas para algumas estrelas (reduzido)
                if (i % 15 == 0) // Apenas 1 em cada 15 estrelas cintila
                {
                    float cintilacao = 0.7f + 0.3f * (float)Math.Sin((DateTime.Now.Millisecond + i * 50) * 0.005f);
                    cor = Color.FromNonPremultiplied(cor.R, cor.G, cor.B, (int)(cor.A * cintilacao));
                }

                var estrelaRect = new Rectangle((int)posicao.X, (int)posicao.Y, tamanho, tamanho);
                _spriteBatch.Draw(_pixelTexture, estrelaRect, cor);
            }
        }
    }

    private void InicializarEstrelas()
    {
        int largura = _spriteBatch.GraphicsDevice.Viewport.Width;
        int altura = _spriteBatch.GraphicsDevice.Viewport.Height;

        _starPositions = new Vector2[MAX_STARS_COUNT];
        _starColors = new Color[MAX_STARS_COUNT];
        _starSizes = new int[MAX_STARS_COUNT];

        for (int i = 0; i < MAX_STARS_COUNT; i++)
        {
            // Posição determinística baseada no índice para consistência
            int x = (i * 73 + 123) % largura; // Números primos para distribuição uniforme
            int y = (i * 97 + 456) % altura;
            _starPositions[i] = new Vector2(x, y);

            // Cor baseada no tipo de estrela
            int tipo = i % 4;
            switch (tipo)
            {
                case 0: // Estrelas pequenas e fracas
                    _starColors[i] = Color.FromNonPremultiplied(255, 255, 255, 80);
                    _starSizes[i] = 1;
                    break;
                case 1: // Estrelas médias
                    _starColors[i] = Color.FromNonPremultiplied(255, 255, 255, 120);
                    _starSizes[i] = 1;
                    break;
                case 2: // Estrelas brilhantes
                    _starColors[i] = Color.FromNonPremultiplied(255, 255, 255, 180);
                    _starSizes[i] = 2;
                    break;
                default: // Estrelas muito brilhantes (raras)
                    _starColors[i] = Color.White;
                    _starSizes[i] = 2;
                    break;
            }
        }

        _starsInitialized = true;
    }

    private void DesenharAsteroides()
    {
        var lista = (_estadoMenuPausa != EstadoMenuPausa.Fechado)
            ? _asteroidesEmPausa
            : _estadoJogo?.Asteroides;

        if (lista == null || lista.Count == 0) return;

        // Otimização: Culling - só desenha asteroides visíveis na tela
        int larguraTela = _spriteBatch.GraphicsDevice.Viewport.Width;
        int alturaTela = _spriteBatch.GraphicsDevice.Viewport.Height;
        int margem = 100; // Margem extra para objetos parcialmente visíveis

        foreach (var a in lista)
        {
            // Culling - verifica se o asteroide está visível na tela
            if (a.Posicao.X + a.Raio < -margem || a.Posicao.X - a.Raio > larguraTela + margem ||
                a.Posicao.Y + a.Raio < -margem || a.Posicao.Y - a.Raio > alturaTela + margem)
            {
                continue; // Pula asteroides fora da tela
            }

            // --- INÍCIO DA ALTERAÇÃO ---
            // Desenha o sprite do asteroide em vez da forma geométrica
            var textura =  PersonalizacaoJogador.TexturasAsteroide?[a.TipoTextura];
            if (textura == null) return;

            var origem = new Vector2(textura.Width / 2, textura.Height / 2);
            // Ajusta o tamanho do sprite para corresponder ao raio do asteroide
            float escala = (a.Raio * 2) / textura.Width;
            _spriteBatch.Draw(textura, a.Posicao, null, Color.White, 0f, origem, escala, SpriteEffects.None, 0f);
            // --- FIM DA ALTERAÇÃO ---
        }
    }
    

    private void DesenharTiros()
    {
        if (_estadoJogo?.Tiros == null) return;

        // Otimização: Culling - só desenha tiros visíveis na tela
        int larguraTela = _spriteBatch.GraphicsDevice.Viewport.Width;
        int alturaTela = _spriteBatch.GraphicsDevice.Viewport.Height;
        int margem = 50; // Margem para tiros

        foreach (var tiro in _estadoJogo.Tiros)
        {
            // Culling - verifica se o tiro está visível na tela
            if (tiro.Posicao.X < -margem || tiro.Posicao.X > larguraTela + margem ||
                tiro.Posicao.Y < -margem || tiro.Posicao.Y > alturaTela + margem)
            {
                continue; // Pula tiros fora da tela
            }

            Color cor;
            
            // Define cor específica para tiros do jogador atual
            if (tiro.JogadorId == _meuJogadorId)
            {
                cor = _personalizacao?.CorMissil ?? Color.Yellow; // Usa cor personalizada ou amarelo padrão
            }
            else
            {
                cor = Color.Cyan; // Ciano para tiros de outros jogadores
            }
            
            DesenharTiroComEfeito(tiro.Posicao, cor);
        }
    }

    private void DesenharTiroComEfeito(Vector2 posicao, Color corPrincipal)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        
        // Núcleo do tiro (brilhante)
        var nucleoRect = new Rectangle(x - 2, y - 2, 4, 4);
        _spriteBatch.Draw(_pixelTexture, nucleoRect, Color.White);
        
        // Halo interno
        var haloInternoRect = new Rectangle(x - 3, y - 3, 6, 6);
        _spriteBatch.Draw(_pixelTexture, haloInternoRect, corPrincipal);
        
        // Halo externo (mais transparente)
        Color corHaloExterno = Color.FromNonPremultiplied(corPrincipal.R, corPrincipal.G, corPrincipal.B, 128);
        var haloExternoRect = new Rectangle(x - 4, y - 4, 8, 8);
        _spriteBatch.Draw(_pixelTexture, haloExternoRect, corHaloExterno);
        
        // Rastro de partículas
        DesenharRastroTiro(posicao, corPrincipal);
    }

    private void DesenharRastroTiro(Vector2 posicao, Color cor)
    {
        // Adiciona pequenas partículas atrás do tiro para criar rastro
        for (int i = 1; i <= 3; i++)
        {
            Vector2 posRastro = new Vector2(posicao.X, posicao.Y + i * 3);
            Color corRastro = Color.FromNonPremultiplied(cor.R, cor.G, cor.B, 255 - i * 60);
            
            var rastroRect = new Rectangle((int)posRastro.X - 1, (int)posRastro.Y - 1, 2, 2);
            _spriteBatch.Draw(_pixelTexture, rastroRect, corRastro);
        }
    }

    private void DesenharNaves()
    {
        if (_estadoJogo?.Naves == null) return;

        foreach (var nave in _estadoJogo.Naves)
        {
            if (!nave.Viva) continue;

            Color corNave;
            
            // Define cores específicas para o jogador atual vs outros jogadores
            if (nave.JogadorId == _meuJogadorId)
            {
                corNave = _personalizacao?.CorNave ?? Color.CornflowerBlue;
            }
            else
            {
                corNave = Color.Orange; // Laranja para outros jogadores
            }

            // Usa o ModeloNave que veio do servidor para desenhar a textura correta
            var textura = PersonalizacaoJogador.TexturasNave?[(int)nave.ModeloNave];
            if (textura == null) continue;

            var origem = new Vector2(textura.Width / 2, textura.Height / 2);
            _spriteBatch.Draw(textura, nave.Posicao, null, corNave, nave.Rotacao, origem, nave.Tamanho, SpriteEffects.None, 0f);
        }
    }

    private void DesenharParticulas()
    {
        // Otimização ultra-agressiva para 144 FPS: Reduz drasticamente partículas desenhadas
        int maxParticulasRender = Math.Min(_particulas.Count, MAX_PARTICULAS_RENDER);
        
        // Culling otimizado
        int larguraTela = _spriteBatch.GraphicsDevice.Viewport.Width;
        int alturaTela = _spriteBatch.GraphicsDevice.Viewport.Height;
        int margem = 15; // Margem reduzida
        
        int particulasDesenhadas = 0;
        
        // Desenha apenas as partículas mais recentes e visíveis
        for (int i = _particulas.Count - 1; i >= 0 && particulasDesenhadas < maxParticulasRender; i--)
        {
            var particula = _particulas[i];
            
            // Culling agressivo
            if (particula.Posicao.X < -margem || particula.Posicao.X > larguraTela + margem ||
                particula.Posicao.Y < -margem || particula.Posicao.Y > alturaTela + margem)
            {
                continue;
            }
            
            // Desenho simplificado para melhor performance
            int x = (int)particula.Posicao.X;
            int y = (int)particula.Posicao.Y;
            
            // Tamanho fixo pequeno para melhor performance
            int tamanho = 2;
            var particulaRect = new Rectangle(x - 1, y - 1, tamanho, tamanho);
            _spriteBatch.Draw(_pixelTexture, particulaRect, particula.Cor);
            
            particulasDesenhadas++;
        }
    }

    private void DesenharParticulaComEfeito(Particula particula)
    {
        int x = (int)particula.Posicao.X;
        int y = (int)particula.Posicao.Y;
        
        // Calcula o tamanho baseado na vida restante
        float fatorVida = (float)particula.VidaRestante / 30f;
        int tamanho = Math.Max(1, (int)(3 * fatorVida));
        
        // Desenha a partícula com brilho
        var particulaRect = new Rectangle(x - tamanho/2, y - tamanho/2, tamanho, tamanho);
        _spriteBatch.Draw(_pixelTexture, particulaRect, particula.Cor);
        
        // Adiciona brilho se a partícula ainda está "quente"
        if (fatorVida > 0.5f)
        {
            Color corBrilho = Color.FromNonPremultiplied(255, 255, 255, (int)(128 * fatorVida));
            var brilhoRect = new Rectangle(x - 1, y - 1, 2, 2);
            _spriteBatch.Draw(_pixelTexture, brilhoRect, corBrilho);
        }
    }

    private void DesenharHUD()
    {
        // Verificação de segurança para _font
        if (_font == null)
        {
            Console.WriteLine("Aviso: _font e nulo em DesenharHUD");
            return;
        }

        int largura = _spriteBatch.GraphicsDevice.Viewport.Width;
        int altura = _spriteBatch.GraphicsDevice.Viewport.Height;

        // Calcula dimensões responsivas baseadas no tamanho da tela
        int alturaHUD = Math.Max(120, altura / 7); // Aumentado para acomodar mais informações
        int margemLateral = Math.Max(10, largura / 60); // Margem proporcional
        int espacamentoVertical = Math.Max(18, alturaHUD / 6); // Espaçamento entre linhas

        // Fundo semi-transparente para o HUD
        var fundoHUD = new Rectangle(0, 0, largura, alturaHUD);
        _spriteBatch.Draw(_pixelTexture, fundoHUD, Color.Black * 0.7f);
        
        // Borda do HUD
        DesenharBorda(fundoHUD, Color.Cyan, 2);

        // Título do jogo com efeito de sombra - tamanho responsivo
        string titulo = "ASTEROIDES MULTIPLAYER";
        var tamanhoTitulo = _font.MeasureString(titulo);
        
        // Ajusta o título se for muito grande para a tela
        float escalaTexto = 1.0f;
        if (tamanhoTitulo.X > largura * 0.6f) // Reduzido para dar mais espaço
        {
            escalaTexto = (largura * 0.6f) / tamanhoTitulo.X;
        }
        
        var posicaoTitulo = new Vector2((largura - tamanhoTitulo.X * escalaTexto) / 2, alturaHUD * 0.08f);
        
        // Desenha título com escala ajustada
        if (escalaTexto != 1.0f)
        {
            var matrizAnterior = _spriteBatch.GraphicsDevice.SamplerStates[0];
            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, 
                              DepthStencilState.None, RasterizerState.CullCounterClockwise, null, 
                              Matrix.CreateScale(escalaTexto, escalaTexto, 1.0f) * Matrix.CreateTranslation(posicaoTitulo.X / escalaTexto, posicaoTitulo.Y / escalaTexto, 0));
            
            // Sombra do título
            _spriteBatch.DrawString(_font, titulo, new Vector2(2, 2), Color.Black);
            _spriteBatch.DrawString(_font, titulo, Vector2.Zero, Color.Cyan);
            
            _spriteBatch.End();
            _spriteBatch.Begin();
        }
        else
        {
            // Sombra do título
            DesenharTextoComSombra(titulo, posicaoTitulo, Color.Cyan, Color.Black, new Vector2(2, 2));
        }

        // Informações de dificuldade e tempo (lado esquerdo)
        float yInfo = alturaHUD * 0.35f;
        
        // Informação de dificuldade
        string infoDificuldade = $"Dificuldade: {ObterNomeDificuldade()}";
        DesenharTextoComSombra(infoDificuldade, new Vector2(margemLateral, yInfo), Color.Orange, Color.Black, new Vector2(1, 1));
        
        // Tempo de jogo
        string infoTempo = $"Tempo: {_frameCount / 60}s";
        DesenharTextoComSombra(infoTempo, new Vector2(margemLateral, yInfo + espacamentoVertical), Color.LightBlue, Color.Black, new Vector2(1, 1));

        // Pontuações dos jogadores - Centro
        if (_estadoJogo?.Naves != null)
        {
            var navesOrdenadas = _estadoJogo.Naves.OrderByDescending(n => n.Pontuacao).ToList();
            int maxJogadores = Math.Min(4, (alturaHUD - (int)(alturaHUD * 0.4f)) / espacamentoVertical);
            
            // Calcula posição central para as pontuações
            float larguraPontuacao = 200; // Largura estimada para as pontuações
            float xCentro = (largura - larguraPontuacao) / 2;
            
            for (int i = 0; i < navesOrdenadas.Count && i < maxJogadores; i++)
            {
                var nave = navesOrdenadas[i];
                string texto;
                Color cor;
                
                if (nave.JogadorId == _meuJogadorId)
                {
                    texto = $"VOCE: {nave.Pontuacao}";
                    cor = Color.Yellow;
                }
                else
                {
                    texto = $"Jogador {nave.JogadorId}: {nave.Pontuacao}";
                    cor = Color.White;
                }
                
                // Posição responsiva para as pontuações
                float yPosicao = alturaHUD * 0.35f + i * espacamentoVertical;
                DesenharTextoComSombra(texto, new Vector2(xCentro, yPosicao), cor, Color.Black, new Vector2(1, 1));
            }
        }

        // Informações de controle e status (lado direito)
        if (_estadoJogo?.Naves != null)
        {
            var minhaNavе = _estadoJogo.Naves.FirstOrDefault(n => n.JogadorId == _meuJogadorId);
            if (minhaNavе != null)
            {
                // Controles principais
                string infoControles = "WASD/Setas: Mover | ESPACO: Atirar";
                var tamanhoInfo = _font.MeasureString(infoControles);
                
                // Ajusta o texto de controles se for muito grande
                if (tamanhoInfo.X > largura * 0.3f)
                {
                    infoControles = "WASD/Setas+ESPACO";
                    tamanhoInfo = _font.MeasureString(infoControles);
                }
                
                var posicaoInfo = new Vector2(largura - tamanhoInfo.X - margemLateral, yInfo);
                DesenharTextoComSombra(infoControles, posicaoInfo, Color.LightGray, Color.Black, new Vector2(1, 1));
                
                // Controles de menu
                string infoMenu = "M: Menu | ESC: Sair";
                var tamanhoMenu = _font.MeasureString(infoMenu);
                if (tamanhoMenu.X > largura * 0.3f)
                {
                    infoMenu = "M: Menu";
                    tamanhoMenu = _font.MeasureString(infoMenu);
                }
                
                var posicaoMenu = new Vector2(largura - tamanhoMenu.X - margemLateral, yInfo + espacamentoVertical);
                DesenharTextoComSombra(infoMenu, posicaoMenu, Color.Gray, Color.Black, new Vector2(1, 1));
            }
        }

        // Contador de FPS - canto inferior direito
        string fpsTexto = $"FPS: {_currentFps}";
        var tamanhoFps = _font.MeasureString(fpsTexto);
        var posicaoFps = new Vector2(largura - tamanhoFps.X - 10, altura - tamanhoFps.Y - 10);
        
        // Cor do FPS otimizada para 144 FPS (verde > 120, amarelo 90-120, laranja 60-90, vermelho < 60)
        Color corFps = _currentFps >= 120 ? Color.LimeGreen : 
                      _currentFps >= 90 ? Color.Yellow : 
                      _currentFps >= 60 ? Color.Orange : Color.Red;
        
        DesenharTextoComSombra(fpsTexto, posicaoFps, corFps, Color.Black, new Vector2(1, 1));

        // Status do jogo - posição responsiva na parte inferior
        if (!_jogoAtivo)
        {
            string statusTexto = "Aguardando jogadores...";
            var tamanhoStatus = _font.MeasureString(statusTexto);
            var posicaoStatus = new Vector2((largura - tamanhoStatus.X) / 2, altura - tamanhoStatus.Y - 20);
            DesenharTextoComSombra(statusTexto, posicaoStatus, Color.Orange, Color.Black, new Vector2(1, 1));
        }
    }

    private string ObterNomeDificuldade()
    {
        return _gerenciadorDificuldade.ObterInfoDetalhada();
    }

    private void DesenharTextoComSombra(string texto, Vector2 posicao, Color corTexto, Color corSombra, Vector2? offsetSombra = null)
    {
        // Verificação de segurança para _font
        if (_font == null)
        {
            Console.WriteLine("Aviso: _font e nulo em DesenharTextoComSombra");
            return;
        }

        Vector2 offset = offsetSombra ?? new Vector2(1, 1);
        // Desenha sombra
        _spriteBatch.DrawString(_font, texto, posicao + offset, corSombra);
        // Desenha texto principal
        _spriteBatch.DrawString(_font, texto, posicao, corTexto);
    }

    private void DesenharBorda(Rectangle retangulo, Color cor, int espessura)
    {
        // Topo
        var topo = new Rectangle(retangulo.X, retangulo.Y, retangulo.Width, espessura);
        _spriteBatch.Draw(_pixelTexture, topo, cor);
        
        // Baixo
        var baixo = new Rectangle(retangulo.X, retangulo.Y + retangulo.Height - espessura, retangulo.Width, espessura);
        _spriteBatch.Draw(_pixelTexture, baixo, cor);
        
        // Esquerda
        var esquerda = new Rectangle(retangulo.X, retangulo.Y, espessura, retangulo.Height);
        _spriteBatch.Draw(_pixelTexture, esquerda, cor);
        
        // Direita
        var direita = new Rectangle(retangulo.X + retangulo.Width - espessura, retangulo.Y, espessura, retangulo.Height);
        _spriteBatch.Draw(_pixelTexture, direita, cor);
    }



    private void AdicionarEfeitoTiro()
    {
        // Verifica se já temos muitas partículas (otimização agressiva)
        if (_particulas.Count >= MAX_PARTICULAS * 0.8f) return; // Para antes de atingir o limite

        // Encontra a posição da nave do jogador
        var nave = _estadoJogo?.Naves.FirstOrDefault(n => n.JogadorId == _meuJogadorId);
        if (nave != null)
        {
            // Drasticamente reduzido - apenas 2-3 partículas por tiro para 144 FPS
            int numParticulas = Math.Min(3, MAX_PARTICULAS - _particulas.Count);
            
            // Adiciona apenas partículas essenciais
            for (int i = 0; i < numParticulas; i++)
            {
                Vector2 velocidade = new Vector2(
                    _random.Next(-2, 3), 
                    _random.Next(-4, -1)
                );
                
                Color corParticula = i < 2 ? Color.Orange : Color.Red;
                
                _particulas.Add(new Particula(
                    nave.Posicao + new Vector2(_random.Next(-5, 6), -8),
                    velocidade,
                    corParticula,
                    15 + _random.Next(0, 10) // Vida mais curta
                ));
            }
        }
    }

    private void ProcessarMensagem(MensagemBase mensagem)
    {
        switch (mensagem.Tipo)
        {
            case TipoMensagem.EstadoJogo:
                _estadoJogo = (MensagemEstadoJogo)mensagem;
                _jogoAtivo = _estadoJogo.JogoAtivo;
                
            // Atualiza a pontuação do jogador atual e toca o som se a pontuação aumentou (asteroide foi atingido)
            if (_meuJogadorId != -1)
            {
                var minhaNave = _estadoJogo.Naves.FirstOrDefault(n => n.JogadorId == _meuJogadorId);
                if (minhaNave != null)
                {
                    if (minhaNave.Pontuacao > _pontuacao)
                    {
                        _somExplosao?.Play();
                    }
                    _pontuacao = minhaNave.Pontuacao; // Atualiza a pontuação local
                }
            }
                
                // Se estivermos em pausa global e o roster mudou (desconexão), ajuste conjunto pendente
                if (_pausaEmAndamentoUI)
                {
                    var idsAtuais = _estadoJogo.Naves.Select(n => n.JogadorId).ToHashSet();
                    // Remove de pendentes quem saiu
                    var removidos = _pausaPendentes.Where(id => !idsAtuais.Contains(id)).ToList();
                    foreach (var id in removidos)
                    {
                        _pausaPendentes.Remove(id);
                        if (_pausaTotal > 0) _pausaTotal = Math.Max(0, _pausaTotal - 1);
                    }
                }
                
                // Salva recorde quando o jogo termina
                if (!_jogoAtivo && _pontuacao > 0)
                {
                    SalvarRecorde();
                }
                break;

            case TipoMensagem.JogadorConectado:
                var msgConectado = (MensagemJogadorConectado)mensagem;

                // Modo online: atribui o ID recebido como meu ID
                if (_meuJogadorId == -1)
                {
                    _meuJogadorId = msgConectado.JogadorId;
                    Console.WriteLine($"Meu ID: {_meuJogadorId}");

                    if (_personalizacao != null)
                    {
                        // --- CORREÇÃO: Converte o enum para int ao enviar ---
                        _ = _clienteRede.EnviarMensagemAsync(new MensagemPersonalizacao
                        {
                            JogadorId = _meuJogadorId,
                            ModeloNave = (int)_personalizacao.ModeloNave
                        });
                    }

                }
                // Guarda o nome para exibição
                _nomesJogadores[msgConectado.JogadorId] = msgConectado.NomeJogador;
                break;

            case TipoMensagem.JogadorDesconectado:
                var msgDesc = (MensagemJogadorDesconectado)mensagem;
                _nomesJogadores.Remove(msgDesc.JogadorId);
                if (_pausaEmAndamentoUI)
                {
                    if (_pausaPendentes.Remove(msgDesc.JogadorId))
                    {
                        if (_pausaTotal > 0) _pausaTotal = Math.Max(0, _pausaTotal - 1);
                    }
                    _pausaConfirmados.Remove(msgDesc.JogadorId);
                }
                break;

            case TipoMensagem.GameOver:
                var msgGameOver = (MensagemGameOver)mensagem;
                _jogoAtivo = false;
                Console.WriteLine($"Game Over: {msgGameOver.Motivo}");
                
                // Se foi um reinício do jogo, reseta tudo para um novo jogo
                if (msgGameOver.Motivo.Contains("reiniciado"))
                {
                    _gerenciadorDificuldade.Reiniciar();
                    _pontuacao = 0;
                    _estadoGameOver = EstadoGameOver.Fechado;
                    _opcaoSelecionadaGameOver = 0;
                    _jogoAtivo = true;
                    
                    // Reset do sistema de votação
                    _votosReinicioAtuais = 0;
                    _votosReinicioNecessarios = 0;
                    
                    Console.WriteLine("Jogo reiniciado com sucesso!");
                }
                else
                {
                    // Game Over normal - mostra o menu
                    if (_pontuacao > 0)
                    {
                        SalvarRecorde();
                    }
                    _estadoGameOver = EstadoGameOver.Aberto;
                    
                    // Reset do sistema de votação no game over normal
                    _votosReinicioAtuais = 0;
                    _votosReinicioNecessarios = 0;
                }
                break;

            case TipoMensagem.PausarJogo:
                var msgPausa = (MensagemPausarJogo)mensagem;
                
                if (msgPausa.Pausado)
                {
                    // Início ou atualização de pausa global
                    if (msgPausa.JogadorId != _meuJogadorId)
                    {
                        _estadoMenuPausa = EstadoMenuPausa.Aberto;
                        _jogoPausado = true;
                        CapturarAsteroidesParaPausa();
                    }
                    
                    // Inicializa rastreamento (apenas na primeira atualização da pausa)
                    if (!_pausaEmAndamentoUI)
                    {
                        _pausaPendentes.Clear();
                        _pausaConfirmados.Clear();
                        if (_estadoJogo?.Naves != null)
                        {
                            foreach (var n in _estadoJogo.Naves)
                            {
                                _pausaPendentes.Add(n.JogadorId);
                            }
                            _pausaTotal = _pausaPendentes.Count;
                        }
                        else
                        {
                            _pausaTotal = Math.Max(_pausaTotal, msgPausa.PausadosRestantes);
                        }
                        _pausaEmAndamentoUI = true;
                    }
                    
                    // Se a contagem do servidor diminuiu, considera o ator como confirmado
                    if (_pausaPendentes.Contains(msgPausa.JogadorId) && msgPausa.PausadosRestantes < _pausaPendentes.Count)
                    {
                        _pausaPendentes.Remove(msgPausa.JogadorId);
                        _pausaConfirmados.Add(msgPausa.JogadorId);
                    }
                }
                else
                {
                    // Todos confirmaram retorno
                    if (msgPausa.PausadosRestantes == 0)
                    {
                        _jogoPausado = false;
                        _asteroidesEmPausa.Clear();
                        _estadoMenuPausa = EstadoMenuPausa.Fechado;
                        // Reset de rastreamento
                        _pausaPendentes.Clear();
                        _pausaConfirmados.Clear();
                        _pausaTotal = 0;
                        _pausaEmAndamentoUI = false;
                    }
                }
                break;
                
            case TipoMensagem.ReiniciarJogo:
                var msgReinicio = (MensagemReiniciarJogo)mensagem;
                _votosReinicioAtuais = msgReinicio.VotosAtuais;
                _votosReinicioNecessarios = msgReinicio.VotosNecessarios;
                Console.WriteLine($"Progresso votação reinício: {msgReinicio.VotosAtuais}/{msgReinicio.VotosNecessarios}");
                break;
        }
    }

    // Adiciona o processamento de input do menu de pausa (navegação e seleção)
    private void ProcessarInputMenuPausa(KeyboardState estadoTeclado)
    {
        // Se estamos aguardando confirmações e já confirmamos localmente,
        // não aceitar entradas (apenas exibir o painel de aguardando)
        if (_pausaEmAndamentoUI && ConfirmadoLocal)
            return;

        if ((_estadoMenuPausa == EstadoMenuPausa.Recordes || _estadoMenuPausa == EstadoMenuPausa.Configuracoes) &&
        estadoTeclado.IsKeyDown(Keys.M) && !_estadoTecladoAnterior.IsKeyDown(Keys.M))
        {
            _estadoMenuPausa = EstadoMenuPausa.Aberto;
            _somClick?.Play();
            return;
        }

        // Navegação para cima (W/Up)
        bool upPressed = (estadoTeclado.IsKeyDown(Keys.W) || estadoTeclado.IsKeyDown(Keys.Up)) &&
                         !(_estadoTecladoAnterior.IsKeyDown(Keys.W) || _estadoTecladoAnterior.IsKeyDown(Keys.Up));
        if (upPressed)
        {
            _opcaoSelecionadaPausa = (_opcaoSelecionadaPausa - 1 + _opcoesMenuPausa.Length) % _opcoesMenuPausa.Length;
        }

        // Navegação para baixo (S/Down)
        bool downPressed = (estadoTeclado.IsKeyDown(Keys.S) || estadoTeclado.IsKeyDown(Keys.Down)) &&
                           !(_estadoTecladoAnterior.IsKeyDown(Keys.S) || _estadoTecladoAnterior.IsKeyDown(Keys.Down));
        if (downPressed)
        {
            _opcaoSelecionadaPausa = (_opcaoSelecionadaPausa + 1) % _opcoesMenuPausa.Length;
        }

        // Seleção (Enter)
        bool enterPressed = estadoTeclado.IsKeyDown(Keys.Enter) && !_estadoTecladoAnterior.IsKeyDown(Keys.Enter);
        if (enterPressed)
        {
            _somClick?.Play();
            switch (_opcaoSelecionadaPausa)
            {
                case 0: // Retomar
                    // Em consenso, apenas confirma retorno e mantém o menu aberto aguardando os demais
                    _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo
                    {
                        Pausado = false,
                        JogadorId = _meuJogadorId
                    });
                    // Marca localmente nossa confirmação, caso ainda conste como pendente
                    if (_pausaPendentes.Contains(_meuJogadorId))
                    {
                        _pausaPendentes.Remove(_meuJogadorId);
                        _pausaConfirmados.Add(_meuJogadorId);
                    }
                    // Se por alguma razão não estiver em consenso, fecha o menu e retoma
                    if (!_pausaEmAndamentoUI)
                    {
                        _estadoMenuPausa = EstadoMenuPausa.Fechado;
                        _jogoPausado = false;
                        _asteroidesEmPausa.Clear();
                    }
                    break;

                case 1: // Configuracoes
                    _estadoMenuPausa = EstadoMenuPausa.Configuracoes;
                    break;

                case 2: // Recordes
                    _estadoMenuPausa = EstadoMenuPausa.Recordes;
                    break;

                case 3: // Voltar ao Menu
                    VoltarAoMenu = true;
                    break;

                case 4: // Sair
                    Sair = true;
                    SairDoJogo = true;
                    break;
            }
        }
    }

    // Processa input do menu de Game Over (navegação e seleção)
    private void ProcessarInputGameOver(KeyboardState estadoTeclado)
    {
        // Navegação para cima (W/Up)
        bool upPressed = (estadoTeclado.IsKeyDown(Keys.W) || estadoTeclado.IsKeyDown(Keys.Up)) &&
                         !(_estadoTecladoAnterior.IsKeyDown(Keys.W) || _estadoTecladoAnterior.IsKeyDown(Keys.Up));
        if (upPressed)
        {
            _opcaoSelecionadaGameOver = (_opcaoSelecionadaGameOver - 1 + _opcoesGameOver.Length) % _opcoesGameOver.Length;
            _somClick?.Play(); // Som de navegação
        }

        // Navegação para baixo (S/Down)
        bool downPressed = (estadoTeclado.IsKeyDown(Keys.S) || estadoTeclado.IsKeyDown(Keys.Down)) &&
                           !(_estadoTecladoAnterior.IsKeyDown(Keys.S) || _estadoTecladoAnterior.IsKeyDown(Keys.Down));
        if (downPressed)
        {
            _opcaoSelecionadaGameOver = (_opcaoSelecionadaGameOver + 1) % _opcoesGameOver.Length;
            _somClick?.Play(); // Som de navegação
        }

        // Seleção (Enter)
        bool enterPressed = estadoTeclado.IsKeyDown(Keys.Enter) && !_estadoTecladoAnterior.IsKeyDown(Keys.Enter);
        if (enterPressed)
        {
            _somClick?.Play(); // Som de confirmação
            switch (_opcaoSelecionadaGameOver)
            {
                case 0: // Reiniciar Jogo
                    Console.WriteLine($"Jogador {_meuJogadorId} selecionou 'Reiniciar Jogo'");
                    
                    // Verifica se é single player ou multiplayer
                    int jogadoresConectados = _estadoJogo?.Naves?.Count ?? 1;
                    
                    if (jogadoresConectados == 1)
                    {
                        Console.WriteLine("Modo single player - reinício imediato solicitado.");
                    }
                    else
                    {
                        Console.WriteLine($"Modo multiplayer ({jogadoresConectados} jogadores) - iniciando sistema de votação.");
                    }
                    
                    // Envia mensagem para o servidor solicitando reinício
                    _ = _clienteRede.EnviarMensagemAsync(new MensagemReiniciarJogo 
                    { 
                        JogadorVotou = _meuJogadorId 
                    });
                    break;
                    
                case 1: // Voltar ao Menu
                    Console.WriteLine($"Jogador {_meuJogadorId} selecionou 'Voltar ao Menu'");
                    // Reset completo de estados antes de voltar ao menu
                    _estadoJogo = null;
                    _estadoGameOver = EstadoGameOver.Fechado;
                    _opcaoSelecionadaGameOver = 0;
                    _jogoAtivo = true;
                    // Reset estados de pausa e confirmação
                    _pausaEmAndamentoUI = false;
                    _pausaTotal = 0;
                    _pausaConfirmados.Clear();
                    _pausaPendentes.Clear();
                    _estadoMenuPausa = EstadoMenuPausa.Fechado;
                    VoltarAoMenu = true;
                    break;
                    
                case 2: // Sair do Jogo
                    Console.WriteLine($"Jogador {_meuJogadorId} selecionou 'Sair do Jogo'");
                    Sair = true;
                    SairDoJogo = true;
                    break;
            }
        }
    }

    private void DesenharMenuPausa()
    {
        // Fundo semi-transparente
        var fundoRect = new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        _spriteBatch.Draw(_pixelTexture, fundoRect, Color.FromNonPremultiplied(0, 0, 0, 150));

        // Painel do menu
        int larguraMenu = 500;
        int alturaMenu = 400;
        int x = (_graphics.PreferredBackBufferWidth - larguraMenu) / 2;
        int y = (_graphics.PreferredBackBufferHeight - alturaMenu) / 2;

        var painelRect = new Rectangle(x, y, larguraMenu, alturaMenu);
        _spriteBatch.Draw(_pixelTexture, painelRect, Color.FromNonPremultiplied(20, 40, 60, 240));
        DesenharBorda(painelRect, Color.Cyan, 4);

        // Se já confirmou retorno, mostra somente status x/y e faltantes (tem prioridade sobre submenus)
        if (_pausaEmAndamentoUI && ConfirmadoLocal)
        {
            int confirmados = Math.Clamp(_pausaTotal - _pausaPendentes.Count, 0, _pausaTotal);
            string tituloAguardando = "AGUARDANDO CONFIRMACOES";
            var tamTituloA = _fonte.MeasureString(tituloAguardando);
            var posTituloA = new Vector2(x + (larguraMenu - tamTituloA.X) / 2, y + 40);
            _spriteBatch.DrawString(_fonte, tituloAguardando, posTituloA, Color.Yellow);

            string textoStatus = $"Confirmados: {confirmados}/{_pausaTotal}";
            var tamStatus = _fonte.MeasureString(textoStatus);
            var posStatus = new Vector2(x + (larguraMenu - tamStatus.X) / 2, posTituloA.Y + 50);
            _spriteBatch.DrawString(_fonte, textoStatus, posStatus, Color.Cyan);

            if (_pausaPendentes.Count > 0)
            {
                string tituloFaltando = "Faltando:";
                var tamTitF = _fonte.MeasureString(tituloFaltando);
                var posTitF = new Vector2(x + (larguraMenu - tamTitF.X) / 2, posStatus.Y + 40);
                _spriteBatch.DrawString(_fonte, tituloFaltando, posTitF, Color.White);

                var nomesFaltando = _pausaPendentes
                    .Select(id => _nomesJogadores.TryGetValue(id, out var nome) ? nome : $"Jogador {id}")
                    .ToList();

                string linhaAtual = string.Empty;
                List<string> linhas = new();
                foreach (var nome in nomesFaltando)
                {
                    string candidato = string.IsNullOrEmpty(linhaAtual) ? nome : linhaAtual + ", " + nome;
                    if (_fonte.MeasureString(candidato).X > larguraMenu - 100)
                    {
                        if (!string.IsNullOrEmpty(linhaAtual)) linhas.Add(linhaAtual);
                        linhaAtual = nome;
                    }
                    else
                    {
                        linhaAtual = candidato;
                    }
                }
                if (!string.IsNullOrEmpty(linhaAtual)) linhas.Add(linhaAtual);

                float yLista = posTitF.Y + 28;
                foreach (var linha in linhas)
                {
                    var tamLinha = _fonte.MeasureString(linha);
                    var posLinha = new Vector2(x + (larguraMenu - tamLinha.X) / 2, yLista);
                    _spriteBatch.DrawString(_fonte, linha, posLinha, Color.LightGray);
                    yLista += 24;
                }
            }
            return;
        }

        // Se não confirmou, pode estar em submenus
        if (_estadoMenuPausa == EstadoMenuPausa.Configuracoes)
        {
            DesenharMenuConfiguracoes();
            return;
        }
        if (_estadoMenuPausa == EstadoMenuPausa.Recordes)
        {
            DesenharMenuRecordes();
            return;
        }

        // Menu normal (para quem ainda não confirmou)
        string titulo = "JOGO PAUSADO";
        var tamanhoTitulo = _fonte.MeasureString(titulo);
        var posicaoTitulo = new Vector2(x + (larguraMenu - tamanhoTitulo.X) / 2, y + 30);
        _spriteBatch.DrawString(_fonte, titulo, posicaoTitulo, Color.Cyan);

        string subtitulo = "ESCOLHA UMA OPCAO";
        var tamanhoSubtitulo = _fonte.MeasureString(subtitulo);
        var posicaoSubtitulo = new Vector2(x + (larguraMenu - tamanhoSubtitulo.X) / 2, y + 80);
        _spriteBatch.DrawString(_fonte, subtitulo, posicaoSubtitulo, Color.Yellow);

        Color[] cores = new Color[_opcoesMenuPausa.Length];
        for (int i = 0; i < cores.Length; i++) cores[i] = Color.White;
        cores[_opcaoSelecionadaPausa] = Color.Yellow;

        int inicioOpcoes = y + 140;
        int espacamentoOpcoes = 45;

        for (int i = 0; i < _opcoesMenuPausa.Length; i++)
        {
            var tamanhoOpcao = _fonte.MeasureString(_opcoesMenuPausa[i]);
            var posicaoOpcao = new Vector2(x + (larguraMenu - tamanhoOpcao.X) / 2, inicioOpcoes + i * espacamentoOpcoes);
            _spriteBatch.DrawString(_fonte, _opcoesMenuPausa[i], posicaoOpcao, cores[i]);

            if (i == _opcaoSelecionadaPausa)
            {
                var indicadorRect = new Rectangle((int)posicaoOpcao.X - 30, (int)posicaoOpcao.Y + 8, 20, 20);
                _spriteBatch.Draw(_pixelTexture, indicadorRect, Color.Yellow);
                DesenharBorda(indicadorRect, Color.Orange, 2);
            }
        }

        string instrucoes = "W/S: Navegar | Enter: Selecionar | M: Confirmar Retorno";
        var tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        var posicaoInstrucoes = new Vector2(x + (larguraMenu - tamanhoInstrucoes.X) / 2, y + alturaMenu - 40);
        _spriteBatch.DrawString(_fonte, instrucoes, posicaoInstrucoes, Color.Gray);
    }

    private void DesenharMenuGameOver()
    {
        // Verificação de segurança para _fonte
        if (_fonte == null)
        {
            Console.WriteLine("Aviso: _fonte e nulo em DesenharMenuGameOver");
            return;
        }

        // Obtém as dimensões da tela
        int largura = _spriteBatch.GraphicsDevice.Viewport.Width;
        int altura = _spriteBatch.GraphicsDevice.Viewport.Height;
        
        // Fundo semi-transparente adicional para o menu
        var fundoMenuRect = new Rectangle(0, 0, largura, altura);
        _spriteBatch.Draw(_pixelTexture, fundoMenuRect, Color.FromNonPremultiplied(0, 0, 0, 180));

        // Painel do menu - maior para acomodar todas as informações
        int larguraMenu = 700;
        int alturaMenu = 600;
        int x = (largura - larguraMenu) / 2;
        int y = (altura - alturaMenu) / 2;

        var painelRect = new Rectangle(x, y, larguraMenu, alturaMenu);
        _spriteBatch.Draw(_pixelTexture, painelRect, Color.FromNonPremultiplied(30, 20, 20, 250));
        DesenharBorda(painelRect, Color.Red, 4);

        // Título "GAME OVER" - mais destaque
        string tituloGameOver = "GAME OVER";
        var tamanhoTituloGameOver = _fonte.MeasureString(tituloGameOver);
        var posicaoTituloGameOver = new Vector2(x + (larguraMenu - tamanhoTituloGameOver.X) / 2, y + 30);
        
        // Efeito de sombra no título
        _spriteBatch.DrawString(_fonte, tituloGameOver, posicaoTituloGameOver + new Vector2(3, 3), Color.Black);
        _spriteBatch.DrawString(_fonte, tituloGameOver, posicaoTituloGameOver, Color.Red);

        // Informações do jogo atual
        if (_estadoJogo?.Naves != null && _estadoJogo.Naves.Count > 0)
        {
            var minhaNave = _estadoJogo.Naves.FirstOrDefault(n => n.JogadorId == _meuJogadorId);
            if (minhaNave != null)
            {
                string infoPontuacao = $"Sua Pontuacao: {minhaNave.Pontuacao} pontos";
                var tamanhoInfo = _fonte.MeasureString(infoPontuacao);
                var posicaoInfo = new Vector2(x + (larguraMenu - tamanhoInfo.X) / 2, y + 90);
                _spriteBatch.DrawString(_fonte, infoPontuacao, posicaoInfo + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, infoPontuacao, posicaoInfo, Color.Yellow);

                // Verifica se é um novo recorde
                bool novoRecorde = _gerenciadorRecordes.AdicionarRecorde(
                    "Jogador", minhaNave.Pontuacao, _gerenciadorDificuldade.NivelAtual);
                
                if (novoRecorde)
                {
                    string textoRecorde = "*** NOVO RECORDE! ***";
                    var tamanhoRecorde = _fonte.MeasureString(textoRecorde);
                    var posicaoRecorde = new Vector2(x + (larguraMenu - tamanhoRecorde.X) / 2, posicaoInfo.Y + 25);
                    
                    _spriteBatch.DrawString(_fonte, textoRecorde, posicaoRecorde + new Vector2(2, 2), Color.Black);
                    _spriteBatch.DrawString(_fonte, textoRecorde, posicaoRecorde, Color.Gold);
                }
            }

            // Melhor recorde atual
            var melhorRecorde = _gerenciadorRecordes.ObterMelhorRecorde(_gerenciadorDificuldade.NivelAtual);
            if (melhorRecorde != null)
            {
                string textoMelhorRecorde = $"Melhor Recorde: {melhorRecorde.Pontuacao} pts";
                var tamanhoMelhorRecorde = _fonte.MeasureString(textoMelhorRecorde);
                var posicaoMelhorRecorde = new Vector2(x + (larguraMenu - tamanhoMelhorRecorde.X) / 2, y + 135);
                
                _spriteBatch.DrawString(_fonte, textoMelhorRecorde, posicaoMelhorRecorde + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, textoMelhorRecorde, posicaoMelhorRecorde, Color.LightGreen);
            }

            // Informações sobre modo multiplayer
            if (_estadoJogo.Naves.Count > 1)
            {
                string infoMultiplayer = $"Jogadores Conectados: {_estadoJogo.Naves.Count}";
                var tamanhoMulti = _fonte.MeasureString(infoMultiplayer);
                var posicaoMulti = new Vector2(x + (larguraMenu - tamanhoMulti.X) / 2, y + 160);
                _spriteBatch.DrawString(_fonte, infoMultiplayer, posicaoMulti + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, infoMultiplayer, posicaoMulti, Color.Cyan);
            }
            else
            {
                string infoSingle = "Modo Single Player";
                var tamanhoSingle = _fonte.MeasureString(infoSingle);
                var posicaoSingle = new Vector2(x + (larguraMenu - tamanhoSingle.X) / 2, y + 160);
                _spriteBatch.DrawString(_fonte, infoSingle, posicaoSingle + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, infoSingle, posicaoSingle, Color.LightGreen);
            }
        }

        // Título do menu de opções
        string subtitulo = "ESCOLHA UMA OPCAO";
        var tamanhoSubtitulo = _fonte.MeasureString(subtitulo);
        var posicaoSubtitulo = new Vector2(x + (larguraMenu - tamanhoSubtitulo.X) / 2, y + 200);
        _spriteBatch.DrawString(_fonte, subtitulo, posicaoSubtitulo + new Vector2(1, 1), Color.Black);
        _spriteBatch.DrawString(_fonte, subtitulo, posicaoSubtitulo, Color.White);

        // Desenha as opções do menu com indicadores visuais aprimorados
        int inicioOpcoes = y + 250;
        int espacamentoOpcoes = 75; // Aumentado para acomodar as descrições

        for (int i = 0; i < _opcoesGameOver.Length; i++)
        {
            var tamanhoOpcao = _fonte.MeasureString(_opcoesGameOver[i]);
            var posicaoOpcao = new Vector2(x + (larguraMenu - tamanhoOpcao.X) / 2, inicioOpcoes + i * espacamentoOpcoes);
            
            // Cor da opção (selecionada ou normal)
            bool selecionado = i == _opcaoSelecionadaGameOver;
            Color corOpcao = selecionado ? Color.Yellow : Color.White;
            Color corSombra = Color.Black;

            // Fundo destacado para a opção selecionada - mais elaborado
            if (selecionado)
            {
                var fundoSelecao = new Rectangle(
                    (int)posicaoOpcao.X - 60, 
                    (int)posicaoOpcao.Y - 8, 
                    (int)tamanhoOpcao.X + 120, 
                    45
                );
                
                // Gradiente simulado com duas camadas
                _spriteBatch.Draw(_pixelTexture, fundoSelecao, Color.FromNonPremultiplied(100, 100, 0, 80));
                
                var fundoInterno = new Rectangle(
                    fundoSelecao.X + 3,
                    fundoSelecao.Y + 3,
                    fundoSelecao.Width - 6,
                    fundoSelecao.Height - 6
                );
                _spriteBatch.Draw(_pixelTexture, fundoInterno, Color.FromNonPremultiplied(150, 150, 0, 40));
                
                // Bordas duplas para mais destaque
                DesenharBorda(fundoSelecao, Color.Orange, 2);
                DesenharBorda(fundoInterno, Color.Yellow, 1);
            }

            // Desenha sombra
            _spriteBatch.DrawString(_fonte, _opcoesGameOver[i], posicaoOpcao + new Vector2(2, 2), corSombra);
            // Desenha texto principal
            _spriteBatch.DrawString(_fonte, _opcoesGameOver[i], posicaoOpcao, corOpcao);

            // Indicadores visuais aprimorados para opção selecionada
            if (selecionado)
            {
                // Seta à esquerda - mais elaborada
                string setaEsquerda = "► ";
                var tamanhoSetaEsq = _fonte.MeasureString(setaEsquerda);
                var posicaoSetaEsq = new Vector2(posicaoOpcao.X - 50, posicaoOpcao.Y);
                _spriteBatch.DrawString(_fonte, setaEsquerda, posicaoSetaEsq + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, setaEsquerda, posicaoSetaEsq, Color.Orange);

                // Seta à direita
                string setaDireita = " ◄";
                var tamanhoSetaDir = _fonte.MeasureString(setaDireita);
                var posicaoSetaDir = new Vector2(posicaoOpcao.X + tamanhoOpcao.X + 20, posicaoOpcao.Y);
                _spriteBatch.DrawString(_fonte, setaDireita, posicaoSetaDir + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, setaDireita, posicaoSetaDir, Color.Orange);

                // Brilho adicional no texto selecionado
                _spriteBatch.DrawString(_fonte, _opcoesGameOver[i], posicaoOpcao, Color.FromNonPremultiplied(255, 255, 255, 100));
            }

            // Adiciona ícones/descrições para cada opção
            string descricaoOpcao = "";
            Color corDescricao = Color.Gray;
            
            switch (i)
            {
                case 0: // Reiniciar Jogo
                    if (_estadoJogo?.Naves?.Count == 1)
                    {
                        descricaoOpcao = "(Reinicio imediato)";
                        corDescricao = Color.LightGreen;
                    }
                    else if (_estadoJogo?.Naves?.Count > 1)
                    {
                        descricaoOpcao = "(Requer votacao)";
                        corDescricao = Color.Orange;
                    }
                    break;
                case 1: // Voltar ao Menu
                    descricaoOpcao = "(Retorna ao menu principal)";
                    break;
                case 2: // Sair do Jogo
                    descricaoOpcao = "(Encerra o jogo)";
                    break;
            }

            if (!string.IsNullOrEmpty(descricaoOpcao))
            {
                var tamanhoDesc = _fonte.MeasureString(descricaoOpcao);
                var posicaoDesc = new Vector2(x + (larguraMenu - tamanhoDesc.X) / 2, posicaoOpcao.Y + 25);
                _spriteBatch.DrawString(_fonte, descricaoOpcao, posicaoDesc + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_fonte, descricaoOpcao, posicaoDesc, corDescricao);
            }
        }

        // Mostra progresso da votação se estiver em modo multiplayer e houver votação
        if (_estadoJogo?.Naves?.Count > 1 && _votosReinicioNecessarios > 1 && 
            _votosReinicioAtuais > 0 && _votosReinicioAtuais < _votosReinicioNecessarios)
        {
            string progressoVotacao = $"Votacao para Reinicio: {_votosReinicioAtuais}/{_votosReinicioNecessarios} jogadores";
            var tamanhoProgresso = _fonte.MeasureString(progressoVotacao);
            var posicaoProgresso = new Vector2(x + (larguraMenu - tamanhoProgresso.X) / 2, inicioOpcoes + _opcoesGameOver.Length * espacamentoOpcoes + 30);
            
            // Fundo para o progresso da votação - mais elaborado
            var fundoProgresso = new Rectangle((int)posicaoProgresso.X - 15, (int)posicaoProgresso.Y - 8, (int)tamanhoProgresso.X + 30, 35);
            _spriteBatch.Draw(_pixelTexture, fundoProgresso, Color.FromNonPremultiplied(50, 50, 100, 220));
            
            var fundoInternoProgresso = new Rectangle(fundoProgresso.X + 2, fundoProgresso.Y + 2, fundoProgresso.Width - 4, fundoProgresso.Height - 4);
            _spriteBatch.Draw(_pixelTexture, fundoInternoProgresso, Color.FromNonPremultiplied(80, 80, 150, 180));
            
            DesenharBorda(fundoProgresso, Color.Orange, 2);
            DesenharBorda(fundoInternoProgresso, Color.Cyan, 1);
            
            _spriteBatch.DrawString(_fonte, progressoVotacao, posicaoProgresso + new Vector2(2, 2), Color.Black);
            _spriteBatch.DrawString(_fonte, progressoVotacao, posicaoProgresso, Color.Orange);
            
            // Indicador visual de progresso
            string indicadorProgresso = ">>> Aguardando outros jogadores... <<<";
            var tamanhoIndicador = _fonte.MeasureString(indicadorProgresso);
            var posicaoIndicador = new Vector2(x + (larguraMenu - tamanhoIndicador.X) / 2, posicaoProgresso.Y + 30);
            _spriteBatch.DrawString(_fonte, indicadorProgresso, posicaoIndicador + new Vector2(1, 1), Color.Black);
            _spriteBatch.DrawString(_fonte, indicadorProgresso, posicaoIndicador, Color.Yellow);
        }

        // Instruções de controle - mais claras e informativas
        string instrucoes = "W/S ou ↑/↓: Navegar | Enter: Selecionar | M: Menu de Pausa";
        var tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        
        // Ajusta as instruções se forem muito grandes
        if (tamanhoInstrucoes.X > larguraMenu - 20)
        {
            instrucoes = "W/S: Navegar | Enter: Selecionar";
            tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        }
        
        var posicaoInstrucoes = new Vector2(x + (larguraMenu - tamanhoInstrucoes.X) / 2, y + alturaMenu - 50);
        
        // Fundo para as instruções
        var fundoInstrucoes = new Rectangle((int)posicaoInstrucoes.X - 10, (int)posicaoInstrucoes.Y - 5, (int)tamanhoInstrucoes.X + 20, 30);
        _spriteBatch.Draw(_pixelTexture, fundoInstrucoes, Color.FromNonPremultiplied(20, 20, 20, 180));
        
        _spriteBatch.DrawString(_fonte, instrucoes, posicaoInstrucoes + new Vector2(1, 1), Color.Black);
        _spriteBatch.DrawString(_fonte, instrucoes, posicaoInstrucoes, Color.Gray);
    }

    private void DesenharMenuRecordes()
    {
        // Verificação de segurança para _fonte
        if (_fonte == null)
        {
            Console.WriteLine("Aviso: _fonte e nulo em DesenharMenuRecordes");
            return;
        }

        // Fundo semi-transparente
        var fundoRect = new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        _spriteBatch.Draw(_pixelTexture, fundoRect, Color.Black * 0.7f);

        // Painel do menu
        int larguraMenu = 600;
        int alturaMenu = 500;
        int x = (_graphics.PreferredBackBufferWidth - larguraMenu) / 2;
        int y = (_graphics.PreferredBackBufferHeight - alturaMenu) / 2;

        var painelRect = new Rectangle(x, y, larguraMenu, alturaMenu);
        _spriteBatch.Draw(_pixelTexture, painelRect, Color.DarkSlateGray);

        // Borda do painel
        DesenharBorda(painelRect, Color.Gold, 3);

        // Título
        string titulo = "RECORDES";
        var tamanhoTitulo = _fonte.MeasureString(titulo);
        var posicaoTitulo = new Vector2(x + (larguraMenu - tamanhoTitulo.X) / 2, y + 20);
        _spriteBatch.DrawString(_fonte, titulo, posicaoTitulo, Color.Gold);

        // Exibir recordes por dificuldade
        int yAtual = y + 80;
        var dificuldades = new[] { NivelDificuldade.Facil, NivelDificuldade.Medio, NivelDificuldade .Dificil };
        var coresDificuldade = new[] { Color.Green, Color.Yellow, Color.Red };

        for (int i = 0; i < dificuldades.Length; i++)
        {
            var dificuldade = dificuldades[i];
            var cor = coresDificuldade[i];
            
            // Nome da dificuldade
            string nomeDificuldade = dificuldade.ToString().ToUpper();
            var tamanhoNome = _fonte.MeasureString(nomeDificuldade);
            var posicaoNome = new Vector2(x + 50, yAtual);
            _spriteBatch.DrawString(_fonte, nomeDificuldade, posicaoNome, cor);

            // Melhor recorde
            var melhorRecorde = _gerenciadorRecordes.ObterMelhorRecorde(dificuldade);
            string textoRecorde = melhorRecorde != null 
                ? $"{melhorRecorde.Nome}: {melhorRecorde.Pontuacao} pontos"
                : "Nenhum recorde";
            
            var posicaoRecorde = new Vector2(x + 200, yAtual);
            _spriteBatch.DrawString(_fonte, textoRecorde, posicaoRecorde, Color.White);

            yAtual += 50;
        }

        // Recorde atual da sessão
        if (_pontuacao > 0)
        {
            yAtual += 30;
            string textoSessao = "SESSAO ATUAL:";
            var tamanhoSessao = _fonte.MeasureString(textoSessao);
            var posicaoSessao = new Vector2(x + (larguraMenu - tamanhoSessao.X) / 2, yAtual);
            _spriteBatch.DrawString(_fonte, textoSessao, posicaoSessao, Color.Cyan);

            yAtual += 40;
            string pontuacaoAtual = $"Pontuacao: {_pontuacao}";
            var tamanhoPontuacao = _fonte.MeasureString(pontuacaoAtual);
            var posicaoPontuacao = new Vector2(x + (larguraMenu - tamanhoPontuacao.X) / 2, yAtual);
            _spriteBatch.DrawString(_fonte, pontuacaoAtual, posicaoPontuacao, Color.White);

            // Verificar se é novo recorde
            if (EhNovoRecorde())
            {
                yAtual += 30;
                string novoRecorde = "NOVO RECORDE!";
                var tamanhoNovoRecorde = _fonte.MeasureString(novoRecorde);
                var posicaoNovoRecorde = new Vector2(x + (larguraMenu - tamanhoNovoRecorde.X) / 2, yAtual);
                _spriteBatch.DrawString(_fonte, novoRecorde, posicaoNovoRecorde, Color.Gold);
            }
        }

        // Instruções
        string instrucoes = "M: Voltar ao Menu de Pausa";
        var tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        var posicaoInstrucoes = new Vector2(x + (larguraMenu - tamanhoInstrucoes.X) / 2, y + alturaMenu - 40);
        _spriteBatch.DrawString(_fonte, instrucoes, posicaoInstrucoes, Color.Gray);
    }

    private void DesenharMenuConfiguracoes()
    {
        // Verificação de segurança para _fonte
        if (_fonte == null)
        {
            Console.WriteLine("Aviso: _fonte e nulo em DesenharMenuConfiguracoes");
            return;
        }

        // Fundo semi-transparente
        var fundoRect = new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        _spriteBatch.Draw(_pixelTexture, fundoRect, Color.Black * 0.7f);

        // Painel do menu
        int larguraMenu = 500;
        int alturaMenu = 400;
        int x = (_graphics.PreferredBackBufferWidth - larguraMenu) / 2;
        int y = (_graphics.PreferredBackBufferHeight - alturaMenu) / 2;

        var painelRect = new Rectangle(x, y, larguraMenu, alturaMenu);
        _spriteBatch.Draw(_pixelTexture, painelRect, Color.DarkSlateGray);

        // Borda do painel
        DesenharBorda(painelRect, Color.Cyan, 2);

        // Título
        string titulo = "CONFIGURACOES";
        var tamanhoTitulo = _fonte.MeasureString(titulo);
        var posicaoTitulo = new Vector2(x + (larguraMenu - tamanhoTitulo.X) / 2, y + 20);
        _spriteBatch.DrawString(_fonte, titulo, posicaoTitulo, Color.Yellow);

        // Desenha as configurações do menu de personalização
        if (_menuPersonalizacao != null)
        {
            _menuPersonalizacao.Desenhar(_spriteBatch, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        }

        // Instruções
        string instrucoes = "Use WASD ou ↑↓←→ para navegar, Enter para selecionar, M para voltar";
        var tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        var posicaoInstrucoes = new Vector2(x + (larguraMenu - tamanhoInstrucoes.X) / 2, y + alturaMenu - 40);
        _spriteBatch.DrawString(_fonte, instrucoes, posicaoInstrucoes, Color.Gray);
    }

    private void SalvarRecorde()
    {
        try
        {
            // Usa o GerenciadorRecordes para salvar o recorde
            var dificuldadeEnum = Enum.Parse<NivelDificuldade>(_dificuldadeAtual);
            _gerenciadorRecordes.AdicionarRecorde("Jogador", _pontuacao, dificuldadeEnum);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar recorde: {ex.Message}");
        }
    }

    private int ObterMelhorRecorde()
    {
        try
        {
            var dificuldadeEnum = Enum.Parse<NivelDificuldade>(_dificuldadeAtual);
            var melhorRecorde = _gerenciadorRecordes.ObterMelhorRecorde(dificuldadeEnum);
            return melhorRecorde?.Pontuacao ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private bool EhNovoRecorde()
    {
        return _pontuacao > ObterMelhorRecorde();
    }

    /// <summary>
    /// Rotaciona um ponto em torno da origem
    /// </summary>
    /// <param name="ponto">Ponto a ser rotacionado</param>
    /// <param name="angulo">Ângulo de rotação em radianos</param>
    /// <returns>Ponto rotacionado</returns>
    private Vector2 RotacionarPonto(Vector2 ponto, float angulo)
    {
        float cos = (float)Math.Cos(angulo);
        float sin = (float)Math.Sin(angulo);
        
        return new Vector2(
            ponto.X * cos - ponto.Y * sin,
            ponto.X * sin + ponto.Y * cos
        );
    }
}

/// <summary>
/// Classe para efeitos de partículas
/// </summary>
public class Particula
{
    public Vector2 Posicao { get; set; }
    public Vector2 Velocidade { get; set; }
    public Color Cor { get; set; }
    public int VidaRestante { get; set; }
    public bool Morta => VidaRestante <= 0;

    public Particula(Vector2 posicao, Vector2 velocidade, Color cor, int vida)
    {
        Posicao = posicao;
        Velocidade = velocidade;
        Cor = cor;
        VidaRestante = vida;
    }

    public void Atualizar()
    {
        Posicao += Velocidade;
        VidaRestante--;
        
        // Fade out
        float alpha = (float)VidaRestante / 30f;
        Cor = Color.FromNonPremultiplied(Cor.R, Cor.G, Cor.B, (int)(255 * alpha));
    }
}