# Global Arena — Gate 09 R23: experimento controlado de estabilização (Draft)

Data: 13/11/2026. Base: PR #311/R22 (diagnóstico aprovado nas duas
plataformas), por sua vez empilhado sobre R21/R20.

## Questão econômica e computacional

O R22 identificou um gargalo possivelmente local (42 nós: 97,8% do gap
concentrado nos três maiores contribuidores), mas os mesmos três grupos
representaram apenas 34,7% num cenário de 162 nós. A análise não autoriza
concluir que o gargalo é sempre local, tampouco que variações temporárias
pequenas no gap global caracterizem equilíbrio.

Queremos testar se o uso de taxas de relaxação distintas por produtor
reduz a instabilidade causada por redistribuições simultâneas, sem alterar
os preços, demandas, custos, congestionamentos, tarifas ou receitas.

## Desenho experimental

- Todos os grupos preservam a busca pelo melhor mercado e caminho do F-072
  no mesmo estado congelado de preços/cargas.
- Atualização dirigida: quarto superior da contribuição ponderada absoluta
  ao arrependimento recebe alpha=1/8, demais alpha=1/32.
- Controle negativo: inverter as alphas mantendo o mesmo critério de ranking.
- Controle normalizado por esforço: alpha único igual à média ponderada
  pela oferta das alphas do modelo dirigido, em cada snapshot.
- Referências homologadas: alpha fixo 1/8 e decaimento progressivo R21.
- Todos: mesma semente e orçamento de atualizações, uma avaliação R14
  principal por passo; sondagens no mesmo estado congelado com custos
  registrados à parte.

## Critério de observação numérica

O gate exploratório exige simultaneamente: gap agregado, pior grupo,
variação máxima de preços, variação máxima de carga e variação máxima
de quantidade <= 1e-4 por três snapshots consecutivos. A satisfação
não certifica equilíbrio global nem implica viabilidade financeira F-059.

Para isolar o mecanismo, exigir reconstituição determinística do R20
para alfa homogêneo, coincidência da trajetória F-072 do R21 original,
venda integral, preços finitos e rateio F-084F conservativo.

## Limites de decisão e próximos passos

Se a política dirigida melhorar consistentemente tanto o gap agregado
quanto o pior grupo frente aos dois controles, ela vira candidata para
R24; se piorar ou trouxer trade-offs, manter a política como hipótese
rejeitada no cenário e investigar seleção de passos/trust region.
Preços/cargas elevados devem ser tratados como critérios de veto numérico,
não disfarçados por uma métrica de gap global.

F-059, prova de convergência, paridade C# do solucionador completo e
promoção a produção permanecem **abertos**. PR R23 Draft sobre R22;
nenhum merge ou alteração do motor autoritativo.
