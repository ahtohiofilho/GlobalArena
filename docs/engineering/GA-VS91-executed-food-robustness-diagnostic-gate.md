# GA-VS91 — Gate OBSERVACIONAL de robustez econômica multiciclo

**Issue:** #215. **Base:** VS90 PR #214 SHA \`a5d6c95d585463cb8c2610c5dbd8e7862b1ae9ed\` — 56/56 workflows \`success\`. **Guardrails:** GA-ADR-ECON-007 R2 (#188).

## Decisão de arquitetura

A VS90 autentica os preços de alimentos realmente pagos nas compras locais cash-food do motor V12 em janelas de **3 a 8 ciclos econômicos reais**. A VS89 fornece ganhos experimentais explicitamente escolhidos, sem autorizar produção. A VS91 cria um gate de **qualidade/sensibilidade dos dados**, não um gate de execução: testa a robustez da interpretação a **critérios declarados**, mantendo preço adimensional VS84 e preço nominal realmente pago V12 em domínios separados.

Não se pode concluir que uma variação de quantidade vendida foi causada por uma mudança na oferta ou demanda; demanda pode não ter sido atendida, oferta pode ter sido conservada, logística pode ter alterado o acesso ou o saldo monetário do solver legado pode ter limitado compras. A VS91 mede **volume de vendas realmente executado**, cobertura e oscilações, mas não inventa experiências causais de choques.

## Componentes

\`CanonicalMultiCycleFoodRobustnessGate.FromVerifiedHistory(session, historyVS90, policies)\` recebe:
- Sessão física canônica **corrente**, com \`historyVS90.RequireCurrent\` e \`VerifyIndependent\`, que exigem também a matriz VS89, a janela VS88 e as provas temporais/recibos VS85/VS87.
- 1 a 8 políticas imutáveis de \`CanonicalFoodRobustnessScenario\`, cada uma com identificador único de até 40 caracteres \`[A-Za-z0-9_-]\` e ganho \`s\` já certificado na matriz VS89/VS90.
- Cada política especifica \`minimumCommonPhysicalCoveragePermille\` entre 0 e 1000, \`maximumExecutedVolumeShiftPermille\` entre 0 e 1000, mínimo de janelas fisicamente elegíveis, mínimo de deltas não nulos e máximo de inversões consecutivas não nulas. Nenhum coeficiente de feedback é adaptado automaticamente.

Por janela real \`i→i+1\`, a coorte comparável é a interseção de tiles com preços **efetivamente pagos** dos dois ciclos, ponderada por \`min(q_i,q_{i+1})\`. A elegibilidade depende de **ambos**:

\[
1000\,q_\mathrm{comum}\ \ge c_\mathrm{mín}\,q_\mathrm{vendido,origem}
\quad\text{e}\quad
1000\,q_\mathrm{comum}\ \ge c_\mathrm{mín}\,q_\mathrm{vendido,destino}
\]

\[
1000\,|q_\mathrm{vendido,destino}-q_\mathrm{vendido,origem}|
\ \le v_\mathrm{máx}\,\max(q_\mathrm{vendido,origem},q_\mathrm{vendido,destino}).
\]

Todas as operações usam inteiros e \`BigInteger\`, sem erro de ponto flutuante. Apenas quando a janela é elegível e seus preços nominais do mesmo produto são positivos se calcula o sinal de tendência já usado na VS88–VS90: \`delta=trunc(s*(P_dest-P_orig)/(P_dest+P_orig))\`. Preços ausentes são **ausência**, não zero ou cotação imputada. Reversões são contadas somente entre **janelas adjacentes** com \`delta != 0\`; qualquer falta de cobertura, variação de volume além do limite, ausência de preço ou \`delta=0\` interrompe a continuidade.

O resultado \`CanonicalFoodRobustnessObservation\` informa volumes de janelas elegíveis/rejeitadas por cobertura/volume, deltas não nulos, reversões e máxima amplitude, além da classificação do cenário:

| Diagnóstico | Interpretação |
| --- | --- |
| \`NoComparableExecutedFoodSales\` | Nenhuma janela contém os mesmos tiles com compras efetivamente pagas |
| \`InsufficientPhysicalCoverage\` | Existem preços observados, mas nenhuma janela atende à cobertura declarada |
| \`ExcessiveObservedVolumeShift\` | Cobertura atende, mas nenhuma janela atende ao limite de variação de **vendas efetivas** |
| \`InsufficientComparableHistory\` | O número de janelas elegíveis é menor que o mínimo explícito |
| \`InsufficientNonzeroDirectionalEvidence\` | Deltas não nulos suficientes não foram observados |
| \`ExcessiveObservedReversals\` | O limite explícito de inversões consecutivas foi excedido |
| \`WithinDeclaredObservationalCriteria\` | A amostra cumpre os critérios **diagnósticos escolhidos**, mas NÃO comprova causalidade, acurácia preditiva ou segurança para promoção à economia real |

O último diagnóstico **não** libera utilização na simulação. Não existe método de aplicar preços, mudar ganho, executar \`SETTLE\`, autorizar crédito, alterar trabalhadores ou manipular o save. O gate não escolhe políticas “vencedoras”; o chamador decide os limiares e observa os diferentes resultados.

\`VerifyIndependent\` reconstrói todas as observações dos recibos canônicos verificados; \`RequireCurrent\` falha com sessão estrangeira, ação/tick novo ou mudança física da rede. A API \`EvaluateHypotheticalCriteria\` é um helper **puro** para comparar limiares e testar cenários fictícios, sem atestar proveniência dos dados; só \`FromVerifiedHistory\` associa observações à partida física.

## Alinhamento estratégico

Preservados os invariantes da economia GA: trabalhadores são residentes+produtores; povoamentos surgem de trabalho, não fundação formal; oferta potencial não vira estoque invisível; não existe limite material de vendas igual à demanda do consumidor na economia abstrata planejada; nenhum caixa fictício \`B=40\`, banco, crédito, juros ou obrigação futura é introduzido. O cash-food V12 é **legado**, e seus microcréditos NÃO são preços pagos de madeira, pedra, ferro, rotas ou mercados macro. Nenhuma mudança é feita em \`WorldState\`, \`AdvanceMovementTurn\`, save ou no ritmo econômico.

## QA / entrega

- Testes adversariais da cobertura em ambos os lados, limite exato da variação do volume de vendas, ausência de preço efetivamente pago, indeterminação em amostras pequenas, delta truncado a zero, reversões interrompidas por lacuna, ganhos não certificados, políticas duplicadas/fora dos limites, replay estrangeiro, obsolescência por turno e rede, determinismo e ausência de mutação.
- Benchmark 8/32/128 sobre prova de 3 ciclos reais da VS90 com três políticas explícitas: baseline 0/1000, alta cobertura 900/1000 e volume rígido 0/100, sem alteração de mundo.
- Gate Linux regressão VS40–VS91, Windows suíte completa, benchmark e compilações Godot Mono Release/Debug; **57 workflows esperados**. PR Draft sobre VS90; \`main\` inalterada e homologação restrita ao mesmo SHA quando todos forem \`completed/success\`.

## Próxima decisão

Antes de qualquer integração ao motor, construir observações reais de preços e quantidades para **mais bens**, compatíveis com poder de compra abstrato, e um desenho de experimentos capazes de separar os efeitos de oferta, demanda e logística. Este gate sinaliza **limites de observação**, não causa nem política econômica aprovada.
