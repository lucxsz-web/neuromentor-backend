# language: pt
@rag @unidade
Funcionalidade: Pré-processamento de texto para RAG (caixa branca)
  Como engenheiro de qualidade
  Quero validar a limpeza e a seleção de trechos do material
  Para garantir a qualidade do contexto enviado à IA (mitiga risco de RAG)

  Cenário: Limpeza remove números de página e linhas em branco
    Dado o texto bruto:
      """
      Introdução à Biologia

      A célula é a unidade básica da vida.

      Page 1
      12
      """
    Quando aplico a limpeza de texto
    Então o texto limpo deve conter "A célula é a unidade básica da vida."
    E o texto limpo não deve conter "Page 1"

  Cenário: Limpeza remove cabeçalhos/rodapés repetidos
    Dado o texto bruto:
      """
      CABECALHO REPETIDO
      Conteúdo relevante da primeira parte do material.
      CABECALHO REPETIDO
      Conteúdo relevante da segunda parte do material.
      CABECALHO REPETIDO
      Conteúdo relevante da terceira parte do material.
      CABECALHO REPETIDO
      """
    Quando aplico a limpeza de texto
    Então o texto limpo não deve conter "CABECALHO REPETIDO"
    E o texto limpo deve conter "Conteúdo relevante da primeira parte do material."

  Cenário: Extração de trecho prioriza parágrafo com mais palavras-chave
    Dado o texto bruto:
      """
      Este parágrafo fala sobre culinária e receitas diversas.

      A fotossíntese ocorre nos cloroplastos usando clorofila e luz solar para produzir glicose.
      """
    Quando extraio o trecho para o módulo "Fotossíntese" com os conceitos "clorofila, cloroplastos"
    Então o trecho extraído deve conter "fotossíntese ocorre nos cloroplastos"
