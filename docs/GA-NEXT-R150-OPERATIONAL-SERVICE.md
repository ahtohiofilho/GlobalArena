# GA-Next R150 — Trabalho operacional como insumo de serviço

Status: **projeção física experimental**, não parametrização final do frete nem nova restrição de mercado.

## Decisões herdadas

- Um trabalhador real disponibiliza esforço no nó físico, exclusivamente na atividade que está executando.
- Trabalho LAND e trabalho PORT são recursos distintos; o PORT atua no terminal terrestre.
- Em cada nó, a distribuição da capacidade de trabalho entre as arestas incidentes é proporcional ao peso operacional efetivo (base natural + capital confirmado), conforme R145.
- Ambos os terminais de uma ligação podem contribuir, mas nunca multiplicam o esforço de um trabalhador.
- O mercado homologado mantém frete unitário sem congestionamento; nenhuma nova função de saturação é introduzida.

## R150 — conversão de insumo em serviço

Depois de alocar o esforço de todos os nós, a projeção **soma o esforço da mesma aresta** e calcula seu serviço operacional:

`ServiceMicro(edge) = floor(TotalEffortMicro(edge) * ServiceMicroPerWholeEffort(edge) / 1_000_000)`

O coeficiente `ServiceMicroPerWholeEffort` é **obrigatório e explicitamente fornecido pelo chamador para cada aresta com trabalho positivo**. Não há constante implícita, frete arbitrário, demanda artificial, estoque ou parametrização histórica incorporados ao núcleo. Soma-se primeiro o trabalho dos terminais, depois faz-se uma única divisão inteira; a ordem de enumeração não altera o resultado. As contas são feitas em inteiros, com produto intermediário de precisão estendida.

Invariantes:
1. `sum(effort on crossings) + idle effort = sum(effort available at nodes)`.
2. Zero trabalhadores operacionais implica zero serviço; não existe trabalho gratuito gerado pela presença de infraestrutura.
3. LAND não emprega PORT automaticamente; nenhuma contribuição tem sua quantidade ampliada ao atravessar várias arestas.
4. Os coeficientes de produtividade não dependem de carga, preço, demanda, receita ou rotas selecionadas naquele mesmo ciclo.
5. A conversão não altera trabalhadores, ordens, caixa, investimento, frete ou o diário de eventos.

## Atenção à interpretação do capital

O peso operacional do R146 é atualmente calculado usando o **custo monetário natural de construção** e o capital investido. Isso é um parâmetro experimental: custo elevado de construção não é, necessariamente, infraestrutura natural mais produtiva. Não canonizar essa equivalência sem calibração conceitual. A R150 **não duplica** o capital multiplicando o serviço pelo investimento novamente.

## Gate seguinte

Antes de uma ligação causal ao mercado, resolver explicitamente:

- Qual unidade física de serviço representa o esforço (carga por ciclo, operações de terminal etc.).
- Como atribuir `ServiceMicroPerWholeEffort` a cada aresta a partir de atributos canônicos da infraestrutura, sem contar o investimento duas vezes.
- Como a disponibilidade operacional limita o comércio mantendo o frete unitário sem congestionamento, sem criar dependência circular nem eliminar o mercado mínimo natural inadvertidamente.

A R150 publica somente uma transformação pura e testes, incluindo caminho real a partir de ocupações confirmadas. Não é demonstração de alteração de tráfego ou redução de custos.
