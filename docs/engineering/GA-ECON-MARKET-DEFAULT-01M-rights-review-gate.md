# GA-ECON-MARKET-DEFAULT-01M — revisão externa de direitos institucionais por lote local

**Data:** 06/11/2026. **Base:** GA-ECON-MARKET-DEFAULT-01L / PR #292, HEAD `6b2b87016fb7ad3796e202224cfbec444f9c0b51` (FAST confirmado 19/19). **Situação:** preflight somente leitura, opt-in; PR Draft sem merge.

## Objetivo e separações obrigatórias

A entrega física de 01J/01K identifica **qual lote**, **qual tile**, **qual grupo** e **qual movimentação real**. O 01L registra contrapartes institucionais expressamente propostas; porém uma declaração unilateral não autoriza a instituição a vender ou receber o bem. O 01M adiciona uma revisão *externamente fornecida* de duas permissões independentes, com **vigência e escopo determinados**, vinculadas ao mundo e à operação exata.

Uma permissão `SourceOfferDisposition` refere-se à instituição **de origem** do lote; a permissão `RecipientPhysicalAcceptance` refere-se à instituição **receptora**. Não existe atalho que converta a existência de trabalhadores, um estoque ou uma instituição textual em direito de propriedade ou compra.

## Contrato de revisão

`CanonicalLocalEconomicRightClaim`: `AuthorizationId`, `AuthorizationVersion`, `CurrentWorldHashHex`, `PhysicalTransferActionOrdinal`, `PhysicalTransferAfterWorldHashHex`, `AgreementId`, `AgreementVersion`, `Scope`, `ProposedRightsHolder`, `ProposedIssuingAuthority`, `EffectiveFromMovementTurn` e `EffectiveThroughMovementTurn`.

O `CanonicalLocalEconomicRightsReviewPreflight.FromCurrent(session, parties01L, proposedRights, reviewer)`:

1. Exige a mesma sessão e snapshot do preflight 01L e reconstrói 01H/01J/01K para provar a entrega física.
2. Rejeita autorização sem identidade única, versão, intervalo de validade que inclua o turno atual, correspondência com o **hash atual do mundo**, hash do handoff, ordinal de transferência e agreement versionado.
3. Exige um direito de origem para o actor expressamente declarado como origem, e um direito de aceite para a contraparte expressamente declarada como receptora. Não aceita dois direitos do mesmo escopo para um único lote nem duplicidade de `AuthorizationId`.
4. Exige um **emissor explícito e distinto das duas contrapartes**, do tipo Community, Cooperative ou PublicAuthority; rejeita ator Unspecified, CommercialOperator emissor, IDs vazios e contradição de categoria institucional. Isso não supõe que exista uma autoridade estatal no Neolítico: o catálogo permite conselho comunitário ou cooperativa, mas **não cria automaticamente nenhum deles**.
5. Exige `ICanonicalLocalEconomicRightsReviewer` implementado pelo host. O kernel **não contém revisor que sempre aceite** nem reconhece claims sem callback. O revisor recebe simultaneamente o claim e a atribuição física. Recusa acarreta *fail-closed*. Os stubs nos testes são exclusivamente instrumentação de QA, não autoridade de produção.
6. Mantém as autorizações parciais como **parciais**. Somente ambas as permissões revisadas geram `HasReviewedBothProposedRights=true`, sem transformar isso em recibo de venda.
7. Proíbe propor autorização sobre material **já consumido ou devolvido**. Uma mudança física ou de hash invalida o preflight; a revogação do revisor invalida `VerifyIndependent()` na próxima consulta.
8. Retorna uma visão imutável de direitos revisados e quantidades fisicamente ainda sob custódia, **sem transferir lotes, alterar sessão, save, hash, moedas ou UI**.

## Limite semântico crítico

**Revisão de direitos declarados NÃO é autenticação judicial/institucional de titularidade legal.** A interface permite ao host conferir evidências externas e preparar auditoria; não lhe dá uma autoridade real por construção. Não existem aqui contrato juridicamente celebrado, transmissão da propriedade, preço executado, liquidação financeira, geração de dinheiro, crédito, imposto ou receita estatal.

Os indicadores do objeto permanecem deliberadamente **false** para `AuthenticatesLegallyEnforceableTitle`, `IssuesBindingSalesContracts`, `ExecutesCustodyOrTitleChanges`, `AuthorizesMoneySettlementOrTaxes`, `CreatesBuyerCashOrCredit`, `MutatesWorldStateOrCanonicalSave`.

Qualquer futuro mecanismo de transferência de titularidade precisa de um **evento canônico separado**, com prova da autoridade efetivamente implementada, custódia física ainda ativa e uma política de direitos verificável. A receita monetária, quando existir, deve respeitar calibração marginal e poder de compra abstrato, nunca carteira fictícia dos moradores.

## QA

`GaEconMarketDefault01mTests`: aprovação isolada das duas permissões num handoff real, consistência após replay do save antigo, direitos ausentes não supridos, recusa/revogação externa, invalidação após devolução, falsificação de hashes/ordinais/agreement/vigência/dono/autoridade e duplicidade de claims/escopos. O teste usa **somente** um revisor de laboratório, com validade explicitamente não jurídica.

O PR fica Draft e empilhado sobre #292. Gates: FAST direcionado no HEAD; nenhum workflow `skipped` é considerado aprovado. Não introduzir uma migração do save V9–V40, nem regressões de replay.

## Transportes

A localização de uma infraestrutura entre tiles é uma **ligação lógica entre centros** que cruza a fronteira compartilhada, não uma estrada seguindo o perímetro dos polígonos. A `StrategicEdgeId` é identidade de conexão; `EconomicTransportLogicalLink` permite modelagem de nós, linhas, escalas e diferentes tecnologias sem confundí-las com um traçado visual rigidamente perpendicular. Nada em 01M cria transferência remota sem prova de transporte.
