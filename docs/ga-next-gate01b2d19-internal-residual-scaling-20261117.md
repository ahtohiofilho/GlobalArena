# GA-Next — Gate 01B.2D-19: custos internos do fluxo global por escala

**17/11/2026 · Draft sobre PR #411 · observabilidade C# opt-in · sem alteração econômica, UI ou merge**

## Escopo

O Gate 18 eliminou a alocação sequencial descartada antes do solver global. O presente gate mede os custos internos restantes do solucionador de fluxo segmentado, sem arredondar preços, simplificar o frete ou deixar de auditar as entregas reais. Esta fase é diagnóstica: nenhuma heurística ou decisão de jogo muda.

A classe interna FoundationPhysicalGlobalFlowCostProfile acumula medições por fase somente quando o chamador de testes fornece o perfil. Na API pública, profile permanece null: não se chama Stopwatch nem GC. O solver residual mantém a mesma rede, custos BigInteger, desempates, augmentations e condição de orçamento.

## Fases instrumentadas

- Preparação de trabalhadores, cotações e mercados reais: worker-market-preflight
- Grafo físico: physical-graph-build
- Caminhos mínimos para produtores: source-shortest-paths
- Arcos de oferta a mercados: seller-market-arc-build
- Rede residual: residual-network-setup
- Busca e augmentação residual: residual-augmenting-search
- Reconciliação e utilidade marginal exata: residual-conservation-and-utility
- Reconstrução das entregas e rotas: delivery-route-materialization
- Auditoria física independente final: independent-final-physical-audit

As fases são não sobrepostas, mas não cobrem toda a orquestração. A distribuição do tempo instrumentado **não corresponde** a percentual de CPU total. Para várias commodities, as fases são somadas e informam quantidade de amostras.

## Escala de teste e contrato A/B

| Tiles Goldberg conectados | Produtores físicos | Mercados reais | Pares possíveis |
|---:|---:|---:|---:|
| 112 | 8 | 104 | 832 |
| 208 | 16 | 192 | 3.072 |
| 320 | 24 | 288 | 6.912 |
| 416 | 32 | 384 | 12.288 |

Os tiles são um subconjunto conectado da malha global G(3,0), G(5,0), G(6,0), G(7,0), respectivamente. A capacidade por produtor é 1 milhão de micro-bens e toda oferta precisa ser entregue. Um trabalhador que produz também demanda. Segmentation = 4 segmentos por mercado; orçamento 8192 augmentations.

Para cada tamanho, os testes executam uma solução instrumentada e outra sem perfil sob o mesmo censo, mercados, estradas e políticas e verificam igualdade exata das quantidades, mercados, caminhos, potencial segmentado e integrado BigInteger, número de augmentations, frete e cargas por aresta, hash de estado e ausência de ciclo econômico fechado. Não há publicação monetária.

CI Windows/Ubuntu arquiva quatro JSON por runner com schema ga-next.global-flow-internal-scaling.v1. O script PowerShell valida dimensões, fases, métricas finitas, conservação e A/B antes de publicar o relatório. A CI total inclui Kernel, Godot, StateHash, frete de referência e testes completos do motor.

## Interpretação e próximos passos

Stopwatch mede **tempo de parede**, não CPU exclusiva. GC.GetAllocatedBytesForCurrentThread mede **alocação acumulada gerenciada pela thread**, não memória residente ou heap máximo. Cada linha é uma única amostra por runner, afetada por JIT, GC e agendamento; nenhum SLA, comparação estatística ou certificação para milhares de nós deve ser inferido. Não se soma tempo de solver integral a fases internas.

A decisão para o Gate 20 deve ocorrer **após** a medição: se a busca residual dominar, otimizar estruturas de caminho/residual mantendo o mesmo grafo e a mesma política de desempate; se predominarem caminhos mínimos, grafo ou auditoria, seguir esse gargalo. Qualquer mudança futura necessita A/B econômico e físico rigoroso.

**Governança:** PR Draft empilhado sobre #411, sem merge e sem publicação de preços, tarifas ou liquidação.
