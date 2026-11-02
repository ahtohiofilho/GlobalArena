# GA-VS98 — origem abstrata de receita e atribuição institucional com proveniência física

**Issue:** #231. **Base homologada:** VS97 PR #230, SHA `7b2f21f970c49bc1a4b0a4e139cde47bb54c814f` (47/47). **Entrega:** PR Draft empilhada na VS97; nenhuma integração em `main` automática.

## Premissa monetária canônica ratificada em 02/11/2026

**As populações não possuem um caixa monetário a debitar pelas compras.** A fonte de receita é **abstrata**, porém **regulada** pelas funções econômicas previamente discutidas — produção, oferta efetivamente absorvida, demanda como intensidade, preço marginal decrescente — e poderá entrar no caixa do Estado sem saída de um caixa de consumidores. Não instituir `B=40`, limite monetário de compras, saldo de família, financiamento bancário, empréstimos ou crédito.

**Módulo legado:** `PhysicalFoodCashMarketState` ainda possui `HouseholdMicrocredits`, `ProducerMicrocredits` e `InitialMicrocreditsPerWorker`. É implementação experimental anterior para alimentos, não diretriz do sistema monetário futuro. VS98 não o modifica e inclui verificação explícita de imutabilidade. Os recibos legados executados de alimento não viram origem/transferência financeira obrigatória do futuro modelo.

## Fronteira de autoridade

VS94 prova capacidade física; VS95 distribui **oferta planejada** local + remoto com fretes autenticados; VS96 calcula a absorção hipotética e índices de receita por **mercado + mercadoria**; VS97 os compartilha no observatório regional.

VS98 agrega a esses resultados uma **atribuição institucional hipotética das receitas econômicas por fonte física** — não uma liquidação. Usam-se as duas sensibilidades **não ratificadas** do preço VS78 (1 e 2). A função fornece **índices relativos**, ainda não valores monetários spendáveis. **Nenhuma conversão índice→microcrédito é feita aqui**: não se deve improvisar uma escala de preço, porque ela alteraria silenciosamente o poder de compra. PublicAuthority na saída NÃO significa dinheiro registrado em um tesouro ou poder de tributar ratificado.

### Contrato de origem

Para cada fonte real `(Source,Commodity,Activity)`, VS98 confirma `LocalTrial + RemoteLots == VS95.Source.ProposedLocalUseRaw + ProposedRemoteDeliveryRaw`. Depois reagrupa contribuições por `(Source,Market,Commodity,Activity)`. Um embarque remoto se associa ao **produtor da origem**, ainda que a demanda e a curva de preço sejam do mercado de destino. Assim, o beneficiário da receita não é confundido automaticamente com o comprador.

Para cada mercado `m` e bem `c`, a VS96 dispõe da oferta absorvida total `Q[m,c]` e dos índices integrados de receita relativos `R_g[m,c]` e `R_s[m,c]`. Para cada fonte `o` que entrega `q[o,m,c]`, o diagnóstico distributivo usa a partilha física *pro rata*:

```text
R_g_origin[o,m,c] = R_g[m,c] * q[o,m,c] / Q[m,c]
R_s_origin[o,m,c] = R_s[m,c] * q[o,m,c] / Q[m,c]
SUM_o R_g_origin[o,m,c] = R_g[m,c]
SUM_o R_s_origin[o,m,c] = R_s[m,c]
```

Esta é uma **convenção experimental de atribuição** (não prova canônica do modo futuro de divisão de receita entre origens). É pertinente porque cada fonte participa de um único mercado competitivo para a mesma mercadoria, e a curva já foi calculada sobre a oferta conjunta. Não se atribui o valor integral do mercado a cada origem nem se presume propriedade estatal de uma mercadoria.

### Destinação institucional explícita

O chamador apresenta `CanonicalAbstractRevenueEntitlement(Source, Commodity, Activity, Beneficiary, SharePermille)` de cada fonte ativa. As participações devem ser estritamente positivas, distintas por beneficiário na fonte e somar exatamente `1000 permille`; a cobertura tem de ser completa. `Beneficiary` declara ID e tipo institucional (público, comunidade, cooperativa ou operador comercial), preservando consistência entre fontes. **Não existe uma política implícita de propriedade ou imposto**: nem “tudo do Estado” nem “todos privados” são presumidos pelo motor.

Cada índice relativo atribuído à fonte é dividido entre beneficiários, com frações exatas. O relatório `CanonicalAbstractInstitutionalRevenuePreflight` conserva por produto:
- soma de origem = receita de mercado VS96;
- soma dos beneficiários = soma das origens;
- parcela pública é apenas o recorte das instituições explicitamente tipadas PublicAuthority;
- toda atribuição é específica de mercadoria e curva. **Não somar índices de bens distintos como se fossem caixa estatal!**

Não há mudança de workers, save, estoque ou dinheiro; não há nomeação automática de tributo, débito a consumidor, crédito gasto, `SETTLE` ou `TreasuryCredit`. O código expõe indicadores booleanos claros para todas essas fronteiras.

## Auditabilidade, determinismo, QA

`RequireCurrent` e `RequireSameSites`/`RequireSameDisposition`/`RequireSameAbsorption` verificam a mesma sessão, rede, ciclo e **mesmas instâncias de evidência** VS95–VS97. A implementação recalcula independentemente as distribuições, conserva quantidades físicas e compara índices com `CanonicalEndogenousPriceIndex.CompareTo`. O algoritmo tem ordenação canônica de fontes, mercado, mercadoria e instituição.

Testes `GaVs98AbstractInstitutionalRevenueTests` contemplam: repartição 600/400 experimental com volume de madeira remetido, conservação por produto até PublicAuthority, falta/excesso/duplicação de titularidade, identidades institucionais contraditórias, zero oferta, permutação e replay, rede e turno obsoletos, restauração de save estrangeiro, **imutabilidade dos microcréditos legados**.

Benchmark `GaVs98AbstractInstitutionalRevenueBenchmarkTests`: 8/32/128 reexecuções em ciclo real e rede autenticada, sem threshold rígido de runner. O novo gate em Linux valida regressões VS40–98 e Godot Mono Release; Windows executa suíte integral e Godot Mono Debug. Both run benchmark. Em paralelo, os gates cross-platform cobrem Windows, Ubuntu e macOS. A homologação será feita somente para o **HEAD exato da PR**, com confirmação das etapas/jobs, após todos workflows disparados passarem.

## Política para a próxima fase

A VS99 poderá estudar **valoração monetária calibrada + destinação estatal**, mantendo explícitas (a) unidade de moeda e função de conversão respaldada no projeto; (b) direito institucional/tributário autorizado; (c) geração e registro monetário atômicos e idempotentes; (d) **nenhum débito de caixa populacional**. Nenhuma das funções experimentais de VS78/96/98 deve ser promovida automaticamente como dinheiro efetivo até a decisão de política institucional e de preço.
