# GA-Next R174 — Ganho líquido da redistribuição com frete realmente utilizado

## Questão
R172 e R173 mostraram 5 a 13 passagens com ganhos de receita bruta inferiores a 0,1% nos casos analisados, mas nenhum transporte efetivo. Precisamos verificar se os ganhos da redistribuição justificam o custo quando mercadorias realmente cruzam arestas.

## Cenários
Dois trabalhadores produtores em nós adjacentes, mais um consumidor físico não produtor de demanda elevada em um nó, todos vinculados a trabalhadores reais. Aresta terrestre real com custo físico fixo, sem imposto/tarifa, mercadoria/carga compatível. Cotações de planejamento uniformes P0=1. Produções de 5 e 50 unidades por produtor (em micro-unidades canônicas).

## Comparação
A distribuição inicial e a coordenada usam exatamente o mesmo census, demanda, preço universal e custo fixo por aresta. Medimos:
- receita bruta pelo mesmo preço inteiro `p=floor(1e6 sqrt(D/Q))`;
- frete físico escalado a partir de cada entrega que percorre a aresta;
- saldo bruto menos frete, antes de impostos (a política do cenário não adiciona impostos);
- diferença absoluta e relativa de saldo líquido, número de passagens e tempo;
- conservação de mercadorias e prova de transporte efetivo nos dois candidatos.

Estas medidas são **diagnóstico de planos candidatos**, não vendas autorizadas pelo kernel. O código da economia e do solver continua intocado.

## Critério
Comparar magnitude do ganho líquido em diferentes volumes e custos físicos antes de qualquer proposta de simplificação. Ganhos pequenos não justificam, por si, descartar uma regra que poderia ser importante em mercados maiores. Não criar novas iterações para refinar esse diagnóstico.
