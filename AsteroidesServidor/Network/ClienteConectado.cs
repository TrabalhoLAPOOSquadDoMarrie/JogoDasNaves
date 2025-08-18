using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using AsteroidesServidor.Network;

namespace AsteroidesServidor.Network;

/// <summary>
/// Representa um cliente conectado ao servidor
/// </summary>
public class ClienteConectado
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public TcpClient TcpClient { get; set; }
    public NetworkStream Stream { get; set; }
    public bool Conectado { get; set; } = true;
    public DateTime UltimaAtividade { get; set; } = DateTime.UtcNow;

    public ClienteConectado(int id, TcpClient tcpClient)
    {
        Id = id;
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
    }

    /// <summary>
    /// Envia uma mensagem para o cliente de forma assíncrona
    /// </summary>
    public async Task EnviarMensagemAsync(MensagemBase mensagem)
    {
        try
        {
            if (!Conectado || !TcpClient.Connected) return;

            string json = JsonConvert.SerializeObject(mensagem);
            
            byte[] dados = Encoding.UTF8.GetBytes(json);
            byte[] tamanho = BitConverter.GetBytes(dados.Length);
            
            // Envia o tamanho da mensagem primeiro, depois a mensagem
            await Stream.WriteAsync(tamanho, 0, 4);
            await Stream.WriteAsync(dados, 0, dados.Length);
            await Stream.FlushAsync();
            
            UltimaAtividade = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar mensagem para cliente {Id}: {ex.Message}");
            Desconectar();
        }
    }

    /// <summary>
    /// Recebe uma mensagem do cliente de forma assíncrona
    /// </summary>
    public async Task<MensagemBase?> ReceberMensagemAsync()
    {
        try
        {
            if (!Conectado || !TcpClient.Connected) return null;

            // Lê o tamanho da mensagem primeiro
            byte[] bufferTamanho = new byte[4];
            int bytesLidos = await Stream.ReadAsync(bufferTamanho, 0, 4);
            
            if (bytesLidos != 4) return null;
            
            int tamanhoMensagem = BitConverter.ToInt32(bufferTamanho, 0);
            
            // Lê a mensagem completa
            byte[] bufferMensagem = new byte[tamanhoMensagem];
            int totalLido = 0;
            
            while (totalLido < tamanhoMensagem)
            {
                int lido = await Stream.ReadAsync(bufferMensagem, totalLido, tamanhoMensagem - totalLido);
                if (lido == 0) return null;
                totalLido += lido;
            }
            
            string json = Encoding.UTF8.GetString(bufferMensagem);
            
            // Primeiro, deserializa apenas para obter o tipo
            var objetoTemp = JsonConvert.DeserializeObject<dynamic>(json);
            if (objetoTemp?.Tipo == null) return null;
            
            TipoMensagem tipo = (TipoMensagem)(int)objetoTemp.Tipo;
            
            // Agora deserializa para o tipo correto baseado no campo Tipo
            MensagemBase? mensagem = tipo switch
            {
                TipoMensagem.ConectarJogador => JsonConvert.DeserializeObject<MensagemConectarJogador>(json),
                TipoMensagem.MovimentoJogador => JsonConvert.DeserializeObject<MensagemMovimentoJogador>(json),
                TipoMensagem.AtirarTiro => JsonConvert.DeserializeObject<MensagemAtirarTiro>(json),
                TipoMensagem.PausarJogo      => JsonConvert.DeserializeObject<MensagemPausarJogo>(json),
                TipoMensagem.DesconectarJogador => JsonConvert.DeserializeObject<MensagemBase>(json),
                _ => null
            };
            
            UltimaAtividade = DateTime.UtcNow;
            return mensagem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao receber mensagem do cliente {Id}: {ex.Message}");
            Desconectar();
            return null;
        }
    }

    /// <summary>
    /// Desconecta o cliente
    /// </summary>
    public void Desconectar()
    {
        try
        {
            Conectado = false;
            Stream?.Close();
            TcpClient?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao desconectar cliente {Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica se o cliente está inativo há muito tempo
    /// </summary>
    public bool EstaInativo(TimeSpan timeout)
    {
        return DateTime.UtcNow - UltimaAtividade > timeout;
    }
}