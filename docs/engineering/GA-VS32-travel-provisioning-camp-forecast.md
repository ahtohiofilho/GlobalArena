# GA-VS32 — Previsão de acampamentos e fontes opcionais de suprimentos

Data: 31/10/2026. Base: GA-VS31/PR #135 (26/26 workflows aprovados). Diretriz de colonização sem limite artificial: GA-ADR-EXP-006/#131.

## Motivação
Uma viagem não precisa acampar por ser neolítica, medieval ou moderna. Acampar e coletar são apenas estratégias disponíveis **quando o grupo não consegue alcançar o destino com seus próprios suprimentos ou serviços logísticos**. O mesmo planejador poderá futuramente consultar pousadas, povoamentos, serviços ferroviários, transportadoras e fornecedores de alimentos — desde que a propriedade e o pagamento sejam liquidados no motor econômico.

O V15 ainda consome alimento através do mercado de origem dos viajantes e não implementa uma carga física própria. **Não converter esse estoque comercial em estoque dos viajantes sem compra ou transferência autorizada.**

## Incremento estritamente read-only
- `ITravelFoodSource`: contrato opcional para fontes de alimento em um tile, devolvendo potencial **por trabalhador e por liquidação econômica**. Não compra, coleta nem cria a propriedade; a integração com fornecedores comerciais terá contratação à vista em outro marco.
- `PrimitiveForageFoodSource`: primeira fonte provisória, com produtividade de coleta igual a **75% da produtividade agrícola canônica do tile**, arredondada para baixo. O coeficiente é experimental e NÃO uma afirmação de equivalência geográfica entre agricultura e caça/pesca/coleta. Quando tivermos mapa de caça/pesca/coleta, substituímos esse provedor sem mudar o contrato de viagem.
- `JourneyProvisioningForecastProjection.PreviewPlanned`: cenário puro com origem, destino físico não adjacente, atividade, número de trabalhadores e quantidade **hipotética** de alimento já na expedição. Usa a rota Goldberg VS28 e o tempo por aresta centralizado na VS31. Para cada trecho:
  - conta liquidações alimentares efetivamente devidas durante os turnos viajados;
  - só oferece parada se a carga não basta até o próximo ponto físico;
  - cada ciclo de coleta **consome tempo de movimento real**, segundo a cadência econômica existente; não existe exploração de acampar e sair no mesmo turno;
  - durante a parada, os viajantes produzem alimento bruto pela fonte escolhida e consomem 0,5 unidade por trabalhador/ciclo, sem participação comercial;
  - se a coleta potencial não supera o consumo, o grupo não consegue acumular a diferença no local: resultado `InsufficientFoodAndNoProductiveStop`. É insuficiência dessa estratégia, nunca proibição eterna do destino.
- Para um grupo que já possui comida hipotética suficiente: a projeção não cria acampamentos, não alonga a viagem e mantém a previsão da VS31, inclusive no turno de chegada.
- A quantidade hipotética **NÃO é retirada** de `State.Stocks`, **NÃO afirma propriedade**, não cria pedido de mercado, não emite ordem de viagem nem fundação; não altera `WorldState`, `CurrentHash`, journal, caixa, estoque, demanda, cadência, saves V9–V15 ou formatos de rede.

## UI e automação
Na gaveta avançada (não reduz viewport): campo **Comida hipotética (unidades)** e botão **SIMULAR PARADAS E COLETA (SEM ALTERAR PARTIDA)**, exibindo paradas, ciclos de coleta, ETA, quantidade produzida/consumida e alerta expresso de que se trata de um cenário. Não confundir este botão com `ORDENAR VIAGEM LONGA`: aquele continua com as regras V15 antigas até que haja liquidação econômica de alimento portátil.

Testes xUnit validam provedores intercambiáveis, zero paradas com carga suficiente, espera de ciclos para produzir, impossibilidade de comida instantânea em terreno improdutivo, conservação de quantidades, cadência após o turno 30 e não mutação, inclusive após reabrir o save. Godot 4.7.2 E2E testa a consulta real com carga de 25 e 0 unidades, verificando que o journal e a movimentação continuam intactos; QA visual Xvfb preservado. CI Windows/Linux executa VS32 e regressões VS15/VS20–VS31.

## Não confundir com a próxima etapa de mutação econômica
**Ainda NÃO existem** cargo físico adquirido, transferência do estoque local, compra à vista pela expedição, alimentação efetivamente debitada do inventário viajante, serviços comerciais, acampamentos persistidos ou novo save V16. O cumprimento dessas políticas exige um evento de compra/transferência de propriedade e um ledger separado para a expedição, sem dupla cobrança da demanda na origem. Esse marco deverá também contemplar o trabalho de instalação do assentamento (um ciclo real de preparação inicialmente, não um mero tick de calendário).

**Sem modificações** à cadência, movimento, produtividade neolítica estabelecida, posse política, combate, modal futuro ou fórmulas de tarifa. PR draft empilhada sobre VS31, sem merge isolado na main.
