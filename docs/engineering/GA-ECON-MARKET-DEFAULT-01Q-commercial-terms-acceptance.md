# GA-ECON-MARKET-DEFAULT-01Q — aceite comercial bilateral autenticado, sem liquidação fictícia

**Data:** 07/11/2026. **Base:** PR #296 (01P), HEAD `58a1a46dfd9df249667907c23e603e401221c847` (quatro workflows FAST, kernel, hash e benchmark concluídos, 0 falhas). **Estado:** comando real no journal canônico opt-in, PR Draft, não ativado por padrão no Godot.

## A operação que este marco EXECUTA

Os incrementos 01J–01O já deram à madeira/pedra uma origem física, entrega a grupo produtivo efetivo, instituições identificadas, revisão de direito, título econômico reconhecido **no jogo** e reversão formal. O 01P disponibilizou curvas VS78 sobre material já titularizado. Faltava uma ação comprovável de **aceitação bilateral das condições comerciais**. Não é correto tratar o simples índice de preços como valor pago nem criar saldos fictícios dos moradores.

O 01Q introduz `CanonicalPhysicalTacticalSession.AcceptLocalCommercialTerms(titleActionOrdinal, termsId, version, rawUnitScale, curve, sourceConsentId, recipientConsentId, reviewerInstitution, reviewer)`.

O comando é um **ato executado**: insere no journal a ação `accept_local_commercial_terms`, avança revisão/hash e produz `CanonicalRecordedLocalCommercialTerms`. Não há novo lote nem segunda entrega física; a posse física e o título já existem. Não movimenta dinheiro, bens, tributos ou qualquer carteira da população.

### Condições de execução

1. **Lote local real:** exige madeira ou pedra efetivamente ofertada e transferida em 01J, com titularidade da simulação registrada em 01N e ainda não revertida em 01O. O material tem de estar **sob custódia física** do grupo destinatário e não pode ter sido consumido/devolvido.
2. **Condições comerciais expressas:** exige novo ID/versão de termos (não reutiliza o acordo de título), IDs independentes de aceite da origem e do destinatário (não reutilizam os grants anteriores), e emissor/revisor institucional identificado, tipado e **distinto das duas partes**. Só `PublicAuthority`, `Community` ou `Cooperative` podem ser emissores propostos. Um emissor textual não é órgão público por inferência.
3. **Host reviewer obrigatório:** `ICanonicalLocalCommercialTermsReviewer` valida externamente o consentimento deste lote a estes termos precisos. Não há revisor permissivo em produção; stubs de teste não são assinatura contratual.
4. **Preço limitado a referência endógena:** o motor calcula obrigatoriamente `CanonicalTitledLocalMaterialMarginalExposure.FromCurrent` no **hash atual** da partida e escolhe uma das curvas VS78. Utiliza a **quantidade de títulos ativos do mercado** e os **habitantes reais do tile**, com escala explícita positiva. O preço médio marginal integrado do mercado, multiplicado pela quantidade física do lote, gera um **índice relativo bruto alocado**. Não aceita preço nominal arbitrário digitado pelo caller.
5. **Aritmética exata:** pressão, volumes, numeradores e denominadores racionais são salvos como **strings decimais canônicas**, evitando arredondamento em JSON, overflow e diferenças entre plataformas. O replay recalcula integralmente esses valores na fronteira histórica da ação e recusa preço/volume adulterado, **mesmo se o usuário recalcular o SHA do payload**.
6. **Ato único por título:** um mesmo lote titularizado não pode aceitar dois contratos sem cancelar formalmente o anterior. Ao aceitar termos, impede-se a reversão silenciosa de título; futura reversão deverá cancelar o ato comercial antes. O consumo físico pela atividade produtiva real (01K) continua possível, mantendo a evidência comercial histórica separada da baixa de estoque.

### Importante: definição da fronteira econômica

**Aceite comercial bilateral executado não é venda paga.** Aqui existe um registro de consenso sobre índice de referência; NÃO existe:

- pagamento do destinatário; valor de compra debitado de família; conta bancária, empréstimo, crédito ou dinheiro novo;
- preço nominal calibrado ou poder de compra abstrato oficialmente ratificado;
- lucro/receita do produtor, crédito em tesouraria ou imposto; o benchmark de valores relativos não autoriza nenhum desses lançamentos;
- contrato jurídico executório verificável externamente, pois os IDs de aceite não são assinaturas criptográficas;
- transporte remoto, escala de transbordo ou consumo de alimento agregado legado.

A próxima etapa de **liquidação comercial efetiva** deverá ser desenhada apenas quando a unidade monetária abstrata e a política de receita forem ratificadas, sem criar caixa fictício para os indivíduos. A opção de acompanhar o índice sem pagamentos existe para avançar materialmente no jogo sem violar essa decisão canônica.

### Persistência e replay

- **V45:** aceite comercial com título e eventual reversão anterior **de outros lotes**, sem reserva de Tesouro.
- **V46:** mesmo evento com tesouraria dormente, sem crédito.
- `CanonicalPhysicalAction.CommercialTermsEvidence` é opcional, omitido dos eventos antigos; saves V9–V44 não sofrem qualquer alteração de serialização.
- `RestoreSaveJson` refaz o histórico até o instante exato da aceitação e **recalcula a curva marginal**, ordem dos lotes, quantidades, hashes, atribuição de título e existência dos dois aceites. Igualdade exata da ação reconstruída rejeita campos forjados. O callback externo não é reexecutado no replay: a evidência não reivindica autenticar o revisor no futuro.

### Testes e gates

`GaEconMarketDefault01qTests`: aceite autêntico com preço racional exato e saldo físico intacto; negativa de revisor e segundo contrato; IDs/curvas/emissores inválidos; consumo físico posterior e bloqueio de reversão silenciosa; adulteração de valor com SHA refeito; downgrade/upgrade de envelopes V41/V45 recusados; V46 com Tesouro dormente sem receita; coexistência de títulos revertidos antigos e termos de outro lote no mesmo save.

Gates: FAST no HEAD, regressão de kernel/hash multiplataforma e benchmark de frete em checkpoint. `skipped` jamais conta como aprovação. PR Draft, sem merge.
