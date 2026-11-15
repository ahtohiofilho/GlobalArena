# Global Arena — ADR-041: contrato de equilíbrio por complementaridade

**Estado:** REVIEW MATEMÁTICO CONCLUÍDO; ESPECIFICAÇÃO PROPOSTA, AINDA NÃO RATIFICADA PARA IMPLEMENTAÇÃO.
**Data:** 14/11/2026, Pernambuco (execuções CI posteriores podem apresentar 15/11 UTC).
**Base imutável:** PR #329 / R40; arquivos R20–R40 e kernel C#/Godot NÃO alterados.
**Decisão anterior vinculante:** NO_GO_THREE_SOLVER_FAMILIES_REDESIGN_SOLVER_FORMULATION.

> Este ADR é a entrega da revisão de arquitetura exigida depois de R40.
> NÃO é o quarto solucionador, NÃO executa ensaio novo de otimização,
> NÃO abre investigação adicional dos rankings e NÃO ratifica F-059.
> Um PR Draft não autoriza integrar o contrato ao motor sem aprovação específica.

## 1. Evidência original e problema que realmente queremos resolver

As fontes econômicas que fundamentam esta especificação são:

- experiments/gate09/r20/baseline/gate09.py:
  Edge.congestion_cost, Edge.weight_for, Group, Market, Column,
  Network.snapshots, Network.validate_column e commercial_gap.
- experiments/gate09/r20/baseline/clearing_r5.py:
  _reachable, seed_accessible_markets, _evaluate, _mix, _change_metrics.
- experiments/gate09/r20/baseline/oracle_cache_r14.py:
  FrozenRouteCache.paths/weights e evaluate_r14.
- experiments/gate09/r24/ci/run_r24.py: numerical_pass.
- docs/ga-gate09-r40-final-solver-family-architecture-gate-draft.md:
  fechamento das três famílias e mandato desta revisão.

O R14 já computa uma observação completa dos fluxos em um estado
congelado: quantidades dos mercados, preços, carga das arestas,
melhor rota/mercado de cada produtor e arrependimento comercial.
R13 e R14 foram exigidos como oráculos independentes nas
homologações anteriores. **Não devemos substituir este conteúdo
por uma função de demanda nova ou por um índice de satisfação.**

R38 (troca coordenada), R39 (predição–correção) e R40 (atualização
proporcional) não cumpriram o gate de solução nas oito fixtures,
sob os tetos de consultas R14 predefinidos. Isso rejeita as três
**implementações testadas**, não prova inexistência do equilíbrio.

## 2. Domínio físico de uma civilização econômica congelada

O grafo lógico é finito. Para cada grupo produtor g:

- s_g > 0 é a quantidade física já produzida; seu total é fixo.
- o_g, k_g e v_g são origem, mercadoria e vendedor/civilização.
- C_g contém **todas as colunas comerciais viáveis**, cada uma
  com mercado de destino m(c), mesma mercadoria k_g e um caminho
  simples válido de o_g a m(c). Caminhos diferentes ao mesmo
  mercado são alternativas comerciais distintas.
- x_gc >= 0 é a fração física de sua produção enviada por c.
  A igualdade de conservação é sum_{c in C_g} x_gc = s_g.

O domínio global de oferta é o produto dos simplexes
    X = product_g { x_g >= 0 : sum_c x_gc = s_g }.

Os produtos k e as demandas dos mercados D_m > 0 são FIXOS
no snapshot. Nenhum comprador recebe carteira simulada e
nenhum estoque é adicionado.

Para mercado m, aresta e:
    Q_m(x) = sum_{g,c:m(c)=m} x_gc
    B_e(x) = sum_{g,c:e in c} x_gc.

O código original usa custo por aresta para o vendedor:
    w_e^g(x) = c_free,e * [1+(B_e(x)/K_e)^4] + tau_e(v_g).
K_e > 0; c_free,e > 0. tau_e é a tarifa existente para
terceiro não isento, produzida por Edge.weight_for, e NÃO
depende numericamente do fluxo no snapshot. Tarifas e
titularidade permanecem as originais.

    C_gc(x) = sum_{e in c} w_e^g(x)
    P_m(x)  = sqrt(D_m / Q_m(x))
    S_gc(x) = P_{m(c)}(x) / [1+C_gc(x)].

