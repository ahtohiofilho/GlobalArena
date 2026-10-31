# GA-VS28 — Planejamento de rotas sem limite artificial de colonização

**Decisão canônica:** [GA-ADR-EXP-006 / issue #131](https://github.com/ahtohiofilho/GlobalArena/issues/131), 31/10/2026. **Base:** GA-VS27 / PR #130, homologada com 30/30 workflows.

## Diretriz
**Colonização não exige destino adjacente.** O jogador pode eleger qualquer tile de toda a malha física como destino final. Distância, tempo de deslocamento, consumo durante a viagem, disponibilidade de modais, infraestrutura e riscos podem criar custos elevados nas eras primitivas, mas o sistema não deverá criar regra arbitrária `distância máxima de colonização = 1`.

Diferenciar:
- **Destino estratégico da colonização:** arbitrário na malha. Pode inclusive estar em outra massa continental quando houver modalidade de travessia e tecnologia adequada.
- **Trecho físico de movimento:** no motor atual, a marcha primitiva percorre uma aresta entre tiles adjacentes, com **2 turnos por trecho**. Isso é uma mecânica de deslocamento, **não uma limitação do destino final**.
- **Disponibilidade do modal:** uma ilha sem caminho a pé ou um tile aquático não possui rota de caminhada hoje, mas pode exigir futura tecnologia marítima ou assentamento especializado. A ausência de rota **não significa proibição eterna**.
- **Custo:** somente estimar duração já fundamentada. Não antecipar uma tarifa monetária, custo de frete, rações ou risco sem o sistema correspondente, nem arredondar arbitrariamente a distância máxima para fins de gameplay.

## Incremento técnico e operacional
- `PhysicalRemoteDestinationRouteProjection.Preview` recebe origem e **qualquer tile físico de destino**. Verifica apenas a legitimidade do trabalhador e o modo de movimento disponível.
- Usa BFS na topologia **canônica Goldberg** pelas arestas da malha. Os vizinhos são enumerados por IDs crescentes, com desempate de caminho determinístico. Um caminho terrestre alcançável tem `N` trechos, estimativa mínima `2 × N` turnos e turno de chegada mínimo `turnoAtual + 2 × N`.
- O resultado apresenta **sequência efetiva de nós adjacentes**, não salta do início ao destino nem cria assentamento intermediário. Se a caminhada não alcança o destino, retorna estado estruturado de rota dependente de outro modal; não inventa oceano percorrível a pé.
- O **modo de transporte único atual** só permite emissão canônica de viagens adjacentes. **VS28 não emite uma viagem de longo curso** e não altera `OrderWorkerTransit`, journal, replay ou save. A transição para uma *única ordem de viagem longa*, com itinerário e custos de suprimento, ficará em marco próprio e exigirá progresso físico de trecho para trecho ao longo dos turnos.
- Interface adicional opcional, **dentro das ferramentas avançadas já recolhíveis**, permite digitar o ID de qualquer tile e consultar disponibilidade da rota com início/fim, trechos e duração estimada. Não cria atalhos de colonização nem exige que o mapa tático exiba todo o planeta ao mesmo tempo; no futuro a seleção global poderá ocorrer diretamente no mapa estratégico.
- Resultados são somente leitura; o jogador não perde dinheiro, trabalhadores, tempo, posse ou capacidade de construir ao estudar qualquer destino.

## Testes e aceite
1. .NET VS28: um destino não adjacente encontrado na malha é alcançado por uma cadeia determinística de arestas; `MovementLegs ≥ 2` e `PrimitiveTravelTurns = 2 × MovementLegs`. Todos os trechos estão efetivamente na topologia; sem teleporte.
2. Verificar invariância de save, hash, journal, população, mercado, cadência e controle político. Em V13/V14 o roteiro é idêntico para a mesma geografia. Legado V9 sem movimento temporal permanece intacto.
3. O resultado ao escolher um tile sem acesso terrestre indica **modo atual insuficiente**, não `colonização impossível`.
4. Godot E2E real: abrir ferramentas recolhíveis, digitar destino físico global não adjacente e consultar sem movimentar ou aumentar o turno. O fluxo antigo de selecionar um vizinho e emitir o movimento **continua funcionando**.
5. Godot visual Xvfb: mapa quadrado e coluna lateral sem regressão; captura estratégica/tática, picking real, sem novos controles permanentes.
6. CI Windows/Linux com regressões VS20–VS28 e compilação Godot Debug/Release.

## Planejamento posterior — não antecipar
Para executar colonização realmente distante, deverão existir uma única ordem canônica de deslocamento ao destino final, itinerário de múltiplos trechos, suprimentos consumidos fisicamente durante viagem, modos disponíveis, custos acumulados sem teto arbitrário e persistência/replay determinísticos. A partir daí será economicamente natural colonizar mais perto no Neolítico **sem nenhuma obrigação de fazê-lo**.

**Diretrizes imutáveis nesta etapa:** macroeconomia (#110/#111/#103), cadência absoluta futura (#121), política/guarnição, cidades emergentes e inexistência de bancos/crédito (#GA-ADR-ECON-005). **Sem modificações no comportamento já homologado.** PR draft encadeada à #130, sem merge isolado na `main`.
