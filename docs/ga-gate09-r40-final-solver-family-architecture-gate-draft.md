# Global Arena — Gate 09 R40: encerramento da rodada de métodos e decisão de arquitetura

Data base: 14/11/2026, Pernambuco. PR R39 #328 como baseline.
R20–R39 e motor autoritativo C#/Godot permanecem inalterados.

## Escopo executivo vinculante

R38 concluiu NO-GO para método coordenado por produtor.
R39 concluiu NO-GO para método simultâneo preditor–corretor.
A política aprovada no R37 limita esta frente a **três famílias**,
sem retornos intermináveis à análise de inversões comerciais.

**R40 é a terceira e ÚLTIMA família da rodada.**
Teste separado, comparativo e falsificável, e no fim
registre uma decisão definitiva sobre este ciclo:
  - PROVISIONAL GO PARA VALIDAÇÃO INDEPENDENTE se cumprir
    os contratos na íntegra, sem certificar produção;
  - NO-GO DAS TRÊS FAMÍLIAS + DECISÃO DE REVISAR A
    FORMULAÇÃO DO SOLUCIONADOR, se falhar qualquer fixture.

## Hipótese matemática distinta

Atribuir pesos proporcionais a TODOS os caminhos comerciais
financiados e rotas canônicas de cada mercado compatível.
O score de cada coluna é o original P/(1+C), com
P=sqrt(D/Q), custo físico c_free*(1+(B/K)^4)
e tarifa do vendedor, sempre obtidos no mesmo estado F072.

Criar suporte numérico pequeno — 4% da própria oferta
distribuída dentro de cada grupo — para não prender
mercados a pesos iniciais estritamente nulos.
Não é nova demanda, preferência, renda nem regra do jogo.

Para coluna c de produtor g:
  b_gc = 0,96*x_gc + 0,04*supply_g/|C_g|
  w_gc = b_gc * exp(eta*(S_gc/Sbest_g - 1))
  x'_gc = supply_g*w_gc/sum_C w_gc.
Usar eta 2, 8, 24 e 48 predefinidos. Avaliar
cada candidato com o oráculo R14 original completo.
Primeira redução estrita do mérito numérico
M=gap_global + 0,3*gap_pior e sem piora do gap
global original será escolhida; se inexistente,
melhor candidato já avaliado será identificado
como fallback explícito (sem fingir convergência).

"Espelhamento entrópico" é descrição operacional da
regularização multiplicativa; não se assume prova
de convergência de mirror descent clássico.

## Comparabilidade com R38–R39

8 fixtures: mapas de 42/162 nós, demanda e oferta
original ou heterogênea, capacidades relativas
x0,20 e x1,00. Três políticas originais intactas:
R21 decay_sqrt, R21 backtracking e R26 progressive_global.

Mesmos tetos R14: 129 consultas (42 nós), 97
(162 nós), incluindo todas as tentativas descartadas.
Contabilizar custo separado de scans independentes
de rotas. Mesmo teto é comparabilidade operacional,
não igualdade de chamadas efetivamente usadas.

## Gate objetivo inegociável

Cinco condições <=1e-4 simultâneas por três estados
atualizados consecutivos:
- gap relativo GLOBAL F072;
- gap do PIOR produtor;
- maior variação relativa de PREÇOS;
- maior variação relativa da CARGA das arestas;
- maior variação relativa de QUANTIDADES por mercado.

Adicionalmente vencer o MELHOR controle existente,
independentemente em gap global e no pior produtor,
em todas as oito fixtures.

Os princípios de economia definidos com o usuário
não podem ser modificados para fazer um método passar:
receita por venda da produção, demanda exógena por
atividade, P=sqrt(D/Q), F-072 P/(1+C), custo e tarifa
canônicos, propriedade, infraestrutura e conservação
financeira permanecem congelados.

## Decisão se NO-GO — sem quarto candidato

A homologação do NO-GO exige registrar expressamente:
  NO_GO_THREE_SOLVER_FAMILIES_REDESIGN_SOLVER_FORMULATION.

O próximo trabalho é um **REVIEW DE ARQUITETURA MATEMÁTICA**,
não um experimento R41 renomeado. Revisar a formulação
do problema de equilíbrio: caracterizar condições de
existência, continuidade e regularidade do operador;
avaliar viabilidade de especificar resíduos de
complementaridade para fluxos sob restrições simplex;
estudar como preço, congestionamento e dependência
de rota interagem com complementaridade; escolher
formalmente entre solver de viabilidade, equilíbrio
aproximado ou necessidade de mudança explícita da
economia — NUNCA alterar silenciosamente.

Esta decisão **NÃO** equivale a afirmar que o equilíbrio
é impossível. Os experimentos só rejeitam as três
famílias e orçamento específico; a economia do jogo
permanece válida como hipótese até revisão científica.

Manifesto R20 16/16, paridade R13/R14, F084F, 100%
venda, replay dos controles e preços positivos são
cláusulas de veto. F059 continua questão financeira
independente. Sem ratificação automática, merge,
C# ou Godot. PR Draft empilhado em R39.
