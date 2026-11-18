# Global Arena — Gate 01B.2D-22: provas de dominância não negativa no caminho residual

**2026-11-17 · implementação experimental C# · PR Draft empilhado sobre #414 · sem merge**

## Contexto e objetivo

O Gate 21 reduziu em 23,51% as alocações gerenciadas acumuladas na busca residual do cenário com 384 mercados, mas ainda permaneceu aproximadamente 1,93 GiB de alocação acumulada. Nenhuma dessas quantidades deve ser tratada como RAM residente. Este Gate 22 investiga **comparações algébricas suficientes para rejeitar ou validar uma aresta sem criar um BigInteger intermediário**, conservando a matemática com exatidão arbitrária e a mesma ordem das decisões.

## Implementação delimitada

No Dijkstra de custo reduzido, sejam `c = cost(u,v)`, `p[u]` e `p[v]` os potenciais exatos, e `h[u] = distanceReduced[u] + p[u]` os rótulos deslocados. A referência do Gate 21 checa em toda aresta residual com capacidade:

- `p[u] + c >= p[v]` (custo reduzido não negativo);
- `h[u] + c < h[v]` para atualizar o destinatário.

O caminho **opt-in interno**, ativado por padrão neste Gate, usa provas de dominância seguras:

1. Se `c >= 0` e `p[u] >= p[v]`, a invariante `p[u] + c >= p[v]` já está provada apenas por comparação, sem somar `BigInteger`. Em qualquer outro caso, executa a mesma soma e verificação da referência; **nenhuma aresta positiva deixa de ter a invariante validada**.
2. Depois de validar, se `c >= 0` e o destinatário já tem rótulo `h[v]` tal que `h[u] >= h[v]`, então `h[u] + c >= h[v]`; o destino não pode melhorar. Não se calcula a soma do candidato nem se altera a fila.
3. Se `c = 0`, o candidato é exatamente `h[u]` sem soma BigInteger. Nas outras situações não dominadas, mantém-se a soma e comparação BigInteger exatas anteriores.

Nenhum número é convertido a `Int128`, `double` ou moeda arredondada. Potencial, prioridade `(BigInteger, nodeId)`, condição de empate estrito, arestas reversas negativas, segmentos de consumo, conservação física, orçamento de augmentations e auditoria continuam inalterados. O caminho anterior é disponibilizado apenas aos testes com `useResidualDominanceFastPath:false`; a API pública não muda.

## Certificação A/B

O cenário unitário testa frete com mais de 240 bits, arcos reversos residuais, mercados parcialmente alcançáveis, custos iguais, ambas filas (indexada e BCL) e ambos os workspaces; inclui orçamento exaurido e mercado inalcançável. A comparação original x dominância exige igualdade de alocações, total de mercado, potenciais BigInteger integrados e segmentados, e augmentations.

Os cenários Goldberg de trabalhadores reais preservam exatamente as dimensões do Gate 21:

| Fine tiles conectados | Produtores reais | Mercados demandantes | Pares potenciais |
|---:|---:|---:|---:|
| 112 | 8 | 104 | 832 |
| 208 | 16 | 192 | 3.072 |
| 320 | 24 | 288 | 6.912 |
| 416 | 32 | 384 | 12.288 |

Cada produtor vende um milhão de micro-bens e demanda como trabalhador. Os testes executam os dois modos sobre o mesmo mundo, com fila indexada, rótulo deslocado, quatro segmentos por mercado e orçamento 8192 augmentations. Devem coincidir, sem tolerâncias, as entregas ordenadas, rotas e carga por aresta, demanda, frete, pontuações BigInteger, contagem de augmentations, `StateHash` e `NoClosedCycle`; nenhuma liquidação é criada.

A CI Ubuntu/Windows arquiva quatro JSON por runner no schema `ga-next.exact-residual-dominance-ab.v1`, valida as conservações e os nove campos de custo/escala no script PowerShell, e exige cinco workflows de regressão em verde. Medições de `Stopwatch` são **tempo de parede de uma única observação**; `GC.GetAllocatedBytesForCurrentThread` é fluxo cumulativo de alocações gerenciadas da thread, não RAM simultaneamente alocada.

## Critério de decisão

Um A/B perfeito garante correção, não melhoria de performance. Uma regressão de tempo ou de alocações gerenciadas deve ser relatada como tal; o caminho original pode ser restaurado como padrão sem perder o oráculo. Não se extrapolará escala de 416 tiles /384 mercados para milhares de nós, e não haverá simplificação artificial de demanda, tarifas ou preço. **Governança:** manter Draft, sem alterações na `main`.
