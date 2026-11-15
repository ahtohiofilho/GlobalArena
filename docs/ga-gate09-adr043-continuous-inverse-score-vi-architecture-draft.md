# Global Arena — ADR-043: desigualdade variacional contínua do equilíbrio F-072

**Estado:** especificação D4 de arquitetura matemática, com prova de existência sob hipóteses explícitas; **NÃO é um solucionador implementado**.
**Base:** ADR-041/PR #330, ADR-042/PR #331, R40/PR #329, motor R20 original preservado.
**Data local:** 14/11/2026 (Pernambuco).
**Escopo:** laboratório, sem alteração em preço, transporte, propriedade, oferta, demanda, tarifas, impostos, F-084F, F-059 ou C#/Godot.

## 1. Decisão arquitetural recomendada

**Representar o equilíbrio comercial como uma desigualdade variacional
contínua (VI) de custo RECÍPROCO ao score F-072**, em domínio fechado
de fluxos não negativos e oferta exatamente conservada.

Esta representação é matematicamente equivalente à escolha da rota
de melhor score do F-072 em qualquer estado cujos mercados alcançáveis
tenham quantidade positiva. Na borda Q=0, permite uma extensão
CONTÍNUA do problema de equilíbrio, sem alterar a fórmula de preço.
O estado final continua sujeito ao **certificador estático ADR-042**
e à paridade original R13/R14.

**Decisão de método proposta para futuro projeto:** VI com conjunto
de colunas restrito, geração de colunas por separação de menor caminho
R14 e um núcleo de complementaridade (MCP/VI) com viabilidade de
simplex. Não iniciar numericamente esse núcleo antes dos gates finais
deste ADR. O núcleo concreto e seu método de globalização permanecem
uma seleção de implementação separada; não se anuncia uma quarta
tentativa de relaxação por melhor resposta.

## 2. Domínio exato do laboratório

O universo do snapshot é um grafo lógico FINITO com
arestas de custo físico c_free,e >0 e capacidade K_e >0.
Tarifas externas existentes são não negativas e dependem
do proprietário e do vendedor, não de um preço de mercado
artificial. Toda mercadoria tem demanda D_m>0 por mercado
cadastrado, e cada produtor g tem oferta s_g>0.

Para cada produtor g, C_g é o conjunto FINITO de suas
rotas simples válidas até qualquer mercado compatível
alcançável, incluindo diferentes caminhos para o mesmo destino.
Pelo menos uma coluna válida é exigida para cada produtor.

Há finitamente muitas rotas simples em um grafo finito,
embora enumerá-las possa ter custo exponencial. Cada
quantidade física x_gc satisfaz:

    x_gc >= 0
    sum_{c in C_g} x_gc = s_g.

Definir X como o produto cartesiano dos simplexes
dos produtores. X é **não vazio, compacto e convexo**
em dimensão finita. Sua definição não impõe Q>0;
a fronteira com mercados sem fluxo está incluída.

Mercado m e aresta e têm quantidades:

    Q_m(x) = sum_{g,c:destino(c)=m} x_gc
    B_e(x) = sum_{g,c:e pertence a c} x_gc.

Um mercado é **estruturalmente alcançável** quando
existe ao menos um g com mesmo produto e rota válida
até ele. A fronteira de mercados que NÃO têm esse
produtor é excluída das exigências de preço e do
certificado de equilíbrio (conforme ADR-042).

## 3. Transformação exata do score em custo recíproco

A formulação ORIGINAL F-072 nos estados Q_m>0 é:

    P_m(x)   = sqrt(D_m/Q_m(x))
    C_gc(x)  = sum_{e em c} (
                   c_free,e * [1+(B_e(x)/K_e)^4]
                   + tarifa_e(vendedor_g))
    S_gc(x)  = P_m(x) / [1+C_gc(x)].

Definir o custo matemático recíproco:

    H_gc(x) = [1+C_gc(x)] * sqrt(Q_m(x)/D_m).

Para todo Q_m>0, a identidade é EXATA em números reais:

    H_gc(x) * S_gc(x) = 1.

