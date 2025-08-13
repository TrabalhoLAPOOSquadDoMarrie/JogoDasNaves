using AsteroidesServidor;

namespace AsteroidesServidor;

/// <summary>
/// Ponto de entrada do servidor Asteroides Multiplayer
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== SERVIDOR ASTEROIDES MULTIPLAYER ===");
        Console.WriteLine("Implementação com TCP, Async/Await e Paralelismo");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        try
        {
            int porta = 8890;
            if (args.Length > 0 && int.TryParse(args[0], out int portaArg))
            {
                porta = portaArg;
            }

            var servidor = new ServidorAsteroides(porta);
            await servidor.IniciarAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro fatal no servidor: {ex.Message}");
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}