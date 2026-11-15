# Global Arena — ADR-047: complementaridade por blocos e geração restrita de rotas

**Estado:** implementação experimental com testes e CI por homologar.
**Base:** PR #335 ADR-046, PR #334 ADR-045. **Data:** 15/11/2026.
**Escopo:** PR Draft somente laboratório, sem merge, sem C#/Godot.

## Diretriz de produto

O Global Arena terá muitos milhares de nós e comércio complexo.
O congestionamento quartico é parte do design, não um parâmetro a
reduzir preventivamente. Aceitamos ciclos econômicos potencialmente
mais caros e desacoplados da renderização, com reserva de CPU para
outras camadas.

Preço ORIGINAL P_m = sqrt(D_m/Q_m) com Q_m>0.
Custo ORIGINAL C_gc = soma das impedâncias c_free*(1+(B/K)^4)
e da tarifa por vendedor, sem alterações.
A formulação de solução H=(1+C)*sqrt(Q/D) é matematicamente
equivalente ao score P/(1+C), como provado no ADR-043.
A tolerância comercial original global e pior grupo <=1e-4,
R13/R14 independentes e F084F são critérios absolutos.
F059 financeiro permanece sem ratificação.

## Intervenção A — pré-condicionamento em blocos de produtores

O gargalo do ADR-046 foi o condicionamento das equações normais
no caso 2.562 nós, onde o Jacobi diagonal exato exigiu
20.000 produtos Krylov sem certificado final.

A nova implementação reúne em um bloco todas as incógnitas
de um MESMO produtor: seus fluxos não-âncora e o
multiplicador Fischer-Burmeister. Cada bloco é exatamente
a submatriz principal correspondente de J transposta J,
computada pelas incidências rota-aresta-mercado e pelas
derivadas físicas/financeiras ORIGINAIS. Nenhum Jacobiano
global denso é montado.

A regularização Levenberg–Marquardt adiciona lambda
na diagonal do bloco; uma Cholesky LOCAL resolve
o pré-condicionador do CG. A matriz global continua
implícita via Jv e Jtv exatos; portanto, a economia
não é separada artificialmente por produtor.

A memória aproximada dos blocos é a soma dos quadrados
dos números de incógnitas de cada grupo, NÃO o quadrado
da soma. Cada fatoração e coeficiente trabalhado é
contabilizado. A implementação exige testes comparando
os blocos ao sistema denso original.

## Intervenção B — geração de colunas mais seletiva

A inicialização mantém TODAS as rotas com fluxo efetivo,
mais as duas rotas de melhor custo H para cada produtor.
Não altera preferências: H é exatamente o inverso
do score original F072 para Q>0.

A cada raiz MCP restrita, o R14 ORIGINAL faz varredura
global para TODOS os mercados compatíveis acessíveis
e suas rotas canônicas de menor impedância. Toda rota
omitida mais vantajosa deve ser adicionada como
obrigatória na rodada seguinte; nenhuma raiz do
problema restrito pode ser certificada sem essa prova
de oportunidades globais, o ADR042 e F084F.

Mercado inicialmente vazio recebe somente produção
REAL deslocada de outra rota do mesmo produtor
e nunca preço artificial. Em falha por budget,
o último estado provisório pode ser auditado,
mas é proibido publicá-lo como fluxo final
ou como solução já homologada.

## Orçamento de ensaio — 2.562 nós

Fixture Goldberg R13 original, 7.680 arestas, 48
produtores, 24 mercados, 4 mercadorias e até 288
rotas canônicas disponíveis. O número de nós NÃO
equivale ao número de agentes econômicos do jogo.

| Unidade de trabalho | Teto |
|---|---:|
| Avaliações H completas, rejeições incluídas | 430 |
| Scans de caminhos | 900 |
| Certificações ADR-042 completas | 4 |
| Colunas ativas | 440 |
| Gerações globais | 5 |
| Passos Newton aceitos | 65 |
| Backtracking por tentativa LM | 10 |
| Produtos Jv/Jtv | 14.000 |
| Iterações CG por direção | 130 |
| Coeficientes dos blocos | 100.000 |

O orçamento é mais restritivo em produtos Krylov que
o último ADR046 (20.000), mas difere em outros limites.
Não declarar superioridade por comparações que usem
orçamentos distintos; discriminar os recursos reais.

## Gate de aceitação

Paridade matemática dos blocos e Cholesky em relação
ao Jacobiano denso original. Resolução efetiva de
dois casos multigrupo controlados, com certificação
ADR042. Regressões em mapas de 42 e 162 nós.
Tentativa em 2.562 nós, com PASS SOMENTE se as
duas métricas F072 originais e demais invariantes
estiverem dentro dos limites.

A CI verde garante integridade do contrato; caso
2.562 nós possa continuar NO-GO operacional e
deve permanecer explicitamente visível.

Se persistir o gargalo, o próximo investimento
de engenharia considerará precondicionamento
por mercadorias/mercados, gestão dinâmica de
suportes e fatoração esparsa, conforme dados.
Nenhum merge nem alteração econômica automática.

## Controle de mudança dos orçamentos — 15/11/2026

Na primeira CI de código executável #43677047122, a algebra e 22/22
contratos passaram em cada plataforma, mas a fixture 2.562 nós
recebeu NO-GO ao esgotar exatamente o limite de **16.000
coeficientes de blocos** herdado do intervalo valido de limites.
O snapshot provisório auditado pelo ADR-042 teve gap global
0,0006040743891 e pior grupo 0,0520573199053 — nenhum dos dois
atingiu a tolerancia economica 0,0001. O algoritmo não retornou
fluxo final aprovado e os controles de 42 e 162 nós passaram.

**A segunda execução preserva os demais tetos** de 430 H,
900 scans, 65 Newton, 14.000 produtos Krylov, 130 CG por
solucao e 440 colunas, mas eleva o limite de **coeficientes
de blocos para 100.000**, contabilizado como trabalho acumulado.
Esse teto tem unidade DIFERENTE e não altera a faixa dos
limites originais de avaliacoes/oraculos. São dois ensaios
com orçamento declarado e separado, não uma comparacao
com budget idêntico. O novo resultado deverá constar no PR.
