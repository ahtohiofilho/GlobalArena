# GA-VS38 — Facts físicos de conexões terrestres compartilháveis (somente leitura)

**Data:** 31/10/2026. **Base:** GA-VS37 PR #143 (7/7 workflows aprovados). **Status:** implementação experimental empilhada, gate próprio necessário.

## Princípio arquitetural

Uma **única realidade geográfica física** alimenta mobilidade e transporte econômico, mas cada domínio aplica políticas de custo próprias. O jogador mantém uma interface de ordens simples; a simulação administra geometria, roteamento e logística. A economia continua agregada, sem instanciar cada remessa como caravana canônica.

## Implementação neste marco

`CanonicalLandEdgeFactsSnapshot` materializa, por sessão de mundo 01C, **uma única vez** o conjunto das conexões terrestres dirigidas sobre a topologia tática Goldberg.

Cada `CanonicalDirectedLandEdgeFacts` disponibiliza:
- Origem e destino (ambos adjacentes e caminháveis no mundo vigente).
- Distância em milionésimos do raio **por corda esférica**, não km/metragem.
- Desnível procedural dirigido, positivo/negativo, e umidade média 01C.
- Presença de cobertura florestal como fato ambiental categórico.
- Cotação **experimental** do deslocamento terrestre VS37, expressamente não canonizada como custo de frete.

O snapshot implementa a interface `IScalarPhysicalEdgeCostProvider` da VS36 como **opção**, para que a prévia de movimento por pontos busque custos já calculados em vez de repetir quantização e raiz quadrada para cada consulta. A rota-base não muda: o Dijkstra escolhe exatamente o mesmo custo/itinerário que o provedor VS37 sem cache.

**Isolamento obrigatório:**
- O snapshot é propriedade da sessão que o criou; recusará uso com outra sessão, mesmo de seed idêntica.
- O dicionário interno não pode ser mutado pelo chamador. O conjunto dos fatos é read-only e materializado de forma determinística, incluindo ambas as direções de cada aresta terrestre.
- Não persistir snapshot no save. Recriar a partir da geografia versionada do mundo ao carregar.
- Arestas oceânicas/ilegais não aparecem; não liberar estreitos, navegação ou pontes artificiais.

## Economia: contrato e não ativação

Uma futura política de frete poderá consultar **distância, desnível, ambiente e conectividade** desse snapshot. **Não** deve interpretar `ExperimentalWalkingQuote.TotalPoints` como preço monetário, tarifa, tonelada-km ou velocidade de trem. Custo econômico requer serviço próprio com tecnologia, modalidade, infraestrutura, saturação, transbordo e volume. A geografia muda raramente; rede logística e carga possuem ciclos de atualização distintos.

Não acoplar qualquer dos preços antigos, o solver econômico, contratos financeiros, a cadência econômica, o journal ou o sistema de saves à tabela nesta versão.

## QA / impacto de desempenho

- Contagem exata das **arestas terrestres dirigidas** em toda a geografia, ausência de duplicatas, reciprocidade de distância/desnível/ambiente e ausência de conexões de água.
- Mesma cotação VS37 e **mesma sequência de caminho, pontos totais e ETA** com provedor cacheado ou calculado ao vivo.
- Reutilização real de descritores por identidade; centenas de leituras não recalculam o WorldGen.
- Leitura independente de turnos econômicos, sem mutar hash, save ou evento.
- Rejeição de sessão estrangeira; benchmarks futuros devem medir **custo da criação da tabela** e **amortização em N rotas** com mundo G(18,0) e densidades maiores.

**Não alegar aceleração percentual sem benchmark**: existe custo inicial de pré-cálculo, memória adicional e o grafo pode crescer. A validade é arquitetural e a regressão é checada em Windows/Linux.

## Roadmap após a VS38

1. Políticas de **infraestrutura e modalidade** consumindo fatos físicos compartilhados.
2. Custos logísticos agregados separados dos pontos militares e com caches versionados por rede/ciclo.
3. Somente depois, ativação de movimentação escalar com save versionado e salvaguardas de gameplay.
4. Navegação, rios e estreitos, e eventual ficha militar massa/velocidade 1–5 permanecem hipóteses de etapas próprias (issue #140).

**Produto:** a interface continua uma ordem de destino, como no xadrez. Nenhum comando de micromanagement é acrescentado.
