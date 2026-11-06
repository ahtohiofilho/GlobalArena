# GA-ECON-MARKET-DEFAULT-01N — registro canônico de titularidade econômica no jogo

**Data:** 06/11/2026.  
**Base:** PR #293 / GA-ECON-MARKET-DEFAULT-01M, HEAD `f630b16ea4068967705dba1913a762cb0c27ff13` (FAST aprovado).  
**Status:** comando explícito e versionado de laboratório, PR Draft sem merge, não ativado na modalidade jogável padrão.

## Problema e decisão

01J/01K registraram entrega física a um grupo real, uso e devolução. 01L atribuiu nomes de possíveis contrapartes, e 01M exigiu **duas permissões distintas, externamente revisadas**: poder dispor do lote e poder recebê-lo.

Essas camadas, sozinhas, não produzem titularidade econômica **reconhecida pela simulação**. 01N introduz a ação canônica `RegisterLocalSimulatedEconomicTitle(commitmentActionOrdinal, reviewedRights)`: um lote físico ainda sob `LocalRecipientCustody` ganha **registro de titularidade econômica em nome da instituição destinatária**, sem criar bens, dinheiro, transferência remota ou venda liquidada.

### Execução e conservação

1. O comando exige preflight 01M **atual e revalidado**, com ambas as permissões propostas aceitas pelo revisor externo no instante da execução. Sem revisão, com direito faltante/revogado, lote não transferido ou já encerrado: **falha sem mutação**.
2. O registro `CanonicalLocalSimulatedEconomicTitleEvidence` fixa compromisso, ordinal/hash do handoff, tile, commodity, volume, acordo/versão, instituições distintas e os **dois IDs de autorização revisados**.
3. A ação `register_local_economic_title` entra no journal canônico; incrementa revisão/hash do mundo e gera `CanonicalRegisteredLocalSimulatedEconomicTitle`. O título é identidade separada dos lotes, que **continuam na custódia física do destinatário**. Não existe segunda baixa do estoque; nenhuma renda é computada.
4. A conciliação `VerifyRegisteredLocalSimulatedEconomicTitles` exige correspondência exata entre eventos, recibos históricos, origem física, quantidade, hash da entrega e sequência temporal. Não admite duplicação de título por compromisso ou acordo.
5. Após o registro, **`ReturnLocalRecipientCustody` é bloqueado**: seria incoerente devolver o material à disponibilidade do antigo custodiante sem desfazer antes o título. O comando 01K `UseLocalRecipientMaterial` continua possível com grupo produtivo real e baixa física única; o título histórico permanece auditável após consumo.
6. O acesso `RegisteredLocalSimulatedEconomicTitles` expõe a titularidade reconhecida pelo motor para o lote, separada de `ExecutedLocalRecipientCustodyTransfers` e de qualquer caixa/tesouraria. Nenhum rótulo `ProvesExecutedSale` é promovido.

### Replay e versionamento

`CanonicalPhysicalAction.TitleEvidence` é um campo opcional `JsonIgnore(WhenWritingNull)`, presente **apenas na ação de registro**. Saves antigos sem title preservam a mesma representação byte a byte.

Novos envelopes:
- **V41** — registro de título econômico simulado sem tesouraria.
- **V42** — registro com tesouraria dormente preservada; nenhum crédito monetário.

O `RestoreSaveJson` aceita essas versões somente quando a ação de título existir. A reabertura reproduz a ação **a partir dos elementos congelados no journal**, verifica novamente os invariantes materiais e a identidade exata do recibo e exige igualdade bit a bit do payload. **Replay não chama novamente a autoridade externa**, por definição: o hash e o ID da revisão preservam o testemunho histórico, não oferecem uma assinatura jurídica revalidável. Por isso uma instalação que precise de autenticação jurídica externa deverá introduzir **provas assinadas + validação pública no carregamento** antes de usar o recibo como título jurídico.

### Limite essencial

**Título reconhecido pelo jogo ≠ propriedade legal fora da simulação.** Autorização 01M foi revisada por callback fornecido pelo host: o kernel não implementa autoridade legal permissiva. Nem o callback, nem um replay de ID de autorização constituem prova autônoma de assinatura, contrato de compra e venda, pagamento ou tributação.

**A transferência de título sem liquidação é apenas um ato de regras simuladas**; não contabiliza receita, não gera `N`, não cria carteiras, não cobra imposto, não regula preços e não emite dinheiro. Titulação real com exigência de pagamento imediato precisa de outro comando atômico que confira físico, direito, preço e origem econômica da remuneração.

### Restrições futuras

- Não mover de volta um lote titularizado ao estoque livre sem registrar antes **reversão formal de título**. O 01N bloqueia esse caminho; a reversão será outra ação versionada.
- O registro é único por lote; repartições/parciais exigem extensões explícitas do modelo, não divisão invisível.
- O fato de duas instituições terem identidade distinta não garante que existam duas pessoas ou entidades comerciais modernas; na fase neolítica elas são atores econômicos do jogo.
- Alimento agregado legado não participa desse mecanismo. Frete remoto exige transporte efetivamente executado antes de destino.
- O ciclo econômico segue a cadência canônica; o modelo de poder de compra permanece **abstrato e condicionado à produção real**, nunca carteira fictícia da população.
- O mapa tático segue interações centro-a-centro nas conexões do grafo, cruzando fronteiras entre tiles, não linhas desenhadas sobre suas fronteiras geométricas.

### QA e promoção

`GaEconMarketDefault01nTests`: registro após ambas as permissões, estoque e cobertura inalterados, sobrevivência ao replay, uso após registro, retorno bloqueado, recusa por revogação e segunda execução, saves V37 antigos intactos, rejeição de envelope adulterado e roundtrip V42 sem crédito no Tesouro.

**Gate:** FAST no HEAD, depois checkpoint multiplataforma de kernel + hash por se tratar de novo evento canônico e novos envelopes. Skipped não contam como aprovações. PR permanece Draft até decisão explícita de promoção; sem merge.
