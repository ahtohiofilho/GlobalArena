# GA-ECON-MARKET-DEFAULT-01A — Preflight global de oferta por padrão

**Data:** 2026-11-06  
**Base:** GA-ECON-CONVERGENCE-01 / PR #279.  
**Situação:** incremento de pré-execução *read-only*, não promoção do motor de vendas. PR Draft e sem merge.

## Objetivo

Substituir a leitura isolada do tile por uma **classificação global determinística** de todos os bens fisicamente presentes, conciliando estoque livre, oferta já comprometida e retenção deliberada, sem transformar uma intenção de mercado em venda. A regra-alvo do produto é: **toda produção elegível ao mercado é comercializada automaticamente, exceto a parcela deliberadamente retida ou destinada a uso físico autorizado**. O modo corrente continua legadamente acumulando estoque; este incremento mede o desvio sem falsear sua execução.

## Implementação

`CanonicalDefaultMarketPoolPreflight.FromCurrent(session)` captura a sessão real e valida a custódia via `CanonicalPhysicalStockCommitmentWitness`. Cada par `(FineTile, Commodity)` recebe:

- `UncommittedLocalRaw`: mercadoria livre e ainda não vendida (sem presumir retenção).
- `AlreadyCommittedOfferRaw`: mercadoria fisicamente segregada em `MarketOffer`, excluída da parcela livre para evitar dupla oferta.
- `DeliberatelyRetainedRaw`: custódia física `DeliberateRetention`, excluída de oferta.

Identidade contábil: `TotalAccounted = UncommittedLocal + AlreadyCommittedOffer + DeliberatelyRetained`. A quantidade candidata à oferta padrão é `EligibleByDefault = UncommittedLocal + AlreadyCommittedOffer`; valores de outras custódias (como rações de jornada) não são convertidos em mercadoria local. Os somatórios usam `BigInteger`, não assumem unidade de mercado D nem limite por pessoa, e são ordenados por tile e commodity (1 alimento agregado legado, 2 pedra, 3 madeira).

`VerifyCurrent` rejeita recibos após qualquer evento/hash alterado, valida o ledger real e reconcilia soma dos saldos com todos os compromissos ativos. Não há escrita em estado, reserva, preço, consumo, venda, imposto, transporte, hash, journal ou save. Dados de qualidade nutricional continuam indisponíveis; não consumir automaticamente alimento para forçar uma meta numérica.

**Limite explícito:** `CertifiesActualAutomaticOffers = false`, `CertifiesExecutedSales = false`, `AuthorizesSettlementOrRevenue = false`. A estrutura é uma classificação *antes* do fechamento comercial, não prova que um destino físico absorveu a mercadoria.

## Validação exigida

`GaEconMarketDefault01aTests` verifica: turno zero; estoques de um ciclo; ofertas explícitas sem contagem duplicada; retenção integral e parcial; liberação; igualdade físico-contábil; rejeição de evidência obsoleta; save/replay/hashes; impossibilidade de produzir receita fictícia ou considerar simples estoque como retenção voluntária. Executar dotnet testes FAST no HEAD; não contar jobs `skipped` como aprovados.

## Próximo incremento

`GA-ECON-MARKET-DEFAULT-01B`: criar contrato **versionado de destino físico**, com verificação de origem, consumidor/destinatário real, custo/acesso e conservação. Cada unidade da oferta deve receber exatamente um destino; faltando prova de destino, o fechamento **falha fechado**, sem apagar estoque nem declarar receita. Somente depois, com função de absorção calibrada, journal e replay aprovados, a execução poderá autorizar `ExecutedSale`. A integração nutricional continua exigindo seis alimentos reais identificados e consumos atestados, nunca a divisão fictícia do alimento agregado.
