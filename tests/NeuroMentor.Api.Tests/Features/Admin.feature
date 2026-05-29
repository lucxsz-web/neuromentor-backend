# language: pt
@admin
Funcionalidade: Painel administrativo
  Como administrador
  Quero gerenciar usuários, acesso à IA e materiais
  Para administrar a plataforma com segurança

  Cenário: Admin lista os usuários
    Dado um administrador autenticado
    E existe um usuário comum "joao@escola.com"
    Quando o admin lista os usuários
    Então a resposta deve ter status 200
    E a lista deve conter pelo menos 2 usuários

  Cenário: Usuário comum não acessa a lista de usuários
    Dado um aluno autenticado
    Quando o admin lista os usuários
    Então a resposta deve ter status 403

  Cenário: Admin cria outro administrador
    Dado um administrador autenticado
    Quando o admin cria o administrador "Novo" e-mail "novo.admin@escola.com" senha "senha123"
    Então a resposta deve ter status 200

  Cenário: Criar admin com e-mail duplicado é rejeitado
    Dado um administrador autenticado
    E existe um usuário comum "dup.admin@escola.com"
    Quando o admin cria o administrador "Dup" e-mail "dup.admin@escola.com" senha "senha123"
    Então a resposta deve ter status 409

  Cenário: Criar admin com senha curta é rejeitado
    Dado um administrador autenticado
    Quando o admin cria o administrador "Curto" e-mail "curto@escola.com" senha "123"
    Então a resposta deve ter status 400

  Cenário: Admin concede acesso à IA a um usuário
    Dado um administrador autenticado
    E existe um usuário comum "semia@escola.com"
    Quando o admin habilita o acesso à IA desse usuário
    Então a resposta deve ter status 200
    E o usuário deve ficar com acesso à IA habilitado

  Cenário: Admin não pode deletar a própria conta
    Dado um administrador autenticado
    Quando o admin tenta deletar a própria conta
    Então a resposta deve ter status 400

  Cenário: Admin deleta outro usuário
    Dado um administrador autenticado
    E existe um usuário comum "remover@escola.com"
    Quando o admin deleta esse usuário
    Então a resposta deve ter status 200

  Cenário: Admin lista os materiais enviados
    Dado um administrador autenticado
    E existe um usuário comum "prof.mat@escola.com"
    E esse usuário possui uma aula "Material 1"
    Quando o admin lista os materiais
    Então a resposta deve ter status 200
    E a lista deve conter pelo menos 1 material
