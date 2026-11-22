# GA-Next R170 — Reconciliação financeira do mercado temporal

## Premissa
A R169 já introduziu oferta e demanda dos trabalhadores reais por frações de turno no **mesmo** solucionador de water-filling. Esta entrega reconcilia as transações físicas do plano com as mesmas fórmulas de preço universal, frete, pedágios e impostos que já existem na economia autoritativa, sem multiplicar trabalhadores ou criar um segundo solucionador monetário.

## Escopo implementado
- `FoundationPhysicalEconomicAccountingReconciler` aceita `FoundationPhysicalCommodityMarketInputs` nativos, distribuição marginal global e frete físico fixado, com o rol explícito de civilizações.
- A antiga entrada via censo chama o mesmo núcleo, preservando o comportamento econômico prévio.
- `FoundationTemporalFinancialReconciler.Evaluate` recebe o `WorldState` físico original, a evidência temporal R168 e a política econômica completa. Reutiliza a R169 e confere:
  - 100% da produção real vendida/consumida, sem estoque;
  - preço `sqrt(D/Q)` uniforme por mercado;
  - custo físico e pedágio extraídos da infraestrutura real e do caminho;
  - impostos pós-receita e crédito de tarifas ao titular;
  - `Receita bruta = custo físico + líquido dos vendedores + impostos + pedágios`;
  - `Créditos às civilizações + custo físico = receita bruta`.
- O resultado é determinístico, somente leitura e rejeita política tributária incompleta.

## Gate ainda pendente — autoria e publicação econômica
**Não há nova liquidação no caixa nesta entrega.** O `FoundationAuthoritativeEconomicSalesSettlementPhase` ainda deriva seus dados do censo instantâneo. Não é seguro publicar a reconciliação temporal sem antes ligar a evidência de frações do journal R167 ao evento SYSTEM do mesmo turno e ao replay independente, validando inclusive infraestrutura investida no mesmo turno.

O bloqueio da R167 contra liquidação antiga simultânea à viagem fracionária permanece obrigatório. O recibo de R170 é um candidato auditado, não autorização para creditar dinheiro. A integração ao journal econômico e ao estado de tesouraria será tratada como gate seguinte, com testes de replay e fraude.

## Governança
PR empilhada sobre R169 (#562), Draft, sem merge. Não abrir Godot neste ciclo.
