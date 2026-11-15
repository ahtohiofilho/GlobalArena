# Global Arena — ADR-050: densidade máxima do laboratório R13

**Data:** 15/11/2026. **Base:** ADR-049 PR #338, ADR-048 PR #337, ADR-047 PR #336. **Escopo:** rodar o perfil já implementado de densidade máxima, sem mudar a economia nem o solver.

## Domínio e limites de representatividade

O trabalhador físico produz quando alocado a produção e SEMPRE gera demanda, mesmo durante deslocamento. A demanda durante o ciclo de viagem deve se distribuir pelos tiles proporcionalmente ao tempo de permanência, com soma exatamente igual à demanda que o trabalhador geraria no ciclo; o deslocamento não multiplica consumo. A diretriz foi incorporada à documentação do ADR-049, mas **não foi implementada no kernel C#/Godot**.

O construtor R13 usa grupos produtores sintéticos e mercados por par (nó,produto), sem derivar demanda de trabalhadores. Seu limite de 240 grupos e 120 mercados NÃO são 240 nós de produção versus 120 nós de demanda; tais cardinalidades não são diretamente comparáveis. O teste avalia a escalabilidade do equilíbrio matemático, NÃO a representatividade do futuro sistema trabalhador–produção–demanda. Um gerador worker-coupled com invariante de nós físicos P subconjunto de D ainda será necessário.

## Cenário e baseline

| Caso | Nós Goldberg | Arestas | Grupos sintéticos | Mercados (nó,produto) | Produtos | Pares compatíveis |
|---|---:|---:|---:|---:|---:|---:|
| ADR-048 | 2.562 | 7.680 | 144 | 48 | 6 | 1.152 |
| ADR-049 geográfico | 5.762 | 17.280 | 144 | 48 | 6 | 1.152 |
| **ADR-050 densidade máxima R13** | **2.562** | **7.680** | **240** | **120** | **10** | **2.880** |

O catálogo de dez produtos é sintético e não altera o catálogo de itens canonizados do jogo. A melhor comparação de densidade é o ADR-048 de 2.562 nós com 144/48; o ADR-049 geográfico variou também o número de nós.

## Método e orçamento predeclarado

O perfil maximum_density já existe no arquivo experiments/gate09/adr049_geography_density/ci/run_adr049.py. O workflow novo o torna um **gate de regressão com código de falha real** em Windows e Ubuntu. Preserva F-072 P=sqrt(D/Q), congestionamento original c_free*(1+(B/K)^4), tarifas originais, titularidade, massa integral e F084F. Não relaxar a tolerância comercial global e pior grupo de 1e-4.

| Unidade de trabalho | Teto |
|---|---:|
| H completa, incluindo rejeições | 980 |
| Scans dos caminhos | 6.500 |
| Certificados independentes ADR-042 | 6 |
| Colunas ativas | 1.600 |
| Gerações globais de rotas | 6 |
| Passos Newton aceitos | 185 |
| Backtracking por λ | 10 |
| Produtos Krylov Jv/Jtv | 58.000 |
| Iterações CG por direção | 180 |
| Coeficientes dos blocos | 248.000 |

Somente o certificado ORIGINAL ADR-042 com GAP global E pior grupo F-072 <=1e-4, R13/R14, separação global das rotas, preços finitos e F084F concede GO de laboratório. Em caso de NO-GO, não publicar estado provisório como solução. Testes verdes sem execução econômica bem-sucedida não autorizam GO.

## Diretriz de processamento e governança

Tempos de GitHub Actions Python NÃO são benchmark do C# ou SLA para computadores dos jogadores. Não existe agendador cooperativo nem retomada de estado econômico durante turnos; a política de reserva de CPU para guerra, política, interface e outras camadas permanece decisão futura.

Após este gate, a priorização deve favorecer um gerador trabalhador–demanda espacialmente consistente e benchmarks em densidade física real de milhares de agentes. A curva quartica só poderá ser reconsiderada com evidência e decisão explícita do usuário.

**PR Draft empilhado sobre #338. Não altera R13/R20–R49, F072/F084F, F059, motor C#/Godot ou main. Sem merge.**
