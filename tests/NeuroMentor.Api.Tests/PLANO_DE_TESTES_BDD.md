# Plano de Testes BDD — NeuroMentor.Api

> Documento técnico de QA que **operacionaliza** o Plano de Testes da Squad 26
> (Plataforma de Mentoria com IA) em testes automatizados executáveis.
> Framework: **Reqnroll** (sucessor oficial do SpecFlow) sobre **xUnit** — alvo **.NET 10**.

---

## 1. Por que Reqnroll e não SpecFlow

O documento da squad cita "C#/.NET". A biblioteca de BDD historicamente mais famosa do
ecossistema C# é o **SpecFlow**. Porém:

| Critério | SpecFlow | Reqnroll |
|---|---|---|
| Manutenção | **Descontinuado** pela Tricentis (fim de 2024) | Ativo (fork comunitário oficial) |
| Suporte a .NET 8/9/10 | Não | **Sim** |
| Sintaxe Gherkin / `.feature` | Idêntica | **Idêntica** (migração 1:1) |
| Runners | xUnit / NUnit / MSTest | xUnit / NUnit / MSTest |

Como o backend é **.NET 10**, SpecFlow não compila/roda. **Reqnroll** entrega exatamente
a mesma experiência de BDD (mesmos arquivos `.feature`, mesmos atributos `[Given]/[When]/[Then]`)
e é a escolha tecnicamente correta. Runner escolhido: **xUnit v2** (exigência do Reqnroll.xUnit 3.x).

---

## 2. Objeto de teste e arquitetura testada

API ASP.NET Core (`NeuroMentor.Api`) com:

- **Controllers**: `Auth`, `Lessons`, `Exercises`, `Chat`, `Review`, `Admin`, `Classes`.
- **Serviços de lógica pura**: `TextExtractionService` (pré-processamento RAG), `JwtService`.
- **Dependência externa**: `ClaudeService` → API da Anthropic (HTTP).

### Estratégia de isolamento (decisões de QA)

| Dependência | Como é tratada nos testes | Motivo |
|---|---|---|
| PostgreSQL + EF Core | **EF Core InMemory**, banco único por cenário | O `Program.cs` roda `Migrate()` no boot, incompatível com InMemory → testamos os controllers **instanciados diretamente**, sem subir o host. |
| `ClaudeService` (HTTP Anthropic) | **`FakeClaudeHandler`** (HttpMessageHandler stub) com resposta canônica configurável e **captura do prompt enviado** | Testes determinísticos, sem rede nem custo de API; permite validar que a IA recebe o material correto. |
| JWT | `JwtService` real + `IConfiguration` de teste | Validar geração de token e claims (caixa branca). |

---

## 3. Rastreabilidade — Cenários do PDF → Features BDD

| Cenário PDF | Caso PDF | Feature BDD | Cobertura adicional (QA Sênior) |
|---|---|---|---|
| **TS01** Upload de materiais | TC01 | `Upload.feature` | arquivo vazio → 400; sem texto extraível → 422 |
| **TS02** Processamento RAG | TC02 | `ProcessamentoRag.feature` + `ExtracaoTexto.feature` | limpeza de cabeçalhos/rodapés/números de página; chunk por relevância; status de módulo inválido → 400; sem IA → 403 |
| **TS03** Sessão de mentoria IA | TC03 | `MentoriaIA.feature` | prompt enviado à IA **contém o material do módulo**; sem IA → 403 |
| **TS04** Correção de exercícios | TC04 | `Correcao.feature` | XP 50 (correto) / 10 (incorreto); rejeição do professor zera acerto e reduz XP; sem IA → 403 |
| **TS05** Dashboard do aluno | TC05 | `Dashboard.feature` | histórico ordenado; soma de XP acumulado |
| Segurança/Autenticação (escopo) | — | `Autenticacao.feature` | e-mail duplicado → 409; papel inválido → 400; senha incorreta → 401; troca de senha (atual errada / nova curta) → 400; claims do JWT |

---

## 4. Técnicas de teste aplicadas (alinhadas ao PDF)

- **Caixa preta**
  - *Particionamento de equivalência*: papel válido vs inválido; arquivo com/sem texto; com/sem acesso à IA.
  - *Análise de valor limite*: senha nova com 5 vs 6 caracteres; arquivo de 0 byte.
  - *Transição de estados*: módulo `Pending → Approved/Rejected`; tentativa `PendingReview → Accepted/Rejected`.
- **Caixa branca**
  - *Cobertura de ramificações*: ramos de `TextExtractionService.Clean`/`ExtractChunk`; geração de claims no `JwtService`.
  - *Testes unitários* da lógica pura de RAG.

## 5. Níveis de teste

- **Unidade**: `ExtracaoTexto.feature`, claims do `JwtService`.
- **Integração**: controllers + EF InMemory + `ClaudeService` stub (todas as demais features).

## 6. Critérios de aceitação (Definition of Done dos testes)

1. Todas as features executam verdes em `dotnet test`.
2. Cada cenário do PDF (TS01–TS05) possui ao menos 1 cenário BDD verde.
3. Caminhos de erro (4xx) e de permissão (403) cobertos.
4. Nenhum teste depende de rede externa, banco real ou chave de API real.

## 7. Como executar

```bash
# da raiz do backend
dotnet test                       # roda toda a suíte
dotnet test --filter "Category=auth"   # por tag (@auth, @rag, @mentoria, etc.)
```

## 8. Riscos cobertos (do PDF) e como

| Risco (PDF) | Mitigação via teste |
|---|---|
| IA responder informações incorretas | `MentoriaIA.feature` garante que o material do módulo é injetado no prompt da IA. |
| Falha no processamento RAG | `ProcessamentoRag.feature` valida persistência dos módulos e estados; `ExtracaoTexto.feature` valida a limpeza/seleção de texto. |
| Lentidão no upload | `Upload.feature` valida o caminho de extração de forma isolada e rápida (sem rede). |
