# language: pt
@defeitos
Funcionalidade: Defeitos encontrados pelos testes e corrigidos
  Cenários que expuseram defeitos reais no código de produção (ver RELATORIO_DEFEITOS.md)

  # DEF-01 — duplicidade de e-mail com espaços ao redor
  Cenário: Registro duplicado com espaços ao redor do e-mail deve ser bloqueado
    Dado um usuário cadastrado com e-mail "dup@escola.com" e senha "senha123"
    Quando registro o usuário "Intruso" e-mail "  dup@escola.com  " senha "outra123" papel "Student"
    Então a resposta deve ter status 409

  # DEF-02 — papel inexistente informado como número
  Cenário: Registro com papel numérico inexistente deve ser rejeitado
    Quando registro o usuário "Fulano" e-mail "fulano@escola.com" senha "senha123" papel "99"
    Então a resposta deve ter status 400

  # DEF-03 — limpeza de texto RAG não remove marcador de página em português
  Cenário: Limpeza remove marcadores de página em português
    Dado o texto bruto:
      """
      Conteúdo importante do material didático.
      Página 5
      """
    Quando aplico a limpeza de texto
    Então o texto limpo não deve conter "Página 5"
    E o texto limpo deve conter "Conteúdo importante do material didático."
