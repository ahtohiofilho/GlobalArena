# GA-Next TRADE-OVERLAY-001 — Gate 01A: auditoria de carga por aresta física

**17/11/2026 · incremento de código sobre PR #385 · Draft, sem merge · NÃO é ainda a publicação de um ciclo econômico real**

## Por que este corte técnico existe

O PR #385 documentou a proposta aprovada: a camada opcional deve mostrar o fluxo **efetivamente transportado no último ciclo econômico concluído**, com **largura por volume por aresta física** e **cor pelo titular histórico da infraestrutura**.

O motor `PhysicalWorkerTradeCycle` contém `FoundationTurn.EdgeLoads`, mas é um laboratório independente com `double` e IDs de arestas locais. Os PRs #383/#384 ainda representam entradas econômicas físicas, não uma solução autoritativa de frete/mercado v11. Publicar linhas no Godot usando esses dados seria inventar uma realidade que ainda não existe no mundo jogável.

Este PR implementa somente a **auditoria e agregação física reutilizável**, a ser chamada por uma futura liquidação real. Não executa comércio, não calcula preços, não dá titularidade, não abre camada visual e não publica um ciclo fechado. A origem e a autorização das entradas **continuam responsabilidade do futuro host**. Um teste pode fornecer dados de exemplo sem fingir que são eventos de gameplay.

## API/contratos adicionados

`GlobalArena.Simulation/Foundation/FoundationPhysicalTradeEdgeLoadAudit.cs`:

- `FoundationFreightDeliveryRouteEvidence`: entrega com ordinal único, ID de mercadoria existente, carga positiva em microunidades comparáveis, código explícito de unidade logística e **sequência física de tiles do itinerário fornecido**. Uma única posição é entrega local e não usa nenhuma aresta.
- `FoundationTransportEdgeOwnershipEvidence`: titularidade explicitamente fornecida para **cada aresta utilizada**, capturada no instante histórico pertinente. A enumeração distingue `Unknown`, `NoInfrastructure`, `UnownedInfrastructure` e `Owned(CivilizationId)`. O código não infere titularidade a partir de tile, produtor, consumidor ou investimentos simulados.
- `FoundationPhysicalTradeEdgeLoadAuditor.Audit(incidence, cargoUnitCode, deliveries, ownership)`: consome somente dados fornecidos; usa a topologia `PhysicalTacticalIncidenceMap.FineTopology.Edges` para mapear cada par de centros adjacentes à identidade geométrica qualificada `PhysicalTacticalEdgeId`. A fronteira é uma **travessia perpendicular**, não uma via longitudinal desenhada sobre a divisão territorial.
- `FoundationPhysicalTradeEdgeLoadAudit`: retorno somente leitura com `UsedEdges` ordenadas por ID; cada aresta contém um volume total, discriminação por mercadoria ordenada, dois centros canônicos e um único estado de titularidade; totais de carga vendida **submetida**, carga contada em todas as travessias e entregas locais.

O código NÃO inventa a rota mais curta: recebe uma lista de tiles e valida **cada salto** como uma verdadeira adjacência física. Sem essa sequência, rejeita a entrega. Para evitar duplicação, um ordinal de entrega só pode aparecer uma vez; o itinerário é simples, sem repetição de tile. Rotas locais não geram um corredor.

## Invariantes/fail-closed

1. `Σ_used_edges LoadMicro = Σ_submitted_deliveries CargoMicro × Crossings`; o teste utiliza dois sentidos na mesma aresta e produtos diferentes. Tráfego oposto **soma**.
2. `Σ_submitted_deliveries CargoMicro` é separado de `Σ_edge Loads`; uma entrega local conta no primeiro, não no segundo. Uma rota de dois saltos conta duas vezes no somatório de travessias, mas só uma entrega.
3. Somente arestas efetivamente atravessadas aparecem em `UsedEdges`; não há linhas para capacidade instalada sem utilização.
4. `Unknown` requer entrada explícita e continua distinto de `NoInfrastructure` e `UnownedInfrastructure`. Se faltar a titularidade de uma aresta usada, a auditoria falha, sem assumir cor neutra nem usar a civ do produtor.
5. ID físico de outro Goldberg, salto não adjacente, tile repetido, delivery ordinal repetido, unidade incompatível, carga zero, proprietário inválido, enum inválido ou overflow são rejeitados **antes de produzir saída**.
6. O resultado ordena arestas por ID e mercadorias por ID de modo determinístico; a ordem de enumeração dos inputs não altera as cargas finais. O teste concentra **10 mil entregas em uma única travessia**, sem 10 mil objetos visuais.
7. A identidade de proprietário fica guardada por valor no resultado; novo argumento de ownership não recolore o audit previamente construído.
8. Se não há entregas, o resultado é um **audit vazio**, que deliberadamente **NÃO certifica que houve ciclo econômico concluído**. Só um futuro publisher autoritativo poderá distinguir `NoClosedCycle` de `ClosedCycleWithZeroTransport`.

As unidades `CargoUnitCode` são exigidas e confrontadas, mas **a conversão de commodities diferentes para a mesma unidade logística ainda não existe**. É responsabilidade explícita do produtor dos dados reconciliar as unidades antes de enviar. Testes usam `test-normalized-cargo-micro`, que **não é parâmetro de gameplay**.

## Gates CI

`FoundationPhysicalTradeEdgeLoadAuditTests` cobre:
- Três entregas transportadas, uma local, dois produtos, dois sentidos e duas arestas (contagem conservativa separada de vendas).
- Estados históricos de ownership, inclusive desconhecido vs natural vs infraestrutura sem dono.
- Ordenação determinística dos itinerários e donos.
- Falha fechada de dados sem titularidade, aresta estrangeira, salto inválido, repetição, unidades incompatíveis e overflow.
- Dez mil entregas reais de fixture, agregadas em **uma única** aresta.

Workflow `ga-foundation-physical-worker-cycle.yml`: etapa explícita nos runners Windows/Ubuntu, com todas as regressões anteriores; Godot 4.7.2, hashes, benchmark e kernel preservados. **Nenhum screenshot de rotas comerciais é esperado**: o overlay não existe nesta mudança.

## Próximo Gate 01B — publicação real, ainda pendente

Conectar o mercado físico real do GA-Next ao futuro resultset de **entregas e itinerários aceitos**, vinculá-lo a ciclo econômico/turno/revisão/hash e ao snapshot de titularidade de infraestrutura já definido no servidor. O publisher deve garantir que só resultados finais entrem no auditor e que o read model seja autorizado por civilização. Posteriormente, construir o renderer Godot com largura escalada e cores do titular.

**Governança:** PR Draft empilhado sobre #385; sem merge na `main`; não mudar cenas, controles aprovados, hash/journal v11, preços ou regras econômicas. O resultado do auditor é **pré-requisito técnico**, não declaração de que o Gate 01 completo está finalizado.
