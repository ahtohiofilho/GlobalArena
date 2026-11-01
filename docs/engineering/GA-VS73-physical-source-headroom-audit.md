# GA-VS73 — Conservação física na origem com frete hipotético fixo

**Baseline:** GA-VS72, PR #179, head 39e88ce8be9c94fca2b4b15622db1539a63b4f22 (38/38 workflows completed/success). PR VS73 deve permanecer empilhada em Draft, sem merge na main.

## Escopo canônico mínimo

A VS72 identificou o teto físico de autoprovisão de cada povoamento/tile e commodity, **antes** de considerar remessas externas. A VS71 já calcula dois cenários mutuamente excludentes de frete comercial selecionado, condicionados a poder de compra abstrato uniforme ou regional. A VS73 apenas verifica sua compatibilidade com a produção na origem.

Para cada cenário VS71, e para cada origem e commodity com demanda física:

- Confere a capacidade **por origem, mercadoria e atividade** com a oferta física observada e os lotes de frete selecionados e autenticados na VS67/VS66.
- Agrega as reservas de remessa selecionada **somente dentro da mesma mercadoria** na mesma origem.
- Registra produção física, demanda física local, saída planejada, produção que restaria se a saída já selecionada for mantida, e teto hipotético de provisão local dentro dessa condição.
- Expõe entrada de frete planejada **em coluna independente**, reconciliada com a VS71, sem somar com a possibilidade local.
- Registra fração média adimensional de cobertura local condicionada, quantidade de pares origem/produto com saída selecionada e quantidade de pares cujo teto local condicional é inferior ao teto VS72.

**Interpretação:** a expressão “reservado” aqui significa uma **condição contrafactual de leitura**: “supondo fixas as remessas já escolhidas pela VS71, o que sobraria na origem?”. Não existe uma transação, reserva persistente ou política de prioridade das exportações. **Não** representa consumo local efetivo, abastecimento efetivo por frete, bem-estar, estoque, nem a preferência econômica final entre consumir e exportar.

Os cenários uniforme e regional são alternativos, jamais cumulativos. O fator de poder de compra continua abstrato, o resíduo H fica protegido em cada comprador e todos os direitos institucionais, corredores e preços continuam sendo validados pelo seletor original. Não se altera o seletor, a lógica do mercado, o ritmo de turnos, a produção, o catálogo de recursos, a UI ou o save.

## Contrato

API de leitura: CanonicalPrimitiveLocalSupplyPotentialDiagnostics.DiagnoseSourceReservations().

Recibo: CanonicalPrimitiveSourceReservationDiagnostics, com Baseline e Regional. Cada linha CanonicalPrimitiveSourceCommodityHeadroom inclui:

- PhysicalProductionRaw
- PhysicalLocalDemandRaw
- SelectedOutboundFreightRaw
- UnreservedPhysicalProductionRaw
- MaximumLocalUseAfterOutboundRaw
- MaximumLocalCoverageAfterOutbound
- SelectedInboundFreightRaw

A fonte econômica deriva do **mesmo objeto CanonicalPrimitiveSiteDiscovery** usado em VS72, sem inventário arbitrário. Para cada atividade produtiva, o total de lotes selecionados deve ser menor ou igual à produção física daquela atividade e mercadoria; a soma das saídas deve caber na produção da mesma commodity. A reconstrução também exige que cada origem e cada comprador selecionado constem no inventário auditado, com demanda reconhecida no escopo de produtos primitivos corrente. Caso contrário, falha fechada.

VerifyIndependent() exige auditorias VS72/VS71/VS67/VS66/VS65 e reconstrói origem/atividade/commodity, somas e frações. RequireCurrent() invalida o recibo ao mudar o mundo, a rede, a sessão ou ao reabrir um save. Recibos são imutáveis/read-only e não são autorizações.

CanAuthorizeRealSettlement=false, IsCommitted=false, IsActualConsumption=false, IsPersistentReservation=false.

## QA

Os testes da VS73 cobrem conservação da oferta por origem e mercadoria, prova das entradas e saídas contra lotes VS71, cenários sem frete viável, dois corredores independentes, reordenação determinística e invalidação após restore/mutação de rede. O benchmark --vs73-source-headroom repete 8/32/128 consultas sobre **um único cenário conectado e fixo**, com aquecimento e três amostras, sem representar mapa massivo. Gate Linux/Windows executa regressões, benchmark e Godot Mono.

## Fora de escopo

Não introduzir decisões de prioridade entre exportação e consumo local, algoritmo de negociação, outro otimizador, estoques, salários, preços novos, crédito, cobrança, contratos, SETTLE, UI ou novos recursos. Uma seleção simultânea que escolha o compromisso final entre uso local e exportação precisará de autorização conceitual separada; a VS73 **não** a implementa.

Homologação apenas com **todos** os workflows do SHA final em completed/success.
