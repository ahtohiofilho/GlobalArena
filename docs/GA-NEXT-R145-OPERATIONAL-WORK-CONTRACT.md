# GA-Next R145 — Contrato de trabalho operacional (primeira etapa)

## Decisão aprovada

O esforço físico do trabalhador é um insumo universal e conservado por turno. O trabalhador continua localizado em um nó físico fino, mesmo quando participa de uma obra ou opera uma ligação. A camada operacional distingue alocação terrestre (arestas terra–terra) e portuária (arestas terra–mar): os dois orçamentos de trabalho são independentes.

Em cada nó e em cada grupo operacional, o esforço disponível é distribuído **proporcionalmente ao peso de capital operacional efetivo** das arestas incidentes. Uma ligação sem capital monetário não recebe peso zero: possui uma contribuição natural positiva. O peso de capital é medido numa unidade operacional comum, não se confundindo necessariamente com valores brutos em dinheiro.

Para um grupo com esforço L e pesos W:

`W_i = W_natural_i + W_investido_i`

`L_i = L * W_i / sum(W)`

O algoritmo é determinístico e inteiro. Distribui resíduos de arredondamento aos maiores restos, com desempate pelo identificador canônico da aresta. Assim, a soma das parcelas é exatamente L (quando existe ao menos uma aresta elegível). Caso contrário, o esforço permanece não alocado; nunca é fabricado.

A alocação é **independente** de preços, volumes de carga, demanda, tarifas e fluxo comercial. Não há otimização de trabalhadores por rota e não há qualquer mecanismo de congestionamento.

## Fronteiras desta entrega

A R145 introduz um **projetor puro** `FoundationOperationalWorkAllocator` e testes matemáticos. As arestas fornecidas por um chamador futuro precisam ser verificadas contra a topologia física e classificadas como LAND ou PORT; esta classe não inventa arestas. Ela também não muda o censo, ordens do jogador, caixa, construção, frete ou o fechamento econômico.

A ligação do esforço aos indivíduos reais, a validação da incidência física, a transformação de capital monetário investido em peso operacional, a produtividade do esforço, a participação dos dois extremos e a política para ausência de trabalho serão incrementos posteriores, sujeitos a testes com o jogo.

### Critério de segurança

O frete unitário físico vigente continua sem dependência de carga ou congestionamento. Não presumir que os pesos operacionais são automaticamente equivalentes às cotações monetárias. Qualquer alteração na eficiência de transporte exigirá uma fórmula explícita evitando contar o mesmo capital duas vezes, além de um gate econômico específico.
