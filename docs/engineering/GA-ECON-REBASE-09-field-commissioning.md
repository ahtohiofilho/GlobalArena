# GA-ECON-REBASE-09 — Comissionamento de campo de trabalho primitivo com prova pós-montagem

**Base:** PR #256, REBASE-08, HEAD `94ed11a46b0559c4bab5058bd2982945b399b33a`.
**Entrega:** ação explícita de registro/comissionamento **de campo** de uma atividade primitiva existente, a partir de montagem material executada e de trabalho comprovado no ciclo econômico **posterior**.

## Decisão de escopo

O jogo já permite produzir sem instalação inicial (Neolítico). As atividades `Agriculture`, `Forestry` e `Mining` têm produtividade física real determinada pelo terreno, trabalhadores e ciclos. REBASE-08 acrescentou montagem de madeira e pedra **sem** regra canônica de tempo de construção, mão de obra dedicada ou rendimento de um novo edifício.

Portanto, **não** criaremos uma construção instantânea, um trabalhador fantasma nem uma produtividade artificial. A REBASE-09 registra a **validação operacional de campo** de um conjunto já montado, reconhecendo que trabalhadores alocados à atividade primitiva realizaram produção **posterior à montagem**. É uma certificação de funcionamento **da atividade preexistente no local**, não a comprovação de trabalho de construção dedicado. Não adiciona fábrica, cidade, capacidade produtiva, nova classe de mercadoria, dinheiro ou infraestrutura ao `WorldState`.

## Contrato executado

`CommissionPrimitiveWorkSite(tile, assemblyActionOrdinal, activity)` exige:
- montagem `assemble_primitive` real, identificada por ordinal existente e vinculada exatamente ao tile;
- montagem ainda não vinculada a outro comissionamento e ausência de duplicata da mesma atividade/tile;
- último **ciclo econômico fechado** no turno físico atual, com `CycleOrdinal` correto; um tick fora da cadência econômica não satisfaz;
- fechamento posterior à montagem tanto em turnos físicos quanto na ordem de eventos;
- evento `tick` real, com hash correspondente à `CanonicalExecutedPhysicalCycleProvenance` de fechamento;
- trabalhador **atualmente alocado** ao mesmo tipo de trabalho no tile;
- linha de **produção efetivamente executada** para a atividade, origem e commodity correspondente no ciclo que veio após a montagem, quantidade positiva.

Não é possível aprovar um campo apenas pela existência de estoque antigo ou por uma projeção futura. A prova testemunha operação física efetiva — já contabilizada uma única vez pelo motor histórico —, sem alegar que esses mesmos trabalhadores gastaram uma segunda cota de trabalho construindo a estrutura.

## Efeitos e observabilidade

`CanonicalCommissionedPrimitiveWorkSite` preserva ordinal da montagem e do comissionamento, tile, atividade, mercadoria, ordinal/turno econômico verificado, quantidade realmente produzida pelo trabalho primitivo, hash do tick comprovante e hashes do evento de comissionamento. A lista `CommissionedPrimitiveWorkSites` é uma vista imutável do journal reconstruído por replay, e `IsCommissionedWorkSiteCurrentlyStaffed(site)` consulta os trabalhadores **agora**, não cria uma equipe artificial. O evento `commission_primitive` avança revisão e hash canônicos, mas **não altera** estoques, trabalhadores, ciclos, demanda, preços, produção, rede ou tesouro.

Declarações de fronteira explícitas: `IsDedicatedConstructionLaborAuthenticated=false`, `IsCanonicalWorldAsset=false`, `AuthorizesAdditionalProductivity=false`, `CreatesTradeableManufacturedGood=false` e `CanAuthorizeRealPurchasingPower=false`.

## Journal e saves

Novos campos opcionais em `CanonicalPhysicalAction`: `CommissionAssemblyOrdinal` e `CommissionActivity`; `JsonIgnore(WhenWritingDefault)` mantém os eventos antigos byte a byte. Um único `commission_primitive` vincula a montagem e a atividade, e a combinação entre registro físico e evento é reexecutada deterministicamente no restore; toda adulteração de ordinal, atividade, requisito temporal, hash ou envelope diverge e é rejeitada.

Envelopes:

| Versão | Regra |
|---|---|
| 9–24 | Saves históricos, unchanged se não houver comissionamento |
| 25 | Pelo menos um `commission_primitive`, sem reserva dormente de tesouro |
| 26 | Comissionamento e reserva dormente, ativada antes/depois |

O ruleset interno `CanonicalPhysicalSavePayload.Version` permanece o mesmo histórico (9–17). O envelope v25/v26 também exige ações válidas de montagem, e aceita eventuais ações `material_use`. O histórico usa journal, não lista de ativos inserida artificialmente no state hash.

## QA

`GaVs118PrimitiveWorkSiteCommissioningTests` cobre: recusa antes de ciclo fechado e com off-cadence tick; prova de execução em agricultura/floresta/mineração; rejeição de duplicatas; tile/ordinal/atividade inválidos; ausência de produção real; emprego atual sem fantasma; replay completo de v25 e v26 com ordem distinta de ativação do tesouro; bytes de save v23 preservados; rejeição de journal rehash adulterado; manutenção do registro e ausência de segundo output em ciclos seguintes.

**Gates de homologação:** seis testes reais de kernel+hash em Linux, Windows e macOS Intel x64, CI FAST e benchmark. macOS ARM64 permanece pendente.

## Próxima decisão de produto

O **comissionamento de um edifício produtivo de fato** depende de um contrato canônico de construção: trabalhador dedicado removido da produção durante a construção, duração em turnos/ciclos, categoria funcional da instalação, permissões e capacidade produtiva concreta. Com esse contrato, um novo `WorldState` versionado poderá incorporar e conservar ativos sem dupla contagem. **Não confundir o registro de campo deste incremento com essa futura integração.**

**Diretriz de N:** somente produção final real com preços-base autenticados poderá alimentar o poder de compra abstrato. O novo registro não cria receita, remuneração, orçamento nominal ou conversão intermediária duplicada.
