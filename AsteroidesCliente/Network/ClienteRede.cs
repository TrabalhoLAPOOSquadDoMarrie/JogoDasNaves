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

            string json = JsonConvert.SerializeObject(mensagem);

            byte[] dados = Encoding.UTF8.GetBytes(json);
            byte[] tamanho = BitConverter.GetBytes(dados.Length);

            // Envia o tamanho da mensagem primeiro, depois a mensagem
            await _stream.WriteAsync(tamanho, 0, 4);
            await _stream.WriteAsync(dados, 0, dados.Length);
            await _stream.FlushAsync();

            return true;
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
                // Lê o tamanho da mensagem primeiro
                byte[] bufferTamanho = new byte[4];
                int bytesLidos = await _stream.ReadAsync(bufferTamanho, 0, 4);
                
                if (bytesLidos != 4) break;
                
                int tamanhoMensagem = BitConverter.ToInt32(bufferTamanho, 0);
                
                // Lê a mensagem completa
                byte[] bufferMensagem = new byte[tamanhoMensagem];
                int totalLido = 0;
                
                while (totalLido < tamanhoMensagem)
                {
                    int lido = await _stream.ReadAsync(bufferMensagem, totalLido, tamanhoMensagem - totalLido);
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
                    _ => null
                };
                
                if (mensagem != null)
                {
                    MensagemRecebida?.Invoke(mensagem);
                }
            }
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