C é a impedância generalizada definida no R20; o denominador
é 1+C (e não apenas C). A pontuação S corresponde ao score
econômico original F-072; **não foi escolhida para facilitar
um solver novo**.

### 2.1 Mercados estruturalmente inalcançáveis e domínio aberto

Definir M+ como os mercados que podem ser alcançados por
pelo menos um grupo de mercadoria compatível no grafo.
Mercados sem produtor fisicamente alcançável devem ser
declarados **estruturalmente inalcançáveis**, como o contrato
existente de _reachable / _evaluate, e não precificados
artificialmente.

Para os mercados alcançáveis, exigir Q_m(x)>0, pois, quando
Q_m=0, P_m=sqrt(D_m/Q_m) é indefinido/infinito no F-072.

    X+ = {x in X : Q_m(x)>0 para todo m in M+}.

X+ pode ser não vazio quando cada g dispõe de pelo menos
uma coluna válida e todo m em M+ tem um produtor alcançável.
Com s_g>0 e quantidade finita de mercados, é construtível
uma semente de produção real positiva arbitrariamente pequena
para cada mercado alcançável, respeitando os simplexes.
A rotina original seed_accessible_markets é um mecanismo
numérico de partida com essa intenção.

**X+ não é fechado:** uma sequência de fluxos positivos pode
convergir a um estado com Q_m=0. Não é legítimo invocar
automaticamente o teorema clássico de existência de VI em
domínio compacto com operador contínuo até a borda.
Também não se deve esconder a singularidade definindo
novo P=sqrt(D/max(Q,epsilon)) como regra econômica.

Um piso temporário Q_m>=epsilon, se algum método exigir,
seria somente uma restrição de TRABALHO e precisaria ser
removido na certificação contra o F-072 original. Um
equilíbrio do problema com piso não é, por si, um equilíbrio
do jogo; seus multiplicadores de fronteira mudariam as
condições de complementaridade.

## 3. Condições de equilíbrio comercial — proposta exata

Para um fluxo fisicamente válido x* em X+, definir
    lambda_g(x*) = max_{c in C_g} S_gc(x*).

**Condição de complementaridade por grupo e coluna:**
    lambda_g - S_gc(x*) >= 0                para todo g,c
    x*_gc >= 0                              para todo g,c
    x*_gc * [lambda_g - S_gc(x*)] = 0        para todo g,c
    sum_{c in C_g} x*_gc = s_g              para todo g.

Interpretação: nenhuma coluna comercial pode oferecer
score superior ao máximo do produtor; toda coluna que
recebe quantidade positiva precisa ser comercialmente
ótima, respeitando os mesmos preços e cargas que TODAS
as outras colunas criam simultaneamente.

O valor de lambda_g NÃO é novo preço, imposto, receita,
carteira de comprador ou gosto do jogador. É um
multiplicador matemático para certificar a melhor
oportunidade do grupo em uma configuração já observada.

### 3.1 Equivalência à condição de primeira ordem F-072

Em aritmética exata, usando lambda_g = max_c S_gc:

    gap_g(x) =
        [sum_c x_gc * (lambda_g - S_gc(x))]
        / [s_g * lambda_g]

e o gap relativo global:

    GAP(x) =
        [sum_g s_g * lambda_g * gap_g(x)]
        / [sum_g s_g * lambda_g].

Todos os termos acima são não negativos com lambda_g>0.
Como s_g>0, GAP(x)=0 se, e somente se, toda coluna com
x_gc>0 satisfizer S_gc(x)=lambda_g; isto é EXATAMENTE
a complementaridade proposta. max_g gap_g corresponde
à medida de pior produtor usada no R14.

É a mesma estrutura da soma de arrependimentos
ponderados implementada em evaluate_r14, calculada ali
com max(0, lambda_g-S_gc) para tolerância numérica.
Logo, **o resíduo econômico necessário já está medido
pelo laboratório**, embora sua norma/numerologia e
seus certificados de rota exijam uma futura especificação
de precisão.

