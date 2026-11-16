# GA-Next — participação humana e IA no turno coletivo

**15/11/2026 (America/Recife) | sequência do PR #359 | Draft isolado**

## Contexto de produto

O PR #358 já oferece um jogo local com ordens G/P/X/E no Godot, com experiência visual aberta no Windows pelo usuário. O PR #359 estabeleceu `FoundationCollectiveTurnCoordinator`: todas as civilizações do mundo aguardam prontidão antes de publicar um turno autoritativo. O próximo investimento é permitir que as civilizações sejam designadas **explicitamente como humanas ou controladas por política de IA**, sem introduzir servidor de rede, combate ou comércio financeiro experimental.

A interface que o usuário já abriu permanece inalterada nesta entrega. O fluxo humano/IA é um **contrato de domínio/console**, separado do host local anterior; evita criar na UI uma falsa experiência de multiplayer. A ligação dessa política à UI ficará para a etapa de UX apropriada, depois de homologar o núcleo.

## Fundação — papéis e injeção de decisões

`FoundationHumanAiTurnHost` é o novo dono de orquestração, construído com:
- Um `CanonicalGameSession` com mundo Goldberg e trabalhadores físicos v9 inicializados;
- Uma lista explícita e sem duplicatas de `CivilizationId` humanos;
- Um dicionário de `CivilizationId -> IFoundationAiTurnPolicy` para civilizações controladas por IA.

**Cada civilização existente precisa exatamente de um papel.** Ausência, repetição ou sobreposição de papéis impedem criar o host. Nenhum papel é inferido por proximidade geográfica, poder econômico ou ordem de criação.

As funções humanas `QueueHumanMove`, `QueueHumanActivity`, `CancelHumanOrder`, `SetHumanReady`, `ReopenHuman` encaminham intenções ao coordenador coletivo apenas para civilizações marcadas como humanas. A confirmação trava a fila até uma reabertura explícita. Não há auto-ready de humano nem timeout oculto.

`PrepareAiCivilizations()` solicita propostas das políticas de IA uma vez por turno e por civilização, em ordem ascendente de ID. Elas consultam um snapshot **imutável** do mesmo estado do início do turno e devolvem uma lista de `FoundationAiWorkerDecision`. **Todas as propostas são validadas antes de enfileirar a primeira**, incluindo identidade/titularidade do trabalhador, IDs reais Goldberg, adjacência, atividade, capacidade de produção e ausência de duplicatas. Propostas inválidas falham sem publicação de estado e sem consumir turno. Após aceitar plano íntegro, a civilização IA é marcada como pronta. Chamadas adicionais no mesmo turno não repetem decisões. O dicionário original de políticas é copiado na criação do host para evitar troca externa de papéis enquanto uma partida roda.

`TryCloseTurn()` prepara IAs ainda não prontas e solicita ao `FoundationCollectiveTurnCoordinator` o fechamento coletivo. Se algum humano estiver pendente, retorna `null` **sem avançar relógio/revisão/hash**. Quando todos ficam prontos, publica **um único snapshot** com as ordens humanas e IA, em ordem global de trabalhador; reproduz journal, confere SHA256 v9, limpa as confirmações e passa ao próximo turno.

**A ordenação por worker ID é para replay determinístico, não uma regra de iniciativa física**; colisões, bloqueio por combate e simultaneidade física continuam fora do escopo.

## Políticas de IA provisórias e explícitas

- `FoundationAiHoldPositionPolicy`: zero ordens, mas marca a IA como pronta. **Não gera produção extra, custo, deslocamento, demanda nem trabalhadores.**
- `FoundationAiResumeProductionPolicy`: para trabalhadores **já viajando**, da civilização correta, com capacidade produtiva positiva, propõe mudança local de atividade para `Producing` sem deslocamento. Não escolhe recursos, rotas, preços, adversários ou estratégias econômicas. A movimentação militar e a decisão de produzir outros itens não são automatizadas.

A interface `IFoundationAiTurnPolicy` permite futuramente plugar estratégias de IA mais elaboradas; não afirma que elas existam hoje. Todas as decisões são proposições, não eventos autoritativos.

## Execução de CI

```powershell
dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -c Release -- --foundation-human-ai-turn
```

Cenário experimental canônico Goldberg G(3,0), duas civilizações, seis trabalhadores físicos. No setup inicial, o trabalhador #4 da civilização IA está em `Traveling` com capacidade produtiva preservada. A civilização humana enfileira o movimento do trabalhador #1; a IA propõe retorno do #4 à produção. Enquanto o humano não confirma, não ocorre publicação. Após confirmação, fecha **um único turno com dois eventos** e journal reproduzido hash v9 idêntico. Turno seguinte, ambas ficam ociosas: avanço de calendário sem evento artificial.

A suite `FoundationHumanAiTurnHostTests` cobre: papel completo/exclusivo; humanos não pulados; política hold; retorno à produção; múltiplos polls idempotentes; rejeição de ordens estrangeiras, duplicadas e inválidas antes de publicar; estado externo obsoleto; dicionário de políticas congelado; turno vazio; limites militares e hash determinístico entre duas sessões.

O workflow de fundação executa a suíte, o console e todos os testes C# históricos em Windows/Ubuntu. O workflow Godot 4.7.2 Mono permanece como regressão da UX anterior, sem alterações de frontend.

## Limites relevantes para planejamento

- **Não** é IA estratégica. A política de produção é local, curta, explícita e opcional.
- **Não** é multiplayer. O parâmetro `CivilizationId` não constitui autenticação nem sessão de rede.
- **Não** substitui combate, regras de encontros entre unidades, logística militar, decisões econômicas ou prazo de encerramento de turnos.
- **Não** movimenta trabalhadores durante planejamento, não duplica demanda, não contabiliza mercados ou impostos e não aprova precisão F072.
- O checkpoint visual de PR #358, já aberto no Windows do usuário, permanece a referência de uso; expandiremos a UI apenas quando um novo recurso for visivelmente útil.

**Governança:** PR Draft empilhado sobre PR #359, sem merge automático. Acrescentar resultados da CI à descrição antes de certificação.
