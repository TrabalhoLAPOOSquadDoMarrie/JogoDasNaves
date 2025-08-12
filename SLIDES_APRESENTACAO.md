# Slides de Apresentação - Asteroides Multiplayer

## Slide 1: Título
**ASTEROIDES MULTIPLAYER**
*Transformação de Jogo Single-Player em Multiplayer Cooperativo*

**Tecnologias:**
- C# .NET 9.0
- Arquitetura Cliente-Servidor TCP
- Programação Assíncrona (async/await)
- Paralelismo (Task, Parallel.ForEach, PLINQ)
- MonoGame Framework

---

## Slide 2: Arquitetura do Sistema

```
┌─────────────────┐    TCP/IP    ┌─────────────────┐
│   CLIENTE 1     │◄────────────►│                 │
│  (MonoGame)     │              │    SERVIDOR     │
└─────────────────┘              │   (Console)     │
                                 │                 │
┌─────────────────┐              │                 │
│   CLIENTE 2     │◄────────────►│                 │
│  (MonoGame)     │              │                 │
└─────────────────┘              └─────────────────┘
```

**Comunicação:**
- Protocolo TCP para confiabilidade
- Serialização JSON para mensagens
- Porta 8888 (configurável)

---

## Slide 3: Programação Assíncrona no Cliente

**Implementação async/await:**

```csharp
// ClienteRede.cs
public async Task<bool> ConectarAsync(string endereco, int porta)
{
    _tcpClient = new TcpClient();
    await _tcpClient.ConnectAsync(endereco, porta);
    _stream = _tcpClient.GetStream();
    
    // Inicia recepção assíncrona
    _ = Task.Run(ReceberMensagensAsync);
    return true;
}

public async Task EnviarMensagemAsync(MensagemBase mensagem)
{
    var json = JsonConvert.SerializeObject(mensagem);
    var dados = Encoding.UTF8.GetBytes(json + "\n");
    await _stream.WriteAsync(dados, 0, dados.Length);
}
```

**Benefícios:**
- Interface não-bloqueante
- Operações de rede assíncronas
- Melhor responsividade

---

## Slide 4: Paralelismo no Servidor

**1. Aceitação de Conexões:**
```csharp
// Task para aceitar múltiplas conexões
_ = Task.Run(async () => {
    while (_executando) {
        var cliente = await _listener.AcceptTcpClientAsync();
        _ = Task.Run(() => ProcessarCliente(cliente));
    }
});
```

**2. Loop Principal do Jogo:**
```csharp
// Thread dedicada para o jogo (60 FPS)
_ = Task.Run(async () => {
    while (_executando) {
        _estadoJogo.Atualizar();
        await BroadcastEstadoJogo();
        await Task.Delay(16); // ~60 FPS
    }
});
```

**3. Broadcast Paralelo:**
```csharp
Parallel.ForEach(_clientes.Values, cliente => {
    _ = cliente.EnviarMensagemAsync(mensagem);
});
```

---

## Slide 5: Otimização com PLINQ

**Problema Computacionalmente Pesado:**
- Detecção de colisões entre tiros e asteroides
- Operação O(n×m) - custosa com muitos objetos

**Solução com PLINQ:**
```csharp
// EstadoJogo.cs - Verificação paralela de colisões
public void VerificarColisoes()
{
    // Usa PLINQ para paralelizar verificações
    var colisoesDetectadas = Tiros.AsParallel()
        .SelectMany(tiro => Asteroides.AsParallel()
            .Where(asteroide => asteroide.Colide(tiro))
            .Select(asteroide => new { Tiro = tiro, Asteroide = asteroide }))
        .ToList();

    // Processa colisões detectadas
    foreach (var colisao in colisoesDetectadas)
    {
        ProcessarColisao(colisao.Tiro, colisao.Asteroide);
    }
}
```

**Benefícios:**
- Utiliza múltiplos cores do processador
- Reduz latência em cenários com muitos objetos
- Escalabilidade automática

---

## Slide 6: Protocolo de Comunicação

**Tipos de Mensagem:**
```csharp
public enum TipoMensagem
{
    ConectarJogador,      // Cliente → Servidor
    MovimentoJogador,     // Cliente → Servidor  
    AtirarTiro,          // Cliente → Servidor
    EstadoJogo,          // Servidor → Cliente
    JogadorConectado,    // Servidor → Cliente
    JogadorDesconectado, // Servidor → Cliente
    GameOver             // Servidor → Cliente
}
```

**Exemplo de Mensagem:**
```json
{
    "Tipo": "MovimentoJogador",
    "JogadorId": 1,
    "Esquerda": true,
    "Direita": false,
    "Cima": false,
    "Baixo": false
}
```

---

## Slide 7: Tratamento de Desconexões

**Detecção de Desconexão:**
```csharp
// Monitoramento de clientes inativos
_ = Task.Run(async () => {
    while (_executando) {
        var clientesInativos = _clientes.Values
            .Where(c => DateTime.Now - c.UltimaAtividade > TimeSpan.FromSeconds(30))
            .ToList();
            
        foreach (var cliente in clientesInativos) {
            await RemoverCliente(cliente.Id);
        }
        await Task.Delay(5000);
    }
});
```

**Limpeza de Recursos:**
- Remoção automática de clientes desconectados
- Notificação para outros jogadores
- Continuidade do jogo com jogadores restantes

---

## Slide 8: Recursos Implementados

**✅ Requisitos Obrigatórios:**
- Comunicação TCP robusta
- Programação assíncrona (async/await)
- Paralelismo no servidor (Task, Parallel.ForEach)
- Otimização computacional (PLINQ)
- Tratamento de desconexões
- Menu inicial configurável

**✅ Pontos Extras:**
- Efeitos visuais (partículas, estrelas)
- Interface polida com múltiplas telas
- Sistema de cores para diferenciação de jogadores
- Reinício automático de partidas

---

## Slide 9: Demonstração

**Cenários de Teste:**

1. **Conexão Básica:**
   - Iniciar servidor
   - Conectar cliente
   - Verificar comunicação

2. **Multiplayer:**
   - Conectar 2 clientes
   - Testar movimentação simultânea
   - Verificar sincronização

3. **Performance:**
   - Observar uso de CPU durante colisões
   - Demonstrar eficiência do PLINQ

4. **Robustez:**
   - Desconectar cliente abruptamente
   - Verificar recuperação do sistema

---

## Slide 10: Aprendizados e Desafios

**Principais Aprendizados:**
- Implementação prática de TCP em C#
- Uso efetivo de async/await para responsividade
- Aplicação de paralelismo para performance
- Sincronização de estado entre múltiplos clientes

**Desafios Superados:**
- Sincronização precisa do estado do jogo
- Tratamento robusto de desconexões de rede
- Otimização de operações computacionalmente pesadas
- Integração entre MonoGame e comunicação TCP

**Tecnologias Dominadas:**
- C# .NET 9.0 avançado
- Programação assíncrona e paralela
- Arquitetura cliente-servidor
- Protocolos de comunicação TCP/IP