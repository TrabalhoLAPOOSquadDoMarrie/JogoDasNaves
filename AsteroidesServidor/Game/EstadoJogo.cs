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
    public Nave? ObterNave(int jogadorId)
        {
            _naves.TryGetValue(jogadorId, out var nave);
            return nave;
        }
    private readonly Dictionary<int, Tiro> _tiros = new();
    private readonly Dictionary<int, Asteroide> _asteroides = new();
    private readonly Random _random = new();
    private readonly object _lock = new();
    private readonly HashSet<int> _jogadoresPausados = new();

    private int _proximoIdTiro = 1;
    private int _proximoIdAsteroide = 1;
    private int _frameCount = 0;
    private float _tempoJogoSegundos = 0f; // Tempo real de jogo em segundos
    private float _ultimoSpawnAsteroide = 0f; // Último spawn em segundos
    private bool _gameOverMensagemExibida = false;

    public const int LarguraTela = 1280;
    public const int AlturaTela = 720;
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
    public void AtualizarMovimentoNave(int jogadorId, bool esquerda, bool direita, bool cima, bool baixo, float deltaTime)
    {
        lock (_lock)
        {
            if (_naves.TryGetValue(jogadorId, out var nave) && nave.Viva)
            {
                nave.Atualizar(esquerda, direita, cima, baixo, LarguraTela, AlturaTela, deltaTime);
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
    /// Atualiza o estado completo do jogo com movimento independente de framerate
    /// </summary>
    public void AtualizarJogo(float deltaTime)
    {
        // Verifica se o jogo pode ser atualizado sem lock
        bool jogoAtivo;
        bool simulacaoPausada;
        
        lock (_lock)
        {
            jogoAtivo = JogoAtivo;
            simulacaoPausada = SimulacaoPausada;
            
            if (!jogoAtivo || simulacaoPausada)
                return;

            _frameCount++;
            _tempoJogoSegundos += deltaTime;
        }

        // Atualiza componentes com locks mais granulares
        AtualizarTirosComLockGranular(deltaTime);
        AtualizarAsteroidesComLockGranular(deltaTime);
        VerificarColisoesComLockGranular();

        lock (_lock)
        {
            SpawnarAsteroidesComDificuldade(deltaTime);
            VerificarGameOver();
        }
    }

    /// <summary>
    /// Atualiza todos os tiros e remove os que saíram da tela
    /// </summary>
    private void AtualizarTiros(float deltaTime)
    {
        var tirosParaRemover = new List<int>();

        foreach (var tiro in _tiros.Values)
        {
            tiro.Atualizar(deltaTime);
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
    private void AtualizarAsteroides(float deltaTime)
    {
        var asteroidesParaRemover = new List<int>();

        foreach (var asteroide in _asteroides.Values)
        {
            asteroide.Atualizar(deltaTime);
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
    /// Atualiza todos os tiros com lock granular para reduzir contention
    /// </summary>
    private void AtualizarTirosComLockGranular(float deltaTime)
    {
        var tirosParaRemover = new List<int>();
        
        // Cria uma cópia para iteração sem lock prolongado
        Dictionary<int, Tiro> tirosCopia;
        lock (_lock)
        {
            tirosCopia = new Dictionary<int, Tiro>(_tiros);
        }

        // Atualiza tiros sem lock
        foreach (var tiro in tirosCopia.Values)
        {
            tiro.Atualizar(deltaTime);
            if (tiro.ForaDaTela(AlturaTela))
            {
                tirosParaRemover.Add(tiro.Id);
            }
        }

        // Remove tiros fora da tela com lock mínimo
        if (tirosParaRemover.Count > 0)
        {
            lock (_lock)
            {
                foreach (var id in tirosParaRemover)
                {
                    _tiros.Remove(id);
                }
            }
        }
    }

    /// <summary>
    /// Atualiza todos os asteroides com lock granular para reduzir contention
    /// </summary>
    private void AtualizarAsteroidesComLockGranular(float deltaTime)
    {
        var asteroidesParaRemover = new List<int>();
        
        // Cria uma cópia para iteração sem lock prolongado
        Dictionary<int, Asteroide> asteroidesCopia;
        lock (_lock)
        {
            asteroidesCopia = new Dictionary<int, Asteroide>(_asteroides);
        }

        // Atualiza asteroides sem lock
        foreach (var asteroide in asteroidesCopia.Values)
        {
            asteroide.Atualizar(deltaTime);
            if (asteroide.ForaDaTela(AlturaTela))
            {
                asteroidesParaRemover.Add(asteroide.Id);
            }
        }

        // Remove asteroides fora da tela com lock mínimo
        if (asteroidesParaRemover.Count > 0)
        {
            lock (_lock)
            {
                foreach (var id in asteroidesParaRemover)
                {
                    _asteroides.Remove(id);
                }
            }
        }
    }

    /// <summary>
    /// Verifica colisões com lock granular usando snapshots thread-safe
    /// </summary>
    private void VerificarColisoesComLockGranular()
    {
        // Cria snapshots das coleções para evitar modificação durante PLINQ
        Dictionary<int, Tiro> snapshotTiros;
        Dictionary<int, Asteroide> snapshotAsteroides;
        Dictionary<int, Nave> snapshotNaves;

        lock (_lock)
        {
            snapshotTiros = new Dictionary<int, Tiro>(_tiros);
            snapshotAsteroides = new Dictionary<int, Asteroide>(_asteroides);
            snapshotNaves = new Dictionary<int, Nave>(_naves);
        }

        var asteroidesParaRemover = new List<int>();
        var tirosParaRemover = new List<int>();

        // Usa PLINQ seguro com snapshots
        var colisoesTiroAsteroide = snapshotAsteroides.Values.AsParallel()
            .SelectMany(asteroide => snapshotTiros.Values.AsParallel()
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
            }
        }

        // Verifica colisões nave × asteroide
        var navesParaMatar = new List<int>();
        foreach (var nave in snapshotNaves.Values.Where(n => n.Viva))
        {
            foreach (var asteroide in snapshotAsteroides.Values)
            {
                if (asteroide.Colide(nave))
                {
                    navesParaMatar.Add(nave.JogadorId);
                    break;
                }
            }
        }

        // Aplica resultados com lock mínimo
        lock (_lock)
        {
            // Remove tiros e asteroides que colidiram
            foreach (var id in tirosParaRemover)
            {
                _tiros.Remove(id);
            }
            
            foreach (var id in asteroidesParaRemover)
            {
                _asteroides.Remove(id);
            }

            // Adiciona pontos e mata naves
            foreach (var colisao in colisoesTiroAsteroide)
            {
                if (_naves.TryGetValue(colisao.Tiro.JogadorId, out var nave))
                {
                    nave.AdicionarPontos(10);
                }
            }

            foreach (var jogadorId in navesParaMatar)
            {
                if (_naves.TryGetValue(jogadorId, out var nave))
                {
                    nave.Morrer();
                }
            }
        }
    }

    /// <summary>
    /// Spawna asteroides com base na dificuldade e tempo de jogo (independente de framerate)
    /// </summary>
    private void SpawnarAsteroidesComDificuldade(float deltaTime)
    {
        // Calcula intervalo baseado no tempo real (segundos)
        float intervaloSpawn = CalcularIntervaloSpawn(_tempoJogoSegundos);
        
        // Verifica se é hora de spawnar baseado no tempo real
        if (_tempoJogoSegundos - _ultimoSpawnAsteroide >= intervaloSpawn)
        {
            // Calcula quantos asteroides spawnar baseado no tempo
            int quantidadeBase = CalcularQuantidadeAsteroides((int)_tempoJogoSegundos);
            
            // Aumenta em 1,45x a quantidade (arredonda para cima) e limita a 8 por spawn
            int quantidadeAsteroides = Math.Clamp((int)Math.Ceiling(quantidadeBase * 1.40f), 1, 8);
            
            for (int i = 0; i < quantidadeAsteroides; i++)
            {
                SpawnarAsteroide();
            }
            
            _ultimoSpawnAsteroide = _tempoJogoSegundos;
        }
    }

    /// <summary>
    /// Calcula o intervalo entre spawns baseado no tempo de jogo
    /// </summary>
    private float CalcularIntervaloSpawn(float tempoSegundos)
    {
        // Intervalo em segundos: começa em 2.5s e diminui gradualmente até 1.0s
        float intervaloBase = 2.5f;
        float reducao = tempoSegundos / 60f; // Reduz 1 segundo a cada 60 segundos
        return Math.Max(1.0f, intervaloBase - reducao);
    }
    
    /// <summary>
    /// Calcula quantos asteroides devem ser spawnados baseado no tempo
    /// </summary>
    private int CalcularQuantidadeAsteroides(int segundos)
    {
        // Progressão mais lenta da quantidade de asteroides
        if (segundos < 90) return 1;         // 0-1.5 min: 1
        if (segundos < 180) return 2;        // 1.5-3 min: 2
        if (segundos < 360) return 3;        // 3-6 min: 3
        if (segundos < 600) return 4;        // 6-10 min: 4
        return 5;                             // Máximo de 5
    }

    /// <summary>
    /// Spawna um novo asteroide em posição aleatória
    /// </summary>
    private void SpawnarAsteroide()
    {
        float x = _random.Next(LarguraTela);
        
        // Velocidade baseada no tempo real (pixels por segundo) - mais lenta
        float velYBase = 70f + (_tempoJogoSegundos * 1.2f); // Crescimento mais suave
        float velY = velYBase + (float)_random.NextDouble() * 30f; // Variação menor
        
        // Tamanho varia um pouco (mantido)
        float raio = 20 + (float)_random.NextDouble() * 10; // 20-30 pixels
        
        var asteroide = new Asteroide(
            _proximoIdAsteroide++,
            new Vector2(x, -30),
            new Vector2(0, velY), // Velocidade em pixels por segundo
            raio,
            _random.Next(0, 3) // Tipo de textura (0 a 2)
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
                    Rotacao = n.Rotacao,
                    Viva = n.Viva,
                    Pontuacao = n.Pontuacao,
                    ModeloNave = n.ModeloNave,
                    Tamanho = n.Tamanho
                }).ToList(),
                Tiros = _tiros.Values.Select(t => new DadosTiro
                {
                    Id = t.Id,
                    JogadorId = t.JogadorId,
                    Posicao = t.Posicao,
                }).ToList(),
                Asteroides = _asteroides.Values.Select(a => new DadosAsteroide
                {
                    Id = a.Id,
                    Posicao = a.Posicao,
                    Raio = a.Raio,
                    TipoTextura = a.TipoTextura
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
            _tempoJogoSegundos = 0; // REINICIA O TEMPO DO JOGO
            _ultimoSpawnAsteroide = 0; // REINICIA O TEMPO DE SPAWN
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

    /// <summary>
    /// Reativa uma nave existente ou cria uma nova se necessário
    /// Preserva personalização e pontuação
    /// </summary>
    public void ReativarOuCriarNave(int jogadorId)
    {
        lock (_lock)
        {
            if (_naves.TryGetValue(jogadorId, out var naveExistente))
            {
                // Nave já existe - apenas reativa se estiver morta
                if (!naveExistente.Viva)
                {
                    Vector2 posicaoInicial = new Vector2(LarguraTela / 2f + (jogadorId * 50), AlturaTela - 100);
                    naveExistente.Posicao = posicaoInicial;
                    naveExistente.Viva = true;
                    naveExistente.Rotacao = 0f;
                    
                    Console.WriteLine($"Nave reativada para jogador {jogadorId} (pontuação preservada: {naveExistente.Pontuacao}, modelo: {naveExistente.ModeloNave})");
                }
                else
                {
                    Console.WriteLine($"Nave do jogador {jogadorId} já estava ativa");
                }
            }
            else
            {
                // Nave não existe - cria nova
                AdicionarNave(jogadorId);
            }
        }
    }
}