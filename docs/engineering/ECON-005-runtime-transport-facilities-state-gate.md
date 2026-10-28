# GA — Gate D3: instalações logísticas no estado do mundo e hash canônico v9

**Status:** experimento estrutural em PR empilhado no Gate D2 (#49), sem merge na `main`, sem catálogo de itens e sem ações de construção ou liquidação.

## Problema

D1 validava a continuidade de trechos físicos e a localização dos transbordos. D2 (#49) adicionou evidências operacionais e capacidades tecnológicas, mas estas são declarações independentes do `WorldState`. O estado autoritativo **não continha registros de instalações**; portanto, um identificador atestado pelo chamador não era prova de sua existência no mundo.

## Representação mínima proposta

`GlobalArena.Runtime.TransportFacilitiesRuntimeState` contém registros canônicos, imutáveis e ordenados por `FacilityId`. Cada `TransportFacilityRuntimeRecord` guarda apenas:
- identidade numérica estável;
- localização fina qualificada `PhysicalTacticalTileId`;
- condição operacional;
- permissões explícitas e direcionais de transferência, definidas por tecnologia de entrada, tecnologia de saída e versão do catálogo de tecnologia.

Permissões duplicadas e instalações duplicadas são recusadas. Transição inversa não é presumida. O estado começa vazio. `WorldState.WithTransportFacilities` permite **substituição imutável do snapshot completo** somente para um mundo vinculado; esta é uma costura de estado, **não** um comando de construção ou uma autorização de gasto. Outras operações `With...` e `AdvanceRevision` preservam o registro.

## Hash canônico v9

O formato `CanonicalWorldStateHasher` foi elevado **de v8 para v9**. A nova seção **35** serializa, em ordem canônica, ID da instalação, parâmetros Goldberg M/N da malha fina, célula fina, bit operacional e capacidades direcionais ordenadas (IDs de tecnologias e versão de catálogo). O estado vazio também registra a seção com contagem zero. Todos os **seis vetores SHA-256 conhecidos** do estado foram recalculados independentemente pela codificação big-endian e mantidos como teste de regressão. O hash muda se local, identidade, capacidade ou status operacional mudarem.

**Compatibilidade:** todo consumidor que compare hashes v8 deve tratar v9 como **mudança intencional de formato**, não como equivalência automática de saves antigos. Este PR não implementa migração de saves nem formato de armazenamento em disco. A mudança exige revisão explícita antes de merge.

## Projeção de evidência para D2

`EconomicWorldFacilityAuthorizationProjector.Project(turn, world, expectedHash, bindings)`:
1. exige mundo vinculado e **hash esperado igual ao estado efetivamente fornecido**;
2. exige que cada `FacilityId` exista no registro do `WorldState`;
3. exige mesma localização fina, instalação operacional e uma capacidade exatamente autorizada no sentido e versão informados;
4. rejeita evidência ausente, desativada, obsoleta ou de outro estado;
5. passa a evidência validada ao Gate D2, que preserva sua auditoria de recursos de custo e itinerário da malha fina.

O `EconomicPhysicalResourceId` é a identidade de **custeio econômico**, não uma mercadoria física ou uma prova autônoma da construção.

**Limites inegociáveis:** um snapshot no `WorldState` só demonstra que a instalação está *registrada naquele estado*. Ainda **não** existe comando autoritativo de construção, controle de propriedade/acesso, reserva de insumos, custo de construção, capacidade, desativação transacional ou autorização institucional. A API `WithTransportFacilities` não deve ser interpretada como uma construção concluída nem ser acessível indiscriminadamente por sistemas de gameplay. O registro de propriedade e o protocolo de construção poderão ser introduzidos futuramente quando relevantes; não presumimos item intermediário de ferramenta ou armadura.

## Testes e aceite

Testes cobrem estado vazio, mundo não vinculado, reconstrução de hash, mudança de estado operacional/local/direção/versão, rejeição de identidades ou permissões duplicadas, imutabilidade do snapshot, ausência de mutação do mercado, ordenação independente da entrada, e projeção estrita para D2. Além disso, os golden hashes conhecidos do mundo foram atualizados para v9.

Executar as três matrizes GitHub Actions em Windows, Ubuntu e macOS no SHA final: kernel, hashes de estado e benchmark de frete. Os testes não comprovam migração de saves ou transação autoritativa de construção.

**Sem merge automático.** Gate D2 (#49) é dependência, e este PR deverá ficar empilhado nele até revisão.
