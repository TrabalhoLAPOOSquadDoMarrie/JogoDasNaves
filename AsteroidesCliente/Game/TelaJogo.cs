using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AsteroidesCliente.Network;
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
    private bool _reiniciarJogo = false;

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
    public bool ReiniciarJogo => _reiniciarJogo;

    public TelaJogo(ClienteRede clienteRede, PersonalizacaoJogador? personalizacao, SpriteBatch spriteBatch, GraphicsDeviceManager graphics, SpriteFont font, NivelDificuldade dificuldade = NivelDificuldade.Medio)
    {
        _clienteRede = clienteRede;
        _personalizacao = personalizacao;
        _spriteBatch = spriteBatch;
        _font = font;
        _fonte = font; // Usa a mesma fonte para ambas as variáveis
        _graphics = graphics;
        _gerenciadorDificuldade = new GerenciadorDificuldade(dificuldade);
        _gerenciadorRecordes = new GerenciadorRecordes();
        _menuPersonalizacao = new MenuPersonalizacao(font, _pixelTexture, personalizacao ?? new PersonalizacaoJogador());
        _clienteRede.MensagemRecebida += ProcessarMensagem;
        _dificuldadeAtual = dificuldade.ToString();

        _pixelTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(GameTime gameTime)
    {
        var estadoTeclado = Keyboard.GetState();
        
        // Verifica se o menu de pausa foi ativado/desativado (tecla M)
        if (estadoTeclado.IsKeyDown(Keys.M) && !_estadoTecladoAnterior.IsKeyDown(Keys.M))
        {
            if (_estadoMenuPausa == EstadoMenuPausa.Fechado)
            {
                _estadoMenuPausa = EstadoMenuPausa.Aberto;
                _jogoPausado = true;
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
        
        // Atualizar partículas
        for (int i = _particulas.Count - 1; i >= 0; i--)
        {
            _particulas[i].Atualizar();
            if (_particulas[i].Morta)
            {
                _particulas.RemoveAt(i);
            }
        }

        // Atualizar animações de explosão
        for (int i = _explosoes.Count - 1; i >= 0; i--)
        {
            _explosoes[i].Atualizar();
            if (!_explosoes[i].EstaViva())
            {
                _explosoes.RemoveAt(i);
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
            DesenharTelaGameOver();
            
            // Desenha menu de game over se estiver ativo
            if (_estadoGameOver == EstadoGameOver.Aberto)
            {
                DesenharMenuGameOver();
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
    
        // Envia o estado sempre que qualquer tecla estiver pressionada (mesmo sem mudanças)
        // ou quando o estado mudar (teclas soltas)
        if (_meuJogadorId != -1 && 
            (estadoMudou || _esquerda || _direita || _cima || _baixo))
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
        // Obtém as dimensões atuais da tela
        int largura = _spriteBatch.GraphicsDevice.Viewport.Width;
        int altura = _spriteBatch.GraphicsDevice.Viewport.Height;
        
        // Calcula o número de estrelas baseado na área da tela
        int numEstrelas = (largura * altura) / 3200; // Aproximadamente 1 estrela a cada 3200 pixels
        
        // Desenha estrelas de fundo com diferentes tamanhos e brilhos
        for (int i = 0; i < numEstrelas; i++)
        {
            // Usa a posição i para criar estrelas consistentes
            int x = (i * 73 + 123) % largura; // Números primos para distribuição uniforme
            int y = (i * 97 + 456) % altura;
            
            // Varia o tamanho e brilho baseado no índice
            int tipo = i % 4;
            Color cor;
            int tamanho;
            
            switch (tipo)
            {
                case 0: // Estrelas pequenas e fracas
                    cor = Color.FromNonPremultiplied(255, 255, 255, 80);
                    tamanho = 1;
                    break;
                case 1: // Estrelas médias
                    cor = Color.FromNonPremultiplied(255, 255, 255, 120);
                    tamanho = 1;
                    break;
                case 2: // Estrelas brilhantes
                    cor = Color.FromNonPremultiplied(255, 255, 255, 180);
                    tamanho = 2;
                    break;
                default: // Estrelas muito brilhantes (raras)
                    cor = Color.White;
                    tamanho = 2;
                    // Adiciona efeito de cintilação
                    float cintilacao = 0.8f + 0.2f * (float)Math.Sin((DateTime.Now.Millisecond + i * 100) * 0.01f);
                    cor = Color.FromNonPremultiplied(255, 255, 255, (int)(255 * cintilacao));
                    break;
            }
            
            var estrelaRect = new Rectangle(x, y, tamanho, tamanho);
            _spriteBatch.Draw(_pixelTexture, estrelaRect, cor);
            
            // Para estrelas maiores, adiciona um brilho sutil
            if (tamanho > 1)
            {
                Color corBrilho = Color.FromNonPremultiplied(255, 255, 255, 40);
                var brilhoRect = new Rectangle(x - 1, y - 1, tamanho + 2, tamanho + 2);
                _spriteBatch.Draw(_pixelTexture, brilhoRect, corBrilho);
            }
        }
    }

    private void DesenharAsteroides()
    {
        var lista = (_estadoMenuPausa != EstadoMenuPausa.Fechado)
            ? _asteroidesEmPausa
            : _estadoJogo?.Asteroides;

        if (lista == null || lista.Count == 0) return;

        foreach (var a in lista)
        {
            DesenharAsteroideIrregular(a.Posicao, a.Raio);
        }
    }

    private void DesenharAsteroideIrregular(Vector2 posicao, float raio)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        int raioInt = (int)raio;
        
        // Cria pontos irregulares para o asteroide
        List<Vector2> pontos = new List<Vector2>();
        int numPontos = 8 + (int)(raio / 5); // Mais pontos para asteroides maiores
        
        for (int i = 0; i < numPontos; i++)
        {
            float angulo = (float)(2 * Math.PI * i / numPontos);
            
            // Adiciona variação aleatória ao raio para criar irregularidade
            float variacaoRaio = raio * (0.7f + 0.6f * (float)Math.Sin(angulo * 3 + posicao.X * 0.01f));
            
            float px = x + variacaoRaio * (float)Math.Cos(angulo);
            float py = y + variacaoRaio * (float)Math.Sin(angulo);
            
            pontos.Add(new Vector2(px, py));
        }
        
        // Usa as cores do meteoro baseadas na personalização
        var (corInterior, corContorno) = _personalizacao?.ObterCoresMeteoro() ?? (Color.SaddleBrown, Color.Brown);
        
        // Preenche o asteroide
        PreencherPoligono(pontos, corInterior);
        
        // Desenha o contorno
        for (int i = 0; i < pontos.Count; i++)
        {
            Vector2 p1 = pontos[i];
            Vector2 p2 = pontos[(i + 1) % pontos.Count];
            DesenharLinha(p1, p2, corContorno, 2);
        }
        
        // Adiciona detalhes de crateras
        DesenharCrateras(posicao, raioInt);
    }

    private void PreencherPoligono(List<Vector2> pontos, Color cor)
    {
        if (pontos.Count < 3) return;
        
        // Encontra os limites do polígono
        float minY = pontos.Min(p => p.Y);
        float maxY = pontos.Max(p => p.Y);
        
        for (int y = (int)minY; y <= (int)maxY; y++)
        {
            List<float> intersecoes = new List<float>();
            
            for (int i = 0; i < pontos.Count; i++)
            {
                Vector2 p1 = pontos[i];
                Vector2 p2 = pontos[(i + 1) % pontos.Count];
                
                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    float x = p1.X + (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    intersecoes.Add(x);
                }
            }
            
            intersecoes.Sort();
            for (int i = 0; i < intersecoes.Count - 1; i += 2)
            {
                for (int x = (int)intersecoes[i]; x <= (int)intersecoes[i + 1]; x++)
                {
                    var rect = new Rectangle(x, y, 1, 1);
                    _spriteBatch.Draw(_pixelTexture, rect, cor);
                }
            }
        }
    }

    private void DesenharCrateras(Vector2 centro, int raio)
    {
        // Adiciona algumas crateras pequenas para detalhes
        int numCrateras = Math.Max(2, raio / 8);
        
        for (int i = 0; i < numCrateras; i++)
        {
            float angulo = (float)(2 * Math.PI * i / numCrateras + centro.X * 0.01f);
            float distancia = raio * 0.3f * (0.5f + 0.5f * (float)Math.Sin(angulo * 2));
            
            int crateraX = (int)(centro.X + distancia * Math.Cos(angulo));
            int crateraY = (int)(centro.Y + distancia * Math.Sin(angulo));
            int crateraTamanho = Math.Max(1, raio / 12);
            
            var crateraRect = new Rectangle(crateraX - crateraTamanho, crateraY - crateraTamanho, 
                                          crateraTamanho * 2, crateraTamanho * 2);
            _spriteBatch.Draw(_pixelTexture, crateraRect, Color.Black);
        }
    }

    private void DesenharTiros()
    {
        if (_estadoJogo?.Tiros == null) return;

        foreach (var tiro in _estadoJogo.Tiros)
        {
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

            Color corNave, corDetalhes;
            
            // Define cores específicas para o jogador atual vs outros jogadores
            if (nave.JogadorId == _meuJogadorId)
            {
                corNave = _personalizacao?.CorNave ?? Color.CornflowerBlue;
                corDetalhes = _personalizacao?.CorDetalhesNave ?? Color.LightBlue;
            }
            else
            {
                corNave = Color.Orange; // Laranja para outros jogadores
                corDetalhes = Color.Yellow;
            }
            
            // Desenha a nave baseada no modelo selecionado com o tamanho correto
            switch (_personalizacao?.ModeloNave ?? PersonalizacaoJogador.TipoNave.Triangular)
            {
                case PersonalizacaoJogador.TipoNave.Triangular:
                    DesenharNaveTriangular(nave.Posicao, corNave, corDetalhes, nave.Tamanho);
                    break;
                case PersonalizacaoJogador.TipoNave.Losango:
                    DesenharNaveLosango(nave.Posicao, corNave, corDetalhes, nave.Tamanho);
                    break;
                case PersonalizacaoJogador.TipoNave.Hexagonal:
                    DesenharNaveHexagonal(nave.Posicao, corNave, corDetalhes, nave.Tamanho);
                    break;
                case PersonalizacaoJogador.TipoNave.Circular:
                    DesenharNaveCircular(nave.Posicao, corNave, corDetalhes, nave.Tamanho);
                    break;
                default:
                    DesenharNaveTriangular(nave.Posicao, corNave, corDetalhes, nave.Tamanho);
                    break;
            }
        }
    }

    private void DesenharNaveTriangular(Vector2 posicao, Color corPrincipal, Color corDetalhes, float tamanho = 1.0f)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        
        // Aplica o fator de tamanho
        int altura = (int)(12 * tamanho);
        int largura = (int)(8 * tamanho);
        
        // Corpo principal da nave (triângulo)
        Vector2[] pontos = {
            new Vector2(x, y - altura),           // Ponta superior
            new Vector2(x - largura, y + altura), // Base esquerda
            new Vector2(x + largura, y + altura)  // Base direita
        };
        
        // Desenha o corpo da nave
        DesenharTriangulo(pontos, corPrincipal);
        
        // Desenha detalhes da nave (escalados)
        int cockpitSize = Math.Max(2, (int)(2 * tamanho));
        var cockpitRect = new Rectangle(x - cockpitSize/2, y - cockpitSize/2, cockpitSize, cockpitSize);
        _spriteBatch.Draw(_pixelTexture, cockpitRect, corDetalhes);
        
        // Motores (pequenos retângulos na base)
        int motorWidth = Math.Max(1, (int)(2 * tamanho));
        int motorHeight = Math.Max(2, (int)(4 * tamanho));
        int motorOffset = (int)(6 * tamanho);
        
        var motorEsq = new Rectangle(x - motorOffset, y + (int)(6 * tamanho), motorWidth, motorHeight);
        var motorDir = new Rectangle(x + motorOffset - motorWidth, y + (int)(6 * tamanho), motorWidth, motorHeight);
        _spriteBatch.Draw(_pixelTexture, motorEsq, Color.Orange);
        _spriteBatch.Draw(_pixelTexture, motorDir, Color.Orange);
        
        // Brilho no cockpit
        int brilhoSize = Math.Max(1, cockpitSize/2);
        var brilho = new Rectangle(x - brilhoSize/2, y - brilhoSize/2, brilhoSize, brilhoSize);
        _spriteBatch.Draw(_pixelTexture, brilho, Color.White);
    }

    private void DesenharTriangulo(Vector2[] pontos, Color cor)
    {
        // Desenha triângulo preenchido usando linhas
        for (int i = 0; i < pontos.Length; i++)
        {
            Vector2 p1 = pontos[i];
            Vector2 p2 = pontos[(i + 1) % pontos.Length];
            DesenharLinha(p1, p2, cor, 2);
        }
        
        // Preenche o triângulo
        PreencherTriangulo(pontos, cor);
    }

    private void PreencherTriangulo(Vector2[] pontos, Color cor)
    {
        // Algoritmo simples para preencher triângulo
        int minY = (int)Math.Min(pontos[0].Y, Math.Min(pontos[1].Y, pontos[2].Y));
        int maxY = (int)Math.Max(pontos[0].Y, Math.Max(pontos[1].Y, pontos[2].Y));
        
        for (int y = minY; y <= maxY; y++)
        {
            List<int> intersecoes = new List<int>();
            
            for (int i = 0; i < 3; i++)
            {
                Vector2 p1 = pontos[i];
                Vector2 p2 = pontos[(i + 1) % 3];
                
                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    float x = p1.X + (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    intersecoes.Add((int)x);
                }
            }
            
            intersecoes.Sort();
            for (int i = 0; i < intersecoes.Count - 1; i += 2)
            {
                for (int x = intersecoes[i]; x <= intersecoes[i + 1]; x++)
                {
                    var rect = new Rectangle(x, y, 1, 1);
                    _spriteBatch.Draw(_pixelTexture, rect, cor);
                }
            }
        }
    }

    private void DesenharLinha(Vector2 inicio, Vector2 fim, Color cor, int espessura = 1)
    {
        Vector2 diferenca = fim - inicio;
        float distancia = diferenca.Length();
        float angulo = (float)Math.Atan2(diferenca.Y, diferenca.X);
        
        var rect = new Rectangle((int)inicio.X, (int)inicio.Y, (int)distancia, espessura);
        _spriteBatch.Draw(_pixelTexture, rect, null, cor, angulo, Vector2.Zero, SpriteEffects.None, 0);
    }

    private void DesenharParticulas()
    {
        // Desenha partículas de efeitos
        foreach (var particula in _particulas)
        {
            DesenharParticulaComEfeito(particula);
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
        // Encontra a posição da nave do jogador
        var nave = _estadoJogo?.Naves.FirstOrDefault(n => n.JogadorId == _meuJogadorId);
        if (nave != null)
        {
            // Adiciona partículas de efeito do tiro mais elaboradas
            for (int i = 0; i < 8; i++)
            {
                Vector2 velocidade = new Vector2(
                    _random.Next(-3, 4), 
                    _random.Next(-6, -2)
                );
                
                Color corParticula = i < 4 ? Color.Orange : Color.Red;
                
                _particulas.Add(new Particula(
                    nave.Posicao + new Vector2(_random.Next(-8, 9), -12),
                    velocidade,
                    corParticula,
                    20 + _random.Next(0, 20)
                ));
            }
            
            // Adiciona algumas partículas brilhantes
            for (int i = 0; i < 3; i++)
            {
                _particulas.Add(new Particula(
                    nave.Posicao + new Vector2(_random.Next(-5, 6), -10),
                    new Vector2(_random.Next(-1, 2), _random.Next(-3, 0)),
                    Color.White,
                    15
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
                
                // Atualiza a pontuação do jogador atual
                if (_meuJogadorId != -1)
                {
                    var minhaNave = _estadoJogo.Naves.FirstOrDefault(n => n.JogadorId == _meuJogadorId);
                    if (minhaNave != null)
                    {
                        _pontuacao = minhaNave.Pontuacao;
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
                
                // Se foi um reinício do jogo, reseta a flag
                if (msgGameOver.Motivo.Contains("reiniciado") || _reiniciarJogo)
                {
                    _reiniciarJogo = false;
                    _gerenciadorDificuldade.Reiniciar();
                    _pontuacao = 0;
                    _estadoGameOver = EstadoGameOver.Fechado;
                    _opcaoSelecionadaGameOver = 0;
                    _jogoAtivo = true;
                    Console.WriteLine("Jogo reiniciado com sucesso!");
                }
                else
                {
                    // Salva recorde no game over normal
                    if (_pontuacao > 0)
                    {
                        SalvarRecorde();
                    }
                    _estadoGameOver = EstadoGameOver.Aberto;
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
        }
    }
        private void DesenharNaveLosango(Vector2 posicao, Color corPrincipal, Color corDetalhes, float tamanho = 1.0f)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        
        // Aplica o fator de tamanho
        int altura = (int)(10 * tamanho);
        int largura = (int)(6 * tamanho);
        
        // Corpo principal da nave (losango)
        Vector2[] pontos = {
            new Vector2(x, y - altura),      // Ponta superior
            new Vector2(x - largura, y),     // Lateral esquerda
            new Vector2(x, y + altura),      // Ponta inferior
            new Vector2(x + largura, y)      // Lateral direita
        };
        
        // Desenha o corpo da nave
        DesenharPoligonoPreenchido(pontos, corPrincipal);
        
        // Desenha detalhes da nave (escalados)
        int cockpitSize = Math.Max(2, (int)(2 * tamanho));
        var cockpitRect = new Rectangle(x - cockpitSize/2, y - cockpitSize/2, cockpitSize, cockpitSize);
        _spriteBatch.Draw(_pixelTexture, cockpitRect, corDetalhes);
        
        // Motores nas laterais
        int motorWidth = Math.Max(2, (int)(3 * tamanho));
        int motorHeight = Math.Max(1, (int)(2 * tamanho));
        int motorOffset = (int)(8 * tamanho);
        
        var motorEsq = new Rectangle(x - motorOffset, y - motorHeight/2, motorWidth, motorHeight);
        var motorDir = new Rectangle(x + motorOffset - motorWidth, y - motorHeight/2, motorWidth, motorHeight);
        _spriteBatch.Draw(_pixelTexture, motorEsq, Color.Orange);
        _spriteBatch.Draw(_pixelTexture, motorDir, Color.Orange);
    }

    private void DesenharNaveHexagonal(Vector2 posicao, Color corPrincipal, Color corDetalhes, float tamanho = 1.0f)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        
        // Aplica o fator de tamanho
        float raio = 8 * tamanho;
        
        // Corpo principal da nave (hexágono)
        Vector2[] pontos = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angulo = (float)(Math.PI / 3 * i - Math.PI / 2); // Começa do topo
            pontos[i] = new Vector2(
                x + raio * (float)Math.Cos(angulo),
                y + raio * (float)Math.Sin(angulo)
            );
        }
        
        // Desenha o corpo da nave
        DesenharPoligonoPreenchido(pontos, corPrincipal);
        
        // Desenha detalhes da nave (escalados)
        int cockpitSize = Math.Max(3, (int)(3 * tamanho));
        var cockpitRect = new Rectangle(x - cockpitSize/2, y - cockpitSize/2, cockpitSize, cockpitSize);
        _spriteBatch.Draw(_pixelTexture, cockpitRect, corDetalhes);
        
        // Motor central
        int motorWidth = Math.Max(1, (int)(2 * tamanho));
        int motorHeight = Math.Max(2, (int)(4 * tamanho));
        int motorOffset = (int)(6 * tamanho);
        
        var motor = new Rectangle(x - motorWidth/2, y + motorOffset, motorWidth, motorHeight);
        _spriteBatch.Draw(_pixelTexture, motor, Color.Orange);
    }

    private void DesenharNaveCircular(Vector2 posicao, Color corPrincipal, Color corDetalhes, float tamanho = 1.0f)
    {
        int x = (int)posicao.X;
        int y = (int)posicao.Y;
        int raio = Math.Max(4, (int)(8 * tamanho));
        
        // Desenha círculo preenchido
        for (int dy = -raio; dy <= raio; dy++)
        {
            for (int dx = -raio; dx <= raio; dx++)
            {
                if (dx * dx + dy * dy <= raio * raio)
                {
                    var pixelRect = new Rectangle(x + dx, y + dy, 1, 1);
                    _spriteBatch.Draw(_pixelTexture, pixelRect, corPrincipal);
                }
            }
        }
        
        // Desenha anel de detalhes
        int raioInterno = Math.Max(2, raio - 4);
        int raioExterno = Math.Max(3, raio - 2);
        
        for (int dy = -raioExterno; dy <= raioExterno; dy++)
        {
            for (int dx = -raioExterno; dx <= raioExterno; dx++)
            {
                int distSq = dx * dx + dy * dy;
                if (distSq <= raioExterno * raioExterno && distSq >= raioInterno * raioInterno)
                {
                    var pixelRect = new Rectangle(x + dx, y + dy, 1, 1);
                    _spriteBatch.Draw(_pixelTexture, pixelRect, corDetalhes);
                }
            }
        }
        
        // Motor
        int motorWidth = Math.Max(1, (int)(2 * tamanho));
        int motorHeight = Math.Max(2, (int)(4 * tamanho));
        int motorOffset = Math.Max(2, raio - 2);
        
        var motor = new Rectangle(x - motorWidth/2, y + motorOffset, motorWidth, motorHeight);
        _spriteBatch.Draw(_pixelTexture, motor, Color.Orange);
    }

    private void DesenharPoligonoPreenchido(Vector2[] pontos, Color cor)
    {
        if (pontos.Length < 3) return;
        
        // Encontra os limites do polígono
        float minY = pontos.Min(p => p.Y);
        float maxY = pontos.Max(p => p.Y);
        
        for (int y = (int)minY; y <= (int)maxY; y++)
        {
            List<float> intersecoes = new List<float>();
            
            for (int i = 0; i < pontos.Length; i++)
            {
                Vector2 p1 = pontos[i];
                Vector2 p2 = pontos[(i + 1) % pontos.Length];
                
                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    float x = p1.X + (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    intersecoes.Add(x);
                }
            }
            
            intersecoes.Sort();
            for (int i = 0; i < intersecoes.Count - 1; i += 2)
            {
                for (int x = (int)intersecoes[i]; x <= (int)intersecoes[i + 1]; x++)
                {
                    var rect = new Rectangle(x, y, 1, 1);
                    _spriteBatch.Draw(_pixelTexture, rect, cor);
                }
            }
        }
    }

    private void DesenharMenuPausa()
    {
        // Se estiver no menu de configurações, desenha o menu de configurações
        if (_estadoMenuPausa == EstadoMenuPausa.Configuracoes)
        {
            DesenharMenuConfiguracoes();
            return;
        }

        // Se estiver no menu de recordes, desenha o menu de recordes
        if (_estadoMenuPausa == EstadoMenuPausa.Recordes)
        {
            DesenharMenuRecordes();
            return;
        }

        // Fundo semi-transparente
        var fundoRect = new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        _spriteBatch.Draw(_pixelTexture, fundoRect, Color.FromNonPremultiplied(0, 0, 0, 150));

        // Painel do menu - maior e mais consistente com o Game Over
        int larguraMenu = 500;
        int alturaMenu = 400;
        int x = (_graphics.PreferredBackBufferWidth - larguraMenu) / 2;
        int y = (_graphics.PreferredBackBufferHeight - alturaMenu) / 2;

        var painelRect = new Rectangle(x, y, larguraMenu, alturaMenu);
        _spriteBatch.Draw(_pixelTexture, painelRect, Color.FromNonPremultiplied(20, 40, 60, 240));

        // Borda do painel - mais espessa e colorida
        DesenharBorda(painelRect, Color.Cyan, 4);

        // Título principal
        string titulo = "JOGO PAUSADO";
        var tamanhoTitulo = _fonte.MeasureString(titulo);
        var posicaoTitulo = new Vector2(x + (larguraMenu - tamanhoTitulo.X) / 2, y + 30);
        _spriteBatch.DrawString(_fonte, titulo, posicaoTitulo, Color.Cyan);

        // Subtítulo
        string subtitulo = "ESCOLHA UMA OPCAO";
        var tamanhoSubtitulo = _fonte.MeasureString(subtitulo);
        var posicaoSubtitulo = new Vector2(x + (larguraMenu - tamanhoSubtitulo.X) / 2, y + 80);
        _spriteBatch.DrawString(_fonte, subtitulo, posicaoSubtitulo, Color.Yellow);

        // Opções do menu - melhor espaçamento
        Color[] cores = new Color[_opcoesMenuPausa.Length];
        for (int i = 0; i < cores.Length; i++)
        {
            cores[i] = Color.White;
        }
        cores[_opcaoSelecionadaPausa] = Color.Yellow;

        int inicioOpcoes = y + 140;
        int espacamentoOpcoes = 45;

        for (int i = 0; i < _opcoesMenuPausa.Length; i++)
        {
            var tamanhoOpcao = _fonte.MeasureString(_opcoesMenuPausa[i]);
            var posicaoOpcao = new Vector2(x + (larguraMenu - tamanhoOpcao.X) / 2, inicioOpcoes + i * espacamentoOpcoes);
            _spriteBatch.DrawString(_fonte, _opcoesMenuPausa[i], posicaoOpcao, cores[i]);

            // Indicador de seleção - melhor posicionado
            if (i == _opcaoSelecionadaPausa)
            {
                var indicadorRect = new Rectangle((int)posicaoOpcao.X - 30, (int)posicaoOpcao.Y + 8, 20, 20);
                _spriteBatch.Draw(_pixelTexture, indicadorRect, Color.Yellow);
                
                // Borda do indicador
                DesenharBorda(indicadorRect, Color.Orange, 2);
            }
        }

        // Instruções - melhor posicionadas
        string instrucoes = "W/S: Navegar | Enter: Selecionar | M: Confirmar Retorno";
        var tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        var posicaoInstrucoes = new Vector2(x + (larguraMenu - tamanhoInstrucoes.X) / 2, y + alturaMenu - 40);
        _spriteBatch.DrawString(_fonte, instrucoes, posicaoInstrucoes, Color.Gray);

        // Secção de consenso (x/y e faltantes)
        if (_pausaEmAndamentoUI && _pausaTotal > 0)
        {
            int confirmados = Math.Clamp(_pausaTotal - _pausaPendentes.Count, 0, _pausaTotal);
            string textoStatus = $"Confirmados: {confirmados}/{_pausaTotal}";
            var tamanhoStatus = _fonte.MeasureString(textoStatus);
            var posicaoStatus = new Vector2(x + (larguraMenu - tamanhoStatus.X) / 2, y + alturaMenu - 80);
            _spriteBatch.DrawString(_fonte, textoStatus, posicaoStatus, Color.Cyan);

            if (_pausaPendentes.Count > 0)
            {
                // Monta lista de quem falta
                var nomesFaltando = _pausaPendentes
                    .Select(id => _nomesJogadores.TryGetValue(id, out var nome) ? nome : $"Jogador {id}")
                    .ToList();
                string tituloFaltando = "Faltando:";
                var tamTituloF = _fonte.MeasureString(tituloFaltando);
                var posTituloF = new Vector2(x + (larguraMenu - tamTituloF.X) / 2, (int)posicaoStatus.Y - 30);
                _spriteBatch.DrawString(_fonte, tituloFaltando, posTituloF, Color.Yellow);

                // Exibe nomes em múltiplas linhas se necessário
                string linhaAtual = string.Empty;
                List<string> linhas = new();
                foreach (var nome in nomesFaltando)
                {
                    string candidato = string.IsNullOrEmpty(linhaAtual) ? nome : linhaAtual + ", " + nome;
                    if (_fonte.MeasureString(candidato).X > larguraMenu - 100)
                    {
                        linhas.Add(linhaAtual);
                        linhaAtual = nome;
                    }
                    else
                    {
                        linhaAtual = candidato;
                    }
                }
                if (!string.IsNullOrEmpty(linhaAtual)) linhas.Add(linhaAtual);

                float yLista = posTituloF.Y + 24;
                foreach (var linha in linhas)
                {
                    var tamLinha = _fonte.MeasureString(linha);
                    var posLinha = new Vector2(x + (larguraMenu - tamLinha.X) / 2, yLista);
                    _spriteBatch.DrawString(_fonte, linha, posLinha, Color.White);
                    yLista += 22;
                }
            }
        }
    }

    private void ProcessarInputGameOver(KeyboardState estadoTeclado)
    {
        // Navegação - suporte para WASD e setas
        if ((estadoTeclado.IsKeyDown(Keys.W) && !_estadoTecladoAnterior.IsKeyDown(Keys.W)) ||
            (estadoTeclado.IsKeyDown(Keys.Up) && !_estadoTecladoAnterior.IsKeyDown(Keys.Up)))
        {
            _opcaoSelecionadaGameOver = (_opcaoSelecionadaGameOver - 1 + _opcoesGameOver.Length) % _opcoesGameOver.Length;
        }
        else if ((estadoTeclado.IsKeyDown(Keys.S) && !_estadoTecladoAnterior.IsKeyDown(Keys.S)) ||
                 (estadoTeclado.IsKeyDown(Keys.Down) && !_estadoTecladoAnterior.IsKeyDown(Keys.Down)))
        {
            _opcaoSelecionadaGameOver = (_opcaoSelecionadaGameOver + 1) % _opcoesGameOver.Length;
        }

        // Seleção
        if (estadoTeclado.IsKeyDown(Keys.Enter) && !_estadoTecladoAnterior.IsKeyDown(Keys.Enter))
        {
            switch (_opcaoSelecionadaGameOver)
            {
                case 0: // Reiniciar Jogo
                    _reiniciarJogo = true;
                    _estadoGameOver = EstadoGameOver.Fechado;
                    _opcaoSelecionadaGameOver = 0; // Reset da seleção
                    Console.WriteLine("Solicitando reinicio do jogo...");
                    break;
                case 1: // Voltar ao Menu
                    VoltarAoMenu = true;
                    break;
                case 2: // Sair do Jogo
                    SairDoJogo = true;
                    break;
            }
        }

        // Sair com ESC
        if (estadoTeclado.IsKeyDown(Keys.Escape) && !_estadoTecladoAnterior.IsKeyDown(Keys.Escape))
        {
            SairDoJogo = true;
        }
    }

    private void ProcessarInputMenuPausa(KeyboardState estadoTeclado)
    {
        // Navegação no menu (teclas direcionais)
        if (estadoTeclado.IsKeyDown(Keys.Down) && !_estadoTecladoAnterior.IsKeyDown(Keys.Down))
        {
            _opcaoSelecionadaPausa = (_opcaoSelecionadaPausa + 1) % _opcoesMenuPausa.Length;
        }
        else if (estadoTeclado.IsKeyDown(Keys.Up) && !_estadoTecladoAnterior.IsKeyDown(Keys.Up))
        {
            _opcaoSelecionadaPausa = (_opcaoSelecionadaPausa - 1 + _opcoesMenuPausa.Length) % _opcoesMenuPausa.Length;
        }

        // Seleção de opção (tecla Enter)
        if (estadoTeclado.IsKeyDown(Keys.Enter) && !_estadoTecladoAnterior.IsKeyDown(Keys.Enter))
        {
            switch ((OpcaoMenuPausa)_opcaoSelecionadaPausa)
            {
                case OpcaoMenuPausa.Retomar:
                    // Em consenso, apenas confirma e mantém menu aberto
                    if (_pausaEmAndamentoUI || _jogoPausado)
                    {
                        _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = false, JogadorId = _meuJogadorId });
                        if (_pausaPendentes.Contains(_meuJogadorId))
                        {
                            _pausaPendentes.Remove(_meuJogadorId);
                            _pausaConfirmados.Add(_meuJogadorId);
                        }
                    }
                    else
                    {
                        _estadoMenuPausa = EstadoMenuPausa.Fechado;
                        _jogoPausado = false;
                        _asteroidesEmPausa.Clear();
                        _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = false, JogadorId = _meuJogadorId });
                    }
                    break;
                    
                case OpcaoMenuPausa.Configuracoes:
                    _estadoMenuPausa = EstadoMenuPausa.Configuracoes;
                    _menuPersonalizacao.MenuAtivo = true;
                    _menuPersonalizacao.VoltarParaMenuPrincipal = false;
                    break;
                    
                case OpcaoMenuPausa.Recordes:
                    _estadoMenuPausa = EstadoMenuPausa.Recordes;
                    break;
                    
                case OpcaoMenuPausa.VoltarMenu:
                    // Garante que o servidor retome antes de voltar ao menu
                    _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = false });
                    _jogoPausado = false;
                    _asteroidesEmPausa.Clear();
                    VoltarAoMenu = true;
                    break;
                    
                case OpcaoMenuPausa.Sair:
                    // Garante que o servidor retome antes de sair
                    _ = _clienteRede.EnviarMensagemAsync(new MensagemPausarJogo { Pausado = false });
                    _jogoPausado = false;
                    _asteroidesEmPausa.Clear();
                    SairDoJogo = true;
                    break;
            }
        }
        
        // Volta para o menu principal de pausa quando estiver em submenu
        if (estadoTeclado.IsKeyDown(Keys.Escape) && !_estadoTecladoAnterior.IsKeyDown(Keys.Escape))
        {
            if (_estadoMenuPausa == EstadoMenuPausa.Configuracoes || _estadoMenuPausa == EstadoMenuPausa.Recordes)
            {
                _estadoMenuPausa = EstadoMenuPausa.Aberto;
                if (_estadoMenuPausa == EstadoMenuPausa.Configuracoes)
                    _menuPersonalizacao.MenuAtivo = false;
            }
        }
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
        _spriteBatch.Draw(_pixelTexture, fundoMenuRect, Color.FromNonPremultiplied(0, 0, 0, 150));

        // Painel do menu - maior e mais centralizado
        int larguraMenu = 500;
        int alturaMenu = 350;
        int x = (largura - larguraMenu) / 2;
        int y = (altura - alturaMenu) / 2;

        var painelRect = new Rectangle(x, y, larguraMenu, alturaMenu);
        _spriteBatch.Draw(_pixelTexture, painelRect, Color.FromNonPremultiplied(20, 20, 40, 240));

        // Borda do painel - mais espessa e colorida
        DesenharBorda(painelRect, Color.Red, 4);

        // Título "GAME OVER"
        string tituloGameOver = "GAME OVER";
        var tamanhoTituloGameOver = _fonte.MeasureString(tituloGameOver);
        var posicaoTituloGameOver = new Vector2(x + (larguraMenu - tamanhoTituloGameOver.X) / 2, y + 30);
        _spriteBatch.DrawString(_fonte, tituloGameOver, posicaoTituloGameOver, Color.Red);

        // Subtítulo
        string subtitulo = "ESCOLHA UMA OPCAO";
        var tamanhoSubtitulo = _fonte.MeasureString(subtitulo);
        var posicaoSubtitulo = new Vector2(x + (larguraMenu - tamanhoSubtitulo.X) / 2, y + 80);
        _spriteBatch.DrawString(_fonte, subtitulo, posicaoSubtitulo, Color.Yellow);

        // Opções do menu - melhor espaçamento
        Color[] cores = { Color.White, Color.White, Color.White };
        cores[_opcaoSelecionadaGameOver] = Color.Yellow;

        int inicioOpcoes = y + 140;
        int espacamentoOpcoes = 50;

        for (int i = 0; i < _opcoesGameOver.Length; i++)
        {
            var tamanhoOpcao = _fonte.MeasureString(_opcoesGameOver[i]);
            var posicaoOpcao = new Vector2(x + (larguraMenu - tamanhoOpcao.X) / 2, inicioOpcoes + i * espacamentoOpcoes);
            _spriteBatch.DrawString(_fonte, _opcoesGameOver[i], posicaoOpcao, cores[i]);

            // Indicador de seleção - melhor posicionado
            if (i == _opcaoSelecionadaGameOver)
            {
                var indicadorRect = new Rectangle((int)posicaoOpcao.X - 30, (int)posicaoOpcao.Y + 8, 20, 20);
                _spriteBatch.Draw(_pixelTexture, indicadorRect, Color.Yellow);
                
                // Borda do indicador
                DesenharBorda(indicadorRect, Color.Orange, 2);
            }
        }

        // Instruções - melhor posicionadas
        string instrucoes = "W/S ou ↑/↓: Navegar | Enter: Selecionar | M: Menu de Pausa";
        var tamanhoInstrucoes = _fonte.MeasureString(instrucoes);
        var posicaoInstrucoes = new Vector2(x + (larguraMenu - tamanhoInstrucoes.X) / 2, y + alturaMenu - 40);
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
        var dificuldades = new[] { NivelDificuldade.Facil, NivelDificuldade.Medio, NivelDificuldade.Dificil };
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

