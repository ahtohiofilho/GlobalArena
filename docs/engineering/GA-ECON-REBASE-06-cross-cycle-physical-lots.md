# GA-ECON-REBASE-06 — Lotes físicos entre ciclos, estoque e expedições

**Base homologada:** REBASE-05 / PR #253 / HEAD `b332ff08b84338915dbe7e836f563a2c12531804`.
**Natureza:** contabilidade física determinística por lote e época, **sem identificação física autenticada de lotes fungíveis** e sem ativação do índice `N`.

## Por que este incremento existe

REBASE-05 registra onde cada mercadoria foi produzida e onde os alimentos foram consumidos. Porém, `PhysicalTacticalEconomyRuntimeState.Stocks` é um saldo fungível capaz de reunir várias safras e fontes. No modelo atual não existe identificador físico intrínseco nem prova de qual lote foi retirado para cada consumo. Portanto a vinculação de produção anterior ao consumo presente precisa permanecer uma **convenção de contabilidade**, nunca uma evidência física nominal comprovada.

## Solução implantada

`CanonicalPhysicalLotLineageTracker` mantém em memória um livro determinístico dos saldos materiais por origem, commodity, ciclo ordinal, turno e custódia. As entradas são:

- `UnattributedOpeningStock`: saldo de partida anterior, sem origem ou período inventado;
- `ExecutedWorkerProduction`: output realmente produzido pelo motor em tile/atividade no fechamento do ciclo;
- `ExecutedExpeditionForage`: alimento realmente coletado em acampamento, sob ordem de viagem.

A custódia diferencia `LocalStock` (tile) e `ExpeditionCargo` (ordem). Uma compra de suprimentos retira de verdade os saldos no tile e move suas atribuições à carga; não cria produção. No encerramento de um ciclo, o motor acrescenta apenas produção executada e atribui consumo efetivo usando FIFO, mantendo a origem contábil e o ciclo de cada parcela. Isso permite vários ciclos, lotes residuais e estoques de período anterior.

### Conservação e replay

O livro precisa reconciliar exatamente, a cada mudança material:
- estoque local por tile/mercadoria = soma dos lotes em custódia local;
- alimento realmente transportado em cada ordem = soma dos lotes na carga;
- consumo local ou de expedição = soma das parcelas retiradas dos lotes;
- produção é lançada uma única vez na custódia real, sem receita monetária.

Mudanças são construídas em clone e verificadas contra o próximo `PhysicalTacticalEconomyRuntimeState` **antes** do commit canônico. O sidecar passa a valer somente após a ação física ter sido efetivamente confirmada. No fechamento do ciclo, o `CanonicalPhysicalLotCycleWitness` é imutável, vinculado ao hash real e invalida no próximo tick ou ação. `RestoreSaveJson` reexecuta o journal existente e reconstrói a trilha, sem novos campos nos saves, sem mudança no hash, sem alterar quantidade, demanda, preço ou gameplay.

O sidecar **não é diretamente persistido no journal**; é rederivado dos eventos e da simulação. A invariante de replay garante identidade contábil do mesmo fluxo.

## Fronteiras inegociáveis

- Uma **atribuição FIFO** é uma hipótese determinística de quais lotes fungíveis atendem cada saída. A origem física individual de um consumo **não é certificada**. O contrato publica `PhysicalOriginIdentityIsDirectlyAuthenticated=false` e `CanAuthorizeRealPurchasingPower=false`.
- O estoque inicial não recebe ciclo de produção inventado; quando existir, é explicitamente não atribuído. A produção de madeira e pedra é contabilizada, mas sua destinação final ainda não existe como evento econômico executado.
- O livro não adiciona dinheiro, crédito, orçamento fictício, renda por habitante, demanda fixa ou novos preços. Não autoriza converter suas atribuições para `RealCompletedCycleObservation`.
- A carga da expedição usa origem de estoque e coleta real, mas o rastreamento é limitado às operações de custódia e consumo suportadas hoje. Novas formas de transporte/transbordo exigem adaptador auditado específico antes de sua ativação.

## Qualidade e evolução

`GaVs115PhysicalLotLineageTests`: inexistência da prova antes de um fechamento; conservação por tile/commodity e por ordem de viagem; retenção de vintages entre ciclos; uso alimentar; replay de save legado e modo cash; compra de provisões e replay de carga; testemunho obsoleto; imutabilidade de save/hash; proibição explícita de declarar `N` autenticamente respaldado.

**Próximo gate:** identificação física de lotes na camada canônica (com versionamento, origem e consumo final efetivo para madeira, pedra e insumos), política de redistribuição comercial, e avaliação de capacidade real com pesos de preços-base autorizados. Enquanto isso, `N` permanece no regime observacional, não promovido à jogabilidade.

**Governança:** PR Draft empilhada sobre #253. Validar 6 jobs reais (Linux, Windows, macOS Intel x64), CI FAST, benchmark e todos os gates acionados. macOS ARM64 nativo permanece pendente.
