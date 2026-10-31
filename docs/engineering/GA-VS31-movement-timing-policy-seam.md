# GA-VS31 — Contrato universal de cronograma de movimentação (sem mudar velocidades)

**Data:** 31/10/2026. **Base:** GA-VS30 / PR #134 — 34/34 workflows aprovados. **Objetivo:** criar uma fronteira arquitetural entre movimento, economia, logística e combate sem antecipar a escolha do modelo futuro de velocidade.

## Diretriz confirmada
Não fechar ainda se as unidades terão velocidades em potências de dois ou **pontos inteiros de movimento por turno com excedente transportado**. Essa decisão depende de terreno, infraestrutura, transporte, encontros e combate. Não introduzir RNG de chegada nem um falso parâmetro físico. O motor de movimentação será a fonte única para o calendário de travessia; as demais áreas **consultam o motor** em vez de fixar durações.

## Implementação desta etapa
- `GlobalArena.Runtime/IPhysicalMovementTimeline`: contrato com `AdjacentArrivalTurn`, `RouteArrivalTurn`, `LegCompletionTurn` e `LegStartTurn`. A implementação vigente `PrimitiveFixedLegMovementTimeline` mantém precisamente **2 turnos por aresta**, como V13–V15 já exigiam.
- `PhysicalMovementTimelines.ForExistingMode`: ponto central que seleciona a política compatível com o modo salvo. O modo `LegacyInstant` não ganha uma duração inventada.
- `PhysicalRemoteDestinationRouteProjection` consulta a política para a chegada estimada e duração total. A BFS e sua ordem de desempate permanecem idênticas. Sem limite de colonização por distância.
- `CanonicalPhysicalTacticalSession` consulta a mesma política para partida adjacente, primeira etapa da viagem longa e progressão para a etapa seguinte. Não modifica eventos, itinerários, consumo, estoque ou status da expedição.
- `PhysicalTacticalEconomyRuntimeState` valida os mesmos prazos do V15 através da política, em vez de repetir `2UL` no construtor. `TacticalWorkerMarkerProjection` usa o início de trecho da mesma fonte ao exibir a progressão visual.
- `PhysicalJourneySupplyProjection` **já** recebe as datas canônicas do motor pelo objeto de rota e pela viagem ativa; não necessita de alteração para respeitar a separação.

**Garantia retroativa:** tudo isso é refatoração interna sem nova propriedade serializada, mudança no hash, versão do save ou regra visível. V13, V14 e V15 continuam com a mesma duração física, estados e replay. A política V13–V15 permanece explicitamente vinculada aos dois turnos antigos mesmo quando surgirem outras políticas.

## Contrato futuro, não implementado
Uma futura versão de movimento poderá calcular o custo de cada aresta por terreno, modal, infraestrutura, carga e estado da unidade, somando pontos inteiros de movimento. Excedentes não precisam ser perdidos nem sorteados; encontros/combatentes terão seu próprio tratamento. **O estado de progresso em pontos e o versionamento de save devem ser projetados juntos**: não reinterpretar partidas V15 já gravadas como se seus turnos fossem pontos inteiros.

Um sistema de pontos variável poderá exigir um contrato mais expressivo que o relógio de arestas atual (posição subturno, capacidade e custo efetivo por aresta). O atual `IPhysicalMovementTimeline` isola o acoplamento imediato **sem declarar que essa interface já implementa tais políticas**.

## Teste e critérios
1. xUnit `GaVs31MovementTimingPolicyTests`: cronograma de 2×N com inícios arbitrários, início/término de cada trecho, modo inválido, parâmetro inválido e overflow.
2. Roteiro real de viagem distante com residentes conservados, duas ordens simultâneas, passagem sem povoamento, V15 no meio do itinerário e após chegada, V14 adjacente sem mudança de formato.
3. `UiE2E.gd` no Godot 4.7.2 real: obter percurso distante, emitir a ordem e validar explicitamente `turnoDeChegada = turnoDePartida + 2×trechos`, além da restauração V15.
4. Godot Xvfb mantém mapa quadrado, picking e screenshots sem mudanças de UI.
5. Windows/Linux compilam e rodam as regressões anteriores. Não exigir execução manual no Windows para essa refatoração.

**Sem mudanças nesta etapa:** cadência econômica, comida, estoque portátil, compra de suprimentos, tarifas, atributos de cavaleiro/soldado, terreno, combate, disponibilidade de infraestrutura, roteamento multimodal, soberania ou algoritmo aleatório de chegada. PR draft empilhada na #134; sem merge isolado na `main`.
