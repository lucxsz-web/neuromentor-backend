# Relatório de Defeitos — NeuroMentor.Api

> Documento de QA registrando **3 defeitos reais** detectados pela suíte BDD (Reqnroll/xUnit),
> a causa-raiz, a correção no código de produção e a evidência de reteste (verde).
> Plataforma de Mentoria com IA — Squad 26.

| Item | Valor |
|---|---|
| Data de execução | 29/05/2026 |
| Suíte | `tests/NeuroMentor.Api.Tests` (Reqnroll + xUnit, .NET 10) |
| Feature de evidência | `Features/Defeitos.feature` (tag `@defeitos`) |
| Resultado antes das correções | **3 falhas / 0 aprovados** |
| Resultado após as correções | **3 aprovados / 0 falhas** — suíte completa **56/56** verde |
| Comando | `dotnet test --filter "Category=defeitos"` |

---

## Resumo

| ID | Título | Componente | Severidade | Status |
|----|--------|------------|------------|--------|
| DEF-01 | Cadastro duplicado de e-mail com espaços ao redor não é bloqueado | `AuthController.Register` | **Alta** | ✅ Corrigido |
| DEF-02 | Papel (role) inexistente informado como número é aceito | `AuthController.Register` | **Média** | ✅ Corrigido |
| DEF-03 | Limpeza de texto RAG não remove marcadores de página em português | `TextExtractionService.Clean` | **Média** | ✅ Corrigido |

---

## DEF-01 — Cadastro duplicado de e-mail com espaços ao redor não é bloqueado

- **Cenário de teste (Gherkin):**
  ```gherkin
  Cenário: Registro duplicado com espaços ao redor do e-mail deve ser bloqueado
    Dado um usuário cadastrado com e-mail "dup@escola.com" e senha "senha123"
    Quando registro o usuário "Intruso" e-mail "  dup@escola.com  " senha "outra123" papel "Student"
    Então a resposta deve ter status 409
  ```
- **Severidade:** Alta — permite **contas duplicadas** com o mesmo e-mail, violando a `UNIQUE INDEX` da tabela `Users` (em PostgreSQL geraria erro 500 na gravação; em testes InMemory passa silenciosamente, mascarando o problema).
- **Resultado esperado:** `409 Conflict`.
- **Resultado obtido (antes):** `200 OK` — segundo cadastro criado.
  ```
  Com falha Registro duplicado com espaços ao redor do e-mail deve ser bloqueado
   Assert.Equal() Failure: Values differ
   Expected: 409
   Actual:   200
  ```
- **Causa-raiz:** a verificação de duplicidade usava `req.Email.ToLower()` (sem `Trim()`), enquanto a gravação usava `req.Email.Trim().ToLower()`. Com espaços ao redor, a busca não encontrava o registro existente e seguia para a inserção.
- **Correção (`Controllers/AuthController.cs`):**
  ```diff
  - if (await db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
  + if (await db.Users.AnyAsync(u => u.Email == req.Email.Trim().ToLower()))
        return Conflict(new { error = "Email já cadastrado." });
  ```

---

## DEF-02 — Papel (role) inexistente informado como número é aceito

- **Cenário de teste (Gherkin):**
  ```gherkin
  Cenário: Registro com papel numérico inexistente deve ser rejeitado
    Quando registro o usuário "Fulano" e-mail "fulano@escola.com" senha "senha123" papel "99"
    Então a resposta deve ter status 400
  ```
- **Severidade:** Média — `Enum.TryParse` converte **qualquer string numérica** em um valor de enum, mesmo fora do intervalo definido. `"99"` virava `(UserRole)99`, um papel inexistente persistido no banco.
- **Resultado esperado:** `400 Bad Request`.
- **Resultado obtido (antes):** `200 OK` — usuário criado com papel inválido.
  ```
  Com falha Registro com papel numérico inexistente deve ser rejeitado
   Assert.Equal() Failure: Values differ
   Expected: 400
   Actual:   200
  ```
- **Causa-raiz:** `Enum.TryParse<UserRole>("99", ...)` retorna `true` para valores numéricos fora do enum. Faltava validar se o valor é realmente **definido**.
- **Correção (`Controllers/AuthController.cs`):**
  ```diff
  - if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role))
  + if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        return BadRequest(new { error = "Role inválido. Use 'Student' ou 'Teacher'." });
  ```

---

## DEF-03 — Limpeza de texto RAG não remove marcadores de página em português

- **Cenário de teste (Gherkin):**
  ```gherkin
  Cenário: Limpeza remove marcadores de página em português
    Dado o texto bruto:
      """
      Conteúdo importante do material didático.
      Página 5
      """
    Quando aplico a limpeza de texto
    Então o texto limpo não deve conter "Página 5"
    E o texto limpo deve conter "Conteúdo importante do material didático."
  ```
- **Severidade:** Média — a plataforma é em **português**, mas o filtro de ruído só reconhecia "Page" (inglês). Marcadores como "Página 5" vazavam para o contexto enviado à IA, **degradando a qualidade do RAG** (risco mapeado no Plano de Testes: *"IA responder informações incorretas"*).
- **Resultado esperado:** linha "Página 5" removida.
- **Resultado obtido (antes):** linha mantida no texto limpo.
  ```
  Com falha Limpeza remove marcadores de página em português
   Assert.DoesNotContain() Failure: Sub-string found
  ```
- **Causa-raiz:** a expressão regular de remoção de números de página cobria apenas `[Pp]age`.
- **Correção (`Services/TextExtractionService.cs`):**
  ```diff
  - // Skip lone page numbers: "1", "2", "Page 3", "- 4 -", "3 of 50"
  - if (Regex.IsMatch(t, @"^[-–—\s]*[Pp]age\s*\d+[-–—\s\w]*$")) continue;
  + // Skip lone page numbers: "1", "2", "Page 3", "Página 3", "Pág. 3", "- 4 -", "3 of 50"
  + if (Regex.IsMatch(t, @"^[-–—\s]*([Pp]age|[Pp][áa]gina|[Pp][áa]g\.?)\s*\d+[-–—\s\w]*$")) continue;
  ```

---

## Evidência de reteste (após correções)

```
=== Só os 3 defeitos (--filter "Category=defeitos") ===
Aprovado!  – Com falha: 0, Aprovado: 3, Ignorado: 0, Total: 3

=== Suíte completa (regressão) ===
Aprovado!  – Com falha: 0, Aprovado: 56, Ignorado: 0, Total: 56
```

**Conclusão:** os três defeitos foram corrigidos no código de produção, validados por testes
automatizados dedicados, e a execução completa da suíte confirma a **ausência de regressões**.
