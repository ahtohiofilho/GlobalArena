# GA — G3-G: checkpoint econômico-ambiental verificável, sem colisão com hash v9

**Status:** implementação candidata de integridade transacional, ainda não inserida no scheduler autoritativo e sem persistência em arquivo.  
**Base:** G3-F (#60). PR #50 continua propondo formato 9 do `WorldState` com instalações de transporte.  
**Decisão central:** preservar `CanonicalWorldStateHasher.FormatVersion = 8` nesta branch; introduzir um **compromisso externo independente** com `EconomicEcologicalCheckpoint.CommitmentVersion = 1`, para migrar de forma coordenada somente após resolver #49/#50.

## 1. Por que não adicionar imediatamente duas seções ao WorldState

A branch desta série ainda parte do hash v8, enquanto o PR #50 introduz hash v9 e `TransportFacilitiesRuntimeState`. Modificar aqui `WorldState` para incluir livros de reservas e capital natural com nova versão 9 causaria um conflito semântico. Criar v10 em paralelo ignoraria que a proposta v9 ainda não foi integrada.

O G3-G constrói um checkpoint de **integridade determinística**, com uma assinatura própria que abrange todos os três valores de estado relevantes. Ela detecta alteração nos saldos que o hash v8 *não enxerga*. Isso NÃO é autorização de gameplay nem um formato final de save.

## 2. Checkpoint composto

`EconomicEcologicalCheckpoint(generatedWorld, world, naturalCapital, materialReserves, policy, commodities)`:

- exige mundo gerado idêntico à vinculação do `WorldState`;
- exige mapa ambiental completo e na ordem canônica das células estratégicas;
- exige que cada saldo de reserva referencie ponto econômico existente;
- exige que cursores de reserva apontem para civilizações existentes e não para turnos futuros;
- incorpora ao compromisso SHA-256:
  1. versão de compromisso e hash **com versão** do `WorldState`;
  2. versão de derivação ecológica, turno aplicado, cada célula com capacidade lenhosa, vegetação, pedra superficial e vitalidade do solo;
  3. parâmetros físicos explícitos de capacidade, recuperação e desgaste;
  4. saldos de mercadorias retidas por ponto e cursores de processamento por civilização;
  5. mapeamento semântico de IDs de alimentos, madeira e pedra.

O encoder é canônico, com domínios separados, contagens explícitas, ordenação estabilizada pelos próprios tipos de livro e inteiros big-endian. Não depende do `GetHashCode()` de C# nem de JSON com ordem indefinida.

**Segurança:** SHA-256 detecta divergências entre snapshots comparados, mas não autentica quem gerou os dados. O componente não assina digitalmente o checkpoint e não pode impedir que um chamador não confiável construa e assine seu próprio estado fraudulento. A autoridade sobre o hash esperado precisa permanecer no motor/servidor que efetiva as transições.

## 3. Transição integrada candidata

`EconomicEcologicalTurnCommitResolver.Propose(generatedWorld, previous, expectedCheckpointSha256Hex, turn, workerDemand, originHolds)`:

- verifica o digest esperado (compare-and-swap) e mundo vinculado, antes do cálculo;
- executa **exatamente um** ciclo natural sequencial G3-F, calculando produção física efetiva e recuperação;
- particiona retenções de origem por civilização, numa ordem canônica estável, e as valida contra **essa produção efetiva**, nunca contra potencial imaginado;
- credita reservas somente em quantidade voluntariamente retida; reduz a oferta comercial na mesma quantidade;
- marca em livro de reservas o turno aplicado para cada civilização sem permitir replay do mesmo turno por meio do livro resultante;
- retorna o checkpoint **proposto** e dois fluxos: produção real/demanda de trabalhadores, e oferta pós-retenção/demanda;
- preserva o mundo de entrada e seu hash v8. A evolução ambiental e o estoque reservado alteram o compromisso composto, não `WorldState`.

**Não há** compra/entrega atestada, liquidação, crédito de bens não vendidos, mudança de capital ao acaso, novos trabalhadores, instalações ou fundação de tile nesta etapa.

## 4. Limites não negociáveis

**Este é um gate de integridade, não um estado global autoritativo completo:**

- O jogo ainda não persiste um checkpoint em `WorldState`; não há importador/migrador de saves nem event log de autorização ou replay file-based.
- Passar `previous` e seu próprio hash não protege contra rewind: o motor precisa guardar um digest aprovado, de forma confiável, e só aceitar transições daquele digest; o resolvedor sozinho não possui estado global.
- O cálculo ecológico precede o mercado, mas a operação ainda não efetiva pagamentos, entrega ou liquidação. Uma intenção de aquisição no destino não aumenta materiais guardados.
- A aptidão natural v2 ainda é hipótese procedural e parâmetros de capacidade e recuperação são entradas explícitas sem calibração.
- A identidade espacial continua estratégica; tiles finos, rios e travessias reais aguardam contrato físico.
- O hash do `WorldState` no PR #50 é v9. Ao integrá-lo, escolher uma só versão posterior e atualizar vetores de hash com migração/testes cross-platform; não declarar G3-G v1 como `WorldState` v9 ou v10.

## 5. Testes

Arquivo: `GlobalArena.Tests/G3GIntegratedEconomicEcologicalCheckpointTests.cs`.

Cobre:
- retenção da **produção física real**, redução da oferta, avanço do capital natural e demanda independente da atividade;
- retenções por duas civilizações usando o mesmo recurso ambiental, sem contar árvores duas vezes nem depender da ordem das ordens;
- alterações de estoque ou parâmetros alterando o compromisso, mas **não** o hash v8 do mesmo mundo;
- digest desatualizado, replay ou saltos de turno rejeitados;
- excesso de retenção e origem inexistente rejeitados sem alterar os valores originais;
- equivalência de snapshots com ordenações diferentes;
- recusa de reservas para pontos desconhecidos, cursores futuros e mundos sem vínculo correto.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3GIntegratedEconomicEcologicalCheckpointTests
```

**Somente CI verde no commit exato permite declarar o gate de referência validado.**

## 6. Próxima integração necessária

1. Resolver empilhamento de PRs #49/#50 com base em aprovação explícita.
2. Escolher modelo autoritativo único de estado (uma seção de capital natural e outra de reserva) e versão de hash subsequente a v9, com provas de migração/compatibilidade.
3. Manter compromisso/ecologia acoplados à fase de turno e armazenamento confiável do digest aceito, com eventos auditáveis e replay deterministicamente verificável.
4. Integrar produção efetiva, retenções, pagamentos e entregas antes de creditar estoque de destino.
5. Aprovar geografia de detalhe e custos físicos de fundação/obras melhoradas, sem bloquear a produção primitiva.

Nenhum merge automático. Branch de G3-G isolada de `main`.
