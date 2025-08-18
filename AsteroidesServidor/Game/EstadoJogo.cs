using Microsoft.Xna.Framework;
using AsteroidesServidor.Models;
using AsteroidesServidor.Network;

namespace AsteroidesServidor.Game;

/// <summary>
/// Gerencia o estado do jogo no servidor
/// </summary>
public class EstadoJogo
{
    private readonly Dictionary<int, Nave> _naves = new();
    private readonly Dictionary<int, Tiro> _tiros = new();
    private readonly Dictionary<int, Asteroide> _asteroides = new();
    private readonly Random _random = new();
    private readonly object _lock = new();
    private readonly HashSet<int> _jogadoresPausados = new();

    private int _proximoIdTiro = 1;
    private int _proximoIdAsteroide = 1;
    private int _frameCount = 0;
    private int _ultimoSpawnAsteroide = 0;
    private bool _gameOverMensagemExibida = false;

    public const int LarguraTela = 1200;
    public const int AlturaTela = 800;
    public bool JogoAtivo { get; private set; } = true;

    /// <summary>
    /// Adiciona uma nova nave ao jogo
    /// </summary>
    public void AdicionarNave(int jogadorId)
    {
        lock (_lock)
        {
            if (_naves.ContainsKey(jogadorId)) return;

            // Posiciona a nave no centro da tela
            Vector2 posicao = new Vector2(
                LarguraTela / 2f,
                AlturaTela / 2f
            );

            _naves[jogadorId] = new Nave(jogadorId, posicao);
            Console.WriteLine($"Nave do jogador {jogadorId} adicionada na posição {posicao}");
        }
    }

    public bool SimulacaoPausada { get; private set; }

    public int PausadosRestantes
    {
        get
        {
            lock (_lock)
            {
                return _jogadoresPausados.Count;
            }
        }
    }

    public void IniciarPausaGlobal(IEnumerable<int> jogadoresIds)
    {
        lock (_lock)
        {
            SimulacaoPausada = true;
            _jogadoresPausados.Clear();
            foreach (var id in jogadoresIds)
            {
                _jogadoresPausados.Add(id);
            }
            Console.WriteLine($"Pausa global iniciada. Aguardando retornos: {_jogadoresPausados.Count}");
        }
    }

    public void ConfirmarRetorno(int jogadorId)
    {
        lock (_lock)
        {
            if (_jogadoresPausados.Remove(jogadorId))
            {
                Console.WriteLine($"Jogador {jogadorId} confirmou retorno. Restantes: {_jogadoresPausados.Count}");
            }
            if (_jogadoresPausados.Count == 0 && SimulacaoPausada)
            {
                SimulacaoPausada = false;
                Console.WriteLine("Todos confirmaram retorno. Simulacao retomada.");
            }
        }
    }

    public void DefinirPausaJogador(int jogadorId, bool pausado) // <-- novo
    {
        lock (_lock)
        {
            if (pausado) _jogadoresPausados.Add(jogadorId);
            else _jogadoresPausados.Remove(jogadorId);
            Console.WriteLine(pausado
                ? $"Jogador {jogadorId} pausou. Pausas ativas: {_jogadoresPausados.Count}"
                : $"Jogador {jogadorId} retomou. Pausas ativas: {_jogadoresPausados.Count}");
        }
    }

    public bool SimulacaoEstaPausada() => SimulacaoPausada;

    public void DefinirPausado(bool pausado)
    {
        SimulacaoPausada = pausado;
        Console.WriteLine(pausado ? "Simulacao pausada" : "Simulacao retomada");
    }
    
    /// <summary>
    /// Remove uma nave do jogo
    /// </summary>
    public void RemoverNave(int jogadorId)
    {
        lock (_lock)
        {
            if (_naves.Remove(jogadorId))
            {
                Console.WriteLine($"Nave do jogador {jogadorId} removida");
            }

            // Remove todos os tiros do jogador
            var tirosParaRemover = _tiros.Where(t => t.Value.JogadorId == jogadorId).ToList();
            foreach (var tiro in tirosParaRemover)
            {
                _tiros.Remove(tiro.Key);
            }
        }
    }

    /// <summary>
    /// Atualiza o movimento de uma nave
    /// </summary>
    public void AtualizarMovimentoNave(int jogadorId, bool esquerda, bool direita, bool cima, bool baixo)
    {
        lock (_lock)
        {
            if (_naves.TryGetValue(jogadorId, out var nave) && nave.Viva)
            {
                nave.Atualizar(esquerda, direita, cima, baixo, LarguraTela, AlturaTela);
            }
        }
    }

