# Global Arena — Gate 09 R35: derivadas locais de preço e transporte (Draft)

Data: 14/11/2026. Base: PR #323/R34; R20–R34 preservados.

## Objetivo de pesquisa

O R34 encontrou diferenças significativas entre sensibilidade
de preços e transporte em redes com congestionamento forte.
Os intervalos observados não são limiares físicos universais.
O R35 investiga a resposta local às pequenas realocações de
produção entre dois mercados, sem alterar a quantidade total
produzida, a demanda ou os preços canônicos do jogo.

## Derivação e instrumentação

Para um grupo produtor, escolher a coluna comercial de origem
com maior quantidade positiva e uma coluna de destino com
mercado diferente, ligada por rota canônica R14.

Construir um ponto interior com pequena realocação de origem
para destino; avaliar nele o oráculo integral. Criar duas
perturbações simétricas ±epsilon, em dois tamanhos, sempre
conservando a soma por produtor. Avaliar integralmente os
novos preços e congestionamento, sem aplicar a perturbação
à trajetória econômica principal.

Com Q_A,Q_B >0, P=sqrt(D/Q) impõe:
 d ln(P_B/P_A)/dδ = -1/2*(1/Q_B+1/Q_A).

Para cada aresta, congestionamento físico original é
 c_free * (1+(B/K)^4).
Sua derivada direcional em um fluxo conservativo é
 c_free * 4 * (B/K)^3 * s_e/K,
sendo s_e=-1,0,+1 conforme a aresta seja usada
somente na rota de origem, em nenhuma/ambas, ou
somente na rota de destino. A tarifa original não
depende de δ e, portanto, sua derivada é zero.

Confrontar derivadas analíticas e diferenças finitas
centradas da vantagem logarítmica entre mercados
com ambos os preços e os caminhos mínimos R14
recalculados. Descontinuidades de caminho e de
mercado vencedor são registradas separadamente.
Não certificar a derivada do custo de uma rota fixa
quando caminhos mínimos mudarem em ±epsilon.

## Matriz e limite de generalização

Duas regimes fixos de demanda/oferta (originais,
heterogêneos), seis capacidades relativas ao R13
(x0,15, x0,25, x0,40, x0,50, x1, x4),
dois portes 42 e 162 nós, duas políticas R21
decaimento e backtracking. 48 trajetórias CI,
com até dois produtores de maior arrependimento
por checkpoint inicial e final (amostragem dirigida,
não independente ou representativa).

Duas larguras de diferença finita por produtor
sondado. Cada direção consome CINCO avaliações
R14 extras (ponto interior e quatro avaliações
simétricas); consultas rejeitadas pelas políticas
e scans separados de rota também são cobrados.

## Gate de saída

R20 manifesto SHA-256 16/16, paridade R13/R14,
100% das vendas, F-084F, preço positivo e curva
original, trajetórias R34/R21 intactas e reprodutíveis.
As cinco condições de estabilidade simultânea
<=1e-4 por três atualizações não se alteram.

Aprovação do R35 representa APENAS auditabilidade
das sensitividades locais. Não estabelece derivada
global contínua, causalidade, ponto de equilíbrio
ou autorização de alterar o solucionador.

F-059, convergência global, portabilidade completa
do solver C# e produção continuam pendentes.
PR Draft empilhado sobre R34, sem merge.
