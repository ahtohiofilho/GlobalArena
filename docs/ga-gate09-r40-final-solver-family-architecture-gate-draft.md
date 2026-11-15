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


---

## Registro de Decisão Arquitetural (ADR) — após homologação R40

**Status: DECISÃO ADOTADA no laboratório (não no jogo).**

A CI inicial no commit de laboratório
\`8e38dff972d038158c3bba7872da8fa2e7289c14\`,
run GitHub Actions **#43658754011**, concluiu em Windows e Ubuntu:
16/16 testes e 32/32 percursos por plataforma, numericamente
idênticos. Evidências de ambos os sistemas anexadas ao run.
A decisão emitida pelo próprio runner foi:

\`NO_GO_THREE_SOLVER_FAMILIES_REDESIGN_SOLVER_FORMULATION\`.

### Resultado quantitativo por fixture (mesmo teto R14)

Valores abaixo são por uma plataforma; a outra é replay,
NÃO duplicar denominadores. Cada fixture é combinada com
o R40 e três controles originais, em 42 ou 162 nós.

| Nós | D/oferta | K | Gap global R40 | Melhor controle global | Gap pior R40 | Melhor controle pior |
|---:|---|---:|---:|---:|---:|---:|
|42|original|0,20|0,008192|0,001321|0,010647|0,005276|
|42|original|1,00|0,008247|0,000583|0,010785|0,003356|
|42|heterogênea|0,20|0,008961|0,000324|0,014179|0,001987|
|42|heterogênea|1,00|0,008571|0,002201|0,011410|0,008578|
|162|original|0,20|0,009945|0,001497|0,014289|0,010313|
|162|original|1,00|0,005451|0,000837|0,006379|0,003323|
|162|heterogênea|0,20|0,010398|0,007344|0,013587|0,026022|
|162|heterogênea|1,00|0,009029|0,002980|0,012156|0,019752|

**R40 perdeu o gap global em 8/8 e venceu o gap do
pior produtor em apenas 2/8.** Nenhum dos 32 percursos
(all policies) atendeu às cinco condições simultâneas
durante três atualizações. Candidato usou 129/129 R14
nos mapas de 42 nós e 97/97 nos de 162 nós;
todas as candidaturas descartadas foram cobradas.
A CI PASSA como auditoria; o método recebe NO-GO.

### Registro das três famílias fechadas

- **R38:** troca coordenada de uma alocação de produtor,
  com reprecificação integral e busca de passo:
  perdeu gap global/pior produtor contra melhor controle
  nas oito fixtures e não convergiu.
- **R39:** lookahead preditor–corretor com dois estágios
  reavaliados por R14: também 0/8 em gap global e
  sem estabilidade, embora tenha superado R38.
- **R40:** atualização proporcional/entrópica
  simultânea com normalização por oferta e pequeno
  suporte positivo: 0/8 em gap global, 2/8 em pior
  produtor, nenhuma estabilidade.

Essa rejeição aplica-se às **três implementações
específicas e aos orçamentos/cenários testados**;
não prova que o jogo não admite um equilíbrio
e não invalida por si só a função P=sqrt(D/Q).

### Decisão formal sobre o próximo trabalho

**A rodada de tentativa iterativa é encerrada.
NÃO criar R41 como quarta família por ajuste de
alfa/eta ou nova instrumentação de rankings.**

Instituir um review de FORMULAÇÃO MATEMÁTICA antes
de permitir qualquer outro solucionador.

A hipótese inicial a formalizar (NÃO ratificada):
para cada produtor g, coluna de rota válida c,
fluxo x_gc >=0 e quantidade total
   sum_c x_gc = supply_g.
Dado o score original que depende de TODOS os
fluxos por preço e congestionamento:
   S_gc(x) = sqrt(D_m/Q_m(x)) / (1+C_gc(B(x))).
Introduzir λ_g como melhor score alcançável e testar
as condições de complementaridade:
   λ_g − S_gc(x) >=0,
   x_gc*(λ_g − S_gc(x)) = 0,
com demanda positiva e Q_m>0 para os mercados
alcançáveis que precisam ser precificados.

Essas condições capturam a política F072 de
produtores indiferentes no suporte e nenhum
mercado/rota inativa superior; NÃO demonstram
existência, unicidade nem monotonicidade de
um problema de complementaridade.

**Próxima revisão de arquitetura tem quatro entregáveis
obrigatórios e limitados:**

1. **Contrato matemático:** domínio viável, restrições
   de positividade Q, caminhos possíveis, quantidades
   e dependências Q→P / B→C / tarifas, mantendo
   oferta integral e score original.
2. **Condições de existência/regularidade:** avaliar
   analiticamente se o operador permite métodos
   de complementaridade, projeção ou variational
   inequality, identificando hipóteses não provadas
   e singularidade quando Q→0, sem impor demanda
   artificial para contorná-la.
3. **Especificação de solver alternativa:**
   selecionar uma formulação de resíduo de
   complementaridade por colunas e possíveis
   caminhos canônicos gerados pelo oráculo, com
   avaliação independente R13/R14 e critérios
   de aceitação; PROPOSTA DE ARQUITETURA, não
   quarta implementação automática.
4. **Decisão de produto:** manter a economia
   homologada enquanto for viável; qualquer
   alteração à fórmula de preços, custos, demanda,
   propriedade ou política comercial exige
   decisão do usuário, nunca consequência silenciosa
   de um experimento numérico.

**Gate para retornar à implementação:** somente
após revisão e aprovação explícita de um contrato
de equilíbrio e um critério de parada verificável.
F059 (desempate/titularidade financeira) permanece
uma decisão independente. C#/Godot e a main não
receberão código desta rodada, nem haverá merge
automático.

**Status final R40:** laboratório e CI validados;
candidato NO-GO; três famílias formalmente encerradas;
review de formulação matemática é a próxima fase.