    /// <summary>
    /// Adiciona um tiro disparado por uma nave
    /// </summary>
    public void AdicionarTiro(int jogadorId)
    {
        lock (_lock)
        {
            if (_naves.TryGetValue(jogadorId, out var nave) && nave.Viva)
            {
                var tiro = nave.Atirar(_proximoIdTiro++);
                _tiros[tiro.Id] = tiro;
            }
        }
    }

    /// <summary>
    /// Atualiza o estado completo do jogo
    /// </summary>
    public void AtualizarJogo()
    {
        lock (_lock)
        {
            // Se o jogo não está ativo, não executa a lógica de atualização
            if (!JogoAtivo)
                return;
            if (SimulacaoPausada) return;

            _frameCount++;

            // Atualiza tiros
            AtualizarTiros();

            // Atualiza asteroides
            AtualizarAsteroides();

            // Verifica colisões usando paralelismo
            VerificarColisoes();

            // Spawna novos asteroides com frequência e quantidade dinâmica
            SpawnarAsteroidesComDificuldade();

            // Verifica condição de game over
            VerificarGameOver();
        }
    }

    /// <summary>
    /// Atualiza todos os tiros e remove os que saíram da tela
    /// </summary>
    private void AtualizarTiros()
    {
        var tirosParaRemover = new List<int>();

        foreach (var tiro in _tiros.Values)
        {
            tiro.Atualizar();
            if (tiro.ForaDaTela(AlturaTela))
            {
                tirosParaRemover.Add(tiro.Id);
            }
        }

        foreach (var id in tirosParaRemover)
        {
            _tiros.Remove(id);
        }
    }

    /// <summary>
    /// Atualiza todos os asteroides e remove os que saíram da tela
    /// </summary>
    private void AtualizarAsteroides()
    {
        var asteroidesParaRemover = new List<int>();

        foreach (var asteroide in _asteroides.Values)
        {
            asteroide.Atualizar();
            if (asteroide.ForaDaTela(AlturaTela))
            {
                asteroidesParaRemover.Add(asteroide.Id);
            }
        }

        foreach (var id in asteroidesParaRemover)
        {
            _asteroides.Remove(id);
        }
    }

    /// <summary>
    /// Verifica colisões usando programação paralela (PLINQ)
    /// Esta é a tarefa computacionalmente pesada otimizada com paralelismo
    /// </summary>
    private void VerificarColisoes()
    {
        var asteroidesParaRemover = new List<int>();
        var tirosParaRemover = new List<int>();

        // Usa PLINQ para verificar colisões tiro × asteroide em paralelo
        // Justificativa: Com muitos asteroides e tiros, verificar todas as combinações
        // pode ser custoso. O paralelismo permite distribuir essas verificações
        // entre múltiplos threads, melhorando significativamente a performance.
        var colisoesTiroAsteroide = _asteroides.Values.AsParallel()
            .SelectMany(asteroide => _tiros.Values.AsParallel()
                .Where(tiro => asteroide.Colide(tiro))
                .Select(tiro => new { Asteroide = asteroide, Tiro = tiro }))
            .ToList();

        // Processa as colisões encontradas
        foreach (var colisao in colisoesTiroAsteroide)
        {
            if (!asteroidesParaRemover.Contains(colisao.Asteroide.Id) && 
                !tirosParaRemover.Contains(colisao.Tiro.Id))
            {
                asteroidesParaRemover.Add(colisao.Asteroide.Id);
                tirosParaRemover.Add(colisao.Tiro.Id);

                // Adiciona pontos ao jogador
                if (_naves.TryGetValue(colisao.Tiro.JogadorId, out var nave))
                {
                    nave.AdicionarPontos(10);
                }
            }
        }

        // Verifica colisões nave × asteroide
        foreach (var nave in _naves.Values.Where(n => n.Viva))
        {
            foreach (var asteroide in _asteroides.Values)
            {
                if (asteroide.Colide(nave))
                {
                    nave.Morrer();
                    Console.WriteLine($"Jogador {nave.JogadorId} foi atingido por um asteroide!");
                }
            }
        }

        // Remove objetos que colidiram
        foreach (var id in asteroidesParaRemover)
        {
            _asteroides.Remove(id);
        }

        foreach (var id in tirosParaRemover)
        {
            _tiros.Remove(id);
        }
    }