Não há motivo para trocar a função de preços,
introduzir quantidade retida ou forçar uma preferência
geográfica para obter o problema de complementaridade.

### 3.2 Formulação equivalente como desigualdade variacional

Se F_gc(x)=-S_gc(x), a condição acima equivale, em
aritmética exata e com x* em X+, a:

    sum_{g,c} F_gc(x*) * (y_gc - x*_gc) >= 0
    para todo y em X.

Essa é uma **formulação de equilíbrio VI**, não a prova
de que F seja monotônico, que exista um potencial
convexo ou que algoritmos de VI clássicos convirjam.
A própria definição de F só vale diretamente em X+.

A escolha de converter isso em NCP, MCP, VI, SQP
ou outra família NÃO é tomada por este documento.

## 4. Separação por menor caminho — sem enumerar rotas exponenciais

A definição de C_g inclui caminhos simples, cujo
número pode crescer exponencialmente no grafo.

Contudo, a um x com cargas B fixadas, P_m é igual
para todas as rotas que chegam ao mesmo mercado.
Como S=P_m/(1+C) e todos os pesos de aresta são
estritamente positivos, o MAIOR score para aquele
destino é obtido por seu **menor caminho generalizado
específico do vendedor**.

Essa é precisamente a operação do oráculo R14:
para cada (origem,vendedor), executar a separação
por FrozenRouteCache.paths e comparar o melhor
score de cada mercado alcançável.

Proposta de arquitetura de colunas (NÃO implementada):

1. Manter conjunto restrito de colunas com fluxo
   positivo e eventuais colunas candidatas.
2. Calcular Q, B e preços completos do estado físico,
   com o F-072 original.
3. Consultar o oráculo R14 de MENOR CAMINHO para obter
   lambda_g e uma coluna maximizadora por produtor.
4. Se houver alguma coluna omitida com score maior
   que as colunas financiadas, tratá-la como violação
   de separação e reabrir o conjunto de caminhos.
5. Certificar o resíduo de todas as colunas de fluxo
   positivo e o máximo global obtido pelo R14.
   Não afirmar que "as colunas geradas" são o
   universo se a separação não tiver sido conferida.

Rotas antigas de um mesmo destino podem se tornar
subótimas após alteração do congestionamento;
continuam devidas no resíduo enquanto transportam
fluxo. Rotas canônicas podem mudar de forma discreta
nas trocas de caminho mínimo, embora a função de
custo mínimo seja contínua em cargas finitas
quando os caminhos viáveis são finitos.

Empate entre destinos ou menores caminhos é
co-otimalidade comercial; a alocação financeira
entre titulares em empate é a questão F-059,
**separada** do certificado de GAP=0.

## 5. Duas propriedades demonstráveis no problema atual

### 5.1 É possível ter equilíbrio sem convergência da resposta pura

Considerar um grupo, uma unidade de produção,
dois mercados igualmente alcançáveis, cada D=1.
A origem é conectada aos dois mercados por uma
aresta própria, c_free=1, K=1, tarifa zero.

Em Q_1=Q_2=1/2, cada aresta carrega B=1/2,
P_1=P_2=sqrt(2), C_1=C_2=1+(1/2)^4=17/16:

    S_1=S_2=sqrt(2)/(1+17/16)
           =16*sqrt(2)/33 ~= 0.685679303.

As duas rotas positivas têm exatamente o score
ótimo: GAP=0 e complementaridade satisfeita.

Se as alocações forem 0,60 e 0,40:
    S_1 ~= 0.606214523
    S_2 ~= 0.780578016.

A resposta comercial pura T(x) escolhe a
segunda rota; se deslocar integralmente toda
a massa para lá, a primeira passa a Q=0 e
sai do domínio de preços válidos. Damping e
seeding podem manter as trajetórias numericamente
válidas, mas a oscilação de T(x) não contradiz
a existência do equilíbrio simétrico.

**Conclusão:** rejeitar três regras de atualização
não permite concluir que o equilíbrio não exista.

### 5.2 A distribuição física de equilíbrio não precisa ser única

