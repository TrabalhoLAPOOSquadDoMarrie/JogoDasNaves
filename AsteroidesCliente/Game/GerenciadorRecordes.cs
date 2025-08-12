using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AsteroidesCliente.Game;

/// <summary>
/// Gerencia os recordes de pontuacao do jogador
/// </summary>
public class GerenciadorRecordes
{
    private const string ARQUIVO_RECORDES = "recordes.json";
    private const int MAX_RECORDES = 10;
    
    private List<RecordeJogador> _recordes;
    
    public GerenciadorRecordes()
    {
        _recordes = new List<RecordeJogador>();
        CarregarRecordes();
    }
    
    /// <summary>
    /// Adiciona um novo recorde se a pontuacao for suficiente
    /// </summary>
    /// <param name="nomeJogador">Nome do jogador</param>
    /// <param name="pontuacao">Pontuacao obtida</param>
    /// <param name="dificuldade">Nivel de dificuldade</param>
    /// <returns>True se foi um novo recorde</returns>
    public bool AdicionarRecorde(string nomeJogador, int pontuacao, NivelDificuldade dificuldade)
    {
        var novoRecorde = new RecordeJogador
        {
            Nome = nomeJogador,
            Pontuacao = pontuacao,
            Dificuldade = dificuldade,
            Data = DateTime.Now
        };
        
        _recordes.Add(novoRecorde);
        _recordes = _recordes.OrderByDescending(r => r.Pontuacao).Take(MAX_RECORDES).ToList();
        
        SalvarRecordes();
        
        // Verifica se está entre os top recordes
        return _recordes.Contains(novoRecorde);
    }
    
    /// <summary>
    /// Obtem a lista de recordes ordenada por pontuacao
    /// </summary>
    public List<RecordeJogador> ObterRecordes()
    {
        return _recordes.OrderByDescending(r => r.Pontuacao).ToList();
    }
    
    /// <summary>
    /// Obtem o melhor recorde geral
    /// </summary>
    public RecordeJogador? ObterMelhorRecorde()
    {
        return _recordes.OrderByDescending(r => r.Pontuacao).FirstOrDefault();
    }
    
    /// <summary>
    /// Obtem o melhor recorde para uma dificuldade especifica
    /// </summary>
    public RecordeJogador? ObterMelhorRecorde(NivelDificuldade dificuldade)
    {
        return _recordes.Where(r => r.Dificuldade == dificuldade)
                       .OrderByDescending(r => r.Pontuacao)
                       .FirstOrDefault();
    }
    
    /// <summary>
    /// Verifica se uma pontuacao seria um novo recorde
    /// </summary>
    public bool EhNovoRecorde(int pontuacao)
    {
        return _recordes.Count < MAX_RECORDES || pontuacao > _recordes.Min(r => r.Pontuacao);
    }
    
    private void CarregarRecordes()
    {
        try
        {
            if (File.Exists(ARQUIVO_RECORDES))
            {
                string json = File.ReadAllText(ARQUIVO_RECORDES);
                _recordes = JsonConvert.DeserializeObject<List<RecordeJogador>>(json) ?? new List<RecordeJogador>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar recordes: {ex.Message}");
            _recordes = new List<RecordeJogador>();
        }
    }
    
    private void SalvarRecordes()
    {
        try
        {
            string json = JsonConvert.SerializeObject(_recordes, Formatting.Indented);
            File.WriteAllText(ARQUIVO_RECORDES, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar recordes: {ex.Message}");
        }
    }
}

/// <summary>
/// Representa um recorde de pontuacao
/// </summary>
public class RecordeJogador
{
    public string Nome { get; set; } = "";
    public int Pontuacao { get; set; }
    public NivelDificuldade Dificuldade { get; set; }
    public DateTime Data { get; set; }
    
    public string ObterTextoRecorde()
    {
        return $"{Nome}: {Pontuacao} pts ({Dificuldade}) - {Data:dd/MM/yyyy}";
    }
}