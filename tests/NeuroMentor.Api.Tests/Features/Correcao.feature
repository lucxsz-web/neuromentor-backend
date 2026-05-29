# language: pt
@correcao @ts04
Funcionalidade: Correção automática de exercícios e XP (TS04)
  Como aluno e como professor
  Quero correção automática com feedback e pontuação de XP
  E revisão do professor quando necessário (TC04)

  Cenário: Correção automática retorna feedback (TC04)
    Dado um aluno autenticado com acesso à IA
    E a IA avaliará a resposta como correta com o feedback "Muito bem, resposta completa!"
    Quando o aluno envia para correção a pergunta "O que é fotossíntese?" com a resposta "É a produção de energia pelas plantas."
    Então a resposta deve ter status 200
    E o resultado deve indicar que a resposta está correta
    E o resultado deve conter o feedback "Muito bem, resposta completa!"

  Cenário: Correção é bloqueada para aluno sem acesso à IA
    Dado um aluno autenticado sem acesso à IA
    Quando o aluno envia para correção a pergunta "X" com a resposta "Y"
    Então a resposta deve ter status 403

  # Particionamento de equivalência: resposta correta vs incorreta
  Cenário: Tentativa correta concede 50 XP
    Dado um aluno autenticado
    Quando o aluno registra uma tentativa marcada como correta
    Então a resposta deve ter status 200
    E o XP concedido deve ser 50

  Cenário: Tentativa incorreta concede 10 XP
    Dado um aluno autenticado
    Quando o aluno registra uma tentativa marcada como incorreta
    Então a resposta deve ter status 200
    E o XP concedido deve ser 10

  # Transição de estado: PendingReview -> Rejected (com efeito colateral no XP)
  Cenário: Professor rejeita uma tentativa e o XP é reduzido
    Dado um professor dono de uma turma com um aluno matriculado
    E o aluno possui uma tentativa correta pendente de revisão valendo 50 XP
    Quando o professor rejeita essa tentativa
    Então a resposta deve ter status 200
    E a tentativa deve ficar com status "rejected"
    E a tentativa deve valer 10 XP
    E a tentativa deve ficar marcada como incorreta