Considere agora DOIS grupos produtores idênticos
(origin, mercadoria, vendedor, supply=1 em cada),
mesma rede simétrica de duas arestas e mercados
D_1=D_2=1, c_free=K=1 e tarifa zero.

Para QUALQUER a em [0,1], definir:
    grupo 1: x_11=a,     x_12=1-a
    grupo 2: x_21=1-a,   x_22=a.

Em todos esses estados:
    Q_1=Q_2=B_1=B_2=1,
    P_1=P_2=1,
    C_1=C_2=2,
    S_1=S_2=1/3.

Todas as rotas utilizadas são co-ótimas e todo
a satisfaz a complementaridade. Portanto há
infinitas distribuições de fluxo de equilíbrio,
mesmo sem alterar as fórmulas e mesmo mantendo
quantidades agregadas e preços únicos nesse exemplo.

**Conclusão demonstrada por contraexemplo:**
a unicidade do vetor de fluxo NÃO pode ser
premissa necessária para homologar o jogo.
Uma solução pode precisar de desempate
determinístico para repetibilidade, sem criar
preferência econômica ou alegar unicidade física.

## 6. Regularidade e hipóteses NÃO demonstradas

- Para Q_m>0 e uma coluna FIXA, S_gc(x) é contínuo e
  diferenciável nas quantidades e cargas finitas.
  Sua derivada de preço é negativa em Q_m.
- Para o melhor caminho a um mercado, o custo mínimo
  de número finito de caminhos contínuos também é
  contínuo, mas pode não ser diferenciável em empate
  de rotas. O destino vencedor é uma seleção discreta.
- Não foi demonstrada monotonicidade do operador F=-S
  no espaço de todos os grupos/rotas; a mistura dos
  efeitos de Q→P, B→C e tarifa por vendedor impede
  importar teoremas sem hipóteses adicionais.
- X+ não fechado e P→infinito em Q→0 exigem análise
  adicional de existência. A positividade das sementes
  não demonstra convergência nem a existência de
  equilíbrio para toda rede possível.
- Nem convexidade de um potencial, nem unicidade de
  scores, fluxos ou credores de tarifas é postulada.
- O número de rotas pode ser enorme, mas um oráculo
  de separação por menor caminho pode verificar a
  melhor oportunidade sem enumerar todas a cada teste.
- O custo computacional do oráculo/geração de colunas
  e o desempenho C# ainda não foram aprovados.

A revisão NÃO afirma existir método já comprovado
para resolver todas essas condições.

## 7. Duas certificações que NÃO devemos confundir

### A. Certificado ESTÁTICO de equilíbrio aproximado

Um estado poderia receber apenas um certificado
de primeira ordem F-072 quando, no snapshot original:

- cada s_g é integralmente alocado e todo x_gc>=0;
- todos os mercados M+ têm Q_m>0 e P_m finito;
- os custos originais por vendedor e menor caminho
  são finitos, sem rota omitida mais lucrativa;
- gap_global e max_gap_grupo são ambos <=1e-4
  conforme o R14 original, com paridade independente
  do R13 e F-084F monetário;
- qualquer empate global/rota é rotulado com F-059
  ainda PENDENTE; não confundir conservação com
  ratificação da distribuição de créditos.

Isto não provaria equilíbrio exato, nem existência,
nem unicidade. A precisão do certificado ainda
dependeria de tolerâncias de menor caminho.

### B. Teste DINÂMICO do algoritmo de solução

O gate R24 de implementação anterior exigia
simultaneamente cinco métricas <=1e-4 durante
TRÊS atualizações reais consecutivas:

- gap global, pior grupo;
- variação máxima de preços, cargas e Q.

Esse é um teste operacional de uma TRAJETÓRIA,
e **não uma definição matemática de equilíbrio**.

No exemplo simétrico com GAP=0, a regra de desempate
de T(x) pode escolher uma coluna específica.
Aplicar uma nova mistura com esse alvo altera
desnecessariamente um estado economicamente ótimo.
Logo, exigir "três passos efetivamente aceitos"
pode produzir falsos NO-GO mesmo se uma solução
de complementaridade já tiver sido encontrada.

