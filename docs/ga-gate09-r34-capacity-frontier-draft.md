# Global Arena — Gate 09 R34: fronteira de sensibilidade da capacidade (Draft)

Data: 14/11/2026. Base: PR #322/R33; baselines R20-R33 intactos.

## Hipótese e escopo

O R33 constatou que a reprodução do novo mercado pelo ranking
hipotético de preço novo/transporte antigo não é universal.
No cenário com capacidade x0,20 e demanda/oferta heterogêneas,
preço isolado reproduziu 381/447 inversões (85,2%), enquanto
transporte isolado reproduziu 119/447 (26,6%). Essas categorias
podem sobrepor-se e não constituem partilha causal.

O R34 explorará progressivamente a capacidade física das
arestas e a relação observada B/K, preservando exatamente
os custos c_free*(1+(B/K)^4), as tarifas originais e
a função de preço P=sqrt(D/Q).

## Matriz controlada

Fatores fixos sobre a capacidade de CADA aresta da fixture
R13: x0,15; x0,20; x0,25; x0,30; x0,40; x0,50;
x0,75; x1; x1,5; x4.

Regimes:
1. Demanda e oferta originais.
2. Demanda e oferta mosaico determinístico já usado no
   R33. A capacidade, contudo, volta a ser definida
   unicamente pelo fator R34, não acumulada ao x0,20
   da fixture combined_stress.

Cada fator é uma nova fixture independente, sem
mutações do mercado durante a simulação e sem alterar
o grafo lógico, a geografia, propriedade, tarifa ou
preço. Três políticas numéricas congeladas serão
comparadas: decaimento R21, backtracking R21,
progressiva R26. CI: 42 nós/24 updates e
162 nós/12 updates = 120 trajetórias.

## Metrificação e governança

Reportar para cada fator, regime, mapa e política:
- trocas comerciais totais e reproduzidas por preço
  novo/custo antigo ou preço antigo/custo novo;
- frações por eventos, por percurso e pior percurso;
- máximo de carga/capacidade e quantidade de arestas
  na condição B/K >=1;
- gaps agregados e por pior produtor, oscilações;
- exemplos auditáveis de trocas não reproduzidas
  por preços isolados, com destino original/novo
  e os componentes logarítmicos;
- limiares descritivos de 95% e 99% de reprodução
  de vencedor, com TODAS as faixas de capacidades
  adjacentes onde o indicador cruza o limiar.

Os limiares de 95%/99% são apenas RÓTULOS de diagnóstico
para comparação; não são um critério de equilíbrio e
não autorizam presumir comportamento monotônico.
O experimento não determinará um ponto de fase causal.

Invariantes: manifesto SHA256 R20 16/16, paridade
R13/R14, F-084F, 100% das vendas, replays R33/R21,
conservação numérica e contabilidade de R14 e scans.
Cinco condições simultâneas <=1e-4 durante três
snapshots consecutivos continuam mandatórias.
Horizon de CI R34 é menor que R33; comparação de
gaps precisa controlar orçamento.

F-059, existência/convergência global, solver
integrado C# e autorização de produção seguem
pendentes. PR R34 em Draft, empilhado sobre R33,
sem merge.
