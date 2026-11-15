# Global Arena — ADR-044: núcleo de complementaridade — contrato executável e decisão

**Data:** 15/11/2026.
**Base:** PR #332 ADR-043 (formulação VI contínua com existência condicional), PR #331 ADR-042 (certificador estático).
**Estado:** primeira implementação contratual de RESÍDUO NCP e prova de conceito escalar limitada, **não homologação de solucionador geral**.
**Governança:** PR Draft, sem merge, fontes R20–R43/C#/Godot intocadas; F-059 financeiro pendente.

## Decisão técnica e racional

O F-072 permanece definido por P=sqrt(D/Q), impedância física quarticamente congestionada e tarifa existente por vendedor, com score S=P/(1+C). O ADR-043 provou existência de equilíbrio no grafo finito sob hipóteses explícitas ao escrever H=(1+C)*sqrt(Q/D) e formular VI(H,X) em produtos de simplexes de oferta.

**Seleção da família do núcleo:** resolver diretamente a COMPLEMENTARIDADE de H (MCP/NCP), e NÃO executar mais atualizações síncronas da melhor resposta do R21–R40. Para cada grupo g, definimos

    mu_g = min_{c viável} H_gc(x)
    slack_gc = H_gc(x)-mu_g
    x_gc >= 0, slack_gc >= 0, x_gc*slack_gc = 0
    sum_c x_gc = supply_g.

A função Fischer–Burmeister phi(a,b)=sqrt(a²+b²)-a-b é zero exatamente quando a>=0, b>=0 e a*b=0. Uma verificação normalizada pode usar:

    a_gc = x_gc / supply_g
    b_gc = slack_gc / max(1, max_c H_gc(x))
    residual_NCP = max_{g,c} |phi(a_gc,b_gc)|.

**Não existe tolerância arbitrária de NCP que substitua a certificação comercial.** H e S têm mesmo conjunto de raízes no interior dos mercados, mas magnitudes de residual distintas. O único gate de equilíbrio econômico aproximado segue a validação INDEPENDENTE ADR-042: GAP global e GAP do pior grupo originais F072 ≤1e-4, R13/R14, oferta conservada, preço finito, separação completa e F084F.

## Entrega implementada neste PR

