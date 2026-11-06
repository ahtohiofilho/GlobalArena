# GA-ECON-MARKET-DEFAULT-01K — encerramento real da custódia do destinatário

**Data:** 06/11/2026  
**Base:** PR #290, GA-ECON-MARKET-DEFAULT-01J, HEAD `a6f62f08e8f890b461b5ad8825dc074c5c77f492` (FAST e checkpoint multiplataforma aprovados).  
**Status:** motor canônico versionado, opt-in, em Draft. Sem ativação no gameplay principal, venda, transporte remoto, propriedade legal ou receita.

## Decisão

01J implementou o primeiro movimento físico de `MarketOffer` para `LocalRecipientCustody`, mas os bens permaneciam ali sem regra para uso ou retorno. 01K completa o **ciclo material**, sem fingir que a custódia identifica um comprador ou comprova transação monetária.

São duas operações mutuamente exclusivas para **toda** a quantidade de uma custódia recebida:

1. **`ReturnLocalRecipientCustody(commitmentActionOrdinal)`**: desloca exatamente os mesmos lotes da custódia do destinatário para o estoque livre **no tile original**, preservando origem FIFO e histórico da transferência anterior. Credita o estoque local uma única vez; nunca torna vivo o compromisso antigo e não credita receita.
2. **`UseLocalRecipientMaterial(commitmentActionOrdinal, FinalPhysicalUse|IntermediateInputUse)`**: exige que o grupo produtivo original ainda tenha trabalhadores ativos no tile; remove os lotes **da custódia exclusiva recebida**, gerando recibo de uso físico final/intermediário, com ordinal de origem e transferência, atividade, número atual de trabalhadores, quantidade, proveniência e hashes antes/depois. Não cria edifício ou produto acabado fictício, não transfere valor monetário e não declara venda.

As ações canônicas novas são `return_local_recipient_custody` e `use_local_recipient_material`. A origem `CanonicalPhysicalStockCommitment` continua com `Transferred=true` para registrar a entrega histórica; um recibo `CanonicalExecutedLocalRecipientCustodyDisposition` terminal identifica a devolução ou o uso. Os lotes `LocalRecipientCustody` deixam de existir depois de qualquer conclusão. Não se permite conclusão dupla, retorno após consumo, venda presumida ou revenda automática antes do retorno ao estoque livre.

## Conciliação

O auditor `CanonicalMarketExecutedPhysicalCoverage` diferencia os estados **pendente, oferecido/liberado, usado diretamente, entregue e ainda em custódia, devolvido pelo destinatário, usado pelo destinatário e retido**.

Por tile/mercadoria:
- oferta histórica é igual à soma dos destinos finais e saldos ainda pendentes, cada compromisso contado uma única vez;
- saldo disponível atual é **estoque livre + `MarketOffer` pendente** — não soma devoluções históricas duas vezes;
- `TotalTransferredToRecipientRaw` descreve apenas a **custódia ativa**;
- `TotalReturnedFromRecipientRaw` é devolução histórica efetivamente registrada;
- `TotalExecutedRecipientMaterialRaw` é material realmente usado pelo destinatário;
- `TotalExecutedPhysicalMaterialRaw` agrega usos físicos diretos e usos pelo destinatário, sem afirmar vendas;
- retenção deliberada permanece isolada e inviolada.

A validação canônica reconstrói as ações em ordem e cruza recibos, compromissos, titularidade da custódia dos lotes e estado; não se registra dinheiro, imposto, salário, crédito ou poder de compra abstrato `N`.

## Versionamento e compatibilidade

**V39** (sem tesouraria) e **V40** (com tesouraria dormente) são envelopes ativados exclusivamente quando as ações de encerramento existirem. Saves V9–V38 continuam em suas representações anteriores, com hashes/replays intocados. `RestoreSaveJson` rejeita V39/V40 se não houver evento de encerramento, rejeita esse evento num envelope V37/V38 ou anterior, e reproduz estritamente cada operação física. O payload por tecnologia e a estrutura do `WorldState` são mantidos.

Não ampliar as opções de UI mapa-primeiro e não promover à economia jogável antes de disponibilizar os dois caminhos como parte de uma transação comercial real quando aplicável.

## QA

`GaEconMarketDefault01kTests` valida cinco famílias de cenários:
- execução física por grupo ainda ativo, recibo e origem, sem consumo duplo de estoque;
- devolução dos mesmos lotes, reconciliação, nova oferta apenas via compromisso novo;
- coexistência de usos, devoluções e custódia pendente; conservação e replay;
- proibição de segunda conclusão e tentativa de usar, liberar ou reenviar uma custódia encerrada;
- saves V37 bit a bit preservados e rejeição de reclassificação de V37 em V39 e vice-versa.

**Gate de liberação:** FAST neste HEAD, checkpoint de kernel e hash nos três sistemas antes de qualquer promoção; PR permanece Draft sem merge.

## Próxima dependência

O percurso físico de madeira/pedra local está agora preparado para o **contrato de titularidade econômica e liquidação real**, que deverá identificar quem oferece, quem recebe, quais entregas são efetivas e como receitas marginais ingressam institucionalmente, sem dinheiro fictício nas populações. Não existe até aqui uma venda comercial liquidada. Transferência distante exige execução comprovada de transporte; alimento agregado legado não entra no ledger real de seis nutrientes.
