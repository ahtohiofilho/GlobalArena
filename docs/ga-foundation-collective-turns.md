# GA-Next — turnos coletivos de múltiplas civilizações

**15/11/2026 | base PR #358 | status Draft / não mesclado**

## Contexto de produto

O PR #358 concluiu a primeira homologação do planejamento de vários trabalhadores do mesmo jogador, com um único fechamento de turno. O Godot 4.7.2 Mono já abre no Windows do usuário e o E2E gráfico confirma G/P/X/E. Esta entrega seguinte trata de uma lacuna do **domínio**: várias civilizações devem planejar seus trabalhadores contra **o mesmo snapshot inicial** e **nenhuma** deve antecipar o relógio de todas antes das outras confirmarem.

Por prudência, **não se altera o fluxo visual homologado do Godot** nesta etapa. A camada coletiva é uma opção C# exercitada por console e CI. Não é multiplayer online, não é regra final de iniciativa nem é política de batalha.

## Novo dono do turno

`FoundationCollectiveTurnCoordinator(CanonicalGameSession)` usa o próprio mundo gerado, as civilizações efetivamente registradas e o censo físico autoritativo v9. Todos os participantes são requeridos, inclusive uma civilização sem trabalhadores. Não cria civilizações, produtores, cidades ou mercados em função do número de nós.

Cada participante:
- `QueueMove(issuer, workerId, destination)`: move uma aresta Goldberg por turno, apenas trabalhador de sua civilização. Planeja `Traveling`, sem alterar o snapshot.
- `QueueActivity(issuer, workerId, activity)`: muda atividade local, produção apenas com capacidade positiva.
- `GetOwnOrders(issuer)`: cópia imutável da fila **daquele participante**, sem expor os planos das demais civilizações.
- `CancelOwnOrder(...)`: desfaz intenção ainda não confirmada.
- `SetReady(issuer)`: trava os planos daquele participante, mas **não executa o turno**.
- `Reopen(issuer)`: retira confirmação antes do fechamento para que possa revisar.
- `TryCloseReadyTurn()`: retorna `null` enquanto existir qualquer participante não confirmado. Não avança turno, trabalhador, revisão ou hash.
- `CloseReadyTurn()`: permitido somente quando **todas** as civilizações estão prontas. Ordena as ordens finais por ID global de trabalhador para evitar viés por chegada de comandos; atribui CommandId sequencial sob mesmo TurnNumber; executa tudo no `PhasedTurnResolutionEngine` privado e publica um único snapshot via `AuthoritativeTurnCommitGate`. O journal é reproduzido e o hash SHA256 v9 comparado com o estado publicado; só depois são limpas as filas e confirmações.

Duas civilizações podem demandar a mesma célula e seguir pelas mesmas arestas. Não introduz combate, prioridades por civilização, zonas comerciais ou penalização especial. Nenhum trabalhador executa duas ordens por turno. Todos continuam demandantes no tile em que realmente estiverem quando o núcleo econômico vier a ser acoplado; **não há demanda cobrada na fase de planejamento**.

## Garantias e limites

- **Um turno, N eventos:** ao resolver ordens de dois trabalhadores de civilizações distintas, o turno passa de 1 para 2 uma única vez e a revisão sobe em dois (o executor incrementa por evento). Uma única publicação autoritativa final.
- **Turno vazio:** se todas as civilizações escolherem esperar, o turno avança, mas **revisão, censo e hash não mudam**.
- **Privacidade de planos:** o contrato público do coordenador permite consultar somente a fila do emissor; é **organização de domínio**, não autenticação de rede (um código que consiga forjar o parâmetro `issuer` não está protegido por sessão autenticada).
- **Determinismo:** chegada das ordens e ordem de confirmação não determinam iniciativa. O lote utiliza ID de trabalhador, e a fase existente rejeita ordens duplicadas e destinos não adjacentes. A movimentação é aplicada em sequência canônica, logo isto **não é simultaneidade física exata**, necessária futuramente para conflitos/combate.
- **Snapshot externo:** alteração inesperada do `CanonicalGameSession.Snapshot` bloqueia submissão/fechamento até reconciliação explícita, sem descartes silenciosos de ordens.
- **Economia:** nenhum mercado, preço, imposto, tesouro, receita ou custo é liquidado; o solver F072 continua experimental. Não introduzir convergência aproximada como fonte de caixa.
- **UI:** os botões do Godot seguem o fluxo local de uma civilização testado no PR #358. Não adicionar seletor de civilizações ou interface de rede artificial nesta etapa.

## Evidência executável

```powershell
dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -c Release -- --foundation-collective-turn
```

Cenário real da fundação Goldberg G(3,0), 92 nós, duas civilizações e seis trabalhadores físicos:
1. Civ 2 e Civ 1 enfileiram seus movimentos independentemente; após só a Civ 2 ficar pronta, **nenhuma mutação** é permitida.
2. Civ 1 confirma; um turno com **dois eventos**, ambos de proprietários diferentes, é publicado. Hash v9 e replay devem coincidir.
3. No segundo turno, ambas confirmam sem ordens: **zero evento**, sem revisão artificial.

A suite `FoundationCollectiveTurnCoordinatorTests` cobre identificação e disponibilidade de participantes, fila privada, confirmação individual, proteção contra ordens estrangeiras, reabertura, ordem determinística de cliques, turno vazio, trabalhador sem produção, desvio de snapshot, terceira civilização sem trabalhador e compatibilidade do mundo v9. GitHub Actions mantém toda a regressão C# Windows/Ubuntu e a compilação do Godot existente.

## Próximo gate de produto

Em etapa posterior, decidir a regra para IA e jogadores ausentes, prazos e múltiplos comandos por civilização com papéis diferenciados; unificar a política de turnos para que o Godot consuma o mesmo host coletivo. Evitar simular multiplayer sem rede real. Antes de expandir a UI, aproveitar o checkpoint visual já concluído pelo usuário para inspecionar layout/ergonomia de controles.

**Sem merge automático.**