1. Função **somente leitura** \`complementarity_contract\` recebe Network e fluxo físico válido. Reconstrói Q, carga, menor caminho por vendedor para todos os mercados alcançáveis e mantém as colunas positivas antigas, mesmo não-canônicas. Calcula mu_g, H por rota financiada, residual de VI e FB normalizado. Não move fluxos nem produz preço artificial em Q=0.
2. Estados com Q=0 são classificados como limite da VI, mas **NÃO** recebem PASS F072. Estados com preços definidos são encaminhados obrigatoriamente ao certificador ADR-042 original (oráculos independentes R13 e R14 e F084F).
3. **Referência operacional diminuta:** para **um produtor, dois destinos distintos e duas arestas diretas e disjuntas**, define-se H1(a)−H2(s−a)=0. Nessa topologia específica, a função é estritamente crescente: H1 aumenta com a oferta no primeiro mercado, enquanto H2 decresce quando a oferta nele cai. Os sinais nas bordas são opostos; bisseção limitada resolve a equação. Não precisa introduzir D ou Q fictícios nem altera o fluxo original.
4. O algoritmo de referência cobra cada avaliação de H, incluindo as duas bordas, com **teto 64** por execução; não executa R14 nos pontos em que Q=0. Só pode dar PASS quando a solução interior é conferida novamente por **ADR-042 independente**. Caso o orçamento seja insuficiente ou o residual não atinja a tolerância escalar declarada, retorna NO-GO sem fingir equilíbrio.
5. **20 testes** de contrato: FB elementar, equilíbrio/inequilíbrio simétrico, continuum de soluções físicas, empate comercial versus F059 financeiro, mercado inalcançável versus sem preço, rota mais cara com fluxo, vendedor tributado, recuperação do zero-Q, orçamento minúsculo, topologias não suportadas, supply inválido, fixtures R34 normal/heterogênea estressada e integridade R20.

O exemplo de dois mercados é um **teste de referência da nova formulação**, NÃO um solucionador generalizado por VI e NÃO uma quarta família de best-response R38–R40.

## Próximo núcleo geral — especificação de entrada/saída e arquitetura candidata

**Implementação generalizada ainda NÃO iniciada.** A recomendação para a próxima entrega é um MCP restrito por colunas, com expansão global por menor caminho e controle numérico de segurança. A seleção de um procedimento de Jacobiano/globalização exige um gate próprio de revisão, pois derivadas de sqrt(Q) não são regulares em Q=0.

### Entradas imutáveis

- Network original, grupos, ofertas, mercados D>0, capacidades, custo físico, titulares/tarifas, matriz de rotas existentes;
- orçamento predeclarado de avaliações H, ADR-042/R13/R14, scans de menor caminho, número máximo de colunas e iterações;
- estado inicial que respeite todo simplex; pode ter Q=0 (válido em H, não em preço original).

### Operação exigida

- Inicializar conjunto de colunas que mantenha TODAS as rotas financiadas.
- Resolver em conjunto finito a condição NCP e simplex, sem criar/destruir massa.
- Trabalhar em interior Q>0 quando o método de derivadas exigir: um piso de VIABILIDADE TEMPORÁRIO sobre o candidato físico pode ser considerado pelo núcleo, mas **nenhum preço artificial** e NENHUMA aceitação final com piso ativo ou GAP original inválido.
- Recalcular cargas, Q, H e caminhos conforme o fluxo candidato, sem reutilizar pesos de outro snapshot.
- Separar globalmente os mercados/rotas por vendedor; se uma coluna omitida tiver H menor do que o mínimo das colunas restritas, acrescentar rota e resolver de novo. Podas nunca apagam produção.
- Validar apenas candidatos com todos os Q alcançáveis positivos via ADR-042 original; se F-072 falhar, não declarar convergência só porque o NCP restrito é pequeno.
- Falha de conservação, rota inválida, preço infinito no final, estouro de orçamento, estagnação, divergência ou conflito entre R13/R14: **NO-GO explícito, sem mutação autoritativa**.

### Saídas padronizadas

- STATUS: PASS_STATIC_F072_ONLY, NO_GO_BUDGET, BLOCKED_Q_ZERO, INVALID_PHYSICAL_STATE, FAILED_ROUTE_SEPARATION, FAILED_F072_OR_FINANCE.
- Medidas: residual-NCP (diagnóstico), GAP original global e pior grupo (decisores), cobertura global de rotas, Q e cargas positivas/consistentes, F084F e sinalizações F059, número total de avaliações aceitas/rejeitadas, worklist de colunas, determinismo de replay.
- Nenhuma alegação de convergência global, solução única ou velocidade garantida.

## Critérios para o próximo gate de engenharia

1. Começar com casos analíticos pequenos: bisseção 1D é o oráculo de referência, mas o futuro núcleo precisa encontrar o equilíbrio por generalização da MCP e não conhecer previamente a resposta.
2. Verificar dois produtores com equilíbrio não único, diferentes tarifas e rotas mais caras: nunca exigir unicidade nem usar o desempate de rota como regra financeira.
3. Depois comparar mapas 42/162 nós e congestionamento K×0,20, com limites de trabalho preanunciados, controles R21/R26 e auditoria real dos oráculos.
4. Avaliar desempenho e GAP comercial F-072 por orçamento, sem exigir três passos fictícios em estado já aprovado estaticamente. As derrotas históricas R38–R40 permanecem intactas.
5. Só depois de uma implantação experimental geral aprovada avaliar paridade C#/Godot e decisão financeira F059 por trilha separada.

**Ponto de gestão:** ADR-044 entrega um NCP verificável e uma solução calculada num caso escalar. O próximo PR de implementação, se autorizado, deverá conter um resolvedor multi-produtor/colunas efetivo ou um NO-GO técnico demonstrado — **não mais um PR apenas de diagnósticos e taxonomias**.
