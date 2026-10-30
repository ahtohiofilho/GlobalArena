# GA-VS15 — Deslocamento temporal de trabalhadores no mapa físico

## Objetivo
Implementar o primeiro comando tático em que grupos de trabalhadores escolhidos em um tile adjacente **levam tempo para chegar**. O comando altera o mundo, a produtividade e a localização física; não é somente um efeito visual. A implementação evita gerenciar finanças individuais, custos de viagem inventados e transporte mercantil.

## Contrato MVP testável
- Modalidade **TimedAdjacent**, opt-in para o novo laboratório Godot. Terreno físico canônico 01C e economia adaptativa existentes.
- Origem selecionada no mapa, tipo de trabalho de origem, número de trabalhadores e tipo de trabalho de chegada. O destino deve ser **adjacente e trabalhável**.
- **Duração inicial fixa: dois turnos de movimento**. A evolução de duração por terreno, infraestrutura, tecnologia, velocidade e unidade será posterior; não introduzir multiplicadores sem benchmarks.
- Na emissão da ordem, o grupo sai das alocações produtivas e passa a existir como `PhysicalWorkerTransit` canônico contendo ID, origem, destino, atividades, pessoas e turnos de partida/chegada.
- Enquanto viaja, não produz. Continua gerando a **necessidade alimentar** na origem, representando abastecimento local provisional e **não transferência instantânea de comida entre tiles**. Se faltar alimentação, registrar falta real. Transporte físico de provisões fica para etapa própria; esta regra é provisória e explicitada no HUD.
- Ao completar o segundo turno, passa a integrar as alocações produtivas do destino **antes do processamento econômico daquele turno**.
- Caixa do VS14: permanece na origem até a chegada. Ao chegar, a parcela existente de caixa familiar proporcional à população vinculada à origem acompanha os trabalhadores; receitas dos produtores permanecem com os produtores locais. Não criar dinheiro nem cobrar taxas de viagem fictícias.
- **Conservação:** trabalhadores ativos + em trânsito = total inicial. Sem duplicações na partida ou após reabrir save.
- Cada comando e avanço de turno deve realizar um único commit CAS. Hash V13 vincula ordens em curso, movimento e saldos. Novo **save V13** registra viagens; V9–V12 são compatíveis e mantêm os comandos antigos.
- HUD: botão `ORDENAR DESLOCAMENTO (2 TURNOS)`, painel de ordens ativas e ETA. O atalho de alocação instantânea é proibido apenas em partidas V13; saves V12 mantém o comportamento anterior.

## Limites / próximos gates
Este é o **primeiro comando de movimento funcional**, não um sistema completo de unidades militares, rotas, frete, suprimentos físicos, combate ou conquista. Não alterar a política de poder de compra e produtividade (issues #110–#111). Demanda basal multicommodity #103 permanece congelada.
A regra de abastecimento de viajantes pela origem é um **placeholder físico explícito**, não o sistema definitivo de suprimentos em trânsito. Não prometer interação gráfica de unidades em movimento sobre a superfície nesta etapa; o estado/ETA aparecem no HUD.

## QA
1. Iniciar VS15 no Windows, selecionar tile físico origem e destino adjacente, escolher origem Agricultura, destino Madeira (ou Agricultura) e 2–3 trabalhadores; emitir ordem.
2. Na mesma revisão: trabalhadores **em trânsito** aumentam, total global permanece 8, trabalhadores ativos caem na origem, destino não os recebe imediatamente.
3. Turno seguinte: ETA 1, produção interrompida para viajantes, demanda alimentar mantém pessoas do percurso.
4. Segundo turno: ETA completa e trabalhadores aparecem no destino; total global permanece 8; economia local reage.
5. Salvar enquanto há viagem, avançar até a chegada, reabrir V13 e confirmar que o prazo, pessoas e saldos foram restaurados. Reproduzir chegada; hash e estado equivalentes. Os testes automatizados verificam replay e saldos.
6. Não declarar gate visual aprovado antes do teste real Godot Windows.

Branch empilhada na PR VS14 #109, preservar a main e as versões V9–V12; PR sempre draft.