O R37–R40 fixou o gate antigo e os NO-GO desses
experimentos permanecem corretos sob os critérios
que foram predeclarados. **Não alterar retroativamente
esses resultados.**

### Decisão de governança PENDENTE

Recomenda-se que a próxima especificação diferencie:
1. **aceitação estática da aproximação comercial**,
   que não deve exigir deslocar um equilíbrio exato
   somente para criar três estados adicionais;
2. **teste de robustez dinâmica e replay**, que deve
   verificar perturbações e estabilidade sem fingir
   atualizações aceitas ou modificar a economia.

Essa separação é uma mudança no GATE DE CERTIFICAÇÃO,
não na função de preços ou na preferência do jogador.
Ainda requer aprovação explícita; não é ratificada
pela simples publicação deste ADR.

## 8. Arquitetura candidata — CONTRATO, NÃO implementação

**Hipótese recomendada para futura aprovação:** um
problema de complementaridade de fluxos por coluna,
com restrições de oferta em simplex, cobertura real
dos mercados e separação por menor caminho R14.

A representação usaria λ_g e folgas
    z_gc = λ_g − S_gc(x) >=0
    x_gc >=0, x_gc*z_gc=0.

Poderia medir um resíduo complementar normalizado
pela oferta e pelo score positivo. Uma função
tipo Fischer–Burmeister
    FB(a,b)=sqrt(a²+b²)-a-b
pode ser considerada APENAS como representação
algébrica candidata para x/s e z/λ.
FB=0 implica a complementaridade não negativa
desses dois escalares, mas não escolhe solver nem
substitui os contratos F-072 originais.

**Alternativas ainda abertas:** condições de viabilidade
restrita, solver de complementaridade suave, colunas
ativas, ou análise de existência antes da implementação.
Nenhum desses métodos recebe autorização de código
por este documento.

Requisitos para uma futura prova de conceito, se
o contrato for explicitamente aprovado:

1. Teste analítico dos dois exemplos deste ADR;
   o primeiro deve admitir o equilíbrio simétrico;
   o segundo não pode exigir unicidade de fluxos.
2. Reconstrução de Q, B, P, C e S original com
   referências R13/R14, sem caches entre estados.
3. Oferta total por grupo conservada, Q>0 nos
   mercados estruturalmente alcançáveis, preços
   finitos e nenhuma preferência/tarifa adicionada.
4. A cada candidato, certificado de melhor rota por
   vendedor considerando TODAS as alternativas
   alcançáveis; rota fora do conjunto ativo pode
   invalidar a aproximação.
5. Resíduo comercial original e pior grupo validados
   com oráculo independente; tolerância, política de
   término e tratamento de empates explicitados.
6. Custos de avaliações R14, separação e expansão
   de caminhos contabilizados; escala 42/162 nós
   e cenário congestionado antes de homologação.
7. F-084F (conservação) e F-059 (política financeira)
   avaliados separadamente.
8. Sem merge, integração autoritativa no C#/Godot,
   alegação de convergência global ou mudança nas
   fórmulas até decisão e validação específicas.

## 9. Conclusão e ponto de decisão

**Resultado formal da revisão:** é possível expressar
a regra F-072 atual como condições de complementaridade
de produtores e rotas, com o arrependimento R14 como
resíduo de primeira ordem em estados com preço finito.

A nova arquitetura de EQUILÍBRIO é uma CANDIDATA:
ela não provou existência geral nem escolheu um
algoritmo confiável. Os dois exemplos mostram que
equilíbrio e dinâmica iterativa são questões
diferentes e que a unicidade do vetor de fluxo
não deve ser requisito de produto.

**Decisão solicitada antes de qualquer novo código:**
aprovar ou ajustar (i) o contrato de complementaridade
por colunas e (ii) a separação entre certificado
estático de equilíbrio e ensaio dinâmico.

Até então: ADR proposto e PR Draft; **NO-GO para
implementação nova**. A economia F-072/F-084F,
a regra financeira pendente F-059 e a arquitetura
autoritativa do jogo permanecem intactas.
