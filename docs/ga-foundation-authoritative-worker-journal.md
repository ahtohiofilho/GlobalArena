# Global Arena — Trabalhadores físicos autoritativos e journal de turnos

**15/11/2026** — Base PR #353. Draft sem merge. Esta entrega introduz identificação, localização e atividade física dos trabalhadores no estado autoritativo, com comandos, journal e replay. **Não certifica F072, não implementa tesouro, combate, settlement monetário ou integração de UI.**

## Diretriz

A unidade física central é o trabalhador. Cada trabalhador gera demanda onde se encontra, inclusive durante deslocamento ou atividade militar; produz somente se estiver produzindo. Todos os tiles obedecem exatamente às mesmas leis, sem mercados privilegiados, zonas especiais ou fronteiras comerciais artificiais.

## Estado e identidade

- PhysicalWorkerId: identidade individual imutável, positiva e de 64 bits.
- PhysicalWorkerRuntimeRecord: proprietário CivilizationId, célula Goldberg StrategicCellId, produto CommodityId, atividade PhysicalWorkerActivity, produção por turno OutputMicroPerTurn e DemandMultiplierMicro em unidades inteiras fixas. Não há double em campos autoritativos.
- PhysicalWorkerRuntimeState: censo explicitamente inicializado, inclusive vazio, ou ausente; ordenação canônica por ID, sem duplicação.
- WorldState.Workers preservado em todas as cópias e na mudança de revisão; validação de civilizações e tiles realmente existentes. População agregada antiga e EconomicPoint.Workforce não são reinterpretados como trabalhadores físicos.

### Hash sem quebra do legado

Se Workers não está inicializado, a serialização de WorldState permanece **v8 e byte a byte idêntica**. Se o censo é inicializado, a versão é **v9** e inclui seção de todos os trabalhadores e seus atributos. Essa mudança é opt-in no setup; não afirmar migração de saves.

## Pipeline autoritativo

PhysicalWorkerMoveCommand representa uma ordem originada por comando de jogador/IA. PhysicalWorkerMovementTurnPhase atua no pipeline existente e requer localização física válida, trabalhador pertencente à civilização emissora, no máximo uma ordem por trabalhador e por turno, movimento por exatamente uma aresta original Goldberg ou mudança de atividade no próprio nó. Movimento com atividade não declarada assume Traveling; mudança de atividade sem deslocamento é permitida.

Cada ordem gera PhysicalWorkerMovedEvent com fonte, destino, ID da aresta realmente percorrida (ou nenhum para atividade local) e atividade final. PhysicalWorkerMoveEventExecutor aplica a transição no WorldState imutável e incrementa Revision. O MESMO executor roda na reprodução de SimulationEventLogReplayer. Transições inválidas não publicam estados parciais. A publicação usa AuthoritativeTurnCommitGate existente, que verifica o próximo turno e snapshot inicial.

Outros comandos não são processados pela fase do trabalhador: futuros combates serão integrados sem inventar população militar paralela. Esta entrega não ativa automaticamente a nova fase nas partidas antigas.

## Projeção econômica e apresentação

GoldbergFoundationSession.FromAuthoritativeWorkers lê exclusivamente o censo de trabalhadores do WorldState v9, faz conversão declarada de microinteiros para as quantidades transitórias da economia C# experimental e calcula uma preview de mercado em tiles/arestas reais. Não existe liquidação financeira nem F072 certificado. Preview anterior a um commit torna-se obsoleta e é rejeitada; uma nova instância usa o censo atualizado. InspectTile continua somente leitura, com dados do tile exato, para futura conexão ao painel do Godot.

**Limitação:** a heurística comercial ainda mostra gaps de oportunidade acima do padrão de precisão. Os novos invariantes autoritativos dizem respeito à identidade, localização e atividade, não ao equilíbrio comercial. Não substituir normas financeiras exatas por doubles.

## Execução

    dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -c Release -- --foundation-worker-journal

O programa gera o mundo Goldberg G(3,0), materializa três trabalhadores, comita um movimento individual em turno 1, reproduz o journal partindo do snapshot anterior, compara o hash v9 do estado final com o replay e só então constrói uma preview econômica a partir da autoridade.

Testes Windows/Ubuntu verificam formatos v8/v9, identidade, invariantes geográficos, propriedade, replays, commit, ausência de teleportes/duplicação de ordens, bloqueio de eventos adulterados e reconstrução do inspector após mudanças da autoridade. Todos os testes anteriores também são executados.

## Próximo investimento em jogabilidade

Expor inspeção e comandos na UI Godot ligada ao snapshot canônico. Depois integrar trabalhadores às atividades militares e confrontos, sem duplicação de demanda. Preservar modelo econômico e testar precisão por resultados econômicos efetivos, não por infinitas micro-otimizações.

Status intencional: **primeiro contrato físico autoritativo, não economia definitiva, nem jogo concluído**.
