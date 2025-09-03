using System.Net;
using System.Net.Sockets;
using AsteroidesServidor.Network;
using AsteroidesServidor.Game;

namespace AsteroidesServidor;

/// <summary>
/// Servidor principal do jogo Asteroides Multiplayer
/// Implementa comunicação TCP robusta com suporte a múltiplos clientes
/// </summary>
public class ServidorAsteroides
{
    private readonly TcpListener _tcpListener;
    private readonly Dictionary<int, ClienteConectado> _clientes = new();
    private readonly EstadoJogo _estadoJogo = new();
    private readonly object _lockClientes = new();
    private readonly HashSet<int> _votosReinicio = new(); // IDs dos jogadores que votaram para reiniciar
    
    private bool _rodando = false;  
    private int _proximoIdJogador = 0;
    private const int PortaPadrao = 8890;
    private const int TicksPerSecond = 144; // 144 FPS para alta performance
    private const int TickInterval = 1000 / TicksPerSecond;

    public ServidorAsteroides(int porta = PortaPadrao)
    {
        _tcpListener = new TcpListener(IPAddress.Any, porta);
        Console.WriteLine($"Servidor Asteroides inicializado na porta {porta}");
    }

    /// <summary>
    /// Inicia o servidor e começa a aceitar conexões
    /// </summary>
    public async Task IniciarAsync()
    {
        try
        {
            _tcpListener.Start();
            _rodando = true;
            
            Console.WriteLine("Servidor iniciado! Aguardando conexões...");
            Console.WriteLine("Pressione 'q' para parar o servidor");

            var tarefas = new List<Task>
            {
                Task.Run(AceitarConexoesAsync),      
                Task.Run(LoopPrincipalJogoAsync),    
                Task.Run(MonitorarClientesAsync),    
                Task.Run(MonitorarEntradaUsuarioAsync) 
            };

            await Task.WhenAny(tarefas);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao iniciar servidor: {ex.Message}");
        }
        finally
        {
            Parar();
        }
    }

