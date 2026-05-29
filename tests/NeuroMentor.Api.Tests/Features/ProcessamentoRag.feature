# language: pt
@rag @ts02
Funcionalidade: Processamento RAG — geração e curadoria de módulos (TS02)
  Como professor
  Quero que o material seja transformado em módulos de aprendizagem
  E poder aprovar ou rejeitar cada módulo (TC02)

  Cenário: Geração de módulos a partir do material processado (TC02)
    Dado um professor autenticado com acesso à IA
    E uma aula com o texto "Conteúdo sobre fotossíntese, clorofila e respiração celular."
    E a IA responderá com 3 módulos de aprendizagem
    Quando solicito a geração de módulos para a aula
    Então a resposta deve ter status 200
    E devem existir 3 módulos persistidos para a aula
    E todos os módulos devem estar com status "pending"

  # Transição de estados: Pending -> Approved
  Cenário: Professor aprova um módulo gerado
    Dado uma aula com um módulo pendente
    Quando defino o status do módulo como "Approved"
    Então a resposta deve ter status 200
    E o módulo deve estar com status "approved"

  Cenário: Status de módulo inválido é rejeitado
    Dado uma aula com um módulo pendente
    Quando defino o status do módulo como "Concluido"
    Então a resposta deve ter status 400

  Cenário: Geração de módulos sem acesso à IA é proibida
    Dado um professor autenticado sem acesso à IA
    E uma aula com o texto "Qualquer material"
    Quando solicito a geração de módulos para a aula
    Então a resposta deve ter status 403
