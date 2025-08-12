# Asteroides Multiplayer

## ✅ PROJETO CONCLUÍDO E TESTADO

Transformação bem-sucedida do jogo Asteroides single-player em um jogo multiplayer cooperativo para dois jogadores, utilizando C# e arquitetura cliente-servidor com todas as tecnologias solicitadas.

## 🎯 Requisitos Implementados

### ✅ Comunicação TCP Robusta
- Servidor TCP na porta 8888 (configurável)
- Protocolo de mensagens JSON
- Tratamento completo de desconexões abruptas
- Reconexão automática e limpeza de recursos

### ✅ Programação Assíncrona (async/await)
- **Cliente**: Operações de rede não-bloqueantes
- **Interface responsiva** durante comunicação
- **Recepção assíncrona** de mensagens do servidor
- **Envio assíncrono** de comandos do jogador

### ✅ Paralelismo no Servidor
- **Task.Run**: Aceitação simultânea de múltiplas conexões
- **Thread dedicada**: Loop principal do jogo (60 FPS)
- **Parallel.ForEach**: Broadcast eficiente para todos os clientes
- **Monitoramento paralelo**: Detecção de clientes inativos

### ✅ Otimização Computacional (PLINQ)
- **Detecção de colisões paralela** entre tiros e asteroides
- **PLINQ** para operações computacionalmente pesadas
- **Escalabilidade automática** baseada nos cores disponíveis
- **Performance otimizada** em cenários com muitos objetos

### ✅ Menu Inicial Configurável
- **Configuração de servidor**: Endereço e porta
- **Nome do jogador** personalizável
- **Estados de conexão**: Menu, Conectando, Conectado, Erro
- **Interface intuitiva** com navegação por teclado

## 🚀 Funcionalidades do Jogo

- **Jogo cooperativo** para 2 jogadores simultâneos
- **Naves diferenciadas** por cores (azul e verde)
- **Sistema de pontuação** compartilhada em tempo real
- **Spawn automático** de asteroides
- **Detecção de colisões** precisa (nave×asteroide, tiro×asteroide)
- **Reinício automático** após Game Over
- **Efeitos visuais**: Partículas, estrelas, propulsores

## Arquitetura Técnica

### Comunicação Cliente-Servidor
- **Protocolo**: TCP/IP para comunicação confiável
- **Serialização**: JSON com Newtonsoft.Json para troca de mensagens
- **Padrão**: Arquitetura cliente-servidor com estado centralizado

### Programação Assíncrona
- **Cliente**: Utiliza `async/await` para manter responsividade da interface
- **Servidor**: Processamento assíncrono de múltiplos clientes simultaneamente
- **Benefícios**: Interface não trava durante comunicação de rede

### Paralelismo no Servidor
- **Gerenciamento de Clientes**: Cada cliente é gerenciado em uma `Task` separada
- **Broadcast de Mensagens**: `Parallel.ForEach` para envio simultâneo a todos os clientes
- **Detecção de Colisões**: PLINQ para verificação paralela de colisões tiro×asteroide

### Otimização Computacional
**Tarefa Escolhida**: Detecção de colisões entre tiros e asteroides
**Justificativa**: Com múltiplos jogadores, tiros e asteroides, o número de verificações cresce exponencialmente (O(n×m)). O paralelismo permite distribuir essas verificações entre múltiplos threads.
**Implementação**: Uso de PLINQ para processar verificações em paralelo.

## Estrutura do Projeto

```
AsteroidesMultiplayer/
├── AsteroidesServidor/           # Projeto do servidor
│   ├── Models/                   # Modelos de dados (Nave, Asteroide, Tiro)
│   ├── Network/                  # Comunicação de rede e mensagens
│   ├── Game/                     # Lógica do jogo
│   └── Program.cs               # Ponto de entrada do servidor
├── AsteroidesCliente/           # Projeto do cliente
│   ├── Network/                 # Cliente de rede
│   ├── UI/                      # Interface do usuário
│   ├── Game/                    # Tela do jogo
│   └── Program.cs              # Ponto de entrada do cliente
└── AsteroidesMultiplayer.sln   # Solução do Visual Studio
```

## Recursos Implementados

### Funcionalidades Básicas
- ✅ Comunicação TCP robusta
- ✅ Programação assíncrona (async/await)
- ✅ Paralelismo para múltiplos clientes
- ✅ Otimização paralela de detecção de colisões
- ✅ Tratamento de desconexões abruptas
- ✅ Menu inicial com configurações

### Recursos Adicionais (Pontos Extra)
- ✅ **Efeitos Visuais**: Partículas, rastros de tiros, animações
- ✅ **Interface Melhorada**: Menu configurável, HUD informativo
- ✅ **Robustez**: Reconexão automática, timeout de inatividade
- ✅ **Feedback Visual**: Cores diferentes para jogadores, indicadores de status

