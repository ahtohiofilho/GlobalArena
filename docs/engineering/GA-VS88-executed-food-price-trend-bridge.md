# GA-VS88 — Ponte diagnóstica de tendência de preços alimentares REALMENTE executados

**Issue:** #209. **Base:** VS87 PR #208 SHA \`1e3c8e1fb808a12dc674fea2310327f7ff19bf45\` — 53/53 CI. **Diretriz:** GA-ADR-ECON-007 R2 (#188). Incremento read-only, não-promovido a controle do motor.

## Por que esta fase existe

A VS83 permitiu calcular a produção física hipotética de trabalhadores reais, a VS84 usou preços **relativos hipotéticos** para explorar o ajuste de utilização, a VS85 comprovou a passagem temporal real, a VS86 criou sidecar histórico observacional e a VS87 identificou compras de **alimento local realmente executadas** no sistema legado V12 em microcréditos.

Ainda faltava uma forma segura de **comparar dois preços efetivamente executados** sem pretender que um preço de comida V12 representa todos os produtos VS78 ou que o feedback da VS84 já atua no motor. A VS88 fornece uma **tendência diagnóstica** de alimentos apenas para inspecionar um possível feedback agrícola.

## Protocolo da API

\`CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(session,cycleProofVS85,executedWitnessVS87,maximumAdvisoryStepPermille)\` exige:

1. O mesmo \`CanonicalPhysicalTacticalSession\` vivo e atual de ambos os proofs VS85/VS87.
2. Dois ciclos canônicos realmente concluídos, fonte e o **exatamente próximo** alvo, autenticados por \`State.Cycles\`, hashes e \`CanonicalPhysicalAction\` de ticks da VS85.
3. \`State.FoodCash.Receipts\` de **compras locais de alimento** no ciclo de origem e \`executedWitnessVS87.ExecutedReceipts\` do destino; \`VS87.VerifyIndependent()\` checa as multiplicações exatas em microcréditos e limites do consumo físico do ciclo. A VS88 verifica também \`Σ FoodRaw_source ≤ sourceCycle.FoodConsumedRaw\`.
4. A comparação usa apenas tiles em comum com compras comprovadas em ambos os ciclos; exclui as vendas sem valor efetivamente pago, preço unitário zero ou ausência de observação. Assim, \`HasComparableExecutedFoodPrice=false\` significa **sem sinal**, nunca preço 0 fictício.
5. Para cada tile comparável, o peso fixo é \`w_tile=min(FoodRaw_source,FoodRaw_target)\`, sem alocar consumidores/residentes separados de trabalhadores. Preço monetário V12 tem a **mesma unidade nos dois ciclos**, de modo que os agregados exatos \`Σ w*P_source\` e \`Σ w*P_target\` não sofrem efeito de troca de composição nas quantidades.
6. Produz **somente diagnóstico experimental da agricultura**:
   \[
   \Delta u = \mathrm{trunc}\left(s\frac{\sum w P_{\rm target}-\sum w P_{\rm source}}
   {\sum w P_{\rm target}+\sum w P_{\rm source}}\right),\quad
   u_{\rm suggested}=\mathrm{clamp}(u_{\rm VS85}+\Delta u,0,1000)
   \]
   com \`1≤s≤250\`. Frações exatas usam \`BigInteger\`; nenhum \`double\` em agregação, arredondamento ou fórmula.
7. O \`u_VS85\` é a **hipótese de utilização** já existente na projeção VS83 do ciclo corrente, **não** utilização realmente aplicada no motor. As atividades de mineração/floresta e retenções não são recalculadas. A sugestão NÃO indica causalidade ou lucratividade, nunca muda \`WorldState\`.

\`RequireCurrent\` invalida após nova ação física/revisão da rede ou uso da instância estrangeira. \`VerifyIndependent\` repete todo cálculo dos recibos reais e provas originais, verificando pesos, sinais, taxa agrícola e política, sem depender de valores de sidecar.

## Garantias canônicas

- **Preços reais observados:** apenas compras de alimento em microcréditos no mecanismo local cash-food legado. Não são preços dos demais bens ou frete.
- **Índices VS78/VS84:** preços relativos, hipotéticos, de outros experimentos. Não são convertidos em caixa nem misturados com a cotação real. A razão sem unidade na VS88 usa **dois preços nominais V12 do MESMO produto**, não a escala VS78.
- **Sem intervenção:** não reescreve \`AdvanceMovementTurn\`, \`PhysicalFoodCashMarketState\`, os workers, itinerários, estoque, demanda, save ou execução de mercado.
- **Proibição de autoridade:** nenhum método produz \`SETTLE\`, altera trabalhadores, cria inventário, crédito, renda exógena B=40 ou calcula lucro líquido monetário. A sugestão agrícola permanece manualmente auditável, não executável.

## QA

Testes para ciclos autênticos e compras comprovadas; mesmo tile com peso \`min\`; aritmética exata e delta; gains 1,50,250 e invalid 0,-1,251; identidade de sessão/save/restauração; invalidação após turno ou alteração de rede; determinismo e ausência de mutação.

Benchmark 8/32/128 sobre uma sessão física real no próximo ciclo V12 certificado VS85, usando recibos VS87, sem alteração de saves ou WorldState. Linux: regressões VS40–88; Windows: suíte completa; Godot Mono Release/Debug. 54 workflows esperados no mesmo SHA. PR Draft encadeada na VS87, sem merge na main.

## Próximo gate arquitetural

Antes de usar preço REAL para governar produção REAL, é preciso delimitar a substituição do cash-food legado por um mercado homogêneo, escolher a fórmula e o regime do poder de compra abstrato, fechar regra de apropriação/produção e integrar ao \`AdvanceMovementTurn\` com rollback, invariantes e save. A VS88 é deliberadamente só diagnóstico da tendência alimentar.
