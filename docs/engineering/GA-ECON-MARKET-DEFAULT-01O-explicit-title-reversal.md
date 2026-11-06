# GA-ECON-MARKET-DEFAULT-01O — reversão explícita de titularidade econômica simulada

**Data:** 06/11/2026. **Base:** PR #294 / GA-ECON-MARKET-DEFAULT-01N, HEAD `528f289dbef486a65a78a35de5ec8aca7d590c49` (FAST 19/19, três outros gates do checkpoint em execução na abertura do incremento). **Status:** laboratório opt-in, Draft, sem merge.

## Objetivo

O 01N permite registrar como titular econômico **dentro da simulação** uma instituição que recebeu fisicamente madeira/pedra e apresentou duas permissões revisadas. Esse ato bloqueou corretamente a devolução física ao estoque do titular original, mas ainda não existia um caminho para desconstituir legitimamente o título e, em seguida, devolver o material.

O 01O introduz **uma reversão de titularidade econômica registrada no journal**, com autoridade de revisão distinta daquela presumida pela mera posse física. A reversão e a devolução física são **eventos diferentes**; nenhuma das duas operações deve ser confundida com venda, restituição monetária ou transferência legal de propriedade fora do jogo.

## Contrato canônico

`ReverseLocalSimulatedEconomicTitle(originalTitleActionOrdinal, evidence, reviewer)` valida um `CanonicalLocalSimulatedTitleReversalEvidence` contendo:

- ordinal e hash do título canônico previamente registrado;
- compromisso de origem do lote, e hash atual do mundo;
- acordo de reversão com ID e versão próprios, **distintos** do acordo original;
- autorização nova do antigo titular econômico e aceite novo do titular de origem, **não reutilizáveis** dos IDs de autorização do registro original;
- instituição emissora **expressamente declarada** e distinta das duas partes (Community, Cooperative ou PublicAuthority), sem criar uma autoridade Neolítica fictícia por inferência;
- turno inicial e final de validade, abrangendo o turno atual.

O kernel não fornece aprovação implícita; `ICanonicalLocalSimulatedTitleReversalReviewer` é obrigatório e deve ser implementado pelo host. Testes têm somente stub. Recusa do revisor, título inexistente, título já revertido, lote já consumido/devolvido, autorização vencida/adiantada, ID fraudado ou emissão pela própria parte geram **falha sem mutação**.

## Semântica de execução

1. Com o lote **ainda sob custódia física do destinatário**, o registro `reverse_local_economic_title` avança o hash/revisão do mundo e adiciona um recibo `CanonicalReversedLocalSimulatedEconomicTitle`. O estoque e os lotes físicos **não mudam**.
2. `LocalSimulatedTitlePositions` diferencia titularidade em nome do destinatário, ou título revertido ao titular de origem, e custódia material ainda ativa ou já encerrada.
3. Depois da reversão, `UseLocalRecipientMaterial` é bloqueado para o antigo destinatário. A saída legítima agora é `ReturnLocalRecipientCustody`, o evento 01K que transfere os **mesmos lotes** ao estoque livre original, uma única vez.
4. Sem reversão, a regra 01N continua bloqueando `ReturnLocalRecipientCustody` para não devolver material ainda economicamente titularizado à contraparte.
5. O histórico conserva **ambos os registros** (titularidade e reversão) e o eventual recibo de devolução. Nenhum título antigo é apagado, nenhum saldo em estoque duplicado.
6. Depois da reversão, o lote não pode ser retitularizado através do comando 01N original. Uma nova negociação exigirá **novo compromisso físico, novo handoff e novas autorizações**. No retorno, essa nova oferta poderá ser criada normalmente sobre o estoque livre recuperado.
7. Para a eventual venda final, o material só poderá ser contabilizado como entregue, utilizado ou comercializado se houver **evento específico de liquidação real**; essa reversão nunca constitui pagamento.

## Save e replay

- **V43:** registro da reversão com o formato de save canônico anterior, sem tesouraria.
- **V44:** versão com reserva dormente do Tesouro, que continua sem saldo monetário.
- `CanonicalPhysicalAction.TitleReversalEvidence` é opcional e **omitido quando nulo**, preservando bit a bit os saves V9–V42.
- `RestoreSaveJson` aceita V43/V44 **somente com evento real de reversão**, recusa reversão em envelope anterior e recusa campos institucionais falsamente anexados a ações de outra espécie.
- O replay refaz as operações em ordem, confere hashes, compromisso, duas permissões registradas, acordo e sequência reversão–devolução. Não chama o revisor externo novamente, pois os IDs do save são recibos históricos **não assinados**; não autenticam título jurídico fora do jogo.

## Limites e diretriz econômica

A economia mantém poder de compra **abstrato, sem carteira monetária da população, crédito, bancos ou demanda numérica arbitrária**. Esta etapa não atribui preço, receita, imposto, direito de compra, consumo alimentar ou transporte remoto a nenhum lote. Madeira/pedra continuam sujeitas à origem física; o alimento legado agregado não ganha composição nutricional fictícia.

## QA

`GaEconMarketDefault01oTests` verifica a cadeia:

- handoff real → título 01N → reversão 01O → devolução física 01K, preservando quantidade e hashes;
- sem reversão, retorno proibido; com reversão, uso pelo antigo destinatário proibido; consumo legítimo antes da reversão não pode ser revertido;
- revisor recusa, revogação de possibilidades, IDs adulterados, dupla reversão, validade, emissor inadequado, hashes adulterados e eventos impossíveis;
- saves V41 antigos idênticos, rejeição de envelope V43 adulterado e replay V43/V44, inclusive tesouraria dormente sem renda.

**Gate:** FAST no HEAD e checkpoint completo de regressão de kernel + hash + benchmark em Linux/Windows/macOS antes de qualquer promoção. Workflows `skipped` não são aprovações.

## Diretrizes de transporte, mantidas

No mapa tático, infraestrutura entre tiles representa **ligações do grafo entre células vizinhas**: o desenho da estrada tende a cruzar a fronteira compartilhada em vez de correr paralelamente a ela. `StrategicEdgeId` é a identidade de conexão, não a exigência de traçado geométrico literal. Modais com nós e enlaces lógicos continuam possíveis, sem deslocamento físico implícito.
