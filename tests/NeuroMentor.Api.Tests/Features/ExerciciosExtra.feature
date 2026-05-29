# language: pt
@correcao @ts04
Funcionalidade: Geração de exercícios e fila de revisão
  Como aluno e professor
  Quero gerar exercícios a partir do material e acompanhar revisões pendentes

  Cenário: Aluno gera exercícios a partir de um contexto
    Dado um aluno autenticado com acesso à IA
    E a IA responderá com exercícios válidos
    Quando o aluno gera exercícios sobre o módulo "Fotossíntese" com o contexto "A clorofila capta luz solar."
    Então a resposta deve ter status 200

  Cenário: Geração de exercícios sem material é rejeitada
    Dado um aluno autenticado com acesso à IA
    Quando o aluno gera exercícios sobre o módulo "Vazio" sem contexto
    Então a resposta deve ter status 400

  Cenário: Geração de exercícios sem acesso à IA é proibida
    Dado um aluno autenticado sem acesso à IA
    Quando o aluno gera exercícios sobre o módulo "X" com o contexto "qualquer"
    Então a resposta deve ter status 403

  Cenário: Professor consulta tentativas pendentes de revisão
    Dado um professor dono de uma turma com um aluno matriculado
    E o aluno possui uma tentativa correta pendente de revisão valendo 50 XP
    Quando o professor consulta as tentativas pendentes de revisão
    Então a resposta deve ter status 200
    E a fila de revisão deve conter 1 tentativa

  Cenário: Mentoria por contexto bruto injeta o material no prompt
    Dado um aluno autenticado com acesso à IA
    E a IA responderá com exercícios válidos
    Quando o aluno solicita mentoria com o contexto bruto "Texto exclusivo do material enviado."
    Então a resposta deve ter status 200
    E o prompt enviado à IA deve conter "Texto exclusivo do material enviado."
