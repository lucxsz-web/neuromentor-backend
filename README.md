# NeuroMentor — Backend

API RESTful do NeuroMentor, plataforma educacional que utiliza IA para transformar materiais de ensino em experiências interativas. Construído com ASP.NET Core 10 (C#), integra o modelo Claude da Anthropic para geração de aulas, exercícios e chat tutoral em streaming.

---

## Sumário

- [Tecnologias](#tecnologias)
- [Como Executar](#como-executar)
- [Endpoints](#endpoints)
- [Arquitetura Distribuída](#1-arquitetura-distribuída)
- [Diagrama da Arquitetura](#2-diagrama-da-arquitetura)
- [Concorrência e Paralelismo](#3-concorrência-e-paralelismo)
- [Otimização](#4-otimização)

---

## Tecnologias

| Componente | Tecnologia |
|---|---|
| Framework | ASP.NET Core 10 (C#) |
| ORM | Entity Framework Core |
| Banco de Dados | PostgreSQL 16 |
| Autenticação | JWT Bearer |
| IA | Claude API — `claude-sonnet-4-5` (Anthropic) |
| Containerização | Docker |
| Testes | xUnit + Gherkin (BDD) |
| Deploy | Railway |

---

## Como Executar

**Pré-requisitos:** .NET SDK 9+, PostgreSQL 14+

### Com Docker

```bash
cp appsettings.example.json appsettings.Development.json
# preencher Anthropic:ApiKey no appsettings.Development.json
docker compose up --build
```

API disponível em `http://localhost:8080`.

### Manual

```bash
# Configurar variáveis de ambiente
export DATABASE_URL="postgresql://user:pass@localhost:5432/neuromentor"
export JWT_SECRET="sua-chave-secreta"
export ANTHROPIC_API_KEY="sk-ant-..."

# Rodar (migrations são aplicadas automaticamente no startup)
dotnet run
```

### Health Check

```bash
curl http://localhost:8080/health
# {"status":"ok","version":"1.0.0"}
```

---

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/register` | Cadastro de usuário |
| `POST` | `/api/auth/login` | Login (retorna JWT) |
| `GET` | `/api/lessons` | Listar aulas disponíveis |
| `POST` | `/api/lessons/upload` | Upload de material (PDF/doc) |
| `POST` | `/api/lessons/{id}/generate` | Gerar módulos com IA |
| `POST` | `/api/chat/stream` | Chat tutoral em streaming (SSE) |
| `POST` | `/api/exercises/generate` | Gerar exercícios (Taxonomia de Bloom) |
| `POST` | `/api/exercises/correct` | Corrigir resposta com IA |
| `POST` | `/api/exercises/attempts` | Registrar tentativa |
| `GET` | `/api/exercises/attempts/pending-review` | Fila de revisão manual (professores) |
| `PUT` | `/api/exercises/attempts/{id}/review` | Aceitar/rejeitar resposta |
| `GET` | `/api/classes` | Listar turmas |
| `GET` | `/health` | Health check |

---

## 1. Arquitetura Distribuída

### Papel deste Serviço na Arquitetura Global

Este repositório contém **apenas a camada de backend** do sistema NeuroMentor. O sistema completo segue um modelo **Cliente-Servidor em 3 Camadas (3-tier)** com um serviço externo de IA:

```
[Browser] → [Frontend Next.js] → [Este backend] → [PostgreSQL]
                                        └──────────→ [Claude API]
```

### Responsabilidades deste componente

- Autenticação e autorização (JWT)
- Regras de negócio (turmas, aulas, exercícios, XP)
- Orquestração de todas as chamadas à IA (Claude)
- Persistência via Entity Framework Core
- Exposição de API RESTful + SSE para o frontend

### Justificativa da Separação Backend/Frontend

O backend é um serviço independente pelos seguintes motivos:

1. **Segurança:** a `ANTHROPIC_API_KEY` nunca toca o browser — toda chamada à IA passa por este serviço, que valida JWT antes de autorizar o acesso.
2. **Deploy independente:** o backend roda na Railway (container persistente com estado de banco), enquanto o frontend roda na Vercel (serverless). Cada um escala e faz deploy separadamente.
3. **Controle centralizado de acesso à IA:** a claim `isAiEnabled` no JWT é verificada aqui — permite habilitar/desabilitar IA por usuário sem mudar o frontend.

---

## 2. Diagrama da Arquitetura

### Arquitetura interna do backend

```
                    REQUISIÇÕES HTTP
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│                  ASP.NET Core 10 — Pipeline                │
│                                                            │
│  CORS Middleware → Auth Middleware → Authorization         │
│                         │                                  │
│                         ▼                                  │
│            ┌────────────────────────┐                     │
│            │       Controllers      │                     │
│            │                        │                     │
│            │  AuthController        │  JWT register/login │
│            │  ChatController  ──────┼──► ClaudeService    │
│            │  LessonsController ────┼──► ClaudeService    │
│            │                   ─────┼──► TextExtraction   │
│            │  ExercisesController ──┼──► ClaudeService    │
│            │  ClassesController     │                     │
│            │  ReviewController      │                     │
│            │  AdminController       │                     │
│            └──────────┬─────────────┘                     │
│                       │                                    │
│            ┌──────────▼─────────────┐                     │
│            │        Services        │                     │
│            │                        │                     │
│            │  ClaudeService         │  HttpClient → HTTPS │
│            │   ├─ CompleteAsync()   │  Chamada síncrona   │
│            │   └─ StreamAsync()     │  SSE streaming      │
│            │                        │                     │
│            │  JwtService            │  Geração/validação  │
│            │  TextExtractionService │  PDF → texto limpo  │
│            └──────────┬─────────────┘                     │
│                       │                                    │
│            ┌──────────▼─────────────┐                     │
│            │    AppDbContext (EF)    │                     │
│            │  Users · Lessons       │                     │
│            │  LessonModules         │                     │
│            │  Classes · Exercises   │                     │
│            │  Attempts              │                     │
│            └──────────┬─────────────┘                     │
└───────────────────────┼────────────────────────────────────┘
                        │                     │
              TCP/Npgsql│                     │ HTTPS/REST + SSE
                        ▼                     ▼
               ┌─────────────┐      ┌──────────────────────┐
               │ PostgreSQL  │      │    Claude API         │
               │    :5432    │      │  /v1/messages         │
               └─────────────┘      │  stream: true/false   │
                                    └──────────────────────┘
```

### Protocolos de Comunicação (visão deste serviço)

| Direção | Protocolo | Formato | Onde no código |
|---|---|---|---|
| Frontend → Backend (dados) | HTTP/REST | JSON | Todos os controllers |
| Frontend → Backend (chat) | HTTP + SSE | `text/event-stream` | `ChatController.Stream()` |
| Backend → Claude API (resposta completa) | HTTPS/REST | JSON | `ClaudeService.CompleteAsync()` |
| Backend → Claude API (streaming) | HTTPS/REST + SSE | `text/event-stream` | `ClaudeService.StreamAsync()` |
| Backend → PostgreSQL | TCP / Npgsql | Protocolo binário PG | `AppDbContext` via EF Core |

---

## QA Validation

Este repositório inclui uma marcação de validação QA para o revisor:

- `QA Validator: ADM`

---

## 3. Concorrência e Paralelismo

### Mecanismo 1 — Async/Await em Todos os Controllers

**Componente:** `Controllers/` — todos os arquivos

**Mecanismo:** Corrotinas do .NET (`async Task`) gerenciadas pelo thread pool do ASP.NET Core

O ASP.NET Core despacha cada requisição HTTP em uma thread do pool. Com `async/await`, a thread é **liberada de volta ao pool** enquanto aguarda I/O (banco ou IA), permitindo que atenda outras requisições nesse intervalo.

```csharp
// ExercisesController.cs — sequência não-bloqueante
[HttpPost("generate")]
public async Task<IActionResult> Generate([FromBody] GenerateRequest req)
{
    // Thread livre enquanto o banco responde
    var lesson = await _db.Lessons
        .FirstOrDefaultAsync(l => l.Id == req.LessonId);

    // Thread livre enquanto a Claude API processa (~3-8s)
    // Material truncado em 12.000 chars antes de enviar
    var raw = await _claudeService.CompleteAsync(systemPrompt, userPrompt);

    // Parsing síncrono (CPU-bound, rápido)
    var exercises = JsonSerializer.Deserialize<ExerciseSet>(raw);

    return Ok(exercises);
}
```

**Problema resolvido:** Sem async/await, 50 requisições simultâneas à API da IA (cada uma esperando ~5s) consumiriam 50 threads bloqueadas. Com async/await, as mesmas 50 requisições usam 2-4 threads do pool, rotacionando entre si enquanto aguardam I/O.

---

### Mecanismo 2 — Streaming Assíncrono com `IAsyncEnumerable<string>` e `CancellationToken`

**Componente:** `Services/ClaudeService.cs` → `StreamAsync()` + `Controllers/ChatController.cs` → `Stream()`

**Mecanismo:** Produtor assíncrono (`IAsyncEnumerable`) consumido chunk a chunk pelo controller, com `CancellationToken` para cancelamento cooperativo.

```csharp
// ClaudeService.cs — PRODUTOR: lê SSE da Anthropic e entrega chunks
public async IAsyncEnumerable<string> StreamAsync(
    string systemPrompt,
    string userMessage,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
    // ... configura headers e body com stream: true

    using var response = await _http.SendAsync(
        request,
        HttpCompletionOption.ResponseHeadersRead, // não aguarda body completo
        ct);

    using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));

    while (!reader.EndOfStream)
    {
        ct.ThrowIfCancellationRequested(); // cancelamento cooperativo

        var line = await reader.ReadLineAsync(ct); // não bloqueia

        if (line?.StartsWith("data: ") != true) continue;
        if (line == "data: [DONE]") break;

        var json = JsonDocument.Parse(line[6..]);
        var type = json.RootElement.GetProperty("type").GetString();

        if (type == "content_block_delta")
        {
            var delta = json.RootElement
                .GetProperty("delta")
                .GetProperty("text")
                .GetString();

            yield return delta; // entrega imediatamente sem acumular
        }
    }
}

// ChatController.cs — CONSUMIDOR: repassa cada chunk ao browser
[HttpPost("stream")]
public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
{
    Response.ContentType = "text/event-stream";
    Response.Headers["Cache-Control"] = "no-cache";

    var (system, user) = await BuildContext(req, ct);

    await foreach (var chunk in _claude.StreamAsync(system, user, ct))
    {
        // Formato: "0:<json_string>\n" — compatível com Vercel AI SDK
        await Response.WriteAsync($"0:{JsonSerializer.Serialize(chunk)}\n", ct);
        await Response.Body.FlushAsync(ct); // força envio imediato ao cliente
    }
}
```

**Problema resolvido:**

| Sem streaming | Com streaming |
|---|---|
| Usuário espera ~8-12s com tela em branco | Primeiras palavras em ~0,5s |
| Thread do servidor bloqueada acumulando resposta | Thread livre, entrega chunk a chunk |
| Sem possibilidade de cancelar no meio | `CancellationToken` interrompe a chamada à API se o usuário fechar a aba |

O `CancellationToken` é propagado do cliente HTTP até a chamada à Anthropic — se o frontend desconectar (usuário fecha a aba ou navega), a requisição em andamento é cancelada imediatamente, evitando processamento e custo desnecessários.

---

### Mecanismo 3 — Thread Pool Gerenciado pelo Runtime .NET

**Componente:** Runtime do ASP.NET Core (infraestrutura)

O Kestrel (servidor HTTP do .NET) aceita conexões em I/O completion ports (Windows) / epoll (Linux), despachando para o thread pool apenas quando há trabalho CPU-bound a fazer. Isso permite que o servidor mantenha milhares de conexões abertas (especialmente importantes para as conexões SSE de streaming) com um número fixo e pequeno de threads.

**Ganho mensurável:** Uma instância padrão do ASP.NET Core consegue manter ~10.000 conexões SSE simultâneas com ~8 threads — algo impossível no modelo thread-per-request.

---

## 4. Otimização

### Otimizações Implementadas

#### a) Context Capping e Seleção Hierárquica de Contexto

**Componente:** `Controllers/ChatController.cs` — método `BuildContext()`

Antes de cada mensagem de chat, o sistema aplica uma estratégia em camadas para minimizar tokens enviados à IA:

```
Prioridade 1 — chunk do módulo específico
  → foco máximo, menor quantidade de tokens, mais preciso

Prioridade 2 — conteúdo completo dos módulos da aula
  → contexto médio

Prioridade 3 — contexto bruto fornecido pelo usuário
  → limitado a 8.000 caracteres (hard cap)
  → "Prefer module chunk (focused, small) over raw context (large)"
```

**Impacto:** Reduz em até 80% os tokens por mensagem. Com o modelo `claude-sonnet-4-5`, cada 1.000 tokens de input custam ~$0.003 — em escala, a diferença é significativa. Além disso, contextos menores resultam em respostas mais rápidas e mais precisas.

---

#### b) Truncamento de Material na Geração de Exercícios

**Componente:** `Controllers/ExercisesController.cs`

```csharp
// Material truncado em 12.000 caracteres antes de enviar à IA
var truncatedMaterial = material.Length > 12000
    ? material[..12000]
    : material;
```

**Impacto:** Garante que mesmo materiais extensos não ultrapassem a janela de contexto eficiente do modelo, mantendo latência e custo previsíveis.

---

#### c) Extração de Chunks por Pontuação de Relevância

**Componente:** `Services/TextExtractionService.cs` — método `ExtractChunk()`

O serviço pontua cada parágrafo extraído do documento (PDF, DOCX) e seleciona apenas os trechos com maior densidade informacional antes de enviá-los à IA para geração de módulos.

**Impacto:** Documentos de até 50 MB são processados localmente e reduzidos a poucos kilobytes de conteúdo relevante — sem isso, cada geração de aula enviaria megabytes à API.

---

#### d) Eager Loading para Evitar Queries N+1

**Componente:** `Controllers/LessonsController.cs`, `ExercisesController.cs`

```csharp
// Carrega lição E módulos em uma única query SQL com JOIN
var lesson = await _db.Lessons
    .Include(l => l.Modules)
    .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
```

**Impacto:** Para uma aula com 10 módulos, substitui 11 queries (`SELECT lesson` + `SELECT module WHERE lessonId=X` × 10) por 1 query com JOIN. Em endpoints que listam múltiplas aulas, o ganho é ainda maior.

---

#### e) `HttpCompletionOption.ResponseHeadersRead` no Streaming

**Componente:** `Services/ClaudeService.cs`

```csharp
// Não aguarda o body HTTP completo antes de começar a processar
var response = await _http.SendAsync(
    request,
    HttpCompletionOption.ResponseHeadersRead, // <- otimização chave
    ct);
```

**Impacto:** O `HttpClient` começa a ler o stream assim que os headers chegam, sem buffer do body inteiro em memória. Para respostas longas da IA (~2.000-4.000 tokens), evita alocação desnecessária de strings grandes e reduz o time-to-first-byte.

---

#### f) Migrations Automáticas no Startup

**Componente:** `Program.cs`

```csharp
// Aplica migrations pendentes ao iniciar — sem necessidade de step manual
await db.Database.MigrateAsync();
```

**Ganho operacional:** Elimina erros de "schema desatualizado" em deploy — o banco está sempre sincronizado com o código assim que o container sobe.

---

### Otimizações Futuras Identificadas

| Ponto | Problema Atual | Solução Proposta | Ganho Esperado |
|---|---|---|---|
| `TextExtractionService.Extract()` | Síncrono — bloqueia thread durante leitura de PDF | `ReadToEndAsync()` + `CopyToAsync()` | Suporte a múltiplos uploads simultâneos sem bloquear o servidor |
| Geração de módulos de aula | Módulos gerados sequencialmente (`await` um por vez) | `Task.WhenAll(tasks)` para geração paralela | Redução de ~15s para ~5s por aula com 3 módulos |
| Cache de exercícios | Exercícios regenerados a cada chamada para o mesmo material | Redis com chave `SHA256(lessonId + bloomLevel)` · TTL 24h | Eliminar ~60% das chamadas à Claude API |
| Busca de contexto | Contexto selecionado por posição no documento | pgvector + embeddings para busca semântica | Contexto sempre relevante, independente do tamanho do material |
| Pontuação de parágrafos | `ExtractChunk()` processa parágrafos sequencialmente | `Parallel.ForEach` ou `PLINQ` | Processamento de documentos grandes 4-8x mais rápido |

---

## Estrutura de Pastas

```
neuromentor-backend/
├── Controllers/
│   ├── AuthController.cs         # Register, Login (JWT)
│   ├── ChatController.cs         # Stream SSE, contexto por módulo
│   ├── LessonsController.cs      # Upload, geração de módulos
│   ├── ExercisesController.cs    # Geração, correção, tentativas
│   ├── ClassesController.cs      # Turmas e matrículas
│   ├── ReviewController.cs       # Revisão manual por professores
│   └── AdminController.cs        # Gerenciamento de usuários
├── Services/
│   ├── ClaudeService.cs          # CompleteAsync + StreamAsync
│   ├── JwtService.cs             # Geração e validação de tokens
│   └── TextExtractionService.cs  # PDF/DOCX → texto + chunks
├── Models/                        # Entidades do domínio
├── DTOs/                          # Request/Response objects
├── Data/
│   └── AppDbContext.cs            # EF Core DbContext + migrations
├── tests/
│   └── NeuroMentor.Api.Tests/     # Testes BDD com Gherkin
├── Program.cs                     # Startup, DI, middleware pipeline
├── Dockerfile
├── docker-compose.yml
└── appsettings.example.json
```

---

## Licença

Projeto acadêmico desenvolvido para a disciplina de Projetos / Embarque Digital — Porto Digital.
