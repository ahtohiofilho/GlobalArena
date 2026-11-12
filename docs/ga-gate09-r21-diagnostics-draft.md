# GA Gate 09 R21 — Convergência: plano de diagnóstico controlado (Draft)

Data: 12/11/2026. Dependência experimental: PR #309/R20, commit 9cbc0734782d716e76927eba7316c59fa9af2022.

## Diagnóstico inicial
O R20 aprovou paridade econômica de A/B (R13 vs cache R14), conservação das receitas e execução em Windows/Linux. A redução do tempo por atualização não demonstrou convergência. Nos quatro portes ampliados, os gaps finais observados foram 0,070244 / 0,183124 / 0,358071 / 0,422464, acima de 0,0001. Os cenários R13 limitavam-se a até 10 iterações.

## Perguntas que o R21 precisa responder
1. O gap F-072 desce a um patamar menor quando o orçamento passa de 10 para 48, 96 ou 192 passos?
2. Os reajustes de preço e carga estabilizam, ainda que o gap permaneça alto?
3. Qual a influência de alpha fixo, amortecimento decrescente e seleção de passos via recálculo total?
4. O melhor checkpoint é substancialmente melhor que o final, apontando oscilação?
5. Há persistência de empates F-059 ou alternância de rotas que impeçam interpretação financeira única?

## Critérios e fronteiras
- O R21 registra, em cada snapshot, gap relativo, pior grupo, fluxos e mudanças de preço/carga. Primeira ocorrência estável requer **três checkpoints seguidos**, com os dois gaps e as duas mudanças abaixo de 1e-4. É evidência numérica local, não teorema de equilíbrio.
- Original R13 e cache R14 são comparados sobre estado congelado; o rateio F-084F é auditado em checkpoints independentes.
- Produção integral vendida, preço positivo nos mercados alcançáveis e igualdade monetária das receitas/créditos são condições obrigatórias.
- Nenhum indicador de sucesso operacional implica ratificação F-059, convergência global, paridade C# end-to-end ou aptidão de produção.
- As cinco políticas são experimentais; não automatizar a escolha de uma delas para o motor do jogo com base apenas no menor gap.
- Nenhuma alteração em código autoritativo, WorldState, journal, saves, arquivos do R20 ou PRs #307–#309. PR R21 empilhado sobre ramo do R20, mantendo ambos em Draft.

## Decisão posterior, condicionada à evidência
Se o gap permanecer alto, investigar sensibilidade por mercado, índice de sobrecarga e mudança da melhor rota; somente então implementar um novo solucionador e ensaios de estabilidade/certificação. Se o gap diminuir, medir custo marginal por unidade de redução de gap e robustez a múltiplos cenários, evitando confundir eficiência por iteração com tempo para equilíbrio.
