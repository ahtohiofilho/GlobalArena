# GA-ECON-BENEFICIARY-01 — destinatário operacional local autenticado

**Data:** 2026-11-06. **Base:** PR #272, `GA-ECON-OFFER-DELIVERY-01`. **Status:** uso físico efetivamente executado, limitado ao mesmo tile; não é venda ou titularidade privada.

## Objetivo e escopo

A etapa anterior autentica o uso local de madeira/pedra de uma custódia de oferta, mas não indica **qual grupo de pessoas reais** o recebeu operacionalmente. Este incremento acrescenta um destinatário observável no estado canônico: `PhysicalWorkerAllocation(TileId, Activity, Workers)`. Uma pessoa continua simultaneamente consumidora e produtora; não há cidade, carteira, domicílio, comprador inventado nem unidade administrativa fictícia.

`UseCommittedLocalMaterialForWorkforce(commitmentActionOrdinal, beneficiaryActivity, kind)`:
1. exige uma oferta física `MarketOffer` ativa, com madeira ou pedra já retirada do estoque livre;
2. localiza o tile original da reserva e exige `Workers > 0` atribuídos à atividade beneficiária **nesse mesmo tile**;
3. consome integralmente o lote da custódia exclusiva, sem debitar novamente o estoque físico livre;
4. registra no evento `use_committed_material_for_workers` o tile de origem/destino, `ToJob = Activity`, `Workers = número real efetivamente alocado`, `CommitmentOrdinal`, mercadoria, quantidade, uso classificado e hashes antes/depois;
5. emite o `CanonicalExecutedCommittedMaterialUse` com `BeneficiaryActivity` e `BeneficiaryWorkers` para auditoria e replay.

A verificação cruzada exige que cada execução encontre exatamente uma ação no journal e um compromisso realmente consumido. Uma liberação após consumo, nova execução da mesma oferta, finalidade inválida, atividade inexistente ou tile sem grupo são rejeitados sem mutação.

**Não há destinatário remoto.** A API não aceita um segundo tile, nem calcula frete ou tempo de viagem. Uma futura entrega intertile precisa de ordens e comprovação física de transporte.

## Compatibilidade e prova

- Saves com `use_committed_material_for_workers` utilizam envelope **35** ou **36** (com tesouro dormente). Versões **9–34** mantêm formato e bytes; nenhum campo novo obrigatório foi adicionado ao modelo `CanonicalPhysicalAction`: os campos preexistentes `ToJob` e `Workers` carregam a população registrada no novo evento.
- O replay reexecuta o mesmo comando e **reavalia** a presença da atividade e a quantidade real de trabalhadores **no momento do ato**; uma alteração inconsistente da contagem/atividade é rejeitada.
- Os checksums SHA256 de save protegem integridade acidental, mas não são uma assinatura privada de identidade ou transação comercial. Um grupo pode mudar de composição ao longo dos turnos, e a prova identifica grupo/atividade no instante da execução, **não indivíduos**.
- `ProvesLocalWorkforceGroup = true`, `ProvesNamedIndividualOrLegalBuyer = false`, `ProvesRemoteDelivery = false`, `ProvesMonetarySale = false`.
- `UseCommittedLocalMaterial` legado mantém o evento e envelope 33/34 exatamente como antes; commits 31/32 permanecem compatíveis.

## Diretrizes intocadas

A economia futura permanece `D=P*N`, `x=S/D`, `p=f(x)`, `R_ref=p*D`; N é poder de compra abstrato, e D não é teto físico de consumo. Não foi criada liquidação comercial, banca, microcrédito, dinheiro, imposto ou salário. A nova rotina não produz mercadorias nem instala equipamentos. O alvo continua **seis alimentos físicos distintos** e **cadência econômica por mobilidade**, preservando os saves legados com `AdaptiveThirty`.

## QA incremental

A classe `GaEconBeneficiary01Tests` cobre identidade real e contagem do grupo, falta de atividade, consumo e liberação duplicados, replay exato v35, retrocompatibilidade v31/v33, adulteração do journal, modo cash sem novas operações de caixa e entradas inválidas.

FAST compila Godot Release e executa esse novo conjunto junto ao baseline. O checkpoint multiplataforma do #271 **não** cobre automaticamente o HEAD deste PR. Antes do merge: checkpoint e `ga-ci-full`, sem transformar jobs skipped em sucesso.

## Próxima decisão de produto

Uma **entrega comercial** exigirá identidade econômica/titularidade genuína, credenciamento do destinatário, custódia sob seu controle, destino físico e tempo/custo de transporte quando remota. Este incremento não inventa uma pessoa ou titular jurídico a partir de `PhysicalWorkerAllocation`; fornece apenas a primeira prova de **localização e grupo beneficiário** para usos materiais realmente executados.
