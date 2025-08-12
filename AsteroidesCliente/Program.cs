using AsteroidesCliente;

namespace AsteroidesCliente;

/// <summary>
/// Ponto de entrada do cliente Asteroides Multiplayer
/// </summary>
class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("=== CLIENTE ASTEROIDES MULTIPLAYER ===");
        Console.WriteLine("Jogo Cooperativo com Comunicacao TCP Assincrona");
        Console.WriteLine("=======================================");
        Console.WriteLine();

        try
        {
            using var cliente = new AplicacaoCliente();
            cliente.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro fatal no cliente: {ex.Message}");
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}