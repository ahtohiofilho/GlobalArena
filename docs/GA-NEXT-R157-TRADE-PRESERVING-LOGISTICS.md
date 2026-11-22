# GA-Next R157 — Comparação logística com comércio remanescente

## Diagnóstico do R156

O cenário R156 deslocou um produtor de grãos e mostrou que as arestas usadas caíram de 49 (cenário A) para zero (B e C). A perda de produção foi real, mas **não existia frete comercial sobre o qual o serviço operacional pudesse agir**. Por isso o ganho de serviço B→C foi zero. Não se deve modificar a fórmula de frete para forçar outro resultado: faltava representatividade da *amostra econômica*.

## Estratégia do R157

- Preservar semente de mundo `20261012`, geografia Goldberg G(18,0), infraestrutura natural, políticas de oferta/consumo, demanda, preço, impostos e parâmetros R153.
- Selecionar outro trabalhador **real e já produtivo** no mesmo nó inicial: o segundo produtor da CIV1, cujo bem na fixture é madeira, não grãos.
- Aplicar a mudança `Producing → Other / LandTransport` a este indivíduo, preservando todos os demais atributos e trabalhadores; comparar A/B/C pela R154.
- Validar que **B ainda utiliza arestas reais**, com carga positiva, antes de interpretar qualquer benefício.
- Contabilizar `UsedOperationalCrossings`: intersecção do conjunto de arestas efetivamente usadas para transporte no cenário com aquelas que têm serviço operacional positivo calculado de trabalhadores físicos. Não assumir que qualquer trabalho logístico equivale automaticamente a trabalhar uma rota comercial.
- Exportar o novo relatório numérico exato em `r157-trade-preserving.json`; preservar o resultado negativo do R156 como controle independente.

## Gate de interpretação

No relatório R157, observar:
1. **B usa arestas comerciais?** Se não, o cenário ainda não serve para medir logística, e a seleção deverá ser revista sem mexer na matemática.
2. **B ou C usam arestas com serviço operacional?** Se não, é uma questão de incidência física/rota escolhida, não evidência de que o serviço logístico não gera valor.
3. **C difere de B nos custos, receita ou rotas?** Só isso permite mensurar o benefício econômico da fórmula sombra; `B→C=0` pode ser legítimo mesmo com serviço positivo se as rotas não se cruzarem.
4. **A→C supera A?** Avaliar ganhos/lacunas reais. Não assumir a priori retorno positivo nem reparametrizar para garanti-lo.

## Limites

R157 continua estritamente **precommit**: não altera fretes reais, estoque, trabalhadores no jogo, preços, receitas, diário, turnos ou UI. Não introduz congestionamento. A avaliação compara **realocação de trabalhador existente**, não a entrada de novo trabalhador pelo crescimento populacional; esta última exige uma hipótese de oferta e demanda adicionais e um contrato de cenário independente.

## Evidência real da primeira execução do gate (commit 5c01dbf)

O workflow específico publicou o JSON em
`https://github.com/ahtohiofilho/GlobalArena/actions/runs/44996939267/artifacts/14743214826`.
Os dados abaixo **foram extraídos da execução**, não interpolados nem utilizados para ajustar os coeficientes:

| Métrica | A: produtores | B: sem desconto | C: com serviço |
|---|---:|---:|---:|
| Mercadorias | 5,000000 | 4,000000 | 4,000000 |
| Arestas usadas | 49 | 49 | 49 |
| Carga atravessando arestas (micro) | 16493890 | 39445441 | 39483661 |
| Crédito coletivo (moeda) | 7,648182 | 6,559651468399 | 6,560096531473 |

- Transferência A→B: **−1,088530531601**.
- Serviço B→C: **+0,000445063074**.
- Saldo A→C: **−1,088085468527**.
- Serviço físico total: 1.000.000 unidades micro, distribuído por **2 arestas**; em B e C, **1 das 49 arestas comerciais efetivamente utilizadas** cruza o trabalho dos operadores.
- O resultado confirma que o benefício econômico operacional existe, mas é muito menor que o custo de oportunidade de retirar um trabalhador da produção neste estágio.

### Correção de procedência após o primeiro relatório

O primeiro arquivo também rotulava uma ligação de A como operada porque reutilizava a lista de serviço projetada para B, mesmo que **A não tivesse qualquer operador**. O projetor passou a registrar zero arestas efetivamente operadas no cenário A, mantendo os valores monetários e as rotas intactos. Teste de regressão acrescentado para impedir nova atribuição indevida.
