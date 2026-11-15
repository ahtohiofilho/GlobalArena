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


---

## Homologação da execução — NO-GO de densidade, 15/11/2026

**[GitHub Actions #43710660812](https://github.com/ahtohiofilho/GlobalArena/actions/runs/43710660812): FAILURE ESPERADO PELO GATE**, nos dois sistemas, porque nenhuma execução encontrou um equilíbrio certificado pelo ADR-042 para a configuração de 240 grupos e 120 mercados. Isso é diferente de falha de testes de integridade: **17/17 testes Ubuntu e 17/17 testes Windows passaram**.

| Métrica original do laboratório | Ubuntu | Windows |
|---|---:|---:|
| Nós Goldberg / arestas | 2.562 / 7.680 | 2.562 / 7.680 |
| Grupos sintéticos / mercados (nó,produto) | 240 / 120 | 240 / 120 |
| Status do solver | NO_GO_PREDECLARED_WORK_BUDGET | NO_GO_PREDECLARED_WORK_BUDGET |
| Certificado comercial F-072 | **NÃO** | **NÃO** |
| GAP F-072 final global | **não disponível** | **não disponível** |
| GAP F-072 final pior grupo | **não disponível** | **não disponível** |
| Pico de colunas ativas | 806 | 808 |
| Avaliações H, inclusive rejeições | 234 | 233 |
| Produtos Krylov Jv/Jtv | 32.586 | 32.418 |
| Coeficientes de blocos contabilizados | **247.989 / 248.000** | **247.998 / 248.000** |
| Scans de caminhos | 1.920 | 1.920 |
| Tempo do solver no runner Python | 300,71 segundos | 218,43 segundos |
| Testes independentes de integridade | 17/17 PASS | 17/17 PASS |

ZIPs/JSON de auditoria preservados: ga-gate09-adr050-maximum-density-ubuntu-latest ID 14159944775 e ga-gate09-adr050-maximum-density-windows-latest ID 14159908258.

O gatilho da parada foi o teto de **coeficientes usados nos blocos por produtor**, não o limite de Krylov 58.000, não a precisão econômica 1e-4 e não falta de validade de cenário. Como o orçamento terminou antes de um estado final certificado, é INCORRETO apresentar o último GAP de um snapshot anterior como erro da solução, ou declarar o equilíbrio inexistente.

**Parecer:** NO-GO para o cenário sintético 240/120 **dentro desse orçamento**, e GO para manter a infraestrutura experimental com diagnóstico correto e invariantes preservados. NÃO modificar a lei do congestionamento, preço ou F-059; não aumentar retrospectivamente limites nesta mesma medição.

### Próximo investimento recomendado

1. Reduzir custo do pré-condicionador de produtor com reuso de fatores válidos por linearização, ou testar blocos estruturados por mercado e mercadoria, mantendo produtos Jv/Jtv e auditoria global; comparar o consumo de coeficientes e produtos Krylov antes de simplesmente elevar tetos.
2. Definir gerador de cenários com **trabalhadores físicos**, atividades gerando oferta e TODOS os trabalhadores gerando demanda. No trânsito, ratear demanda pelo tempo de permanência por tile sem duplicação; confirmar invariante físico nós produtores subconjunto de nós demandantes, sem confundir grupos por produto com pontos físicos.
3. Validar processo econômico por ciclos com orçamento compartilhado entre economia e demais camadas. Ainda NÃO há agendador cooperativo em C#/Godot, nem tempos do jogo medidos.
4. Após melhorias, repetir cenário sintético 240/120 com novo orçamento PR/ADR explicitamente separado e verificar se o mesmo modelo F-072 original passa.

**Governança:** PR Draft #339 sobre #338, sem merge. Somente workflow e documento novos. Não houve alteração ao solucionador, gerador R13, F-072/F-084F/F-059, C#/Godot ou main. A CI permanece vermelha PROPOSITALMENTE enquanto o critério comercial independente não for satisfeito.
