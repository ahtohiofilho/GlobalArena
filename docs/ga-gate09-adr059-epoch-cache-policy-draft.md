# Global Arena — ADR-059: política reversível de posse do cache por época de cargas

**Data:** 15/11/2026. **Base:** ADR-058, PR #347, HEAD homologado `e70f70da88a7f8b36ab68aabbfe1915198de6a00`. **Escopo:** laboratório exclusivamente; nenhum merge ou integração no C#/Godot.

## Pergunta de decisão

O ADR-058 mostrou que manter dois caches congelados de cargas distintas faz o heap Python rastreado crescer de ~139,5 MB para ~278,9 MB (192 nós demandantes) e de ~274,9 MB para ~549,7 MB (384 nós). Ao descartar o cache A, quase todo o volume rastreado correspondente foi liberado. As cargas alteradas mudaram também os pesos vendedor/aresta: reutilizar A para B seria incorreto.

**Teste ADR-059:** uma política que libera A **antes** de criar B reduz o pico rastreado sem alterar rotas, pesos ou trabalhadores, quando comparada à coexistência temporária? Se reduzir, em que proporção e com que custo de substituição observado? Esta ADR não altera a implementação original do F072, nem demonstra ganho do solver C#.

## Contrato e políticas

Usar exatamente os dois snapshots físicos válidos A/B do ADR-058, redistribuindo 25% de uma coluna financiada para outra do mesmo grupo produtor. 192 e 384 nós demandantes permanecem em mundo Goldberg de 5.762 nós e com os mesmos 4.096 trabalhadores; A/B **não são** equilíbrios certificados.

Criar um **wrapper experimental** `EpochCacheOwner`, que encapsula o original `FrozenRouteCache(net,loads,4)`, associa um token de época ao conteúdo das cargas e exige esse token em todas as consultas. Após a transição, solicitações com token de A devem falhar, inclusive se A continuar guardado temporariamente na variante comparativa. A rede é a mesma em toda a prova; **o token não é identidade universal de rede**, e não deve circular entre redes/simulações.

- **`overlap` (controle ADR-058):** manter A numa referência privada enquanto B é construído e aquecido, liberar A somente depois. A não pode ser consultado depois da troca da época ativa.
- **`release_first` (candidato):** invalidar A e liberar sua referência/GC *antes* de construir B, mantendo o mesmo fluxo de consultas. Se o construtor de B falhar, o acesso a A continua bloqueado (*fail closed*).
- **Regra comum:** não reconstruir se o hash físico das cargas não mudou; não admitir terceira época enquanto existir um cache retirado pendente; comparação por hash **de todos os pesos vendedor/aresta**, sem guardar uma cópia sombra dos grandes mapas para fins de medição.
- **Mesma física:** preço `P=sqrt(D/Q)`, congestão `c_free*(1+(B/K)^4)`, propriedade, tarifas, rotas originais, worker demand/production, P⊆D e oferta por grupo preservados. Nada de reaproveitar pesos antigos ou artificializar compradores.

| Fixture | Mundo | Trabalhadores | Nós produtores | Nós demandantes | Grupos | Mercados |
|---|---:|---:|---:|---:|---:|---:|
| 192 | 5.762 | 4.096 | 48 | 192 | 96 | 384 |
| 384 | 5.762 | 4.096 | 96 | 384 | 192 | 768 |

## Evidência medida

Cada combinação **sistema operacional × fixture × política roda em processo Python separado**, para isolar os picos. Em cada combinação:

1. Medir bytes Python via `tracemalloc` iniciado **depois** da construção do grafo e dos dois conjuntos de cargas; coletar pós A aquecido, depois de criar B, pós B aquecido, pós liberação do cache retirado e pico da prova. Sob `release_first`, medir também a memória **depois da liberação de A e antes da construção de B**.
2. Registrar tempo CPU/parede de consultas frias/quentes e custo da troca da referência/GC. A política de release-first pode trocar um pico menor por uma lacuna entre snapshots; este custo deve ser visível e não ser assumido desprezível.
3. Comparar, em jobs de auditoria **no mesmo HEAD GitHub e no mesmo SO**, os hashes dos trabalhadores/fluxos/épocas, hashes de **todos** os pesos originais de vendedores, e assinaturas compactas amostradas dos resultados de caminhos A/B. Divergência de rotas/pesos é **falha de CI**. Uma assinatura compacta não substitui o certificado F-072.
4. Calcular diferença de pico Python rastreado `overlap - release_first` e fração `diferença/overlap`. **Uma economia não observada recebe NO_GO explícito**, mas não falsifica uma regressão funcional, desde que a equivalência se mantenha. RSS nativo do processo é *high-water* acumulado: não interpretar como memória devolvida ao OS.
5. Caches A/B continuam sendo **observações de alocações físicas**, não certificados comerciais. Os 2.048 nós demandantes/4.096 mercados continuam apenas ledger espacial, sem F072.

## Gate obrigatório

CI dual-OS com (i) contratos negativos (token obsoleto, construtor falho, fontes de trabalhadores, inflação de demanda, invariantes e dados falsos), (ii) 8 microprovas isoladas e (iii) 4 comparações de políticas por SO/fixture. Em paralelo, manter **todas as regressões originais e independentes ADR-055/F072 para 48, 96, 192 e 384 nós** nas duas plataformas com gaps global e pior grupo ≤1e−4. Os budgets continuam os originais, sem ajustes nesta ADR.

**GO apenas se as políticas preservarem rotas e originais econômicos; ganho de memória requer evidência efetiva.** O gerenciador é uma proposta **reversível exclusivamente para laboratório**. Uma eventual integração no solver ou scheduler C#/Godot exige outra ADR, com interface de invalidação de cache adequada às cargas iterativas, medição no runtime e orçamento de CPU. F-059 financeiro permanece pendente.

### Reprodução (Python 3.12, biblioteca padrão)

```powershell
python -m unittest discover -s experiments/gate09/adr059_epoch_cache/tests -p 'test_*.py' -v
python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 192 --policy overlap --output .
python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 192 --policy release_first --output .
python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 384 --policy overlap --output .
python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 384 --policy release_first --output .
```

Para comparar picos corretamente, executar cada prova em **processo Python distinto**, como faz o workflow. O comparador recebe os dois ZIPs daquele fixture/OS no mesmo HEAD.

**Estado inicial:** instrumento, critérios e limites de inferência declarados antes da execução. PR Draft empilhado sobre #347, sem merge, sem produção.
