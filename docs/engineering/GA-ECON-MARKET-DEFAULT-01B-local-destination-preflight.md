# GA-ECON-MARKET-DEFAULT-01B — Pré-validação de destinatário físico local

**Data:** 2026-11-06.  
**Base:** PR #281, `GA-ECON-MARKET-DEFAULT-01A` e `GA-CANON-DIRECTIVES-01`.  
**Estado:** gate de destino **somente leitura**. Ainda não há venda, baixa, liquidação, ingestão nem transporte executados. PR Draft, sem merge.

## Política canônica preservada

Toda produção elegível será ofertada e absorvida no ciclo comercial, salvo retenção explicitamente deliberada ou insumo já comprometido; a pressão `D=P*N` não pode virar quota de venda ou alimento. A receita deve responder a função de absorção marginal decrescente, ainda não calibrada. O reconhecimento físico de venda requer um destinatário verdadeiro; o transporte entre tiles requer serviços efetivamente disponíveis, custo, direitos e custódia autênticos. **Candidato não é venda nem consumo.**

## Implementação 01B

`CanonicalLocalMarketDestinationPreflight.FromCurrent(session, proposals)` cruza a disponibilidade já reconciliada em `CanonicalDefaultMarketPoolPreflight` com os registros reais de pessoas/trabalhadores. Para cada proposta de destino físico, exige:

- fonte e destino explicitamente identificados, commodity reconhecida e quantidade inteira positiva;
- no momento, **fonte = destino**, porque nenhum serviço de frete autenticado foi informado à API; vizinhança geográfica sozinha não autoriza deslocamento;
- destinatário real: para alimento agregado legado (`CommodityId=1`), pessoas residentes; para madeira/pedra (`3/2`), um grupo produtivo realmente alocado no tile;
- propósito compatível com a mercadoria: `ResidentFoodUse` apenas para alimento e `WorkforceMaterialUse` apenas para materiais; sem transformar material em ingestão;
- identidades de propostas únicas e soma das quantidades atribuídas por `(tile, commodity)` menor ou igual ao estoque elegível, **incluindo a oferta já comprometida apenas uma vez**, sem invadir as retenções;
- testemunho do mesmo hash/eventos para recusar propostas obsoletas após qualquer ação; relatório determinístico e sem mutação do estado.

O contrato **não impõe** `FoodDemandRawPerWorker` ou `min(S,D)` às quantidades do plano; o único teto é o estoque físico real sem compromissos incompatíveis. Tampouco inventa consumidores para absorver excedentes. A API funciona sobre um **conjunto de candidatos**, não prova que todos os bens elegíveis receberam destinação.

## Fronteira explícita de evidência

`ProvesExecutedFinalConsumption=false`; `ProvesExecutedSale=false`; `AuthorizesPaymentOrRevenue=false`; `ChangesWorldStateOrSave=false`. Nenhuma dedução de estoque ou receita foi registrada. O estado de oferta por padrão continua **alvo**, não comportamento já ativado.

A demonstração técnica inclui `GaEconMarketDefault01bTests`: dois materiais em grupos reais, rejeição de quantidade superior ao estoque, duplicação de destino, população inexistente, tipo de uso incompatível, frete sem certificado, retenção integral, release, save/replay/hash, invalidação de documento antigo e alimentos agregados sem fabricação de seis variedades.

## Próximo gate: 01C

Promover propostas validadas a **eventos de uso/destinação realmente executados**, com escolha e conservação de lotes canônicos, destinatário final autenticado, transferências apenas com serviços de frete verificados, eventual recibo financeiro institucional e replay versionado. O fechamento deve falhar se uma quantidade obrigatoriamente ofertada não tiver destino físico legítimo — não pode apagar nem declarar vendida mercadoria sem destino. O tratamento de absorção abstrata/invisível deve ter contrapartida material explicitamente definida antes de ativação. A curva de receita e a origem de N ainda requerem calibração. O ledger nutricional de seis alimentos é incremento independente, só integrado depois de execução real por variedade.
