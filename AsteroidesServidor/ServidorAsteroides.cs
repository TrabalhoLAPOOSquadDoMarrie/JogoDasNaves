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
    
    private bool _rodando = false;
    private int _proximoIdJogador = 1;
    private const int PortaPadrao = 8890;
    private const int TicksPerSecond = 60; // 60 FPS
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
                var cliente = new ClienteConectado(_proximoIdJogador++, tcpClient);
                
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
                    cliente.Nome = msgConectar.NomeJogador;
                    _estadoJogo.AdicionarNave(cliente.Id);
                    
                    // Notifica todos os clientes sobre o novo jogador
                    await BroadcastMensagemAsync(new MensagemJogadorConectado
                    {
                        JogadorId = cliente.Id,
                        NomeJogador = cliente.Nome
                    });
                    
                    Console.WriteLine($"Jogador '{cliente.Nome}' (ID: {cliente.Id}) entrou no jogo");
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
                            JogadorId = cliente.Id // NOVO
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
                            JogadorId = cliente.Id // NOVO
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
                        msgMovimento.Baixo
                    );
                    break;

                case TipoMensagem.AtirarTiro:
                    var msgTiro = (MensagemAtirarTiro)mensagem;
                    _estadoJogo.AdicionarTiro(msgTiro.JogadorId);
                    break;

                case TipoMensagem.ReiniciarJogo:
                    Console.WriteLine($"Jogador '{cliente.Nome}' (ID: {cliente.Id}) solicitou reinício do jogo");
                    _estadoJogo.ReiniciarJogo();
                    
                    // Envia confirmação de reinício para todos os clientes
                    await BroadcastMensagemAsync(new MensagemGameOver
                    {
                        Motivo = "Jogo foi reiniciado!",
                        PontuacaoFinal = new List<DadosNave>()
                    });
                    
                    Console.WriteLine("Jogo reiniciado e confirmação enviada aos clientes");
                    break;

                case TipoMensagem.DesconectarJogador:
                    cliente.Desconectar();
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
                    // Atualiza o estado do jogo
                    _estadoJogo.AtualizarJogo();

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

        // Usa Parallel.ForEach para enviar mensagens em paralelo
        // Justificativa: Enviar mensagens para múltiplos clientes pode ser lento
        // se feito sequencialmente. O paralelismo permite enviar para todos
        // os clientes simultaneamente, reduzindo a latência percebida.
        await Task.Run(() =>
        {
            Parallel.ForEach(clientes, async cliente =>
            {
                try
                {
                    await cliente.EnviarMensagemAsync(mensagem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao enviar broadcast para cliente {cliente.Id}: {ex.Message}");
                }
            });
        });
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