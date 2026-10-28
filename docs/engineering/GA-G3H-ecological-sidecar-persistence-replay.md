# GA — G3-H: sidecar persistível, aceitação atômica e replay ambiental

**Status:** contrato C# de persistência complementar, replay determinístico e host in-memory; dependente de G3-G (#61). **Não implementa save completo de WorldState nem merge com PR #50.**

## 1. Fronteira de compatibilidade: v8 / v9 / futuro v10

O `CanonicalWorldStateHasher` da branch de GA ainda é **v8**, com população do modelo legado. O PR #50, empilhado sobre #49, propõe **v9** para `TransportFacilitiesRuntimeState`, seção `35`: identidade de instalação, `PhysicalTacticalTileId` qualificado, situação operacional e capacidades dirigidas de transferência, ordenadas.

**Não é seguro inventar outra versão v9.** O G3-H oferece um arquivo complementar independente que:
- guarda o **formato e o digest** exatos do hash de `WorldState`;
- guarda integralmente livros ecológicos, reservas e parâmetros/mapeamento necessários aos turnos G3;
- exige um `WorldState` carregado **à parte** que produza o hash esperado;
- **recusa** qualquer mudança de versão do hash, mesmo que o mundo pareça idêntico;
- usa seu próprio `EconomicEcologicalSidecarCodec.FormatVersion = 1`.

Quando o PR #50 for conciliado, a mesma arquitetura poderá acompanhar um hash v9: as instalações estarão incluídas **por meio do digest do `WorldState` v9**, e não por duplicar dados das instalações no arquivo ecológico. Não existe, ainda, leitor de saves v8 → v9.

## 2. Arquivo sidecar v1 (ordem canônica, big-endian)

`EconomicEcologicalSidecarCodec.Encode` produz bytes estáveis:

| Ordem | Campo |
|---|---|
| 1 | Magic `GAECO1\r\n`, `UInt32` de versão sidecar |
| 2 | `UInt32` do formato `WorldStateHash`, 32 bytes do digest SHA-256 |
| 3 | 32 bytes do compromisso `EconomicEcologicalCheckpoint` |
| 4 | `UInt32` versão de derivação geográfica, `UInt64` último turno natural |
| 5 | Parâmetros físicos: duas escalas `UInt64`, três taxas `Int64` |
| 6 | IDs de alimentos, madeira e pedra (`UInt32` cada, do catálogo do chamador) |
| 7 | Quantidade `UInt32` e registros naturais por célula (`CellId, capacidade lenhosa, madeira em pé, pedra superficial, vitalidade solo`) |
| 8 | Quantidade `UInt32` e saldos reservados (`EconomicPointId, CommodityId, RawQuantity`) |
| 9 | Quantidade `UInt32` e cursores por civilização (`CivilizationId, LastAppliedTurn`) |
| 10 | SHA-256 final sobre **todos** os bytes anteriores |

`Decode(generatedWorld, world, encoded)` exige checksum correto, magic/versão aceitos, `WorldStateHash` **incluindo versão** idêntico ao mundo passado, número exato de células e IDs canônicos, registros válidos/sem duplicatas, quantidades/bounds válidos, compromisso semântico reconstituído idêntico ao fornecido e **re-encode byte a byte idêntico**. Recusa corrupção, arquivos truncados, contagens impossíveis, entradas extras e versões de origem diferentes.

O checksum é detector de erro, **não assinatura**. Um agente malicioso pode fabricar outro arquivo auto-consistente; a prova de autoridade requer **digest aceito mantido separadamente em fonte confiável**.

## 3. Limite de aceitação em memória

`EconomicEcologicalCheckpointHost` mantém um único checkpoint aceito sob lock:

- `CommitNext(expectedDigest, turn, workerDemand, holds)` verifica CAS dentro do lock, resolve a produção física, os créditos de retenção e os cursores, e aceita o novo checkpoint somente se toda a projeção concluir. Falhas preservam o estado aceito.
- `CaptureSidecar()` devolve os bytes do último checkpoint aceito.
- `Restore(generatedWorld, world, sidecar, trustedAcceptedDigest)` recusa rollback para um arquivo antigo **mesmo que seja válido e verificável**, desde que `trustedAcceptedDigest` seja o compromisso mais recente.
- Ainda **não** há transação com banco de dados, armazenamento de digest confiável em disco nem sincronização com o `PhasedTurnResolutionEngine`. O host é uma fronteira de referência para o futuro engine de turno, não um servidor completo.

## 4. Replay

`EconomicEcologicalReplayStep` contém entrada de um turno, ordem de retenção e hash esperado do próximo checkpoint.

`EconomicEcologicalSidecarReplayVerifier.Replay`:
1. reidrata o sidecar junto de um `WorldState` cujo hash corresponde;
2. executa um turno ecológico de cada vez com as mesmas regras/entradas;
3. compara o compromisso de cada resultado com o gravado;
4. recusa repetição, salto ou divergência de resultado.

O log de entradas é **fornecido externamente** e deve ser autenticado/salvo por outra camada. Não inclui liquidação comercial, prova física de entrega, construção, instalação nem eventos militares.

## 5. Testes

`GlobalArena.Tests/G3HEconomicEcologicalSidecarPersistenceTests.cs` valida:
- round-trip byte a byte de estado inicial e posterior à retenção;
- vínculo ao mundo e revisão exatos;
- corrupção, truncamento, formato desconhecido, versão incompatível de hash, contagem ilegal e bytes extras rejeitados; **também rejeita alteração de capital natural mesmo quando um invasor recalcula o checksum externo**;
- replay de três turnos e retomada do meio da sequência;
- recusa de divergência e turnos repetidos;
- aceitação CAS atômica, restauração após save e recusa de rollback com digest confiável mais recente;
- exceção na transição sem alterar o checkpoint anteriormente aceito.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3HEconomicEcologicalSidecarPersistenceTests
```

A CI multiplataforma é exigida no **SHA exato**.

## 6. Integração seguinte (G3-I, requer coordenação de merges)

1. Conciliar PR #49/#50 e sua seção 35 sem aceitar merge implícito, e aprovar novo formato de `WorldState` **posterior ao v9**, com seções para material reservado, capital natural, políticas e cursores;
2. definir migrador explícito v8/v9 + sidecar v1 → formato autoritativo futuro; recusar carregamentos incompletos ou versões incompatíveis;
3. incluir snapshot completo de `WorldState`, armazenamento transacional, digest confiável e journal autenticado; testar crash/restart e replay;
4. integrar produção/retensão ecológica à execução da fase de turno, com mercado real e atestação de entregas antes de creditar reservas adquiridas;
5. preservar trabalhadores como fonte única de demanda e produção, ocupação primitiva sem custo de material, potencial geográfico e identidade fina ainda pendente.

**Sem merge automático. Não confundir sidecar com save completo nem aceitar estados self-signed como autenticação.**
