# GA-VS09 — Primeiro contrato físico no WorldState canônico

Data: 29/10/2026. Base: GA-VS08/PR #95, **gate visual Windows
confirmado pelo proprietário**. Branch de trabalho empilhada, sem
merge na `main`.

## Escopo aprovado e implementado

Este incremento estabelece o **contrato de estado e hash** necessário
antes de transferir a autoridade da sessão offline para a sessão
canônica. Não afirma ter concluído a migração operacional.

- `PhysicalTacticalEconomyRuntimeState` imutável representa
  alocações de trabalhadores/produtores-consumidores, estoques por
  `PhysicalTacticalTileId + CommodityId`, turnos e liquidações
  econômicas de seis movimentos.
- Identidades da malha fina devem corresponder a Goldberg compatível
  com a malha estratégica vinculada, identificada pela assinatura
  `RuntimeWorldBinding` do mundo realmente gerado. Não há conversão
  artificial de tile físico em ponto econômico estratégico.
- `WorldState.WithPhysicalEconomy` valida a assinatura e impede
  coexistência com residentes/pontos econômicos estratégicos
  materiais legados. Os métodos `With*` e `AdvanceRevision`
  preservam a nova dimensão em snapshots subsequentes.
- `CanonicalWorldStateHasher` usa o hash V8 **inalterado para
  partidas legadas** e V9 apenas para snapshots que possuem estado
  físico. V9 compromete o digest V8, geometria coarse/fine,
  trabalhadores, estoques, turnos e ciclos em ordem canônica,
  com codificação binária tipada e contagens.
- `EconomicPhysicalTacticalOfflineSession.BuildPhysicalRuntimeSnapshot()`
  cria cópia defensiva do estado VS07 para provas do contrato.
  Não cria pessoas, localidades, transporte ou recursos extras.
- Testes V9 cobrem ligação de mundo, alteração após seis movimentos,
  conservação de trabalhadores, imutabilidade, repetição de saves,
  rejeição de duplicidades e estabilidade da ordenação.

## Limites e responsabilidade de fonte da verdade

**Importante:** o laboratório Godot permanece conduzido pela sessão
offline VS07. A projeção imutável agora pode ser aceita pela
`CanonicalGameSession` sem perder estado; **ainda não é o escritor
autoritativo durante o jogo**, e não modifica a UI. Os saves VS07
continuam sendo replay JSON v1 offline. Não existe ainda save canônico
V9 nem migração/rollback formal de saves antigos.

Não promover para produção nem afirmar que a economia física está
integrada ao loop completo até implementar, em incrementos futuros:

1. Evento físico com transição atômica e validação de revisão/hash;
   uma única autoridade de escrita na `CanonicalGameSession`.
2. Migração de ordens/ticks/estoques/consumo da sessão offline para
   o reducer canônico, com replay sem dupla liquidação.
3. Save canônico V9, leitura versionada dos formatos anteriores e
   rejeição explícita quando a conversão não for segura.
4. Conectar HUD e simulador ao mesmo estado, testar Windows/Intel UHD,
   sem regressões de contornos ou estoque e sem exigir instalações
   arcaicas para agricultura/madeira/pedra.

**QA:** workflow Linux/Windows com .NET 10, regressões V8/VS07/VS09
e compilação do visualizador Godot original. Resultados de CI
precisam ser observados antes de aprovação técnica.

**Marketing:** MKT-00 apenas em preparação. MKT-01 não liberado.
