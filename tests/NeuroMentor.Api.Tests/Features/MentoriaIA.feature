# language: pt
@mentoria @ts03
Funcionalidade: Sessão de mentoria com IA baseada no material (TS03)
  Como aluno
  Quero que a IA responda usando o material do professor
  Para receber mentoria fiel ao conteúdo (TC03 / risco: IA responder fora do material)

  Cenário: A mentoria injeta o material do módulo no contexto da IA (TC03)
    Dado um aluno autenticado com acesso à IA
    E um módulo "Fotossíntese" com o trecho de material "A clorofila capta a luz solar nos cloroplastos."
    E a IA responderá com exercícios válidos
    Quando o aluno solicita exercícios de mentoria sobre esse módulo
    Então a resposta deve ter status 200
    E o prompt enviado à IA deve conter "A clorofila capta a luz solar nos cloroplastos."

  Cenário: Mentoria é bloqueada para aluno sem acesso à IA
    Dado um aluno autenticado sem acesso à IA
    Quando o aluno solicita exercícios de mentoria sem módulo
    Então a resposta deve ter status 403
