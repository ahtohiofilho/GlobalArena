# Global Arena — ADR-058: ciclo de vida do cache entre snapshots físicos

**Data:** 15/11/2026. **Base:** ADR-057 PR #346 em Draft, HEAD homologado `6eba23166a2026d357aa6aa22354c4a3ead9b2fb`. **Situação inicial:** diagnóstico em laboratório, sem otimização de cache, sem produção.

## Por que esta etapa

O ADR-057 confirmou que as mesmas consultas a `FrozenRouteCache.paths(origem,vendedor)`, no **mesmo cache e snapshot fixo**, são muito mais baratas após a primeira passagem. Também observou memória Python rastreada da microprova de aproximadamente 139,5 MB para 192 nós demandantes e 275 MB para 384 nós. Isso não é ganho demonstrado do solver F-072: durante a busca de equilíbrio, os fluxos físicos e as cargas das arestas variam; o cache congelado para uma carga não deve ser reutilizado com carga diferente.

Objetivo do ADR-058: observar **quanto custa criar um novo cache quando o snapshot muda e quanto de memória rastreada é liberada ao descartar o cache antigo**, conservando o contrato físico.

## Fixtures canônicas, sem alterações

| Indicador | 192 nós demandantes | 384 nós demandantes |
|---|---:|---:|
| Mundo Goldberg | 5.762 | 5.762 |
| Trabalhadores | 4.096 | 4.096 |
| Nós produtores | 48 | 96 |
| Grupos produtores | 96 | 192 |
| Mercados (nó,item) | 384 | 768 |
| Original F-072 | Certificado obrigatório | Certificado obrigatório |

A **alocação A** é a seed fisicamente financiada do gerador ADR-052. Para criar **B**, para cada grupo produtor mover exatamente 25% da quantidade de uma das quatro colunas reais e previamente financiadas para outra coluna financiada do **mesmo grupo**, sem criar caminhos ou alterar trabalhador, oferta total, mercado demandante, rede, tarifas ou propriedade. A quantidade por grupo e preços finitos positivos por mercado são verificadas; o hash das alocações e as cargas por aresta devem mudar. Essa perturbação **não é um novo equilíbrio F-072**: A e B são apenas dois estados físicos legítimos.

## Medição no mesmo processo

1. Construir rede e snapshots A/B com `Network.snapshots`. Iniciar `tracemalloc` **após a construção da rede**; obter RSS do processo pela ferramenta nativa do ADR-055.
2. Criar **Cache A** original `FrozenRouteCache(net,loads_A,4)`. Consultar todos os grupos e repetir; verificar assinaturas estáveis dos resultados.
3. Criar **novo Cache B** com `loads_B`; consultar todos os grupos e repetir. Comparar pesos de arestas por vendedor nos dois caches: devem existir mudanças. Nunca reatribuir ou reutilizar Cache A para calcular rotas de B.
4. Com ambos vivos, medir heap rastreado; liberar Cache A, executar GC e medir novamente. Reconsultar B para garantir estabilidade após a liberação. Registrar pico cumulativo do processo e bytes rastreados em cada etapa.

**Restrições das inferências:** o valor `tracemalloc` inclui somente alocações Python rastreadas durante a prova (não o grafo criado antes, nem toda a memória nativa do solver). O RSS é o pico do processo, **não diminui necessariamente quando objetos são liberados**. A diferença entre picos de heap não é garantia de reclamabilidade física do alocador; nem os tempos de cache são SLA do C#/Godot. O custo de reconstrução de dois caches não simula o loop completo do F-072. O hash compacto das respostas só verifica uma amostra de destinos, não substitui o certificado de rotas original.

## Gate de homologação

- Testes negativos Windows/Ubuntu: proibido compartilhar cache entre cargas diferentes, identificar mudança real de pesos, preservar oferta de cada grupo, P⊆D, consumo dos trabalhadores em viagem e assinaturas do cache congelado.
- Provas 192 e 384: ambas as plataformas, cada fixture em processo isolado, JSON/ZIP com A/B, pesos diferentes, tempos frio/quente, memória Python A/B/liberação, hashes e métrica RSS.
- **Regressão independente F-072 separada e obrigatória:** executar o ADR-055 intacto em Windows e Ubuntu, exigindo os certificados originais de **48/96/192/384 nós**. A prova de cache NÃO executa F-072 e não pode ser chamada de equilíbrio comercial.
- Nunca modificar R13/R20–R57, C#/Godot, `main`, F072/F084F, tarifas, preço `P=sqrt(D/Q)` ou congestionamento quártico `c_free*(1+(B/K)^4)`. F-059 financeiro e scheduler cooperativo seguem pendentes.
- **GO laboratorial** somente se tudo passar no mesmo HEAD da CI. Nenhum merge, otimização ou produção autorizado nesta ADR. 2.048 nós demandantes/4.096 mercados permanecem somente ledger espacial.

## Reprodutibilidade

```powershell
python -m unittest discover -s experiments/gate09/adr058_cache_lifecycle/tests -p 'test_*.py' -v
python -u experiments/gate09/adr058_cache_lifecycle/ci/run_adr058.py --fixture 192 --output .
python -u experiments/gate09/adr058_cache_lifecycle/ci/run_adr058.py --fixture 384 --output .
```

**Próxima decisão orientada por dados:** se a duplicação temporária do heap dos caches for relevante, investigar estratégia de construção/liberação de snapshot sem reutilizar pesos vencidos. Qualquer proposta de otimização será nova ADR, com regressões originais independentes preservadas.
