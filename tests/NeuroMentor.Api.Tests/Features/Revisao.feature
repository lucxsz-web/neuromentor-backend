# language: pt
@revisao
Funcionalidade: Revisão guiada personalizada
  Como aluno
  Quero um guia de revisão baseado nas questões que errei
  Para reforçar meus pontos fracos (escopo: Revisão guiada personalizada)

  Cenário: Geração de guia de revisão a partir dos erros do aluno
    Dado um aluno autenticado com acesso à IA
    E a IA responderá com um guia de revisão
    Quando o aluno solicita revisão das questões que errou
    Então a resposta deve ter status 200
    E o guia deve conter um resumo

  Cenário: Revisão é bloqueada para aluno sem acesso à IA
    Dado um aluno autenticado sem acesso à IA
    Quando o aluno solicita revisão das questões que errou
    Então a resposta deve ter status 403
