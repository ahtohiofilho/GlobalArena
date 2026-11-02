# GA-VS100 — Governança de receita estatal: identificador causal e replay idempotente em laboratório

**Issue:** #235. **Baseline homologada:** VS99 PR #234, SHA `9f15020db4263f385cef0cf24a6e9a38977463df`, 49/49 workflows. **Branch:** VS100 Draft empilhada em VS99. Nenhum merge na `main`.

## Decisão monetária preservada

A população não dispõe de contas ou saldos de onde sai o dinheiro das compras. **Receitas monetárias são originadas abstratamente, porém reguladas pelas funções de produção, demanda, oferta e preços**, com destinação institucional que permitirá futura entrada no caixa do Estado. Portanto, em um `SETTLE` futuro o ingresso estatal **não exige débito de caixa de comprador**, mas precisa preservar a proveniência econômica, a escala monetária e a competência institucional autorizada.

Esse modelo **não** implica dinheiro ilimitado nem pressupõe a existência imediata de tesouraria estatal na camada física atual. O ledger legado `PhysicalFoodCashMarketState` conserva contas Household/Producer apenas por compatibilidade com uma implementação anterior de alimentos, sem validade normativa para o modelo macroeconômico futuro.

## Escopo exato da VS100

- **VS99 fornece o único valor de partida** `CanonicalCalibratedStateRevenuePreflight`: proposta não postada por beneficiário e commodity, já respaldada pela sequência VS94–98 e pela calibração da moeda experimental.
- `CanonicalTreasuryGovernanceDeclaration` exige `GovernanceId`, `GovernanceVersion`, `EvidenceId`, política VS99 (ID + versão), moeda, hash do mundo e ciclo. Uma revisão externa **read-only**, `ICanonicalTreasuryGovernanceReviewer.AcceptReadOnlyGovernance`, é obrigatória. Não se fornece implementação permissiva no motor. **Revisão laboratorial não é autoridade para emitir moeda**.
- `CanonicalTreasuryGovernanceDryRun` revalida toda a cadeia VS99 e reconstrói os valores públicos, por instituição `PublicAuthority` **já explicitamente declarada**, separados por commodity. Sem criação de tributos, propriedade ou beneficiário implícito.
- A proposta recebe `ProposalFingerprintHex`: SHA-256 de serialização canônica com comprimentos (BinaryWriter), cobrindo versão/identidade da governança, mundo, ciclo, unidade monetária, política e evidência da calibração, curva escolhida, fatores dos três produtos, **todos** os recebedores institucionais (não apenas públicos) e totais da proposta.
- `StageInLaboratory(existing)` devolve um `CanonicalTreasuryDryRunJournal` **imutável e não integrado ao WorldState**. No primeiro chamado cria o diário. Reaplicar **o mesmo** comprovante devolve o próprio diário existente. Uma diferença na proveniência, nos fatores, na sensibilidade, em valores privados/públicos ou na governança do mesmo ciclo faz o replay falhar. Não acumular duas vezes a mesma receita nem aceitar alteração de proposta após o primeiro ensaio.
- `VerifyIndependent` reconstrói a prova e as propostas públicas, comparando índice, valor e entradas. `RequireCurrent` exige mesma sessão, mesmo estado/rede e ciclo da cadeia VS99.

## Invariantes

```text
Q[m,c] = volume físico absorvido em VS96, com fonte comprovada VS95
R[m,c] = preço marginal integrado VS78/VS96
M[i,c] = função econômica do índice relativo VS98 * calibração explícita VS99
StateProposal[c] = soma(M[i,c] quando i é PublicAuthority)
StateProposalTotal = soma_c StateProposal[c] na mesma moeda VS99

DryRun(proof, null) → immutable worksheet
DryRun(proof, same worksheet) → same worksheet  [NOOP]
DryRun(competing proof, existing worksheet) → REJECT
```

**Importante:** `M` usa a moeda **experimental** declarada pela VS99; os fatores de teste (1/1, 2/1, 3/1) **não são ratificados**. A VS100 não converte essa simulação em caixa real. Não existe taxa, propriedade estatal ou regra fiscal inferida automaticamente.

A origem abstrata que gerará dinheiro efetivo posteriormente continua vinculada à produção física, demanda e preços; o usuário não precisa de saldos populacionais, nem o Estado precisa receber transferências de uma carteira de consumidores.

## Não objetivos e barreiras à promoção

**Proibido em VS100:** criar moeda gastável, alterar WorldState ou saves, aumentar saldo do Estado, diminuir saldos de consumidores, usar `InitialMicrocreditsPerWorker` ou `B=40`, gerar receitas sem oferta física, duplicar um ciclo, emitir empréstimos ou registrar dívida/crédito. A propriedade e a regra tributária continuam externas/pendentes, sem inventar uma autoridade pública para um povoamento apenas por ser um mercado.

O próprio contrato público expõe `CanAuthorizeSettlement == false`, `PostsRealStateTreasuryRevenue == false`, `DebitsPopulationMoney == false`, `ConvertsUnratifiedRatesIntoRealCurrency == false`.

**Para chegar a lançamento estatal efetivo** será necessário ratificar em separado a unidade monetária, a função de conversão economicamente calibrada, o controle público e a forma de incorporar uma tesouraria ao `WorldState`, com atomicidade, idempotência persistida e auditoria sem caixas populacionais. A VS100 prepara precisamente o requisito de prova/replay, mas não resolve essas decisões pela implantação de uma moeda arbitrária.

## Homologação e QA

- `GaVs100TreasuryGovernanceDryRunTests`: ensaio idempotente, replay por novo observador do mesmo mundo, conflito por curva/fatores/governança, zero produção, necessidade de revisor, campos de identidade inválidos, proteção a ambiguidade de string no hash, permutação de fatores, estado de rede alterado, turno posterior, save de outra sessão, dinheiro do ledger legado intacto.
- `GaVs100TreasuryGovernanceBenchmarkTests`: 8/32/128 repetições de um ciclo efetivamente fechado no fixture legado, reconstituindo a cadeia read-only sem mutação, 1 aquecimento + 3 medidas medianas sem threshold de CPU.
- Workflow `ga-vs100-treasury-governance.yml`: Linux com regressões dirigidas VS40–100, benchmarks, Godot Mono Release; Windows com suíte integral, benchmarks, Godot Mono Debug; **checkout exato do HEAD**, sem merge sintético. Gates kernel/hash mantêm macOS.
- Todas as execuções realmente acionadas no HEAD devem terminar em `completed/success`, com comprovante dos passos obrigatórios. PR segue Draft e `main` intacta.

**Próximo marco proposto:** VS101 — contrato de tesouraria persistente no `WorldState` e autorização executável, *somente após* ratificação explícita da política de monetização/competência, não por green CI da VS100.
