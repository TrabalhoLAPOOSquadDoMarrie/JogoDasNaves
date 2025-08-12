# Instruções de Execução - Asteroides Multiplayer

## Como Executar o Jogo

### 1. Compilar os Projetos
```bash
# Na pasta raiz do projeto
dotnet build AsteroidesMultiplayer.sln
```

### 2. Iniciar o Servidor
```bash
# Em um terminal
dotnet run --project AsteroidesServidor
```

O servidor iniciará na porta 8888 e mostrará:
```
=== SERVIDOR ASTEROIDES MULTIPLAYER ===
Implementação com TCP, Async/Await e Paralelismo
Servidor Asteroides inicializado na porta 8888
Servidor iniciado! Aguardando conexões...
Pressione 'q' para parar o servidor
```

### 3. Iniciar o(s) Cliente(s)
```bash
# Em outro(s) terminal(is)
dotnet run --project AsteroidesCliente
```

### 4. Configurar e Jogar

1. **Menu Principal**: Use ↑↓ para navegar, ENTER para selecionar
2. **Configurações**: Configure servidor (localhost:8888), nome do jogador
3. **Iniciar Jogo**: Conecta ao servidor e inicia o jogo
4. **Controles do Jogo**:
   - **WASD** ou **Setas**: Mover a nave
   - **ESPAÇO**: Atirar
   - **ESC**: Sair do jogo

## Recursos Implementados

### ✅ Arquitetura Cliente-Servidor
- Comunicação TCP robusta
- Serialização JSON para mensagens
- Tratamento de desconexões abruptas

### ✅ Programação Assíncrona
- `async/await` no cliente para operações de rede
- Interface não-bloqueante durante comunicação

### ✅ Paralelismo no Servidor
- `Task.Run` para aceitar múltiplas conexões
- `Parallel.ForEach` para broadcast de mensagens
- Loop de jogo em thread separada

### ✅ Otimização Computacional
- **PLINQ** para detecção de colisões entre tiros e asteroides
- Processamento paralelo de verificações de colisão

### ✅ Funcionalidades do Jogo
- Jogo cooperativo para 2 jogadores
- Naves com cores diferentes para cada jogador
- Sistema de pontuação compartilhada
- Spawn automático de asteroides
- Efeitos visuais (partículas, estrelas)
- Reinício automático após Game Over

## Demonstração das Tecnologias

### TCP e Async/Await
- Conexões TCP estáveis entre cliente e servidor
- Operações assíncronas para envio/recebimento de mensagens
- Interface responsiva durante operações de rede

### Paralelismo
- **Servidor**: Múltiplas threads para conexões e loop de jogo
- **Otimização**: PLINQ para detecção de colisões (computacionalmente pesada)

### Tratamento de Erros
- Reconexão automática em caso de falha
- Mensagens de erro informativas
- Limpeza adequada de recursos

## Arquivos Importantes

### Servidor
- `ServidorAsteroides.cs`: Lógica principal do servidor TCP
- `EstadoJogo.cs`: Gerenciamento do estado do jogo com PLINQ
- `ClienteConectado.cs`: Gerenciamento de clientes conectados

### Cliente
- `AplicacaoCliente.cs`: Aplicação principal MonoGame
- `ClienteRede.cs`: Comunicação TCP assíncrona
- `TelaJogo.cs`: Interface do jogo com efeitos visuais

### Comunicação
- `Mensagens.cs`: Protocolo de comunicação JSON

## Testando o Sistema

1. **Teste Básico**: 1 servidor + 1 cliente
2. **Teste Multiplayer**: 1 servidor + 2 clientes
3. **Teste de Desconexão**: Fechar cliente abruptamente
4. **Teste de Performance**: Observar uso de CPU durante colisões

## Pontos Extras Implementados

- ✅ **Efeitos Visuais**: Partículas, estrelas, cores diferenciadas
- ✅ **Interface Polida**: Menu configurável, telas de status
- ✅ **Otimização PLINQ**: Detecção paralela de colisões