Como S_gc>0, temos, para quaisquer duas rotas de um
MESMO produtor com Q positivo:

    S_gc1 >= S_gc2  <=>  H_gc1 <= H_gc2.

Assim, **minimizar H não introduz preferência
comercial diferente da maximização de S**. É a
mesma ordenação econômica, escrita inversamente
apenas para facilitar a matemática da solução.

Quando Q_m=0, definir H_gc=0 pela sua própria fórmula.
Como sqrt(Q/D) tende continuamente a zero e
os custos de aresta são finitos no compacto X,
essa extensão é CONTÍNUA para TODO x em X.

O valor H=0 nesse ponto é uma extensão analítica
DO CUSTO INVERTIDO. NÃO representa preço zero,
preço fictício, consumo efetivo, receita, venda ou
cotação de um mercado. O preço F-072 ORIGINAL
permanece indefinido quando Q=0, e o ADR-042
continua BLOQUEANDO tal snapshot.

**Distinção fundamental:** esta definição não
altera P=sqrt(D/Q), não cria piso Q=epsilon e
não altera o valor de H em nenhum Q>0.

## 4. VI e complementaridade equivalentes

Defina o vetor H(x) sobre TODAS as colunas comerciais
finitas. O problema VI(H,X) é encontrar x* em X com:

    sum_{g,c} H_gc(x*) [y_gc - x*_gc] >= 0
    para todo y em X.

X separa-se por produtor. Para cada g, essa
desigualdade equivale a colocar massa positiva
SOMENTE em colunas que minimizam H_gc(x*):

    mu_g = min_{c em C_g} H_gc(x*)
    H_gc(x*) - mu_g >= 0
    x*_gc >= 0
    x*_gc [H_gc(x*) - mu_g] = 0
    sum_c x*_gc = s_g.

**Em estados em que todos os mercados alcançáveis
têm Q_m>0**, S=1/H e minimizar H equivale
exatamente às condições F-072 do ADR-041:

    lambda_g = max_c S_gc(x*)
    lambda_g - S_gc(x*) >= 0
    x*_gc [lambda_g - S_gc(x*)] = 0.

A formulação NO VETOR H não substitui o resíduo
de certificação comercial do ADR-042. Os números
dos resíduos de H e S NÃO são idênticos, embora
ambos sejam zero exatamente nos mesmos equilíbrios
interiores. A aprovação numérica final exige os
gaps do ADR-042, não uma tolerância arbitrária em H.

## 5. Teorema de existência — hipóteses e demonstração

**Teorema (somente para o modelo FINITO explicitado).**
Se cada grupo possui s_g>0 e ao menos uma rota
válida, cada demanda cadastrada D_m>0, o grafo
tem finitas arestas com custo positivo e capacidade
positiva, e as tarifas atuais são finitas e não
negativas, então existe pelo menos um equilíbrio
comercial para o F-072, com Q_m>0 em TODO mercado
estruturalmente alcançável.

### Demonstração, parte A — VI possui solução

1. X é um produto finito de simplexes não vazios,
   logo é compacto, convexo e não vazio.
2. Cada Q_m(x) e B_e(x) é uma soma linear de x.
   Os custos físicos e a tarifa fixa são contínuos.
   A raiz sqrt(Q_m/D_m) é contínua inclusive em Q=0.
   Portanto TODAS as componentes H_gc(x) são
   contínuas em TODO X.
3. Fixe QUALQUER eta>0 e use a projeção euclidiana
   no convexo fechado X. A função

       T_eta(x) = Proj_X( x - eta*H(x) )

   é contínua de X em X. O teorema de Brouwer
   garante algum ponto fixo x*=T_eta(x*).
4. A condição variacional da projeção implica
   a desigualdade VI(H,X) exibida acima.
   Logo, existe ao menos um x* que resolve VI.

**A função T_eta é apenas instrumento da PROVA
de existência. NÃO é a escolha de um solucionador,
não implica convergência da sua iteração
e NÃO autoriza um quarto candidato R41.**

### Demonstração, parte B — nenhuma demanda alcançável fica em Q=0

