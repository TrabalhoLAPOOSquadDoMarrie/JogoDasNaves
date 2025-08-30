using System;

namespace AsteroidesCliente.Game
{
    /// <summary>
    /// Classe responsável por gerenciar a dificuldade do jogo
    /// </summary>
    public class GerenciadorDificuldade
    {
        private NivelDificuldade _nivelAtual;
        private int _tempoJogo; // Em frames (144 fps)
        private float _velocidadeBase;
        private float _velocidadeAtual;
        
        // Constantes para os diferentes níveis
        private const float VELOCIDADE_FACIL = 2.0f;
        private const float VELOCIDADE_MEDIA = 3.5f;
        private const float VELOCIDADE_DIFICIL = 5.0f;
        
        // Tempos de transição (em segundos) - Aumentados significativamente para o jogo durar mais
        private const int TEMPO_TRANSICAO_MEDIO = 120; // 2 minutos para ir de fácil para médio
        private const int TEMPO_TRANSICAO_DIFICIL_1 = 180; // 3 minutos para ir de fácil para médio (modo difícil)
        private const int TEMPO_TRANSICAO_DIFICIL_2 = 360; // 6 minutos para ir de médio para difícil (modo difícil)
        
        // Controle de spawning de asteroides
        private const int ASTEROIDES_BASE = 1; // Número base de asteroides por spawn
        private const int ASTEROIDES_MAXIMO = 6; // Número máximo de asteroides por spawn (aumentado)
        
        public NivelDificuldade NivelAtual => _nivelAtual;
        public float VelocidadeAtual => _velocidadeAtual;
        public int TempoJogoSegundos => _tempoJogo / 144;
        
        public GerenciadorDificuldade(NivelDificuldade nivel = NivelDificuldade.Medio)
        {
            _nivelAtual = nivel;
            _tempoJogo = 0;
            _velocidadeBase = VELOCIDADE_FACIL;
            _velocidadeAtual = _velocidadeBase;
        }
        
