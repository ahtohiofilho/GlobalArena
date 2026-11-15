# Global Arena — ADR-057: auditoria de rotas, cache congelado e memória

**15/11/2026.** Base: ADR-056, [PR #345](https://github.com/ahtohiofilho/GlobalArena/pull/345), commit homologado `562ac5da26f2b21273572e082a255da31ea48636`. Exclusivamente laboratório; PR empilhado em Draft.

## Evidência que motivou o checkpoint

O ADR-056 certificou os quatro cenários (48, 96, 192 e 384 nós demandantes), mas no perfil Python as funções `certificate_r2.path_audit`, `gate09.weight_for`, `oracle_cache_r14.paths` e `gate09.congestion_cost` apareceram entre os maiores tempos próprios. Na fixture 192→384, mantendo 4.096 trabalhadores, os produtos Krylov cresceram **389→2.643**, enquanto mercados cresceram **384→768** e grupos **96→192**. O pico RSS na etapa 384 ficou próximo de 1,15 GB, sem atribuição causal. Nem Krylov nem cache isoladamente estão estabelecidos como gargalo dominante.

## Hipótese investigável, sem intervenção

**Pergunta objetiva:** em um snapshot físico congelado, chamadas repetidas a `FrozenRouteCache.paths(origem,vendedor)` repetem a busca de caminhos ou reaproveitam parte do trabalho? Medir custo e objetos Python associados ao cache, sem inferir que seja o mesmo custo do certificado completo.

Usar as duas fixtures canônicas do ADR-056, com mundo Goldberg de 5.762 nós e 4.096 trabalhadores:

| Indicador | 192 nós | 384 nós |
|---|---:|---:|
| Nós produtores | 48 | 96 |
| Grupos produtores | 96 | 192 |
| Mercados (nó,produto) | 384 | 768 |
| F072 independente anterior | PASS obrigatório | PASS obrigatório |

Em cada sistema operacional e processo novo: reconstruir a população usando o original ADR-052, fixar `loads` pelas ofertas iniciais reais e criar `FrozenRouteCache(net,loads,4)`. Executar **uma passagem** sobre todos os grupos e repetir **as mesmas consultas**, na mesma ordem e na mesma instância de cache. Comparar assinaturas de resultados, hashes dos fluxos e integridade da produção/demanda. Nenhum fluxo é recomputado pelo solver nessa fase; um snapshot inicial *não* é um equilíbrio.

## Observações (não equivalentes a otimização)

- `cProfile` por passagem registra contagens de `paths`, `dijkstra`, `weight_for`, `congestion_cost`, `weights` e operações auxiliares. O quociente de `dijkstra` entre passagens é **apenas proxy de reaproveitamento**: não é uma taxa real de cache-hit, nem prova de aceleração do F072.
- `tracemalloc` inicia **após** a construção da rede. Exporta bytes Python rastreados depois da criação do cache, primeira passagem e repetição, pico da fase e top arquivos. Não mede o grafo físico já construído, RAM nativa ou memória do solver F072.
- Inspeção de atributos top-level do cache: cardinalidade e `sys.getsizeof` **raso**; não somar valores como tamanho de memória retida. RSS nativo é pico acumulado do processo, não consumo atribuível à passagem.
- Tempo de parede e CPU sob `cProfile` são tempos **instrumentados**. Resultados de primeiro/repetido são comparáveis somente como observações daquele processo congelado, não como garantias de aceleração do jogo.
- Os dois patamares têm grupos, locais produtores e rotas diferentes, logo uma eventual diferença de custo não isola apenas número de mercados.

## Gate e governança

1. **Contratos ADR-057** e **provas de cache** nas fixtures de 192 e 384 em Windows e Ubuntu; nenhuma discrepância nas assinaturas ou hash de oferta.
2. **Regressão econômica separada:** executar *intacta* a CI do ADR-055, exigindo certificados F072 originais independentes de **48, 96, 192 e 384 nós** em ambos OS, com gaps global e pior grupo ≤10⁻⁴. Não chamar o cache microbenchmark de equilíbrio comercial.
3. Emitir JSON/ZIP por plataforma e fixture. O resultado será **GO somente para diagnóstico de cache** se todas as tarefas passarem, e **NO-GO para alterações de solver, preço, tarifa ou política financeira** nesta ADR.
4. Preservar congestionamento quártico, preço `sqrt(D/Q)`, F084F, R13/R14, contratos da população física, `main`, C#/Godot e orçamento original. Sem merge. Política F-059 e scheduler cooperativo continuam pendentes; 2.048 nós demandantes continuam somente ledger espacial.

### Execução no repositório

```powershell
python -m unittest discover -s experiments/gate09/adr057_route_cache/tests -p 'test_*.py' -v
python -u experiments/gate09/adr057_route_cache/ci/run_adr057.py --fixture 192 --output .
python -u experiments/gate09/adr057_route_cache/ci/run_adr057.py --fixture 384 --output .
```

**Decisão antes da CI:** sem certificação ADR-057. A próxima escolha técnica depende das contagens observadas, especialmente das varreduras repetidas versus custos da auditoria original. Não autorizar otimização a partir de uma razão isolada.
