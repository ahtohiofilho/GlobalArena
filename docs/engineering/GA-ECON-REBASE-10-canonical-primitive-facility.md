# GA-ECON-REBASE-10 — Instalação primitiva canônica com construção física, trabalhadores e tempo

**Base homologada:** GA-ECON-REBASE-09, PR #257, HEAD `1c9e09a92adde260204b828369d66403af19bbb1`.
**Entrega:** `PhysicalPrimitiveFacility` como ativo físico do `WorldState.PhysicalEconomy` e hash v27. PR Draft empilhada, sem merge.

## Resultado de produto

A montagem material da REBASE-08 realmente incorpora madeira e pedra, mas permanece uma construção não comissionada. A REBASE-09 reconhece campo de trabalho preexistente sem reservar trabalhadores, comprovar labor construtivo ou criar asset.

Agora, **`StartPrimitiveFacility(tile, assemblyActionOrdinal, activity, committedWorkers)`** vincula explicitamente uma montagem física executada a um novo **abrigo/posto de apoio de trabalho primitivo (`WorkShelter`)**, sem consumir os mesmos insumos novamente. O tipo `WorkShelter` é uma estrutura material local, **não** um centro urbano, um bem comercial ou uma máquina industrial. Não requer fundação de cidade e mantém a produção neolítica sem infraestrutura.

## Regra canônica mínima de construção

- Pré-requisitos: frame real do mesmo tile, ordinal único, atividade de trabalho primitivo válida (agricultura, mineração de pedra, floresta), quantidade de trabalhadores **positiva**, efetivamente alocada ao tile/atividade e ainda não reservada por outra obra. Não pode iniciar uma segunda instalação com a mesma montagem nem outra instalação do mesmo tipo de trabalho no tile.
- Os insumos são os **mesmos** já debitados na montagem REBASE-08. O início da obra **não cobra materiais outra vez**.
- A obra exige **um ciclo econômico completo posterior ao início**, não um número arbitrário de turnos de movimento. O intervalo é governado por `PhysicalEconomicCadence`: 1/1 no começo adaptativo, 1/2 após o primeiro limiar e assim por diante, com o modo fixo legado preservado.
- Trabalhadores comprometidos ficam **reservados** em `PhysicalPrimitiveFacility`, continuam **residentes e consumidores de alimento**, mas são **subtraídos da produção real** de sua atividade no ciclo durante a construção. Reatribuição e partida que excedam trabalhadores livres são bloqueadas pelo construtor de estado. Não se cria trabalhador extra nem se altera a contagem populacional.
- No primeiro fechamento econômico realmente posterior ao início, a obra vira operacional (`CompletedMovementTurn` autenticado) e os trabalhadores voltam a produzir no **próximo ciclo**. O abrigo não recebe `yield` extra, bônus de produção, novo emprego ou produto de comercialização.
- A obra persiste como ativo físico após o término, mesmo se os trabalhadores forem redistribuídos. Sua função inicial é existir e suportar a localização da atividade; produtividade adicional depende de política futura.

## Estado autoritativo, integridade e hash

`PhysicalPrimitiveFacility`, do projeto Runtime, compõe `PhysicalTacticalEconomyRuntimeState.PrimitiveFacilities`, um campo real e imutável do `WorldState`. A validação recusa registros inválidos, duplicação de montagem ou papel/tile, conclusão anterior ao início, conclusão no futuro e somatório de trabalhadores comprometidos superior à mão de obra presente.

`CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion = 27` utiliza um **novo domínio SHA-256 separado**: encadeia o digest canônico preexistente (incluindo a reserva de tesouro quando houver), e autentica em ordem determinística a montagem vinculada, tile, atividade, tipo, empregados comprometidos, turno de início e de conclusão de cada instalação. Se não houver instalação, o digest/hash version anterior permanece **idêntico**, garantindo compatibilidade de todos os saves históricos.

A transição de início é `start_primitive_facility` no journal com ordinal da montagem, atividade e trabalhadores; o turno econômico de conclusão é uma transição `tick`, sem ação manual adicional. O replay reconstrói o avanço, a economia física (com trabalhadores não produtores durante a obra), as reservas e os hashes em cada etapa.

**Fronteira de segurança:** o checksum SHA-256 do envelope é verificação de integridade não assinada. Um agente que possa reescrever **todos** os eventos e recalcular hashes de uma história semanticamente válida não é distinguível apenas pelo checksum; não alegamos assinatura criptográfica ou antiadulteração por terceiros. Diferentemente do campo observacional REBASE-09, entretanto, os parâmetros e progressão do ativo **participam do digest do `WorldState`** em cada snapshot de instalação.

## Compatibilidade e schema

`CanonicalPhysicalAction` recebe propriedades opcionais `FacilityAssemblyOrdinal` e `FacilityActivity`, omitidas quando default. O `CanonicalPhysicalSavePayload` recebe um resumo opcional `PrimitiveFacilities` com ordinal, tile, atividade, tipo, trabalhadores, início e conclusão.

- Envelopes **9–26**, inalterados sem construção verdadeira.
- Envelope **27**: ao menos uma instalação física, sem tesouro reservado.
- Envelope **28**: instalação física com tesouro dormente, independentemente de qual foi ativado primeiro.
- O `Payload.Version` continua sendo 9–17 (ruleset interno). O restore exige ações de montagem e de construção consistentes, valida presença do resumo, reexecuta os comandos e rejeita divergência de hash, de estado físico ou de serialização canônica final.
- Ações `material_use` e `commission_primitive` das etapas anteriores podem coexistir no mesmo journal; v27/v28 não exige um comissionamento observacional REBASE-09.

## Economia e evidência

A montagem não é novamente tratada como produto novo ao iniciar a construção. A obra usa **trabalhadores que deixam de produzir** durante um fechamento econômico real: comparar com a mesma simulação sem obra comprova a queda exata da produção correspondente. O abrigo construído não libera (N), não gera receita/mercadoria fictícia e não altera coeficiente físico de produtividade. A população permanece como pessoas, não como cidade.

Próximos contratos separados: função concreta da infraestrutura e sua curva tecnológica, manutenção e emprego, transporte/instalação em outros tiles, direito de posse se necessário, e valoração de produção final executada com preços-base para eventual ativação de (N).

## QA / homologação

`GaVs119PhysicalPrimitiveFacilityTests` verifica: hash v27 e material conservado; comparação da produção normal com/sem obra; demanda alimentar preservada; trabalhadores reservados não podem migrar/reassinar; rejeição de obra sem frame, sem equipe e duplicada; conclusão apenas em ciclo econômico real nos regimes fixo e adaptativo; replay de obras em andamento e finalizadas no envelope 27; tesouro antes/depois no 28; compatibilidade byte a byte com v23/v25; detecção de resumo adulterado, ordinal falso e downgrade de envelope.

Exigir 6 testes reais de kernel/hash em Linux, Windows e macOS Intel x64, CI FAST, benchmark e todos os workflows válidos. macOS ARM64 nativo segue pendente.
