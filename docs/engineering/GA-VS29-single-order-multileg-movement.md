# GA-VS29 — Uma ordem, vários trechos; progresso canônico e save V15

Data: 31/10/2026. Base: GA-VS28/PR #132. Governança: GA-ADR-EXP-006 / issue #131 — a colonização não exige vizinhança.

## Entrega
Permitir que trabalhadores recebam **uma única ordem de partida para um tile terrestre não adjacente**. O motor avança automaticamente pelos trechos sucessivos do itinerário canônico de caminhada: cada aresta continua custando **2 turnos**, exatamente como o movimento VS15. Os viajantes permanecem fora da produção até chegarem ao destino final. Tiles intermediários não são colonizados, não ganham residentes nem soberania e não precisam de novas ordens do usuário.

O caminho é obtido pelo planejador global VS28 sem limite artificial de trechos. Destinos sem rota terrestre são recusados **apenas para o modal primitivo atual**, não como proibição definitiva de colonização. Viagens adjacentes existentes continuam sob `OrderWorkerTransit` e mantêm idênticos eventos, hashes, saves e duração.

## Estado e reprodução determinística
- `PhysicalLongDistanceTransit`: ID da ordem, origem, destino final, atividades, trabalhadores, turno inicial/final, término do próximo trecho, trecho completado e sequência canônica imutável de tiles.
- `PhysicalTacticalEconomyRuntimeState`: viagens longas separadas das V13; a população total conta residentes + viajantes adjacentes + viajantes longos. A flag `RemoteRoutingEnabled` persiste depois da chegada para manter o formato de save V15.
- `OrderLongDistanceTransit`: valida trabalhadores e rota; remove alocação na origem e grava **um único evento `depart_remote`**. Sem salto imediato; no primeiro tick, o grupo ainda está viajando; ao final de cada segundo tick atravessa a próxima aresta. A chegada só materializa atividade no destino final, não nos intermediários.
- `AdvanceMovementTurn`: concilia chegadas adjacentes e globais por ID canônico, com transferência proporcional de dinheiro familiar somente ao destino final, sem bancos, dívida, criação monetária ou receita inventada. O calendário econômico original continua.
- `CanonicalWorldStateHasher`: versão **15** com separador exclusivo para rota, progresso e população; bytes V9–V14 não alterados. `CanonicalPhysicalTacticalSession` passa de V14/V13 para V15 **na primeira ordem longa**. O formato V15 suporta replay do journal, save no meio do caminho, reabertura e chegada, com hash revalidado. V13/V14 sem ordem remota permanecem inalterados.
- `EmergentSettlementProjection`, `CivilizationalEconomicAttributionProjection` e `TacticalWorkerMarkerProjection` incluem os viajantes longos nos totais, marcando no mapa apenas o trecho atualmente percorrido.
- Interface: botão **ORDENAR VIAGEM LONGA ATÉ DESTINO FINAL** dentro das ferramentas avançadas recolhíveis, usando o ID de qualquer tile e profissão/quantidade já selecionados. Mantém o botão de movimento adjacente e os controles existentes. A nova versão do save usa o arquivo independente `ga-vs29-remote-journey.json` e seu sidecar de câmera, preservando os arquivos antigos.

## Limites transparentes
- O motor atual de alimentação faz a demanda dos viajantes na origem até a chegada, inclusive em viagem longa, preservando **exatamente a política provisória V13**. **Não existe ainda um estoque portátil de rações, custo monetário de frete, risco de rota nem reabastecimento nas paradas.** Isso exige decisão econômica e implementação própria; não representar essa proxy como uma cadeia logística concluída.
- Não existem colonizações automáticas nos pontos de passagem. O destino final precisa ser terreno terrestre trabalhável para caminhada primitiva. Travessia marítima, expedições e modais futuros são fase própria.
- O caminho é o mais curto em número de arestas com custos idênticos por trecho. Não substitui um planejador multimodal futuro com saturação/capacidade de transporte.
- Não altera cadência adaptativa (#121 congelada), macroeconomia (#110/#111/#103 congeladas), velocidades adjacentes, balanços monetários experimentais ou soberania.

## Gate CI
1. Testes .NET Windows/Linux: uma ordem `depart_remote` para destino 2+ arestas, sem colonizações intermediárias; 2 turnos por aresta; chegada com 8 trabalhadores conservados; moeda total constante.
2. Salvar/reabrir no meio do caminho e após chegada V15 deve reproduzir bytes do save e hash, inclusive com viagem adjacente simultânea; V13/V14 sem viagem distante devem permanecer intactos.
3. Godot real E2E: controles anteriores intactos, consulta global não mutante e emissão da viagem; painel informa progresso e save/reabertura. QA visual Xvfb preserva viewport quadrado e screenshots.
4. PR draft empilhada sobre VS28; **não fazer merge isolado na `main`** antes de fechamento de todas as verificações.

Próximo marco: especificação e implementação de suprimento fisicamente portátil e custos endógenos de logística, sem distância máxima nem teleporte.