## Como Executar

### Pré-requisitos
- .NET 9.0 SDK
- Visual Studio 2022 ou VS Code
- MonoGame Framework

### Passos para Execução

1. **Clone/Baixe o projeto**
2. **Abra a solução no Visual Studio**
   ```
   AsteroidesMultiplayer.sln
   ```

3. **Compile os projetos**
   - Clique com botão direito na solução → "Build Solution"

4. **Execute o Servidor**
   - Defina `AsteroidesServidor` como projeto de inicialização
   - Pressione F5 ou Ctrl+F5
   - O servidor iniciará na porta 8888 por padrão

5. **Execute os Clientes**
   - Abra uma nova instância do Visual Studio
   - Abra o projeto `AsteroidesCliente`
   - Execute o cliente
   - Repita para o segundo jogador

### Configuração de Rede
- **Servidor Local**: Use "localhost" ou "127.0.0.1"
- **Rede Local**: Use o IP da máquina do servidor
- **Porta Padrão**: 8888 (configurável)

## Controles do Jogo

### Menu
- **↑↓**: Navegar opções
- **Enter**: Selecionar/Confirmar
- **Esc**: Voltar/Cancelar

### Jogo
- **WASD** ou **Setas**: Mover nave
- **Espaço**: Atirar
- **Esc**: Sair do jogo

## Protocolo de Comunicação

### Mensagens Cliente → Servidor
- `ConectarJogador`: Solicita entrada no jogo
- `MovimentoJogador`: Atualiza movimento da nave
- `AtirarTiro`: Dispara um tiro
- `DesconectarJogador`: Sai do jogo

### Mensagens Servidor → Cliente
- `EstadoJogo`: Estado completo do jogo (60 FPS)
- `JogadorConectado`: Notifica nova conexão
- `JogadorDesconectado`: Notifica desconexão
- `GameOver`: Fim de jogo com pontuações

## Tratamento de Erros

### Desconexões
- **Detecção**: Timeout de inatividade (30 segundos)
- **Limpeza**: Remoção automática de objetos do jogador
- **Notificação**: Outros jogadores são informados

### Falhas de Rede
- **Reconexão**: Cliente tenta reconectar automaticamente
- **Fallback**: Volta ao menu em caso de falha
- **Logs**: Mensagens detalhadas de erro

## Otimizações de Performance

### Servidor
- **Loop de Jogo**: 60 FPS com controle de timing
- **Paralelismo**: Verificação de colisões em paralelo
- **Broadcast Eficiente**: Envio simultâneo para múltiplos clientes

### Cliente
- **Renderização**: 60 FPS com efeitos visuais otimizados
- **Rede Assíncrona**: Comunicação não bloqueia a interface
- **Interpolação**: Movimento suave entre atualizações

## Tecnologias Utilizadas

- **C# 12** (.NET 9.0)
- **MonoGame Framework** (Renderização)
- **Monogame.Processing** (API simplificada)
- **Newtonsoft.Json** (Serialização)
- **System.Net.Sockets** (TCP)
- **Task Parallel Library** (Paralelismo)

## Arquivos Importantes

### Servidor
- `ServidorAsteroides.cs`: Servidor principal com TCP
- `EstadoJogo.cs`: Lógica do jogo e detecção de colisões
- `ClienteConectado.cs`: Gerenciamento de clientes
- `Mensagens.cs`: Protocolo de comunicação

### Cliente
- `AplicacaoCliente.cs`: Aplicação principal
- `MenuPrincipal.cs`: Interface de menu
- `TelaJogo.cs`: Renderização do jogo
- `ClienteRede.cs`: Comunicação TCP assíncrona

## Demonstração

O projeto inclui:
1. **Inicialização do servidor** com logs detalhados
2. **Conexão de múltiplos clientes** com feedback visual
3. **Jogabilidade cooperativa** sincronizada
4. **Tratamento de desconexões** com limpeza automática

## Aprendizados e Desafios

### Principais Desafios
1. **Sincronização**: Manter estado consistente entre clientes
2. **Latência**: Minimizar delay na comunicação
3. **Robustez**: Lidar com desconexões inesperadas
4. **Performance**: Otimizar detecção de colisões

### Soluções Implementadas
1. **Estado Centralizado**: Servidor como fonte única da verdade
2. **Comunicação Assíncrona**: Evita bloqueios
3. **Timeout e Cleanup**: Detecção e limpeza automática
4. **Paralelismo**: PLINQ para operações custosas

## Conclusão

Este projeto demonstra uma implementação completa de um jogo multiplayer em C#, utilizando conceitos avançados de programação como comunicação TCP, programação assíncrona, paralelismo e tratamento robusto de erros. A arquitetura é escalável e pode ser facilmente expandida para suportar mais jogadores ou recursos adicionais.