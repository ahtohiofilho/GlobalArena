# GA-VS97 — concentração regional por mercado e mercadoria

**Issue:** #229. **Baseline:** VS96 PR #228, SHA `67a35e781c966be8d9a2ce533503a3e195328d00`, homologada em 46/46 workflows. **Branch:** PR Draft empilhada sobre VS96. **Política de integração:** sem merge na `main` sem autorização.

## Diretriz econômica ratificada pelo usuário em 02/11/2026

O Global Arena **não possui caixa de população** que precise ser debitado para adquirir os bens produzidos. O **dinheiro que ingressará no caixa do Estado** tem origem abstrata e será regulado pela função econômica de oferta, demanda, preços, vendas e pelas regras de titularidade/tributação. Não se inventa uma fonte ilimitada e livre de restrições; a função de preços e as quantidades físicas são os mecanismos reguladores. Nem receitas do Estado nem tributos devem ser modelados como transferências originadas de uma carteira de consumidores.

Por isso, as fronteiras conceituais são:

- **Sem orçamento monetário do comprador**: não adotar `B=40`, saldos familiares, contas de trabalhadores ou crédito.
- **Com receitas do jogo**: não proibir receitas monetárias do Estado — apenas adiar sua efetiva contabilização até haver regra de cálculo/persistência e autoridade de fechamento.
- **Índices de cenário ≠ receitas liquidadas**: o que a VS96 fornece é índice relativo de receita, e não crédito estatal executado. Os recibos de alimento do V12 são dados legados de execução reais naquele mecanismo, não decisão de que consumidores terão caixa no modelo canônico definitivo.
- **Sem banco/crédito**: preservar ADR ECON-005; não inventar obrigações futuras ou instituições financeiras.
- **Sem confundir tributo, receita econômica e saldo estatal**: será necessária política explícita de propriedade e destinação monetária antes do `SETTLE` real.

## Observatório de mercados como proxies espaciais

A VS97 compara os **pontos econômicos habitados** da VS96. Esses pontos são proxies geográficos de mercados regionais, **não** divisões políticas, Estados, províncias, jurisdições ou títulos de propriedade. O estágio não agrega arbitrariamente mercados em fronteiras que ainda não foram definidas.

`CanonicalRegionalEconomicConcentrationAudit.FromCertifiedAbsorption(session, sites, certifiedVS96)` recebe a **mesma instância** de descoberta da VS96 e um cenário de absorção físico já autenticado. Rejeita snapshots de outro mundo/ciclo/rede ou outra descoberta equivalente por aparência. Faz replay independente de `VS96.VerifyIndependent` antes de comparar.

## Métricas exatas sem unidade monetária

Para **cada mercadoria separadamente** `c` e mercado `m`:

```text
Q[m,c] = VS96.HypotheticallyAbsorbedRaw
S_Q[m,c] = Q[m,c] / SUM_m(Q[m,c])

R_g[m,c] = VS96.GentleGrossRevenueIndex
R_s[m,c] = VS96.SteepGrossRevenueIndex
S_g[m,c] = R_g[m,c] / SUM_m(R_g[m,c])
S_s[m,c] = R_s[m,c] / SUM_m(R_s[m,c])

HHI_Q[c] = SUM_m(S_Q[m,c]^2)
HHI_g[c] = SUM_m(S_g[m,c]^2)
HHI_s[c] = SUM_m(S_s[m,c]^2)
```

A classe `CanonicalRegionalShare` implementa frações exatas com `BigInteger` e redução por MDC. Quando o denominador total de uma métrica é zero, **todas as participações e seu HHI valem zero**; não se cria uma região vencedora sem atividade. Caso positivo, a soma de participações é exatamente 1 e HHI fica em `[1/n,1]`. O líder por participação tem desempate pelo menor identificador de mercado, nunca por ordem de execução ou arquivo.

**Nunca somar R_g/R_s de commodities diferentes**: os índices são relativos e não fixam comparabilidade de moeda ou de bem-estar entre alimentos/madeira/pedra. Não inferir preço pago por madeira/pedra, receita fiscal, saldo público, orçamento de consumo ou legitimidade de transferência. Tampouco impor demanda como teto de quantidade: a oferta física absorvida pode ser maior que o sinal basal.

## Protocolo de QA

- `GaVs97RegionalConcentrationTests`: auditoria por produto/mercado, shares e HHI exatos, empate e ausência de oferta, demanda menor que oferta, transporte regional, valores monetários não extrapolados, frações inválidas, descoberta estrangeira, revisões obsoletas, determinismo e save intacto.
- `GaVs97RegionalConcentrationBenchmarkTests`: 8/32/128 reprocessamentos após criação de sessão real e witness de VS92–VS96; mediana descritiva sem metas arbitrárias.
- `ga-vs97-regional-concentration.yml`: checkout exato do SHA da PR; Linux regressões VS40–97, benchmark, Godot Mono Release; Windows suíte integral, benchmark, Godot Mono Debug. Gates multiplataforma de Kernel e State Hash cobrem macOS em paralelo.

A homologação exige aprovação de todos os **workflows efetivamente acionados** para o último SHA da branch e dos passos específicos, sem confundir estado de fila com teste executado. Não se presume um número fixo universal de workflows.

## Próximo marco

A VS98 deverá **formalizar o mecanismo de geração monetária abstrata e sua distribuição institucional**, inicialmente em camada experimental/auditável, sem carteira de compradores. O código de settlement real deverá esperar pela definição de titulares e impostos. A VS97 cria apenas diagnóstico espacial exato; ela não contabiliza dinheiro nem impede que o Estado o receba nas próximas implementações.
