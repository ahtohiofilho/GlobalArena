# GA-VS99 — calibração monetária auditável e prévia de receita estatal sem caixa populacional

**Issue:** #233. **Baseline:** VS98 PR #232, SHA `d283b7515fc2a25211752cc1c60cb0ce9aa0ee38` homologada em 48/48 workflows. **Integração:** PR Draft empilhada sobre VS98; nenhuma alteração automática em `main`.

## Diretriz ratificada de origem monetária

**A população NÃO possui caixa do qual dinheiro seja retirado para pagar pelos itens.** As quantidades físicas absorvidas e as funções de preço/demanda geram **receitas abstratas reguladas**, as quais poderão ingressar na economia e na tesouraria do Estado. Não existe correspondência obrigatória de dupla entrada com caixa de consumidor; o valor gerado não é ilimitado ou arbitrário: a origem precisa ser a atividade econômica real, a formação de preços, a política de escala monetária e a titularidade institucional.

O módulo histórico `PhysicalFoodCashMarketState` ainda utiliza `HouseholdMicrocredits`, `ProducerMicrocredits` e dotação `InitialMicrocreditsPerWorker`. É compatibilidade do motor anterior, não a arquitetura monetária final. **VS99 não consulta esses saldos para permitir demanda nem altera esse ledger.**

## Fronteira de segurança da VS99

A VS98 já atribuiu índices relativos de receitas de mercado a **fontes de produção** e a **beneficiários institucionais explícitos**, com fretes e quantidades derivados da VS95/96. A VS99 é a camada experimental de **calibração monetária** que falta para poder representar propostas em uma moeda comum. Ela **não** determina que um índice de receita equivale automaticamente a um crédito, pois o índice é adimensional.

O modelo `CanonicalAbstractCurrencyCalibration` exige um documento externo informado pelo chamador com:
- `PolicyId` e `PolicyVersion` positivos, `CurrencyUnitId` explícito e `EvidenceId` da origem da calibração;
- `SourceWorldHashHex` igual ao hash da sessão, `CompletedEconomicCycleMovementTurn` igual ao ciclo da VS98;
- escolha expressa da curva experimental gentle/steep;
- **uma taxa racional estritamente positiva para cada produto** alimento/madeira/pedra, sem padrão embutido, todas interpretadas na mesma moeda nomeada.

Uma implementação de `ICanonicalAbstractCurrencyCalibrationReviewer.AcceptReadOnlyCalibration` pode aprovar uma **prévia de laboratório**. Essa interface NÃO autentica valor monetário canônico, não ratifica política fiscal, nem autoriza despesa ou `SETTLE`. Ela também não comprova que `EvidenceId` seja uma referência externa genuína. Na ausência de escala monetária aprovada ou do revisor, a tentativa falha: não existe fallback para 1 crédito por índice nem inferência de um salário/quantia inicial por trabalhador.

Os fatores `1/1`, `2/1` e `3/1` usados na fixture são **exclusivamente parâmetros de teste explícitos**; não definem preços ou moeda no produto. O resultado é intencionalmente denominado `ProposedNonpostedAmount`.

## Derivação da proposta

Para cada mercadoria `c` e instituição `i` declarada pela VS98:

```text
R[i,c] = índice relativo de receita da curva escolhida em VS98
k[c] = fator positivo de moeda comum / unidade de índice (externo, explícito)
M[i,c] = R[i,c] * k[c]   (fração exata BigInteger; sem arredondamento/centavos)
GROSS[c] = SUM_instituições M[i,c]
PUBLIC[c] = SUM_instituições PublicAuthority M[i,c]
TOTAL_PUBLIC_PROPOSTO = SUM_mercadorias PUBLIC[c]  (mesma CurrencyUnitId)
```

`CanonicalCalibratedMonetaryAmount` é uma **fração racional exata não contabilizada** (MDC, denominador positivo); nada disso está disponível ao jogador, torna-se moeda spendável ou gera saldo na tesouraria. A agregação entre produtos só ocorre **depois** de cada um ter fator explícito da mesma moeda. As duas curvas ficam em cenários separados; não se mistura sensitividades.

A conservação é auditada por mercadoria: a soma de todas as instituições deve ser igual ao total físico-econômico valorado; o recorte público deve corresponder à soma das instituições tipadas PublicAuthority, e `PUBLIC <= GROSS`. Valores de produção zero geram exatos zeros, não receita estatal espontânea.

O Estado poderá futuramente receber diretamente `PUBLIC`, por geração monetária abstrata regulada, sem nenhum lançamento de débito em consumidores. Entretanto, definir a **escala canônica** e o **direito ao ingresso** é condição anterior à escrita atômica no save/estado. A simples escolha de instituição pública não implica titularidade legal ou tributo.

## Auditoria e isolamento

- `CanonicalCalibratedStateRevenuePreflight.FromReviewedCalibration(session, abstract98, calibration, reviewer)`: falha sem aprovação explícita do revisor, exigindo o mesmo mundo/ciclo e as três commodities sem duplicação.
- `VerifyIndependent`: reconstrói diretamente a partir da VS98 (que audita VS95, VS96 e VS97), revalida os fatores, os indexadores e os somatórios.
- Sem saldo populacional, limite de demanda por orçamento, débito de comprador, empréstimos, bancos, juros ou autorização de crédito.
- Sem mudança de `CanonicalWorldState`, inventário, contas antigas, saves, arrecadação executada ou transação `SETTLE`.
- Sem uma unidade monetária ratificada, a prévia é **cenário e não receita efetiva**.

## Testes, qualidade e governança de rollout

- `GaVs99AbstractCurrencyCalibrationTests`: cobertura total das três commodities, mesmo mundo/ciclo, escolha de curva, produção zero, escala ausente/duplicada/negativa, política irregular, rejeição do revisor, frações enormes, rede/turno/sessão obsoletos, imutabilidade do ledger alimentar legado e replay determinístico.
- `GaVs99AbstractCurrencyCalibrationBenchmarkTests`: 8/32/128 reexecuções com dados econômicos e rede reais da fixture VS92–98. Não é benchmark de população mundial nem aprovação de escala monetária.
- `ga-vs99-currency-calibration.yml`: dois jobs, HEAD SHA exato, regressões Linux VS40..99, suíte integral Windows, benchmarks 8/32/128, Godot Mono Release/Debug. Os gates de kernel e hash multiplataforma preservam macOS.
- Homologar somente quando todos os **workflows realmente acionados** tiverem terminado com `completed/success` no mesmo SHA, com passos comprovados e PR Draft sem merge.

## Próxima etapa proposta (VS100)

A decisão que falta é **ratificar a unidade monetária do jogo, a regra que fixa os fatores econômicos e os titulares institucionais legítimos**. Então poderá ser desenhado um `SETTLE` estatal que só credita receita gerada pelas funções econômicas e permite repetição idempotente, com log causal e sem débito populacional. Não confundir a prévia read-only de VS99 com geração monetária efetivamente executada.