    /// <summary>
    /// Aceita novas conexões de clientes de forma assíncrona
    /// </summary>
    private async Task AceitarConexoesAsync()
    {
        while (_rodando)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                var cliente = new ClienteConectado(Interlocked.Increment(ref _proximoIdJogador), tcpClient);

                lock (_lockClientes)
                {
                    _clientes[cliente.Id] = cliente;
                }

                Console.WriteLine($"Cliente {cliente.Id} conectado de {tcpClient.Client.RemoteEndPoint}");

                // Inicia uma tarefa para gerenciar este cliente específico
                _ = Task.Run(() => GerenciarClienteAsync(cliente));
            }
            catch (ObjectDisposedException)
            {
                // Servidor foi parado
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao aceitar conexão: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gerencia um cliente específico de forma assíncrona
    /// </summary>
    private async Task GerenciarClienteAsync(ClienteConectado cliente)
    {
        try
        {
            while (_rodando && cliente.Conectado)
            {
                var mensagem = await cliente.ReceberMensagemAsync();
                if (mensagem == null)
                {
                    break; // Cliente desconectou
                }

                await ProcessarMensagemAsync(cliente, mensagem);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao gerenciar cliente {cliente.Id}: {ex.Message}");
        }
        finally
        {
            await DesconectarClienteAsync(cliente);
        }
    }

    /// <summary>
    /// Processa mensagens recebidas dos clientes
    /// </summary>
   private async Task ProcessarMensagemAsync(ClienteConectado cliente, MensagemBase mensagem)
{
    try
    {
        switch (mensagem.Tipo)
        {
            case TipoMensagem.ConectarJogador:
                var msgConectar = (MensagemConectarJogador)mensagem;
                ClienteConectado? jogadorExistente;
                lock (_lockClientes)
                {
                    // Procura por um jogador já conectado com o mesmo nome, que não seja este cliente
                    jogadorExistente = _clientes.Values.FirstOrDefault(c => c.Nome == msgConectar.NomeJogador && c.Id != cliente.Id);
                }

                if (jogadorExistente != null)
                {
                    Console.WriteLine($"Conexao do jogador '{msgConectar.NomeJogador}' rejeitada. Nome em uso por cliente {jogadorExistente.Id}");
                    // Envia erro para o cliente que tentou se conectar com nome duplicado
                    await cliente.EnviarMensagemAsync(new MensagemErroConexao
                    {
                        MensagemErro = "O nome de jogador ja esta em uso. Por favor, escolha outro."
                    });
                    await DesconectarClienteAsync(cliente);
                    return;
                }
                
                cliente.Nome = msgConectar.NomeJogador;

                // Confirmação de conexão para o cliente específico
                await cliente.EnviarMensagemAsync(new MensagemConfirmacaoConexao
                {
                    JogadorId = cliente.Id,
                    NomeJogador = cliente.Nome
                });
                
                await cliente.EnviarMensagemAsync(_estadoJogo.ObterEstadoJogo());
                // Adiciona nave ao jogo
                    _estadoJogo.AdicionarNave(cliente.Id);
                
                // Notifica todos os outros clientes
                await BroadcastMensagemAsync(new MensagemJogadorConectado
                {
                    JogadorId = cliente.Id,
                    NomeJogador = cliente.Nome
                });
                
                Console.WriteLine($"Jogador '{cliente.Nome}' (ID: {cliente.Id}) conectado e ID confirmado");
                break;
            
            case TipoMensagem.PausarJogo:
                var msgPause = (MensagemPausarJogo)mensagem;
                if (msgPause.Pausado)
                {
                    // Inicia pausa global para todos conectados
                    List<int> ids;
                    lock (_lockClientes)
                    {
                        ids = _clientes.Values.Where(c => c.Conectado).Select(c => c.Id).ToList();
                    }
                    _estadoJogo.IniciarPausaGlobal(ids);
                    await BroadcastMensagemAsync(new MensagemPausarJogo
                    {
                        Pausado = true,
                        PausadosRestantes = _estadoJogo.PausadosRestantes,
                        JogadorId = cliente.Id
                    });
                    Console.WriteLine($"Pausa global solicitada por '{cliente.Nome}' (ID: {cliente.Id}). Restantes: {_estadoJogo.PausadosRestantes}");
                }
                else
                {
                    // Jogador confirma retorno
                    _estadoJogo.ConfirmarRetorno(cliente.Id);
                    await BroadcastMensagemAsync(new MensagemPausarJogo
                    {
                        Pausado = _estadoJogo.SimulacaoPausada,
                        PausadosRestantes = _estadoJogo.PausadosRestantes,
                        JogadorId = cliente.Id
                    });
                    Console.WriteLine($"Retorno confirmado por '{cliente.Nome}' (ID: {cliente.Id}). Restantes: {_estadoJogo.PausadosRestantes}");
                }
                break;
            
            case TipoMensagem.MovimentoJogador:
                var msgMovimento = (MensagemMovimentoJogador)mensagem;
                _estadoJogo.AtualizarMovimentoNave(
                    msgMovimento.JogadorId,
                    msgMovimento.Esquerda,
                    msgMovimento.Direita,
                    msgMovimento.Cima,
                    msgMovimento.Baixo,
                    (float)(TickInterval / 1000.0) // deltaTime em segundos
                );
                break;

            case TipoMensagem.AtirarTiro:
                var msgTiro = (MensagemAtirarTiro)mensagem;
                _estadoJogo.AdicionarTiro(msgTiro.JogadorId);
                break;

            case TipoMensagem.Personalizacao:
                var msgPersonalizacao = (MensagemPersonalizacao)mensagem;
                var navePersonalizada = _estadoJogo.ObterNave(msgPersonalizacao.JogadorId);
                if (navePersonalizada != null)
                {
                    navePersonalizada.ModeloNave = msgPersonalizacao.ModeloNave;
                }
                break;
                    
            case TipoMensagem.ReiniciarJogo:
                int totalJogadores;
                int votosAtuais;
                bool deveReiniciar = false;
                MensagemBase? mensagemParaBroadcast = null;
                    
                lock (_lockClientes)
                {
                    var jogadoresConectados = _clientes.Values.Where(c => c.Conectado).Select(c => c.Id).ToHashSet();
                    _votosReinicio.IntersectWith(jogadoresConectados);
                        
                    totalJogadores = jogadoresConectados.Count;
                        
                    if (totalJogadores == 1)
                    {
                        deveReiniciar = true;
                        _votosReinicio.Clear();
                        
                        Console.WriteLine($"Jogador '{cliente.Nome}' (ID: {cliente.Id}) reiniciou o jogo (modo single player)");
                        mensagemParaBroadcast = new MensagemGameOver
                        {
                            Motivo = "Jogo foi reiniciado!",
                            PontuacaoFinal = new List<DadosNave>()
                        };
                    }
                    else
                    {
                        if (!_votosReinicio.Contains(cliente.Id))
                        {
                            _votosReinicio.Add(cliente.Id);
                            Console.WriteLine($"Jogador '{cliente.Nome}' (ID: {cliente.Id}) votou para reiniciar. Votos: {_votosReinicio.Count}/{totalJogadores}");
                        }
                        
                        votosAtuais = _votosReinicio.Count;
                            
                        if (votosAtuais >= totalJogadores && totalJogadores > 0)
                        {
                            deveReiniciar = true;
                            _votosReinicio.Clear();
                            
                            mensagemParaBroadcast = new MensagemGameOver
                            {
                                Motivo = $"Jogo reiniciado! Todos os {totalJogadores} jogadores concordaram.",
                                PontuacaoFinal = new List<DadosNave>()
                            };
                        }
                        else
                        {
                            mensagemParaBroadcast = new MensagemReiniciarJogo
                            {
                                VotosAtuais = votosAtuais,
                                VotosNecessarios = totalJogadores,
                                JogadorVotou = cliente.Id
                            };
                        }
                    }
                }
                    
                if (deveReiniciar)
                {
                    _estadoJogo.ReiniciarJogo();
                    Console.WriteLine($"Jogo reiniciado por consenso de {totalJogadores} jogadores");
                }
                    
                if (mensagemParaBroadcast != null)
                {
                    await BroadcastMensagemAsync(mensagemParaBroadcast);
                }
                break;

            case TipoMensagem.DesconectarJogador:
                cliente.Desconectar();
                break;

            case TipoMensagem.Heartbeat:
                await cliente.EnviarMensagemAsync(new MensagemHeartbeatResponse());
                break;

            case TipoMensagem.VoltarAoJogo:
                var msgVoltarJogo = (MensagemVoltarAoJogo)mensagem;
                Console.WriteLine($"Jogador '{cliente.Nome}' (ID: {cliente.Id}) voltou ao jogo (reutilizando conexão)");
                _estadoJogo.ReativarOuCriarNave(cliente.Id);
                    
                await cliente.EnviarMensagemAsync(new MensagemJogadorConectado
                {
                    JogadorId = cliente.Id,
                    NomeJogador = cliente.Nome
                });
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao processar mensagem do cliente {cliente.Id}: {ex.Message}");
    }
}
    /// <summary>
    /// Loop principal do jogo que atualiza o estado e envia para os clientes
    /// </summary>
    private async Task LoopPrincipalJogoAsync()
    {
        var ultimoTick = DateTime.UtcNow;
        bool gameOverEnviado = false; // Flag para evitar envio múltiplo de Game Over

        while (_rodando)
        {
            try
            {
                var agora = DateTime.UtcNow;
                var deltaTime = (agora - ultimoTick).TotalMilliseconds;

                if (deltaTime >= TickInterval)
                {
                    // Reinicia o jogo se não houver clientes e o jogo não estiver ativo
                    lock (_lockClientes)
                    {
                        if (_clientes.Count == 0 && !_estadoJogo.JogoAtivo)
                        {
                            _estadoJogo.ReiniciarJogo();
                            Console.WriteLine("Jogo reiniciado. Aguardando novos jogadores...");
                        }
                    }

                    // Atualiza o estado do jogo
                    _estadoJogo.AtualizarJogo((float)(deltaTime / 1000.0)); // deltaTime em segundos

                    // Envia o estado atualizado para todos os clientes
                    var estadoJogo = _estadoJogo.ObterEstadoJogo();
                    await BroadcastMensagemAsync(estadoJogo);

                    // Verifica se o jogo terminou e ainda não enviou Game Over
                    if (!_estadoJogo.JogoAtivo && !gameOverEnviado)
                    {
                        await BroadcastMensagemAsync(new MensagemGameOver
                        {
                            Motivo = "Todas as naves foram destruídas!",
                            PontuacaoFinal = estadoJogo.Naves
                        });

                        gameOverEnviado = true;
                        Console.WriteLine("Game Over enviado aos clientes");
                    }
                    
                    // Reset da flag quando o jogo está ativo novamente
                    if (_estadoJogo.JogoAtivo && gameOverEnviado)
                    {
                        gameOverEnviado = false;
                        Console.WriteLine("Jogo reativado, flag de Game Over resetada");
                    }

                    ultimoTick = agora;
                }

                // Pequena pausa para não sobrecarregar a CPU
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no loop principal do jogo: {ex.Message}");
            }
        }
    }
    /// <summary>
    /// Monitora clientes inativos e os desconecta
    /// </summary>
    private async Task MonitorarClientesAsync()
    {
        var timeoutInatividade = TimeSpan.FromSeconds(30);

        while (_rodando)
        {
            try
            {
                var clientesInativos = new List<ClienteConectado>();

                lock (_lockClientes)
                {
                    clientesInativos.AddRange(
                        _clientes.Values.Where(c => c.EstaInativo(timeoutInatividade))
                    );
                }

                foreach (var cliente in clientesInativos)
                {
                    Console.WriteLine($"Cliente {cliente.Id} inativo há muito tempo, desconectando...");
                    await DesconectarClienteAsync(cliente);
                }

                await Task.Delay(5000); // Verifica a cada 5 segundos
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao monitorar clientes: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Monitora entrada do usuário para comandos do servidor
    /// </summary>
    private async Task MonitorarEntradaUsuarioAsync()
    {
        await Task.Run(() =>
        {
            while (_rodando)
            {
                var tecla = Console.ReadKey(true);
                if (tecla.KeyChar == 'q' || tecla.KeyChar == 'Q')
                {
                    Console.WriteLine("\nParando servidor...");
                    _rodando = false;
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Envia uma mensagem para todos os clientes conectados
    /// </summary>
    private async Task BroadcastMensagemAsync(MensagemBase mensagem)
    {
        List<ClienteConectado> clientes;
        
        lock (_lockClientes)
        {
            clientes = _clientes.Values.Where(c => c.Conectado).ToList();
        }

        // Corrigido: Usa Task.WhenAll em vez de Parallel.ForEach para operações assíncronas
        // Isso evita deadlocks e garante que todas as operações sejam aguardadas corretamente
        var tarefasEnvio = clientes.Select(async cliente =>
        {
            try
            {
                await cliente.EnviarMensagemAsync(mensagem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar broadcast para cliente {cliente.Id}: {ex.Message}");
                // Remove cliente com falha de forma assíncrona
                _ = Task.Run(() => DesconectarClienteAsync(cliente));
            }
        });

        await Task.WhenAll(tarefasEnvio);
    }

    /// <summary>
    /// Desconecta um cliente específico
    /// </summary>
    private async Task DesconectarClienteAsync(ClienteConectado cliente)
    {
        try
        {
            lock (_lockClientes)
            {
                _clientes.Remove(cliente.Id);
                // Remove voto de reinício se existir
                _votosReinicio.Remove(cliente.Id);
            }

            _estadoJogo.RemoverNave(cliente.Id);
            cliente.Desconectar();

            // Se estava em pausa global, a desconexão conta como confirmação de retorno
            if (_estadoJogo.SimulacaoEstaPausada())
            {
                _estadoJogo.ConfirmarRetorno(cliente.Id);
                await BroadcastMensagemAsync(new MensagemPausarJogo
                {
                    Pausado = _estadoJogo.SimulacaoPausada,
                    PausadosRestantes = _estadoJogo.PausadosRestantes
                });
            }

            // Notifica outros clientes sobre a desconexão
            await BroadcastMensagemAsync(new MensagemJogadorDesconectado
            {
                JogadorId = cliente.Id
            });

            Console.WriteLine($"Cliente {cliente.Id} ({cliente.Nome}) desconectado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao desconectar cliente {cliente.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Para o servidor
    /// </summary>
    public void Parar()
    {
        try
        {
            _rodando = false;

            // Desconecta todos os clientes
            lock (_lockClientes)
            {
                foreach (var cliente in _clientes.Values)
                {
                    cliente.Desconectar();
                }
                _clientes.Clear();
            }

            _tcpListener?.Stop();
            Console.WriteLine("Servidor parado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao parar servidor: {ex.Message}");
        }
    }
}