        /// <summary>
        /// Atualiza a dificuldade baseada no tempo de jogo
        /// </summary>
        public void Atualizar()
        {
            _tempoJogo++;
            int segundos = _tempoJogo / 144;
            
            switch (_nivelAtual)
            {
                case NivelDificuldade.Facil:
                    // Velocidade constante
                    _velocidadeAtual = VELOCIDADE_FACIL;
                    break;
                    
                case NivelDificuldade.Medio:
                    // Começa devagar e acelera até velocidade média
                    if (segundos < TEMPO_TRANSICAO_MEDIO)
                    {
                        float progresso = (float)segundos / TEMPO_TRANSICAO_MEDIO;
                        _velocidadeAtual = VELOCIDADE_FACIL + (VELOCIDADE_MEDIA - VELOCIDADE_FACIL) * progresso;
                    }
                    else
                    {
                        _velocidadeAtual = VELOCIDADE_MEDIA;
                    }
                    break;
                    
                case NivelDificuldade.Dificil:
                    // Três fases: fácil -> médio -> difícil
                    if (segundos < TEMPO_TRANSICAO_DIFICIL_1)
                    {
                        // Fase 1: Fácil para médio
                        float progresso = (float)segundos / TEMPO_TRANSICAO_DIFICIL_1;
                        _velocidadeAtual = VELOCIDADE_FACIL + (VELOCIDADE_MEDIA - VELOCIDADE_FACIL) * progresso;
                    }
                    else if (segundos < TEMPO_TRANSICAO_DIFICIL_2)
                    {
                        // Fase 2: Médio para difícil
                        float progresso = (float)(segundos - TEMPO_TRANSICAO_DIFICIL_1) / (TEMPO_TRANSICAO_DIFICIL_2 - TEMPO_TRANSICAO_DIFICIL_1);
                        _velocidadeAtual = VELOCIDADE_MEDIA + (VELOCIDADE_DIFICIL - VELOCIDADE_MEDIA) * progresso;
                    }
                    else
                    {
                        // Fase 3: Difícil constante
                        _velocidadeAtual = VELOCIDADE_DIFICIL;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Altera o nível de dificuldade
        /// </summary>
        public void AlterarNivel(NivelDificuldade novoNivel)
        {
            _nivelAtual = novoNivel;
            _tempoJogo = 0; // Reinicia o tempo
        }
        
        /// <summary>
        /// Obtém o nome do nível atual
        /// </summary>
        public string ObterNomeNivel()
        {
            return _nivelAtual switch
        {
            NivelDificuldade.Facil => "Facil",
            NivelDificuldade.Medio => "Medio",
            NivelDificuldade.Dificil => "Dificil",
            _ => "Desconhecido"
        };
        }
        
        /// <summary>
        /// Obtém informações detalhadas sobre o estado atual da dificuldade
        /// </summary>
        public string ObterInfoDetalhada()
        {
            int segundos = _tempoJogo / 144;
            string fase = "";
            
            switch (_nivelAtual)
            {
                case NivelDificuldade.Facil:
                    fase = "Constante";
                    break;
                    
                case NivelDificuldade.Medio:
                    fase = segundos < TEMPO_TRANSICAO_MEDIO ? "Acelerando" : "Estavel";
                    break;
                    
                case NivelDificuldade.Dificil:
                    if (segundos < TEMPO_TRANSICAO_DIFICIL_1)
                        fase = "Fase 1";
                    else if (segundos < TEMPO_TRANSICAO_DIFICIL_2)
                        fase = "Fase 2";
                    else
                        fase = "Fase 3";
                    break;
            }
            
            return $"{ObterNomeNivel()} ({fase})";
        }
        
        /// <summary>
        /// Calcula quantos asteroides devem ser spawnados baseado no tempo e dificuldade
        /// </summary>
        public int CalcularQuantidadeAsteroides()
        {
            int segundos = _tempoJogo / 144;
            
            switch (_nivelAtual)
            {
                case NivelDificuldade.Facil:
                    // Aumenta muito gradualmente de 1 para 3 asteroides
                    if (segundos < 60) return 1;
                    if (segundos < 180) return 2;
                    return 3;
                    
                case NivelDificuldade.Medio:
                    // Aumenta gradualmente de 1 para 4 asteroides
                    if (segundos < 60) return 1;
                    if (segundos < 120) return 2;
                    if (segundos < 240) return 3;
                    return 4;
                    
                case NivelDificuldade.Dificil:
                    // Aumenta gradualmente de 1 para 6 asteroides
                    if (segundos < 60) return 1;
                    if (segundos < 120) return 2;
                    if (segundos < 180) return 3;
                    if (segundos < 300) return 4;
                    if (segundos < 420) return 5;
                    return ASTEROIDES_MAXIMO;
                    
                default:
                    return ASTEROIDES_BASE;
            }
        }
        
        /// <summary>
        /// Calcula o intervalo entre spawns de asteroides (em frames)
        /// </summary>
        public int CalcularIntervaloSpawn()
        {
            int segundos = _tempoJogo / 144;
            
            switch (_nivelAtual)
            {
                case NivelDificuldade.Facil:
                    // Começa com 120 frames (2s) e diminui gradualmente até 80 frames (1.33s)
                    return Math.Max(120 - (segundos / 15), 80);
                    
                case NivelDificuldade.Medio:
                    // Começa com 100 frames (1.67s) e diminui gradualmente até 60 frames (1s)
                    return Math.Max(100 - (segundos / 12), 60);
                    
                case NivelDificuldade.Dificil:
                    // Começa com 80 frames (1.33s) e diminui gradualmente até 40 frames (0.67s)
                    return Math.Max(80 - (segundos / 10), 40);
                    
                default:
                    return 90;
            }
        }

        /// <summary>
        /// Reinicia o gerenciador de dificuldade
        /// </summary>
        public void Reiniciar()
        {
            _tempoJogo = 0;
            _velocidadeAtual = _velocidadeBase;
        }
    }
}