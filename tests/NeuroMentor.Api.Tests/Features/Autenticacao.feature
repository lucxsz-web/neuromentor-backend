# language: pt
@auth
Funcionalidade: Autenticação e gestão de conta
  Como usuário da plataforma NeuroMentor
  Quero me registrar, autenticar e gerenciar minha senha com segurança
  Para acessar a plataforma de forma confiável (escopo: Segurança e Autenticação)

  Cenário: Registro de novo professor com dados válidos
    Quando registro o usuário "Profª Ana" e-mail "ana@escola.com" senha "senha123" papel "Teacher"
    Então a resposta deve ter status 200
    E a resposta deve conter um token JWT

  Cenário: Registro com e-mail já cadastrado é rejeitado
    Dado um usuário cadastrado com e-mail "dup@escola.com" e senha "senha123"
    Quando registro o usuário "Outro" e-mail "dup@escola.com" senha "outra123" papel "Student"
    Então a resposta deve ter status 409

  Cenário: Registro com papel inválido é rejeitado
    Quando registro o usuário "Fulano" e-mail "fulano@escola.com" senha "senha123" papel "Diretor"
    Então a resposta deve ter status 400

  Cenário: Login com credenciais válidas retorna token
    Dado um usuário cadastrado com e-mail "ok@escola.com" e senha "correta1"
    Quando faço login com e-mail "ok@escola.com" e senha "correta1"
    Então a resposta deve ter status 200
    E a resposta deve conter um token JWT

  Cenário: Login com senha incorreta é rejeitado
    Dado um usuário cadastrado com e-mail "login@escola.com" e senha "correta1"
    Quando faço login com e-mail "login@escola.com" e senha "errada"
    Então a resposta deve ter status 401

  # Análise de valor limite: nova senha com menos de 6 caracteres
  Cenário: Troca de senha com a nova senha muito curta é rejeitada
    Dado um usuário autenticado com e-mail "u1@escola.com" e senha "atual123"
    Quando troco a senha atual "atual123" pela nova senha "123"
    Então a resposta deve ter status 400

  Cenário: Troca de senha com a senha atual incorreta é rejeitada
    Dado um usuário autenticado com e-mail "u2@escola.com" e senha "atual123"
    Quando troco a senha atual "errada00" pela nova senha "novaSenha"
    Então a resposta deve ter status 400

  # Caixa branca: estrutura do token gerado
  Cenário: Token JWT gerado contém as claims do usuário
    Dado um usuário cadastrado com e-mail "claims@escola.com" e senha "senha123" com acesso à IA
    Quando gero um token JWT para esse usuário
    Então o token deve conter a claim "isAiEnabled" igual a "True"
