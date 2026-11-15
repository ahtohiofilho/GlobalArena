# Global Arena — ADR-055: 384 nós demandantes com população controlada e métricas nativas

**Data:** 15/11/2026. **Base:** ADR-054 PR #343 (Draft, head homologado `762dc3c44910e8c8050afc4c9120398734d98a7d`). **Status inicial:** proposta de primeiro experimento; nenhum certificado de 384 nós ainda.

## Problema de escalabilidade

O ADR-054 demonstrou equilíbrio original independente F-072 nos cenários de **48, 96 e 192 nós demandantes** em Windows e Ubuntu, promovidos a regressões obrigatórias. No cenário de 192 nós, havia **4.096 trabalhadores**, 48 nós produtores, 96 grupos produtores e 384 mercados por (nó,item), em um mundo Goldberg de 5.762 nós.

O próximo degrau intermediário proposto é **384 nós demandantes / 768 mercados**, mantendo **os mesmos 4.096 trabalhadores** e a mesma geometria do mundo. Assim, a densidade espacial demandante aumenta sem duplicar o total populacional. Não é um controle causal perfeito: número de hubs produtores, grupos comerciais, rotas e localização das atividades também mudam.

| Etapa | Mundo | Trabalhadores | Nós produtores | Nós demandantes | Grupos | Mercados | Equilíbrio |
|---|---:|---:|---:|---:|---:|---:|---|
| ADR-053 48 | 5.762 | 2.048 | 12 | 48 | 24 | 96 | **F-072 obrigatório** |
| ADR-053 96 | 5.762 | 2.048 | 24 | 96 | 48 | 192 | **F-072 obrigatório** |
| ADR-054 192 | 5.762 | **4.096** | 48 | 192 | 96 | 384 | **F-072 obrigatório** |
| **ADR-055 384** | **5.762** | **4.096** | **96** | **384** | **192** | **768** | **F-072 exploratório, orçamento fechado** |
| Extremo espacial | 5.762 | 8.192 | 512 | 2.048 | 1.024 | 4.096 | **Ledger somente, sem F-072** |

## Contratos inalterados

- Trabalhadores físicos do ADR-052 são as únicas fontes de produção e demanda. Cada trabalhador consome conforme a atividade; trabalhador produtor consome também, e viagem distribui a demanda por permanência em tiles, sem duplicação. Conjunto de locais produtores é subconjunto de locais demandantes.
- As duas mercadorias de laboratório são as de referência, sem artificializar demanda. Nenhuma loja fictícia de R13 é importada.
- Equilíbrio original **P=sqrt(D/Q)**, todas as vendas, rotas R13/R14, tarifas por vendedor, propriedade da infraestrutura, F-084F e custo quartico `c_free*(1+(B/K)^4)` permanecem intactos.
- Somente um certificado **original, independente ADR-042/F-072**, com gaps global **E** pior grupo finitos e `<=1e-4`, pode autorizar PASS de 384 nós.
- R13/R20–R54, C#/Godot e `main` intocados. F-059 financeiro ainda não ratificado. Nada é orçamento real do jogo.

## Orçamento pré-declarado para o primeiro solve original F-072 com 384 nós

| Unidade computacional | Teto |
|---|---:|
| H, incluindo rejeições | 1.600 |
| Scans de menor caminho | 6.000 |
| Certificadores ADR-042 | 8 |
| Colunas ativas | 3.000 |
| Gerações globais | 6 |
| Passos Newton | 240 |
| Backtracking | 12 |
| Produtos Krylov Jv/Jtv | **60.000** |
| Iterações CG por direção | 240 |
| Produtos triangulares de blocos SPD | **250.000** |

Os tetos 60.000 e 250.000 respeitam o limite de pré-validação original do ADR-047, não podem ser aumentados silenciosamente e não são SLAs em milissegundos. Um timeout de CI de 35 minutos é uma proteção operacional independente dos limites algorítmicos.

## Medição adicional sem alterar o solver

Medições por etapa com `time.perf_counter` (parede) e `time.process_time` (CPU total do processo) em Python. Pico de memória **RSS de todo o processo**, com coleta nativa sem polling e sem instrumentação da alocação interna:
- Ubuntu/Linux: `resource.getrusage(RUSAGE_SELF).ru_maxrss`, converter KiB para bytes;
- Windows: `psapi.GetProcessMemoryInfo().PeakWorkingSetSize`, em bytes.

Os valores RSS são **máximos acumulados desde a inicialização do processo** (e não consumo incremental atribuído a um cenário específico); cada etapa recebe o valor observado antes/depois. Valores ausentes ou plataformas não suportadas devem ser declarados, nunca inventados. `process_time` inclui CPU acumulada do processo entre início e fim da etapa, não CPU de threads do motor do jogo. O mesmo processo executa vários cenários e não oferece isolamento de benchmark. Não interpretar diferenças de tempos entre CI como determinismo ou custo assintótico.

## Critério de gate

