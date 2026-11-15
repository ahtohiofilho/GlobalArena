# Global Arena — ADR-056: atribuição de hotspots Krylov e fronteira de memória

**Data:** 15/11/2026. **Base:** PR #344 / ADR-055, HEAD homologado `11a5f5339f1040b9751cd39881a007252536e093`. **Estado inicial:** observabilidade experimental; nenhuma otimização proposta ou aprovada. PR Draft empilhado sobre #344, sem merge.

## Motivação

No ADR-055, o original independente ADR-042/F-072 certificou **48, 96, 192 e 384 nós demandantes**, com o cenário 192→384 mantendo 4.096 trabalhadores e o mundo Goldberg de 5.762 nós. O cenário 384 apresenta **768 mercados** e **192 grupos produtores agregados**. Os produtos Krylov foram **389 em 192** e **2.643 em 384** nesta fixture, multiplicação de aproximadamente 6,8 vezes; o pico **acumulado** RSS do processo Python chegou a aproximadamente 1,15 GB na etapa 384, mas esse valor não é um inventário de objetos nem uma atribuição causal à etapa.

O pré-condicionador do ADR-051 separa as matrizes locais SPD em blocos de largura máxima 2. Os produtos globais exatos `Jv/Jtv` permanecem no núcleo de conjugate gradients original do ADR-047. A matriz global não pode ser substituída por uma simplificação dos requisitos comerciais sem decisão específica. O problema diagnóstico é distinguir **mais iterações**, **mais trabalho por iteração**, **custo de geração de candidatos/colunas** e **ocupação da memória**.

## Instrumentação experimental (sem mudança funcional)

Todos os experimentos conservam o mesmo F-072 original, custo de congestionamento `c_free*(1+(B/K)^4)`, preço `P=sqrt(D/Q)`, tarifas por vendedor, rotas R13/R14, F-084F, demanda física dos trabalhadores e certificados independentes ADR-042. Não tocar no solver original ou na geografia do jogo. **Nenhum ajuste de precondicionador nem de budget nesta ADR.**

Duas fixtures independentes, cada uma em **processo Python novo**, por sistema operacional:

| Indicador | Fixture 192 | Fixture 384 |
|---|---:|---:|
| Goldberg: nós de geografia | 5.762 | 5.762 |
| Trabalhadores físicos | 4.096 | 4.096 |
| Nós produtores | 48 | 96 |
| Nós demandantes | 192 | 384 |
| Grupos produtores agregados | 96 | 192 |
| Mercados por nó e produto | 384 | 768 |
| F-072 original | **Obrigatório** | **Obrigatório** |
| Budget numérico | ADR-054 original | ADR-055 original |

Observações capturadas separadamente:

1. **Construção de população:** medir tempo de parede e CPU Python, rastrear com `tracemalloc(nframe=1)` **somente** durante uma reconstrução física isolada, exportar pico do heap Python rastreado e top 16 arquivos com blocos vivos. Desligar o `tracemalloc` antes do solver. Esses valores excluem parte da memória nativa e **não representam toda a RAM do processo**.
2. **Resolução comercial:** repetir construção idêntica e instrumentar o wrapper original por `cProfile`. Exportar top 20 funções em tempo próprio e cumulativo no processo inteiro e top 25 de código do laboratório, contagens de chamadas, métricas de trabalho original (H, Krylov, scans etc.), tempo de parede e `process_time`. Tempos **do cProfile são medidos em execução instrumentada**, com overhead, e valores cumulativos são aninhados, portanto não podem ser somados.
3. **Memória do processo:** RSS pico acumulado nativo obtido antes/depois da reconstrução e do perfil pelo mecanismo já aprovado do ADR-055 (`resource.getrusage` no Linux, PSAPI no Windows). Mesmo um processo isolado contém importações, registros e overhead do profiler; não alegar tamanho isolado do sistema econômico.
4. **Integridade:** comparar hashes de população/fluxo entre a prova de construção e o solver, exigir F-072 independente original com gaps global e pior grupo `<=10^-4`, garantir P⊆D e ausência de duplicação por viagem. Nunca publicar GAP final ou fluxo autoritativo de um solver NO-GO.

## CI e gate de aceite

- **Contratos estruturais** ADR-056 em Windows e Ubuntu, incluindo testes negativos sobre certificação falsa, corrupção da demanda, hashes divergentes e validade de RSS/tracemalloc.
- **Regressão econômica original independente ADR-055** em ambos sistemas: certificados comerciais **obrigatórios 48, 96, 192 e 384 nós**. Sem tratamento especial para timeouts.
- **Perfil isolado** dos cenários 192 e 384 em Windows e Ubuntu; cada um recebe JSON e ZIP. Qualquer perda de certificação F-072 deixa sua tarefa vermelha. O perfil não é um substituto da regressão econômica.
- Um **sucesso da CI não significa redução de custo**: apenas que a observabilidade preservou os resultados originais. Antes de propor otimização, comparar top funções, chamadas, contadores e RSS, identificar gargalo dominante e estimar riscos de mexer no precondicionador.
- O cenário 2.048 nós demandantes / 4.096 mercados permanece **apenas ledger espacial** no ADR-055, sem execução de F-072 nesta ADR. **Não expandir a escala nesta etapa**.

## Reprodutibilidade

```powershell
python -m unittest discover -s experiments/gate09/adr056_solver_diagnostics/tests -p 'test_*.py' -v
python -u experiments/gate09/adr055_market_384/ci/run_adr055.py --profile ci --output .
python -u experiments/gate09/adr056_solver_diagnostics/ci/run_adr056.py --fixture 192 --output .
python -u experiments/gate09/adr056_solver_diagnostics/ci/run_adr056.py --fixture 384 --output .
```

A CI isola o 192 e o 384 em processos separados (dois jobs por SO) e executa as regressões originais em outro job. Execuções locais consecutivas no mesmo terminal devem seguir os comandos acima em **processos Python distintos**.

## Governança

**GO somente para diagnóstico laboratorial** mediante CI independente. **NO_GO produção.** ADR-055 e anteriores permanecem Draft; não modificar `main`, R13/R20–R55, preços, tarifas, congestionamento quártico, F-072/F-084F ou C#/Godot. F-059 financeira não ratificada e scheduler econômico cooperativo ainda inexistente no motor.

### Política de próxima decisão

Se o perfil apontar gargalo no bloco local SPD, estudar alternative mais leve somente em **outro experimento** com F-072 original e regressões obrigatórias. Se o custo for em `Jv/Jtv`, examinar organização de incidências e alocação de listas sem alterar fórmulas. Se memória cresce por colunas/rotas, estudar compartilhamento ou compactação apenas após medir tamanho e segurança do grafo. Não aceitar "Krylov=2.643" sozinho como diagnóstico de causa.
