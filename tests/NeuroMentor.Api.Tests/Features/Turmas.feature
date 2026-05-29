# language: pt
@turmas
Funcionalidade: Gestão de turmas
  Como professor e aluno
  Quero criar turmas, matricular alunos e vincular aulas
  Para organizar o acesso ao material

  Cenário: Professor cria uma turma com código gerado
    Dado um professor autenticado com acesso à IA
    Quando o professor cria a turma "Turma de Biologia"
    Então a resposta deve ter status 200
    E a turma criada deve ter um código de 6 caracteres
    E deve existir 1 turma persistida

  Cenário: Aluno não pode criar turma
    Dado um aluno autenticado
    Quando o professor cria a turma "Turma Proibida"
    Então a resposta deve ter status 403

  Cenário: Professor lista suas turmas
    Dado um professor autenticado com acesso à IA
    E o professor já possui uma turma "Turma A"
    Quando o professor lista suas turmas
    Então a resposta deve ter status 200
    E a lista deve conter 1 turma

  Cenário: Aluno entra em uma turma pelo código
    Dado um aluno autenticado
    E existe uma turma "Turma X" com o código "ABC234"
    Quando o aluno entra na turma com o código "ABC234"
    Então a resposta deve ter status 200

  Cenário: Entrar em turma inexistente é rejeitado
    Dado um aluno autenticado
    Quando o aluno entra na turma com o código "ZZZ999"
    Então a resposta deve ter status 404

  Cenário: Aluno não pode entrar duas vezes na mesma turma
    Dado um aluno autenticado
    E existe uma turma "Turma Y" com o código "DEF345"
    E o aluno já está matriculado nessa turma
    Quando o aluno entra na turma com o código "DEF345"
    Então a resposta deve ter status 409

  Cenário: Professor adiciona uma aula à turma
    Dado um professor autenticado com acesso à IA
    E o professor já possui uma turma "Turma A"
    E o professor possui uma aula "Aula 1"
    Quando o professor adiciona a aula à turma
    Então a resposta deve ter status 200

  Cenário: Adicionar a mesma aula duas vezes é rejeitado
    Dado um professor autenticado com acesso à IA
    E o professor já possui uma turma "Turma A"
    E o professor possui uma aula "Aula 1"
    E a aula já está vinculada à turma
    Quando o professor adiciona a aula à turma
    Então a resposta deve ter status 409

  Cenário: Professor remove uma turma
    Dado um professor autenticado com acesso à IA
    E o professor já possui uma turma "Turma A"
    Quando o professor remove a turma
    Então a resposta deve ter status 204

  Cenário: Aluno consulta suas turmas matriculadas
    Dado um aluno autenticado
    E existe uma turma "Turma Z" com o código "GHI456"
    E o aluno já está matriculado nessa turma
    Quando o aluno consulta suas turmas matriculadas
    Então a resposta deve ter status 200
    E a lista deve conter 1 turma
