# GA — G3-J2: projeção econômico-ambiental no motor de fases

**Status:** adaptador opt-in para `PhasedTurnResolutionEngine`, baseado no PR #69. **Não** implementa publicação durável conjunta com `AuthoritativeTurnCommitGate`, nem altera a versão 9 do `WorldState`.

## Problema

O G3-J1 preserva em disco o save completo `GAFULL09` e um journal de entradas ecológicas encadeado. O motor autoritativo do GA, entretanto, possui dois contratos diferentes:

- `PhasedTurnResolutionEngine` opera em `WorldState` e exige que fases emitam `TurnResolutionResult` com eventos e logs.
- `AuthoritativeTurnCommitGate` publica o `WorldState` aceito da sessão, mas não recebe os livros G3 de capital natural e reservas.

Uma fase que chamasse `EconomicCompleteV9DurableSaveHost.CommitNext` durante `Resolve` criaria um **segundo publicador**: ela poderia gravar o save no disco antes de o gate autoritativo validar/publicar a sessão. Esse desenho seria incorreto.

## 1. Integração somente como projeção auditável

`EconomicEcologicalTurnProjectionPhase` implementa `ITurnResolutionPhase`, usando o resolvedor real `EconomicEcologicalTurnCommitResolver.Propose`, sem invocar o host de persistência.

A construção recebe explicitamente:
- mundo gerado e checkpoint econômico-ambiental **previamente aceito**;
- compromisso SHA-256 esperado do checkpoint;
- perfis de demanda por **trabalhador**;
- ordens de retenção física na origem.

Os perfis/ordens são copiados na construção, não referenciados a listas mutáveis do chamador. A fase exige:
1. vinculação do checkpoint ao mundo gerado;
2. snapshot de planejamento e snapshot staged serem **exatamente o mesmo objeto `WorldState`** ao qual o checkpoint está vinculado — se uma fase anterior modificar o estado, essa composição deve ser reavaliada num único host transacional futuro;
3. número do turno corresponder a `LastAppliedTurn + 1`, sem replay ou salto;
4. todas as validações econômicas e ecológicas existentes, incluindo produção física disponível e propriedade das retenções.

O resultado é o **mesmo `WorldState`**, um evento de sistema com identidade estável e um log com uma entrada elegível/executada para a *avaliação*. A avaliação em si foi executada; **produção/retensão não foi publicada pelo motor**.

`EconomicEcologicalTurnProjectedEvent` carrega:
- `EventId.ForSystemPhase(turn, "economy.production-demand", ordinal:1)`;
- compromisso de origem;
- `EconomicEcologicalTurnCommitResult` com checkpoint **proposto**, produção e demanda reais e oferta após retenção;
- compromisso proposto, derivado desse resultado.

É um evento transitório e auditável da projeção, **não um evento de liquidação, entrega, transferência de saldo ou declaração de que o checkpoint foi commitado**. O ciclo ecológico pode ser persistido depois, mas só um futuro gateway único poderá vincular essa decisão à publicação da sessão.

## 2. Uma única origem de demanda

`PhaseId` é deliberadamente igual a `EconomicProductionDemandTurnPhase.StablePhaseId`, usado também pelas fases legadas de demanda de residentes. O construtor de `PhasedTurnResolutionEngine` **recusa identidades duplicadas**. Portanto, é impossível instalar simultaneamente esta fase G3 e a produção/demanda clássica ou a versão legada de consumidores residentes sob esse contrato.

O sistema econômico neolítico continua: **trabalhadores produzem e consomem**; o censo legado é preservado apenas para compatibilidade de saves, sem criar outra demanda.

## 3. Fail-closed e consistência

A fase recusa:
- hash esperado desatualizado, mundo diferente ou `WorldState` modificado;
- snapshots de planejamento/staged incompatíveis ou ordem de fase incorreta;
- turno repetido/pulado;
- retenção de materiais inexistentes ou acima da extração física realizada;
- origem de retenção não registrada ou sem propriedade apropriada.

Uma falha não atualiza `WorldState`, reservas, capital natural, head G3-I6, journal G3-J1 ou sessão. Um evento construído com `EventId` de comando disfarçado de sistema é recusado pelo contrato do evento.

## 4. Testes

`GlobalArena.Tests/G3J2EcologicalTurnProjectionPhaseTests.cs`, sete cenários:

1. `PhasedTurnResolutionEngine` produz evento tipado, consumo de todos os trabalhadores, produção física e reserva, deixando o mundo original intocado;
2. determinismo e captura das listas de entrada sem dependência de mutação externa;
3. identidades de fase duplicadas são recusadas, impedindo duplo consumo;
4. mundo staged alterado/planejamento divergente rejeitados;
5. saltos de turno, compromisso desatualizado e mundo divergente rejeitados;
6. retenção impossível não muta estado;
7. `AuthoritativeTurnCommitGate` aceita apenas o `WorldState` imutável e registra o evento de avaliação, **não aceita o livro ecológico projetado**.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J2EcologicalTurnProjectionPhaseTests
```

## 5. Próximo gate G3-J3

Criar uma **única fronteira de confirmação**: verificar comandos realmente fechados/autorizados e eventos do scheduler, calcular efeitos em `WorldState` + livros ambientais, publicar o save completo + journal, preservar um vínculo confiável externo e **só então** mudar a sessão, com protocolo de recuperação para falhas entre disco, âncora e memória. Exigirá decisões explícitas sobre ordenação das fases, estabilização das ordens físicas de aquisição, pagamentos e entregas; nunca creditar compra apenas por intenção.

**Não há merge.** PR #49/#50 e `main` permanecem intactos.