1. Suponha, por contradição, que um mercado m
   estruturalmente alcançável tenha Q_m(x*)=0.
2. Pela definição de alcançabilidade, existe
   algum grupo g de mesma mercadoria e uma coluna
   comercial c0 até m. Como Q_m=0, temos
   H_g,c0(x*)=0.
3. O mesmo grupo g possui oferta s_g>0, totalmente
   alocada. Assim, existe ao menos uma coluna
   c1 com x*_g,c1>0, cujo destino n tem
   Q_n(x*)>=x*_g,c1>0.
4. Como D_n>0, c_free>0, K>0 e o caminho é
   finito, temos [1+C_g,c1(x*)]>0 e,
   portanto, H_g,c1(x*)>0.
5. Mas a complementaridade da VI exige que
   TODA coluna financiada de g tenha o custo
   mínimo do grupo. Isso contradiz a existência
   da coluna alternativa c0 com H=0.

Logo, Q_m(x*)>0 para todo mercado alcançável.
Nesses mercados S=1/H é bem definido e a
VI satisfaz a complementaridade ORIGINAL F-072.
**Fica demonstrada a existência de ao menos
um equilíbrio F-072 no modelo finito especificado.**

### O que o teorema NÃO conclui

- Não prova unicidade do vetor de fluxo; ADR-041
  já fornece contraexemplo com múltiplos equilíbrios.
- Não prova monotonicidade do operador H, convexidade
  de potencial, taxa de convergência, tempo de CPU,
  robustez da implementação ou solução de qualquer
  orçamento R14 observado.
- Não garante os mesmos resultados quando a
  economia FUTURA mudar de modo estrutural, por
  exemplo com demanda zero, custo negativo,
  redes infinitas, efeitos de crédito ou restrição
  adicional de fluxo que destrua o simplex.
- Não resolve F-059, propriedade financeira
  de um empate ou liquidação de tarifas.

**Este é um teorema matemático condicionado a
hipóteses verificadas pelo código do laboratório,
não uma afirmação de que a CI provou convergência.**

## 6. Operação futura: conjunto restrito e separação global

A enumeração de todas as rotas simples é finita
mas computacionalmente proibitiva em mapas grandes.
O contrato recomenda trabalhar com um subconjunto
A_g de colunas por produtor, preservando todas as
rotas atualmente financiadas e pelo menos uma
coluna viável inicial para cada mercado alcançável
quando viável fisicamente.

A parte que poderá ser delegada ao solucionador
RESTRITO é encontrar fluxos não negativos,
conservadores e preferencialmente positivos
por mercado que minimizem um RESÍDUO VI em A_g,
com preço calculado da distribuição inteira.

**A separação externa não pode ser dispensada.**
Para o estado x em avaliação, reconstruir TODAS as
cargas, custos e o conjunto completo de caminhos
de menor impedância por vendedor com FrozenRouteCache
original. Como sqrt(Q/D) é igual para todas as rotas
de um mesmo destino, minimizar H até esse destino
equivale a minimizar C, e a rota canônica de menor
custo original é um representante válido do mínimo.

Para cada mercado alcançável, comparar

    H_melhor(g,m;x) =
        [1 + C_menor_caminho(g,m;B(x))]
        * sqrt(Q_m(x)/D_m).

Esse oráculo examina O(N_mercados) alternativas
por varredura de caminhos, sem enumerar a priori
todas as rotas simples do grafo.

Caso exista uma coluna global omitida com custo
estritamente inferior (dentro de tolerâncias
explicitadas) ao nível do problema restrito,
adicionar o caminho canônico. Um solver
que satisfaça apenas complementaridade em A_g
não é certificado até passar pela separação global.

Rotas atualmente financiadas que se tornarem
não-canônicas DEVEM continuar presentes no
resíduo comercial e no orçamento enquanto
tiverem massa positiva. Podas são mudanças
de representação NUMÉRICA, nunca apagamento
de produção física.

## 7. Contrato de parada e três camadas distintas

**Camada 1 — domínio e conservação:**
- Toda oferta física de cada grupo foi totalmente
  conservada em colunas válidas.