    /// <summary>
    /// Spawna asteroides com base na dificuldade e tempo de jogo
    /// </summary>
    private void SpawnarAsteroidesComDificuldade()
    {
        // Calcula intervalo baseado no tempo (começa com 90 frames e diminui gradualmente até 30)
        int segundos = _frameCount / 60;
        int intervaloSpawn = Math.Max(90 - (segundos / 20), 30); // Progressão mais lenta
        
        // Verifica se é hora de spawnar
        if (_frameCount - _ultimoSpawnAsteroide >= intervaloSpawn)
        {
            // Calcula quantos asteroides spawnar baseado no tempo
            int quantidadeAsteroides = CalcularQuantidadeAsteroides(segundos);
            
            for (int i = 0; i < quantidadeAsteroides; i++)
            {
                SpawnarAsteroide();
            }
            
            _ultimoSpawnAsteroide = _frameCount;
        }
    }
    
    /// <summary>
    /// Calcula quantos asteroides devem ser spawnados baseado no tempo
    /// </summary>
    private int CalcularQuantidadeAsteroides(int segundos)
    {
        // Progressão mais gradual e lenta da quantidade de asteroides
        if (segundos < 60) return 1;        // Primeiro minuto: 1 asteroide
        if (segundos < 120) return 2;       // Segundo minuto: 2 asteroides
        if (segundos < 240) return 3;       // Terceiro e quarto minutos: 3 asteroides
        if (segundos < 360) return 4;       // Quinto e sexto minutos: 4 asteroides
        if (segundos < 480) return 5;       // Sétimo e oitavo minutos: 5 asteroides
        return Math.Min(6, 3 + (segundos / 120)); // Máximo de 6 asteroides, aumenta a cada 2 minutos
    }

    /// <summary>
    /// Spawna um novo asteroide em posição aleatória
    /// </summary>
    private void SpawnarAsteroide()
    {
        float x = _random.Next(LarguraTela);
        
        // Velocidade varia baseada no tempo de jogo - progressão mais lenta
        int segundos = _frameCount / 60;
        float velYBase = 1.5f + (segundos * 0.01f); // Aumenta mais gradualmente
        float velY = velYBase + (float)_random.NextDouble() * 1.5f; // Variação menor
        
        // Tamanho varia um pouco
        float raio = 20 + (float)_random.NextDouble() * 10; // 20-30 pixels
        
        var asteroide = new Asteroide(
            _proximoIdAsteroide++,
            new Vector2(x, -30),
            new Vector2(0, velY),
            raio
        );

        _asteroides[asteroide.Id] = asteroide;
    }

    /// <summary>
    /// Verifica se o jogo deve terminar
    /// </summary>
    private void VerificarGameOver()
    {
        // Game over apenas se há pelo menos 1 jogador E todas as naves estão mortas
        if (_naves.Count > 0 && _naves.Values.All(n => !n.Viva))
        {
            if (!_gameOverMensagemExibida)
            {
                JogoAtivo = false;
                _gameOverMensagemExibida = true;
                Console.WriteLine("Game Over - Todas as naves foram destruídas!");
            }
        }
    }

    /// <summary>
    /// Obtém o estado atual do jogo para envio aos clientes
    /// </summary>
    public MensagemEstadoJogo ObterEstadoJogo()
    {
        lock (_lock)
        {
            return new MensagemEstadoJogo
            {
                JogoAtivo = JogoAtivo,
                Naves = _naves.Values.Select(n => new DadosNave
                {
                    JogadorId = n.JogadorId,
                    Posicao = n.Posicao,
                    Viva = n.Viva,
                    Pontuacao = n.Pontuacao,
                    Tamanho = n.Tamanho
                }).ToList(),
                Tiros = _tiros.Values.Select(t => new DadosTiro
                {
                    Id = t.Id,
                    JogadorId = t.JogadorId,
                    Posicao = t.Posicao
                }).ToList(),
                Asteroides = _asteroides.Values.Select(a => new DadosAsteroide
                {
                    Id = a.Id,
                    Posicao = a.Posicao,
                    Raio = a.Raio
                }).ToList()
            };
        }
    }

    /// <summary>
    /// Reinicia o jogo
    /// </summary>
    public void ReiniciarJogo()
    {
        lock (_lock)
        {
            _tiros.Clear();
            _asteroides.Clear();
            _frameCount = 0;
            _ultimoSpawnAsteroide = 0;
            _proximoIdTiro = 1;
            _proximoIdAsteroide = 1;
            _gameOverMensagemExibida = false;
            JogoAtivo = true;

            // Reinicia as naves
            foreach (var nave in _naves.Values)
            {
                Vector2 posicaoInicial = new Vector2(LarguraTela / 2f, AlturaTela / 2f);
                nave.Resetar(posicaoInicial);
            }

            Console.WriteLine("Jogo reiniciado!");
        }
    }
}