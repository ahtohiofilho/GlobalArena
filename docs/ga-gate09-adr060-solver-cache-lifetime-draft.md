# Global Arena — ADR-060: ciclo de vida dos caches dentro do solver F-072 original

**Data:** 15/11/2026. **Base:** ADR-059, PR #348, HEAD `d1217ceba560ee3360a48aaa9ed8a9a417453758`. **Status inicial:** diagnóstico pré-declarado, sem aprovação de mudança no solver ou produção.

## Problema e hipótese

ADR-059 observou ~50% de redução de **pico Python rastreado** em um microbenchmark artificial de dois caches de rotas simultaneamente vivos, quando A é liberado antes de construir B. Este resultado **não deve ser extrapolado automaticamente** ao solver real.

A inspeção de `run_adr045.py` e `run_adr047.py` identifica a criação do `FrozenRouteCache` original em `R45._route_candidates` e `R45._global_separate`. O `R47.solve_block` chama essas rotinas durante gerações/separação de caminhos; o `R51.solve_chunked` injeta somente o precondicionador original ADR-051, e a verificação F-072 independente ADR-042 é outra rotina não alterada.

**Hipótese a verificar:** o próprio escopo das funções já pode liberar os caches de R45 entre snapshots. Se assim for, forçar a coleta antes de criar cada cache não reproduzirá os ~50% do ADR-059. Medir **coexistência real**, épocas físicas distintas e custo do GC é o objetivo deste gate.

## Experimento no solver real — duas variantes

Em processos Python independentes, tanto em Windows quanto em Ubuntu:

- `baseline`: executar **R51.solve_chunked original** com um *factory observador* colocado temporariamente no nome `R45.FrozenRouteCache`. O factory devolve um **objeto da classe original, sem proxy**, mantendo apenas `weakref` para medir vida útil. O patch é estritamente síncrono e restaurado no `finally`.
- `gc_before_cache`: mesmo factory, mesmo objeto original e mesmo solver, mas chama `gc.collect()` **antes de cada construção** do cache em R45. Isso **não é** o controlador `release_first` de ADR-059: garbage collection não pode destruir caches com referências fortes ainda vivas. Qualquer coexistência será reportada, nunca tratada como liberação garantida.

O factory mede, por construção: índice, token derivado das cargas por aresta no **mesmo objeto Network**, número de caches antigos ainda vivos antes e depois de GC, número simultâneo após construção, tempo de construtor e RSS acumulado nativo. Não faz cópia de rotas, não guarda referências fortes, não altera `paths`, `weights`, `counters`, preços ou desempenho do operador econômico intencionalmente.

### Escala controlada

| Indicador | Caso 192 | Caso 384 |
|---|---:|---:|
| Nós Goldberg | 5.762 | 5.762 |
| Trabalhadores | 4.096 | 4.096 |
| Produtores físicos | 48 | 96 |
| Nós demandantes | 192 | 384 |
| Grupos produtores | 96 | 192 |
| Mercados | 384 | 768 |
| Solver | Original ADR-051/F-072 | Original ADR-051/F-072 |
| Orçamento numérico | Original ADR-054 | Original ADR-055 |

O total de trabalhadores é constante, mas a configuração de produtores, posições e rotas varia. O cenário de 2.048 nós demandantes continua **ledger espacial sem solver F-072**.

## Critérios de aceite

1. **Solver original, não simulação de cache:** executar cada um dos quatro casos `192/384 × baseline/gc_before_cache` em Windows e Ubuntu. Cada execução deve produzir **certificado original independente ADR-042/F072** com gap global e pior grupo finitos ≤ 10⁻⁴. Somente o certificado original autoriza PASS.
2. **Integridade da instrumentação:** mesmo objeto original de cache, observado somente no ponto R45 da geração/separação de rotas; patch restaurado sempre; livro de trabalhadores e fluxo de entrada intactos. Preços `P=sqrt(D/Q)`, congestionamento `c_free*(1+(B/K)^4)`, tarifas de proprietário, R13/R14, F-084F e limites de trabalho numérico intocados. Não alterar o certificador independente F-072.
3. **Comparação no mesmo HEAD/SO:** comparar o **hash da solução final certificada**, gaps global e pior grupo, status original, **todos os contadores numéricos**, gerações, hashes físicos e sequência dos tokens de carga da R45. Divergência reprova CI. Exigir que `gc_before_cache` realmente tenha feito GC antes de todas as construções.
4. **Métricas observacionais:** tempo de CPU e parede **da resolução completa**, pico RSS do processo e sobreposição de vida útil de caches originais. São jobs separados e ruidosos; diferença de RSS/CPU não representa benefício causal comprovado, nem orçamento do scheduler C#/Godot. Para evitar overhead extremo, **não usar tracemalloc ou cProfile durante o solver F-072**. O heap rastreado exclusivamente nos microbenchmarks ADR-058/059 não é reatribuído ao solver.
5. **Regressões obrigatórias independentes:** executar separadamente, no **mesmo commit**, o ADR-055 sem instrumentação para certificar 48, 96, 192 e 384 nós demandantes, ambos sistemas. CI só aprovada quando **todas** as tarefas passarem.
6. **Governança:** nenhuma modificação R13/R20–R59, C#/Godot ou `main`; não ratificar F-059. PR Draft empilhado sobre #348 sem merge, sem autorização de produção.

### Decisão resultante

Se o solver original já mantém apenas um cache R45 vivo por vez, a recomendação será **não adicionar gerenciamento de cache desnecessário** ao solver; concentrar o próximo diagnóstico em outra fonte de RSS/custo ou no agendador cooperativo. Caso sejam vistos caches sobrepostos, avaliar política de propriedade segura em outra ADR. Em qualquer caso, **um PASS numérico do F072 não implica vantagem de desempenho**; CPU/RSS são observações, não SLA.

## Reproduzir (Python 3.12)

```powershell
python -m unittest discover -s experiments/gate09/adr060_solver_cache_lifetime/tests -p 'test_*.py' -v
python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 192 --policy baseline --output .
python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 192 --policy gc_before_cache --output .
python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 384 --policy baseline --output .
python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 384 --policy gc_before_cache --output .
```

Cada execução deve ocorrer em **processo separado**. O workflow faz o download dos pares de evidências por fixture/OS no mesmo HEAD, valida equivalência original F072 e publica comparação JSON.

**Estado inicial:** contrato e experimentos publicados; nenhuma inferência antes da CI bilateral.
