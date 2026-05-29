# language: pt
@dashboard @ts05
Funcionalidade: Dashboard do aluno — histórico e XP (TS05)
  Como aluno
  Quero visualizar meu histórico de atividades e XP acumulado
  Para acompanhar meu progresso (TC05)

  Cenário: Aluno visualiza histórico de tentativas com XP acumulado (TC05)
    Dado um aluno autenticado
    E o aluno possui as seguintes tentativas:
      | correta | xp |
      | true    | 50 |
      | false   | 10 |
      | true    | 50 |
    Quando o aluno consulta seu histórico de atividades
    Então a resposta deve ter status 200
    E o histórico deve conter 3 atividades
    E o XP total acumulado deve ser 110

  Cenário: Aluno sem atividades vê histórico vazio
    Dado um aluno autenticado
    Quando o aluno consulta seu histórico de atividades
    Então a resposta deve ter status 200
    E o histórico deve conter 0 atividades
