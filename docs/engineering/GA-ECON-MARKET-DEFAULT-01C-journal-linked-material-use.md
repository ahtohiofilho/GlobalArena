# GA-ECON-MARKET-DEFAULT-01C — execução local autenticada a partir do plano físico

**Data:** 06/11/2026.  
**Base:** GA-ECON-MARKET-DEFAULT-01B (PR #282) e GA-CANON-DIRECTIVES-01.  
**Status:** implementação CANÔNICA de **uso físico local de madeira/pedra** reaproveitando o journal existente V33/V34. NÃO há venda econômica, clearing automático, reconhecimento de receitas, novo ledger alimentar ou modalidade de frete. PR Draft sem merge.

## Objetivo

Conectar o preflight de destino a **uma ação real** já garantida pelo motor: o uso final/intermediário de madeira ou pedra fisicamente segregada em `MarketOffer`, aplicado a grupo produtivo local efetivamente existente. O preflight não precisa mais ficar sem executor, mas não pode promover uma operação real de material a venda fictícia.

## Contrato executável

`CanonicalLocalMarketMaterialDestinationExecutor.Execute(session, proposal, commitmentActionOrdinal, useKind)`:

1. exige que `proposal` pertença à **mesma instância de sessão**, ao mesmo hash e à mesma contagem de eventos; o novo `RequireCurrent(session)` fecha o problema de propostas vindas de sessões distintas com estado igual;
2. autoriza **uma única proposta** cujo propósito é `WorkforceMaterialUse`, mercadoria 2 (pedra) ou 3 (madeira), fonte=destino e atividade beneficiária comprovada;
3. exige casamento exato com compromisso `MarketOffer` vivo, do mesmo tile, mercadoria e **quantidade integral**; não baixa diretamente saldo livre, nem consome retenção deliberada, nem aceita propostas parciais sobre lote comprometido maior;
4. delega a única mutação a `CanonicalPhysicalTacticalSession.UseCommittedLocalMaterialForWorkforce`, que já faz baixa de custódia sem segundo débito de estoque, atribuição FIFO, evento canônico `use_committed_material_for_workers`, hash, replay V33/V34 e validação de grupo alocado;
5. devolve o próprio `CanonicalExecutedCommittedMaterialUse`, com ordinal, hash anterior/posterior, proveniência material e pessoas reais beneficiárias. Não cria outra camada de journal ou ações repetidas.

A execução rejeita dupla utilização, compromisso inexistente/consumido, pedido sobre estoque livre sem compromisso e uso de alimento agregado. Tentativas rejeitadas não alteram `WorldState`, hash ou save.

## Limite de interpretação

**Fato comprovado:** quantidade real de pedra/madeira foi fisicamente utilizada por um grupo existente no tile. **Não comprovado:** obra concluída, manufatura final, comprador jurídico, transferência comercial, preço, venda, transporte, imposto, valor de `N`, nutrição, absorção total de mercadorias não retidas.

A política de oferta automática continua **alvo**. Este incremento NÃO cria venda automática nem atribui receitas. A ligação com preflight exige reserva explícita anterior e, portanto, não resolve ainda a eliminação do micromanagement de ofertas. O passo para execução comercial automática precisa criar uma transição versionada de `produção → compromisso → destino físico → receita institucional autorizada`, com liquidação de todos os bens elegíveis, transporte real quando indispensável e função marginal calibrada, sem `min(S,D)`.

**Nutrição:** o caminho para `ResidentFoodUse` permanece indisponível aqui. Só ativar após ledger de seis alimentos, consumos realmente atestados e curva `Q=A(f)×V` calibrada; não reintroduzir `FoodDemandRawPerWorker` como meta fisiológica.

## Critérios de aceite

`GaEconMarketDefault01cTests`: execução válida com journal, soma de atribuições físicas, preservação de ciclo, ausência de segunda baixa e de venda; save/replay idêntico; compromisso incompatível; retenção; proposta com quantidade diferente; múltiplas propostas; outra sessão; proposta obsoleta; alimento agregado; reutilização da proposta/dupla execução recusada. FAST no HEAD; homologação multiplataforma posteriormente. PR Draft sem merge.
