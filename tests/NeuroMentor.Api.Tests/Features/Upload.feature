# language: pt
@upload @ts01
Funcionalidade: Upload de materiais didáticos (TS01)
  Como professor
  Quero enviar materiais e ter o texto extraído
  Para que sirvam de base ao processamento RAG (TC01)

  Contexto:
    Dado um professor autenticado com acesso à IA

  Cenário: Upload de material de texto extrai o conteúdo (TC01)
    Quando faço upload do arquivo "aula-biologia.txt" com o conteúdo "Fotossintese é o processo pelo qual as plantas produzem energia."
    Então a resposta deve ter status 200
    E a aula deve ser persistida com o texto extraído
    E o tamanho de texto retornado deve ser maior que zero

  Cenário: Upload de arquivo vazio é rejeitado
    Quando faço upload de um arquivo vazio chamado "vazio.txt"
    Então a resposta deve ter status 400

  Cenário: Upload de arquivo sem texto extraível retorna não-processável
    Quando faço upload do arquivo "branco.txt" com o conteúdo "      "
    Então a resposta deve ter status 422
