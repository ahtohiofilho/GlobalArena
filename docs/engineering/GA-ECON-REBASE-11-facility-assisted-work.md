# GA-ECON-REBASE-11 — Operação de abrigo primitivo e produção assistida atribuída

**Base homologada:** GA-ECON-REBASE-10, PR #258, `dd14825f6a3b29a5d9b88d04277b329f88ad7ef5`.
**Estado:** incremento opt-in em PR Draft empilhada; `N` continua sem autorização.

## Restrição econômica canônica aplicada

O `GA-ADR-ECON-007` confirma o trabalho primitivo sem construção obrigatória e declara explicitamente que **não foi decidido coeficiente de produtividade, limite de vagas produtivas, preço nem fórmula de aumento** das infraestruturas. Assim, um multiplicador arbitrário de produtividade para o `WorkShelter` seria um desvio, ainda que visualmente atraente.

O REBASE-11 implementa a **primeira função produtiva concreta que não depende desses parâmetros pendentes**: trabalhadores existentes podem **operar explicitamente** o abrigo já construído, e o motor passa a demonstrar quanto da produção física do ciclo foi realizada através desse espaço de trabalho. É **classificação operacional de produção realmente executada**, não ganho de `Q`; o ganho quantitativo fica para uma ADR de calibração própria.

## Operação física e pessoas

`StaffPrimitiveFacility(tile, assemblyActionOrdinal, activity, operators)` requer:
- instalação `WorkShelter` canônica realmente concluída e localizada no mesmo tile;
- atividade real `Agriculture`, `Forestry` ou `Mining`, a mesma da instalação;
- operadores entre zero (suspensão) e o limite de `CommittedWorkers` fixado na construção;
- pessoas **de fato alocadas** ao tipo de atividade nesse tile, em número maior ou igual aos operadores;
- recusa comando idempotente sem alteração, ordinal inventado, tile estranho, atividade divergente ou excesso de capacidade.

O operador **não é adicionado à população**: ele continua no grupo `PhysicalWorkerAllocation` existente. Enquanto estiver designado ao abrigo, a invariância do `PhysicalTacticalEconomyRuntimeState` impede movimentação/reatribuição que o retire do local sem antes suspender a operação. A reserva de construção continua separada: uma instalação inacabada não admite operadores.

## Autoridade física, hash e reprodução

`PhysicalPrimitiveFacility` recebe `OperatingWorkers` (padrão `0`). O construtor do estado físico valida o limite por instalação e a existência de residentes realmente alocados.

`CanonicalWorldStateHasher.FacilityAssistedWorkHashFormatVersion = 29` é domínio novo **somente se houver operadores em algum abrigo**: envolve a assinatura v27 existente (que já autentica a instalação) e a quantidade de operadores por ordinal. Sem operadores, o digest v27 continua **byte a byte idêntico**, ainda que a instalação tenha sido construída. Saves sem instalação mantêm versões ainda anteriores.

A operação é a ação `staff_primitive_facility` no journal, com ordinal da montagem, atividade e **quantidade-alvo** de operadores. O modo de produção é reproduzido pelo replay, que confere a ação e os hashes de cada estado.

Persistência:
- envelopes **v9–v28** conservados integralmente quando nunca houve operador;
- **v29:** ao menos uma ação de staffing, com ou sem equipe ativa agora, sem tesouro;
- **v30:** staffing e tesouro dormente, independentemente da ordem de ativação;
- `CanonicalPrimitiveFacilitySaveRow.OperatingWorkers` é opcional e omitido quando zero, para preservar bytes históricos;
- o resumo do estado de instalações é reproduzido e comparado pelo restore. Não há save de testemunha projetada.

## Produção física e reconciliação sem dupla contagem

A produção real continua sendo computada por:
`producedRaw = activeWorkers * primitiveYield(tile, activity)`.

Os trabalhadores no canteiro em construção já estão corretamente deduzidos pelo REBASE-10. Depois da conclusão, dos trabalhadores produtivos ativos, um subconjunto `OperatingWorkers` atua no abrigo; o restante continua trabalho rudimentar normal. O motor **não multiplica a produção, não cria uma nova linha em estoque e não faz novos lançamentos financeiros**.

No fechamento econômico efetivamente executado, o motor registra em memória um `CanonicalFacilityAssistedWorkWitness` (reconstituível no replay) com:
- ordinal da instalação, tile, atividade, mercadoria, operadores reais e quantidade efetivamente produzida por eles;
- saída atribuída igual a `OperatingWorkers * primitiveYield`, restrita ao volume de produção do mesmo tile/commodity/atividade na `CanonicalExecutedPhysicalCycleProvenance`;
- reconciliação `Q = Q_assistido + Q_rudimentar`; `Q_assistido` **já faz parte de** `Q`, jamais adicional;
- `VerifyIndependent` e `RequireCurrent` impedem exposição de recibos contraditórios ou stale. Sem ciclo executado, não há recibo.

Esse registro é **observabilidade do processo produtivo**, com o abrigo funcional como ponto de organização do trabalho. Não representa um upgrade quantitativo de eficiência, transformação de mercadorias, insumo adicional, receita, venda, prova de procedência de lote específico ou autorização de poder de compra abstrato.

## QA de compatibilidade e integridade

`GaVs120FacilityAssistedWorkTests` abrange:
- impossibilidade de operar abrigo em construção;
- staffing dentro de capacidade e com trabalhadores existentes; bloqueio de reatribuição ou movimento enquanto operam;
- retorno ao trabalho primitivo após desativação, sem perda da estrutura;
- hash v29 quando operado, retorno a hash v27 quando vazio; sem duplicação de pessoas/estoques;
- simulações comparativas idênticas com e sem operadores comprovando **igualdade de produção total e de estoques**, e recibo assistido como parte dessa produção;
- saves v29/v30 com tesouro antes/depois, incluindo desativação e replay;
- preservação exata dos saves v27/v28 quando nunca houve operadores;
- adulteração de capacidade, ação e envelope com checksum recalculado rejeitada;
- regime adaptativo primitivo sem turno de produção extra.

Homologar: 6/6 etapas reais (regressão kernel + state hash nos sistemas Linux, Windows e macOS Intel x64), CI FAST, benchmark e todos os workflows elegíveis desse HEAD. ARM64 nativo continua pendente.

## Próxima decisão requerida

Para produzir incremento **quantitativo** (`Q_{assistido} > Q_{rudimentar}`), definir e aprovar **uma ADR de ganho produtivo** que determine o papel da estrutura, insumos, desgaste/manutenção, tecnologia, limites e relação com produtividade geográfica do tile — possivelmente por categoria (roçado, posto florestal, pedreira). Só então implementar coeficientes versionados e testá-los sem fabricar produção ou dupla renda.
