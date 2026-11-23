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

## Resultados observados no CI Ubuntu — 2026-11-23

Cinco testes diagnósticos concluídos com sucesso (2 com frete real, 3 herdados da R172/R173).

| Saída por produtor (micro) | Passagens totais | Bruto inicial (scaled) | Bruto final (scaled) | Frete inicial (scaled) | Frete final (scaled) | Saldo inicial bruto-frete (scaled) | Saldo final (scaled) | Δ líquido (scaled) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 5.000.000 | 3 | 109514373000000000 | 109503707000000000 | 4768051000000000 | 4009807000000000 | 104746322000000000 | 105493900000000000 | +747578000000000 |
| 50.000.000 | 2 | 346119124404200000 | 344578792000000000 | 43787667000000000 | 35142792000000000 | 302331457404200000 | 309436000000000000 | +7104542595800000 |

Percentuais aproximados sobre saldo inicial: **+0,714%** e **+2,350%**, respectivamente. A coluna inteira de `netDeltaBasisPoints` mostrou 71 e 234 bp (truncados). Em ambos os planos, **uma aresta física realmente foi utilizada**.

### Interpretação e decisão
- Ao contrário dos casos sem carga de frete, a redistribuição global reduziu o frete de modo importante. A receita bruta isolada **caiu** nos dois cenários, porém o saldo após frete **subiu**.
- A passagem final que comprova estabilidade não muda volumes. Assim, 2 passagens totais significam **uma passagem de ajuste + uma de certificação**; 3 passagens totais significam duas de ajuste + uma de certificação.
- O benefício de ajuste nestes dois exemplos é relevante. A evidência **não** apoia descartar toda a redistribuição nem dispensar a conservação, mas sugere investigar um procedimento de ajuste de baixa iteração, com vantagem econômica mensurada.
- O cenário tem poucos trabalhadores e apenas uma aresta; ainda não comprova o comportamento em mundos com milhares de nós ou fretes heterogêneos. Tempos no runner não são critério de desempenho final.
- Nenhum valor monetário foi creditado em jogo, nenhum solver foi modificado e **congestionamento permanece ausente**.

**Próximo gate recomendado:** medir em cenários representativos o ganho acumulado após a primeira e segunda passagem de ajuste, comparando com o ponto fixo. Não trocar a regra econômica por limites arbitrários sem auditar se há perdas excessivas ou regressões de justiça.
