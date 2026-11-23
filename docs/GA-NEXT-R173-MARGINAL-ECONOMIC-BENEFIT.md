# GA-Next R173 — Valor econômico da redistribuição marginal

## Objetivo
A R172 revelou que dois produtores podem exigir 5 a 13 passagens para um ponto fixo. A R173 mede se isso altera substancialmente a receita, em vez de presumir que quantidade deslocada equivale a benefício econômico.

## Comparação pareada
Os dois planos usam exatamente os mesmos trabalhadores, oferta, demanda, orçamento, transporte e cotações de planejamento:
1. `FoundationPhysicalMarginalMarketDistributor.Distribute` — distribuição inicial progressiva.
2. `FoundationPhysicalGlobalMarginalCoordinator.Coordinate` — a mesma primeira distribuição, seguida de realocações até estabilizar.

A comparação usa **a fórmula monetária canônica do reconciliador**, por mercado físico (nó, mercadoria):
`p = floor(1_000_000 * sqrt(D/Q))` e `grossScaled = Q * p * 10_000`.
Não há reprecificação por congestionamento, carteiras, estoque ou novos produtores.

Os testes registram `firstGrossScaled`, `finalGrossScaled`, `grossDeltaScaled` e `grossDeltaBasisPoints`, além de passagens, volume reatribuído e tempo. Os cálculos são apenas diagnósticos e não alteram a liquidação autoritativa.

## Limitações
- Receita bruta **não** é lucro, bem-estar ou eficiência integrada. Uma queda de receita bruta não significa por si só que uma realocação com frete menor seja economicamente pior.
- Os cenários iniciais têm dois produtores e costumam não percorrer arestas fisicamente. Para julgar a política de reequilíbrio, precisamos complementar com frete efetivamente percorrido e múltiplas fontes de demanda, antes de mudar o algoritmo.
- O orçamento de 128 passagens é exclusivo do cenário de auditoria; o teto de produção permanece 64.
- A regra universal é `P0=1` no caixa efetivo. As cotações do planejador são dados auxiliares; nunca se devem tratar como receita financeira.
- Não aplicar limiar monetário arbitrário nem aproximação matemática sem decisão expressa sobre a economia do jogo.

## Critério de decisão
Se as passagens extras gerarem ganhos brutos irrisórios em cenários representativos e custos operacionais relevantes, propor simplificação transparente da regra de redistribuição, sem retornar ao acoplamento de congestionamento. Caso contrário, preservar o benefício e procurar simplificação estrutural controlada.