1. **Regressões obrigatórias:** 48, 96 e 192 nós com F-072 original aprovado em Windows **e** Ubuntu no HEAD efetivo da CI. Falha de qualquer um reprova o workflow.
2. **Contrato físico:** 384 nós, 4.096 trabalhadores, 96 produtores, 192 grupos, 768 mercados com demanda e oferta de trabalhadores reais. O ledger extremo de 2.048 nós permanece sem F-072.
3. **Exploração 384:** se certificação original independente PASS, classificar `PASS_EXPLORATORY_ADR055_384_ORIGINAL_F072_CERTIFIED`; se não convergir sob orçamento, `NO_GO_EXPLORATORY_ADR055_384_F072_NOT_CERTIFIED`, sem gaps finais nem fluxos finais não certificados. O NO-GO exploratório pode manter CI verde **somente** com todas as regressões e contratos físicos aprovados; CI verde por si só não é certificação de 384.
4. **Promoção posterior:** somente se 384 nós PASS em **ambos sistemas no mesmo commit**, promover o patamar a regressão obrigatória em commit separado, reforçar casos negativos e executar nova CI. Preservar evidência inicial e da promoção.
5. **Produção:** permanece proibida. Nem o solver Python nem as medições do runner substituem o scheduler cooperativo C#/Godot ou a pendência F-059.

## Reproduzir (Python 3.12)

```powershell
python -m unittest discover -s experiments/gate09/adr055_market_384/tests -p 'test_*.py' -v
python -u experiments/gate09/adr055_market_384/ci/run_adr055.py --profile ci --output .
```

JSON/ZIP com as regressões, hashes, certificados independentes, gaps finais apenas se certificados, work counters, tempos process CPU/parede Python e pico RSS processo. A execução Ubuntu/Windows gera artifacts individuais no workflow. PR Draft empilhado sobre #343 e não merged.

**Decisão inicial:** `NO_GO_PRODUCTION_ADR055_PENDING_384_F072_DUAL_OS`. Estender para 2.048 nós demandantes/4.096 mercados somente após nova avaliação formal de orçamento e desempenho, nunca por inferência de uma seed positiva.

---

## Primeiro PASS independente 384 nós — 15/11/2026

A execução inicial do ADR-055 falhou **antes de executar o solver** por erro de importação de `PopulationConfig`, corrigido em commit posterior sem alteração dos budgets ou da matemática econômica. A primeira execução efetiva é **[CI #43727155682](https://github.com/ahtohiofilho/GlobalArena/actions/runs/43727155682): SUCCESS**, com **23/23 testes de contrato no Windows e 23/23 no Ubuntu**, no HEAD `f1b113f22846f343208c25496f00232853b57a37`.

O ORIGINAL independente ADR-042/F-072 aprovou todas as regressões (48, 96, 192) e **também** o novo equilíbrio dos **384 nós demandantes, 768 mercados e 4.096 trabalhadores**, em ambas plataformas:

| Indicador no mesmo HEAD | 192 nós — Windows | **384 nós — Windows** | 192 nós — Ubuntu | **384 nós — Ubuntu** |
|---|---:|---:|---:|---:|
| Trabalhadores | 4.096 | **4.096** | 4.096 | **4.096** |
| Grupos produtores | 96 | **192** | 96 | **192** |
| Mercados | 384 | **768** | 384 | **768** |
| Original F-072 independente | PASS | **PASS** | PASS | **PASS** |
| GAP global F-072 | 2,2062088e−14 | **6,8285702e−14** | 2,2062088e−14 | **6,8285702e−14** |
| GAP pior grupo | 1,2920144e−13 | **7,0417725e−13** | 1,2920144e−13 | **7,0417725e−13** |
| H, inclusive rejeições | 32 | **80** | 32 | **80** |
| Produtos Krylov | 389 | **2.643** | 389 | **2.643** |
| Scans de caminho | 240 | **768** | 240 | **768** |
| Solver Python (segundos) | 96,58 | **213,88** | 96,58 | **212,86** |
| CPU Python de toda etapa (s) | 96,69 | **213,05** | 97,86 | **213,42** |
| Pico RSS acumulado do processo (MB decimais) | 604,11 | **1.150,23** | 598,45 | **1.150,83** |

O par **192→384** mantém trabalhadores fixos, mas dobra hubs produtores, grupos comerciais e mercados e altera sua posição espacial. Os tempos aproximadamente duplicam e os produtos Krylov crescem mais de seis vezes nesta fixture; não se trata de medição assintótica nem de um custo causal isolado por mercado. O pico RSS é **cumulativo do processo** e não uma medição exata do incremento de memória de cada cenário.

Artefatos da primeira rodada: Windows ID **14167052104**, Ubuntu ID **14166977134**, com JSON/ZIP de status, orçamento, CPU/processo, RSS, contadores e certificados F-072.

### Promoção ao gate obrigatório

Após o PASS **original independente** em Windows e Ubuntu, a política do ADR-055 passa a exigir **48, 96, 192 e 384 nós demandantes certificados** em cada CI, sob os mesmos budgets e regras. Classificação de sucesso: `PASS_ADR055_384_ORIGINAL_F072_CERTIFIED_MANDATORY`; ausência de certificado de 384: `NO_GO_MANDATORY_ADR055_384_F072_REGRESSION` e processo com saída não zero. Novos testes negativos garantem que uma seed positiva, resultado não certificado ou tentativa não executada não produz aprovação.

A promoção é um commit distinto e deverá ter a CI final reexecutada integralmente antes da homologação definitiva do ADR-055. Nenhuma alteração em R13/R20–R54, F072/F084F/F059, C#/Godot ou `main`.

**Decisão parcial:** `GO_ADR055_384_ORIGINAL_F072_CERTIFIED_LAB_ONLY`, condicionada à CI de regressão final. **NO_GO_FOR_PRODUCTION** e **NO_GO_FOR_CLAIMING_2048_DEMAND_NODES_F072_EQUILIBRIUM**. PR Draft empilhado sobre #343, sem merge; F-059 e scheduler cooperativo pendentes.
