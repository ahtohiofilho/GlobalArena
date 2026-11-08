# GA-FOUNDATION-AGRICULTURE-003 — Projeção agrícola no grafo lógico

**Data:** 2026-11-08  
**Status:** laboratório opt-in; validação CI obrigatória antes de promoção.  
**Base:** GA-FOUNDATION-AGRICULTURE-002; encadeada aos PRs #298 (fundação), #299 (contrato agrícola) e #300 (avaliador).  
**Próxima frente fundamental preservada:** F-084, calibração de impedância de aresta unitária (terreno, relevo e água), capacidade natural, tempo de trabalhador e investimento.

## Escopo executado

O avaliador agrícola de seis cultivos foi conectado a um **contrato explícito de geografia lógica fornecida externamente**, sem criar outro gerador de planeta:

- `AgriculturalGeographicNode` recebe **ID estável de nó**, elevação, temperatura, umidade e sinal terra/água.
- `AgriculturalLogicalGeography` encapsula snapshot imutável, adjacências por offsets e índices; valida IDs únicos, faixas físicas, ordem crescente e reciprocidade do grafo não direcionado.
- `AgriculturalGeographyProjection.Build` deriva a declividade local a partir das diferenças de elevação por arestas de **distância lógica unitária** e avalia seis cultivos por nó.
- `AgriculturalGeographyProjection` preserva um cache plano `int[6*N]` e declividades `int[N]`, retorna pontuações de cada cultivo e o dominante do tile; água não possui cultura dominante.
- Desempates entre cultivos seguem a ordem do catálogo estável, nunca a ordem de renderização ou requisição.

**Não há código de fusão com V1/V2, worldgen antigo, setor comercial nem Godot.** O input já está pronto para que o **novo gerador de mundo lógico**, quando implementado, entregue uma fotografia dos atributos dos nós; não se declara que esse novo gerador existe neste incremento.

## Integridade física

A declividade usa a média das diferenças absolutas de elevação entre o nó e os seus vizinhos **lógicos**. Elevar todos os nós igualmente não altera essa declividade; o efeito térmico de altitude, se existir, já deve constar do campo de temperatura recebido. Não acrescentar penalidade por distância geodésica ou pelo tamanho visual de um tile.

Índices são normalizados em 1.000.000. Nenhum bioma é necessário. Os seis cultivos permanecem adequados em todas as amostras terrestres comuns, e a dominância decorre de comparação entre scores, sem proibir variedades inferiores.

## Ensaios geográficos e benchmark

- Três nós com ID próprio e água, conferência individual de seis resultados e declividades.
- Imutabilidade do snapshot contra alterações nas coleções de entrada.
- Permutação de índices com manutenção de identidades estáveis e resultados idênticos.
- Mapas lógicos de laboratório com 12, 120, 1.200 e 12.000 nós; os seis cultivos apresentam lideranças.
- Gradiente climático **sintético** 13×13 de temperatura versus umidade, com seis regiões produtivas e mapa de lideranças explicitamente verificado; não equivale a um planeta procedural.
- Deslocamento uniforme de altitude não cria penalidade adicional.
- Validação de entradas inválidas e de grafo assimétrico.
- Benchmark isolado com 10 mil e 100 mil nós e valência 6, repetido três vezes.

O benchmark mede **apenas a projeção** com o snapshot já validado e reporta checksum de seis scores por nó. Não define meta numérica antes de medições; a memória da projeção equivale a aproximadamente `(6 + 1) * N * sizeof(int)` bytes além do snapshot. O input tem custo adicional proporcional às adjacências, e não exige varredura a cada ciclo econômico. Os mapas são **fixtures de laboratório**, não um novo worldgen autoritativo.

## Limites da promoção

Esta etapa não executa demanda, produtores, preço, tributo, comércio, transporte, trabalho, time-step ou inventários. Uma futura integração autoritativa precisa:

1. Consumir o **grafo lógico canônico real do novo mundo** e seus campos físicos sem criar segundo armazenamento persistente de topologia.
2. Demonstrar distribuição geográfica útil em mundos procedurais reais com diferentes seeds e densidades.
3. Calibrar separadamente perfis numéricos e preferências econômicas, mantendo seis IDs estáveis.
4. Avaliar como armazenar e invalidar o cache quando campos físicos mudarem, sem acoplar o resultado a LOD visual.

## Trilha de retomada

Após o fechamento do laboratório agrícola, **retomar F-084** no PR #298: modificadores físicos locais de impedância sobre arestas lógicas de distância 1; separar esse custo do tempo real de movimentação de trabalhadores e das tarifas, respeitando a hipótese de capacidade natural e investimento individual por aresta. **Não** voltar a quilometragem/geodésica ou reintroduzir modais obrigatórios e transportes físicos descartados.
