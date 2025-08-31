using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using AsteroidesCliente.Network;

namespace AsteroidesCliente.Network;

/// <summary>
/// Cliente de rede para comunicacao com o servidor
    /// Implementa comunicacao TCP assincrona
/// </summary>
public class ClienteRede
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private bool _conectado = false;
    private readonly object _lock = new();
    private DateTime _ultimoHeartbeat = DateTime.UtcNow;
    private int _jogadorId = 0;

    public bool Conectado 
    { 
        get 
        { 
            lock (_lock) 
            { 
                return _conectado && _tcpClient?.Connected == true; 
            } 
        } 
    }

    public int JogadorId => _jogadorId;

    public event Action<MensagemBase>? MensagemRecebida;
    public event Action? Desconectado;

    /// <summary>
    /// Conecta ao servidor de forma assincrona
    /// </summary>
    public async Task<bool> ConectarAsync(string endereco, int porta)
    {
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(endereco, porta);
            _stream = _tcpClient.GetStream();
            
            lock (_lock)
            {
                _conectado = true;
            }

            // Inicia a tarefa de recepção de mensagens
            _ = Task.Run(ReceberMensagensAsync);
            
            // Inicia sistema de heartbeat
            _ = Task.Run(HeartbeatLoopAsync);

            Console.WriteLine($"Conectado ao servidor {endereco}:{porta}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao conectar: {ex.Message}");
            return false;
        }
    }
    
    

    /// <summary>
    /// Envia uma mensagem para o servidor de forma assincrona
    /// </summary>
    public async Task<bool> EnviarMensagemAsync(MensagemBase mensagem)
    {
        try
        {
            if (!Conectado || _stream == null) return false;

            // Configura timeout para evitar travamentos
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            string json = JsonConvert.SerializeObject(mensagem);

            byte[] dados = Encoding.UTF8.GetBytes(json);
            byte[] tamanho = BitConverter.GetBytes(dados.Length);

            // Envia o tamanho da mensagem primeiro, depois a mensagem
            await _stream.WriteAsync(tamanho, 0, 4, cts.Token);
            await _stream.WriteAsync(dados, 0, dados.Length, cts.Token);
            await _stream.FlushAsync(cts.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Timeout ao enviar mensagem para o servidor");
            Desconectar();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar mensagem: {ex.Message}");
            Desconectar();
            return false;
        }
    }

    /// <summary>
    /// Recebe mensagens do servidor de forma assincrona
    /// </summary>
    private async Task ReceberMensagensAsync()
    {
        try
        {
            while (Conectado && _stream != null)
            {
                // Configura timeout para evitar travamentos
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                // Lê o tamanho da mensagem primeiro
                byte[] bufferTamanho = new byte[4];
                int bytesLidos = await _stream.ReadAsync(bufferTamanho, 0, 4, cts.Token);
                
                if (bytesLidos != 4) break;
                
                int tamanhoMensagem = BitConverter.ToInt32(bufferTamanho, 0);
                
                // Validação de segurança
                if (tamanhoMensagem > 1024 * 1024) // 1MB máximo
                {
                    Console.WriteLine($"Mensagem muito grande ({tamanhoMensagem} bytes), desconectando");
                    break;
                }
                
                // Lê a mensagem completa
                byte[] bufferMensagem = new byte[tamanhoMensagem];
                int totalLido = 0;
                
                while (totalLido < tamanhoMensagem)
                {
                    int lido = await _stream.ReadAsync(bufferMensagem, totalLido, tamanhoMensagem - totalLido, cts.Token);
                    if (lido == 0) break;
                    totalLido += lido;
                }
                
                if (totalLido != tamanhoMensagem) break;
                
                string json = Encoding.UTF8.GetString(bufferMensagem);
                
                // Primeiro, deserializa apenas para obter o tipo
                var objetoTemp = JsonConvert.DeserializeObject<dynamic>(json);
                if (objetoTemp?.Tipo == null) continue;
                
                TipoMensagem tipo = (TipoMensagem)(int)objetoTemp.Tipo;
                
                // Agora deserializa para o tipo correto baseado no campo Tipo
                MensagemBase? mensagem = tipo switch
                {
                    TipoMensagem.EstadoJogo => JsonConvert.DeserializeObject<MensagemEstadoJogo>(json),
                    TipoMensagem.JogadorConectado => JsonConvert.DeserializeObject<MensagemJogadorConectado>(json),
                    TipoMensagem.JogadorDesconectado => JsonConvert.DeserializeObject<MensagemJogadorDesconectado>(json),
                    TipoMensagem.GameOver => JsonConvert.DeserializeObject<MensagemGameOver>(json),
                    TipoMensagem.PausarJogo => JsonConvert.DeserializeObject<MensagemPausarJogo>(json),
                    TipoMensagem.ReiniciarJogo => JsonConvert.DeserializeObject<MensagemReiniciarJogo>(json),
                    TipoMensagem.HeartbeatResponse => JsonConvert.DeserializeObject<MensagemHeartbeatResponse>(json),
                    TipoMensagem.ConfirmacaoConexao => JsonConvert.DeserializeObject<MensagemConfirmacaoConexao>(json),
                    _ => null
                };
                
                if (mensagem != null)
                {
                    // Atualiza timestamp do heartbeat se for resposta
                    if (tipo == TipoMensagem.HeartbeatResponse)
                    {
                        _ultimoHeartbeat = DateTime.UtcNow;
                    }
                    
                    // Define ID do jogador imediatamente ao receber confirmação
                    if (tipo == TipoMensagem.ConfirmacaoConexao && mensagem is MensagemConfirmacaoConexao confirmacao)
                    {
                        _jogadorId = confirmacao.JogadorId;
                        Console.WriteLine($"ID do jogador recebido e definido: {_jogadorId}");
                    }
                    
                    MensagemRecebida?.Invoke(mensagem);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Timeout ao receber mensagens do servidor");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao receber mensagens: {ex.Message}");
        }
        finally
        {
            Desconectar();
        }
    }

    /// <summary>
    /// Loop de heartbeat para manter a conexão viva e detectar desconexões
    /// </summary>
    private async Task HeartbeatLoopAsync()
    {
        try
        {
            while (Conectado)
            {
                await Task.Delay(5000); // Envia heartbeat a cada 5 segundos
                
                if (!Conectado) break;

                // Verifica se não recebemos resposta há muito tempo
                if (DateTime.UtcNow - _ultimoHeartbeat > TimeSpan.FromSeconds(15))
                {
                    Console.WriteLine("Servidor não respondeu ao heartbeat, desconectando...");
                    Desconectar();
                    break;
                }

                // Envia heartbeat
                var heartbeat = new MensagemHeartbeat { JogadorId = _jogadorId };
                if (!await EnviarMensagemAsync(heartbeat))
                {
                    Console.WriteLine("Falha ao enviar heartbeat");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no loop de heartbeat: {ex.Message}");
        }
    }

    /// <summary>
    /// Define o ID do jogador para heartbeat
    /// </summary>
    public void DefinirJogadorId(int jogadorId)
    {
        _jogadorId = jogadorId;
    }

    /// <summary>
    /// Desconecta do servidor
    /// </summary>
    public void Desconectar()
    {
        try
        {
            lock (_lock)
            {
                _conectado = false;
            }

            _stream?.Close();
            _tcpClient?.Close();
            
            Desconectado?.Invoke();
            Console.WriteLine("Desconectado do servidor");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao desconectar: {ex.Message}");
        }
    }
}