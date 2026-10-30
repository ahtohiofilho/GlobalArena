# GA-VS19 — Primeira fundação política explícita (V14)

Data: 30/10/2026. Base: GA-VS18 PR #115.

## Decisão de escopo

Este incremento cria **uma civilização inicial e seu território político de origem por comando explícito do jogador**, sem tratar a chegada de trabalhadores como anexação política. É o primeiro comando de política territorial. Não resolve colonização subsequente, expansão, conflitos entre civilizações, guarnições nem combate.

O laboratório V13 começa com 8 trabalhadores, 0 civilizações e 0 territórios controlados. Ao selecionar um tile físico com trabalhadores **ativos** em terreno trabalhável, o comando `FUNDAR PRIMEIRA CIVILIZAÇÃO NO TERRITÓRIO ATUAL` elege o primeiro território estratégico incidente ao tile, a mesma regra de `DisplayBucket` usada pela visão tática. Apenas neste comando:
- cria-se `CivilizationRuntimeRecord(CivilizationId(1), StrategicCellId)` como civilização materializada;
- cria-se um `StrategicTerritoryControlEntry` para o **mesmo** território;
- `WorldState.Civilizations` e `WorldState.Territory` são publicados numa única transição com CAS, hash e +1 revisão;
- registra-se ação `found` com tile físico e território estratégico no diário auditável. O ato não consome trabalhadores, turno, material nem moeda; custos institucionais ficam para etapa própria.

## Guardas
- Um único comando de fundação inicial: rejeitar segunda tentativa sem mutação nem novo evento.
- Requer trabalhadores ativos no tile e `Canonical01C` com movimento temporal; viajantes não fundam antes de chegar.
- Território estratégico deve existir e estar entre as incidências 1/2/3 daquele tile. Não aceitar origem de outro mundo ou controle tácito.
- Trabalhadores não se tornam soldados, e presença física não muda soberania; somente a ação `found`.
- Desenvolvimento político pós-fundação será modelado em etapas próprias, sem efeitos simulados de leis ou impostos imaginários.

## Persistência
- Saves anteriores **V9–V13 são preservados**, com o mesmo algoritmo de hash enquanto não houver fundação.
- Ao fundar, hash físico e envelope passam a **V14**, incluindo no digest o estado político já coberto pelo hash legado e domínio V14, além de reter dados físicos V13.
- O save V14 contém linha de fundação declarativa verificada contra replay do diário `found`, com checksum e comparação exata do payload. Saves com informação política fabricada não substituem a execução canônica.
- O Godot grava partidas fundadas no **arquivo independente** `user://ga-vs19-first-civilization.json` e seu `.view.json`, deixando intocado o último V13; ao reabrir prefere V14 se existir. V13 não recebe fundação silenciosa.
- O nome interno do projeto Godot **não muda**, preservando o diretório `user://` existente desde o VS17; o título do laboratório indica VS19.

## QA manual Windows
1. Partida nova: confirmar 8 trabalhadores e mensagem sem civilização fundada; botão político habilitado no tile ocupado.
2. Clicar no botão **uma vez**. Confirmar Civilização 1 fundada, território inicial identificado e revisão +1; sem avanço de turno, sem alteração de estoque, dinheiro ou trabalhadores.
3. Clicar num tile vazio: não deve fundar por povoamento ausente. Tentar fundar novamente: botão indisponível.
4. Deslocar dois trabalhadores por 2 turnos. A presença territorial muda, mas nenhum novo território vira politicamente controlado.
5. Salvar partida com V14 e câmera alterada, avançar/mover, reabrir; conferir **SAVE POLÍTICO V14 REABERTO E VALIDADO**, território inicial/Civilização 1, saldo/estoques/turno, e câmera recuperada.
6. Testar save anterior V13 antes da fundação: continua legível e sem controle político automático.
7. CI Windows/Linux: testes de fundação atômica, rejeição sem trabalhadores, replay V14, resiliência de V13, recuperação da câmera e build Godot.

## Fora de escopo
Não criar civs adicionais, anexação territorial, guerra, domínio via trabalhadores, economia estatal, impostos nem regras de conquista. Demanda universal #103 e poder de compra endógeno #111 seguem congelados. Sem merge na main até autorização expressa.