- Todas as quantidades são não negativas e finitas.
- Todos os mercados estruturalmente alcançáveis
  do estado FINAL têm Q_m>0.
- Preços, cargas, custos e tarifas ORIGINAIS
  foram reconstruídos no snapshot final.

**Camada 2 — certificado estático independente:**
- Rodar INTEGRALMENTE ADR-042 sobre o estado final.
- R14 e R13 originais coincidem em preço, rota
  ótima, carga, Q, tie flags e gaps até suas
  tolerâncias originais.
- GAP global e GAP do pior grupo F-072 <=1e-4.
- Todas as oportunidades comerciais alcançáveis
  são cobertas por separação global de caminhos.
- F-084F conserva gross_micro=credits_micro;
  F-059 continua pendente quando houver empate.
- Não aprovar somente com resíduo H pequeno:
  a condição numérica definitiva é o F-072 original.

**Camada 3 — dinâmica e operacionalização:**
- Caso um algoritmo venha a ser escolhido,
  testar replay determinístico, resposta a
  perturbações, sensibilidade a capacidade,
  volumes e desempenho em 42/162 nós.
- Registrar evolução das variações de preço,
  carga e Q, passos aceitos/rejeitados e
  trabalho total de oráculos.
- **Não** exigir três movimentações artificiais
  de um estado já aprovado estaticamente
  somente para fazê-lo parecer estável.
- R24/R38–R40 continuam com seus históricos
  gates e NO-GO sem qualquer revisão retroativa.

## 8. Limites operacionais para futura implementação

A especificação recomendada do futuro módulo
de resolução deve conter contratos de trabalho:

- CADA avaliação completa R14/R13 e CADA
  scan de caminho/custo deve ser contabilizado,
  inclusive candidatos rejeitados.
- Usar fixtures 42/162 nós, regimes normal/
  heterogêneo e congestionamento normal/K×0,20,
  além dos dois exemplos simétricos do ADR-041.
- Declarar teto de chamadas e número de colunas
  ANTES de um ensaio e mantê-lo nos controles;
  129/97 R14 do R38–R40 são referência histórica,
  não teto magicamente adequado ao novo
  problema de complementarity.
- Preferir comparação por GAP ORIGINAL sob custo
  oracular, e não por número de passos ou número
  de PRs. Se um solver errar Q ou perda de
  produção, isso é falha de segurança, não NO-GO
  matemático.
- Distinguir impossibilidade de conferir preço
  numa iteração parcial de inviabilidade da VI:
  H existe em Q=0, mas o ADR-042 corretamente
  bloqueia certificação FINAL nesse caso.
- Testar múltiplos equilíbrios, mercado vazio
  inicial, rota não canônica paga, empate global
  e tarifação dependente do vendedor.

**Não aprovar um solucionador só porque VI
admite solução.** O teorema de existência e o
método computacional são questões diferentes.

## 9. Decisões tomadas, reservas e próximo gate

**D4 — Seleção de FORMULAÇÃO do problema:**
aprovar como linha-base de arquitetura o
VI de custo recíproco contínuo H=1/S no
interior, com extensão H=0 no limite Q=0,
e resolver complementaridade de fluxos
com separação global de menor caminho.

**D4 — Seleção do algoritmo numérico:**
NÃO tomada neste PR. A escolha entre
métodos de MCP, raiz de complementaridade,
resíduo de VI com conjunto ativo ou outra
técnica apropriada requer uma especificação
separada e testes de viabilidade; não se
presume que o iterador T_eta da prova seja
um algoritmo convergente.

**Decisão financeira:** F-059 continua
independente, irresoluta e não homologada.
**Decisão do jogo:** NENHUMA regra
econômica, C#/Godot nem branch main alterada.

**Gate para próxima etapa:** criar uma
especificação executável MINIMAL de contrato
de entradas/saídas e residual/complementarity
para que possamos escolher um núcleo numérico
após a prova de existência, comparando
desempenho com o ADR-042 como juiz
independente, sem retomar a sequência
de diagnósticos R30–R37.

Este ADR autoriza apenas a revisão e
confirmação da MATEMÁTICA, não integração
nem merge automático.
