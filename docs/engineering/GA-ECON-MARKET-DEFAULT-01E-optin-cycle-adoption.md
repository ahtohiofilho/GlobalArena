# GA-ECON-MARKET-DEFAULT-01E — linha do tempo opt-in com adoção atômica após o fechamento

**Data:** 06/11/2026.  
**Base:** GA-ECON-MARKET-DEFAULT-01D, PR #284; GA-CANON-DIRECTIVES-01.  
**Status:** laboratório de regras **explicitamente opt-in**, pronto para ciclos em `CanonicalAutomaticMarketOfferTimeline`, **não** vinculado ao Godot/jogador e **não** responsável pelo clearing comercial. PR Draft, sem merge.

## 1. Contrato de produto preservado

Toda mercadoria fisicamente produzida e não deliberadamente retida deve entrar automaticamente em oferta; nenhuma ação individual do jogador é necessária para ofertar alimento/madeira/pedra. Contudo, `MarketOffer` é custódia de oferta, não venda: produto não deve desaparecer, ser considerado ingerido ou gerar receita se não tiver destino real. `D=P*N` é pressão de mercado abstrata, não quota física, e a qualidade alimentar deve resultar de consumo real e diversidade com função não linear, jamais de `FoodDemandRawPerWorker`.

A versão legada permanece disponível e sua interpretação **não** muda. O laboratório 01E não promete ter corrigido nutrição ou absorção.

## 2. Decisão de engenharia: adoção versionada fora do motor legado

`CanonicalAutomaticMarketOfferTimeline` é a camada de orquestração experimental que **é dona** de uma sessão canônica isolada; seu ponteiro de estado muda apenas após o cálculo completo e replay verificado do turno. O opt-in é feito apenas no turno zero (`StartOptIn`) e não pode ser inferido de um save V9–V34 preexistente.

Quando `AdvanceMovementTurn` é chamado:
1. cria-se clone byte-idêntico da sessão atual, preservando hash e save;
2. aplica-se **um** turno de movimento ao clone;
3. se **não** houve fechamento, publica-se o clone validado sem criar oferta;
4. se houve fechamento real, chama-se `CanonicalStagedAutomaticMarketOfferBatch.Prepare(candidate)` para registrar, via compromissos físicos reais no journal, todos os saldos locais livres elegíveis do ciclo — sem duplicar ofertas nem tocar retenções;
5. o snapshot completo é novamente restaurado e comparado byte a byte; **só então** se adota o novo estado, como uma operação atômica do ponto de vista do wrapper;
6. se alguma etapa lança exceção, a sessão publicada permanece intacta. Nenhum commit parcial atravessa o boundary.

A visão externa `CaptureCanonicalSnapshot()` devolve **cópia destacada**, nunca a sessão mutável interna. A execução não cria dinheiro, comprador, transporte, propriedade fictícia, recibo de receita ou evolução nutricional.

## 3. Identidade de regra e persistência

O checkpoint **experimental** é `CanonicalAutomaticMarketOfferCheckpoint` com `RulesetVersion=1`, `RulesetId=GA-ECON-MARKET-DEFAULT-01E-CUSTODY-V1`, save canônico integral, hash de mundo e SHA-256 dos bytes do save. O SHA-256 garante *consistência contra alteração acidental*; não se afirma assinatura criptográfica ou prova de autoria. O restore exige identidade/versão exatas, integridade do save e o replay canônico. Saves legados **não** podem ser importados silenciosamente como checkpoint novo. Na futura promoção para produto, o identificador precisará ser incorporado ao envelope canônico oficial com regras de migração/homologação próprias.

A fonte passada a `StartOptIn` também não é modificada: o novo wrapper adota seu clone. `IsExplicitOptIn=true` significa apenas que houve opt-in neste laboratório. `ExecutesAutomaticSales=false` e `ReplacesLegacyFoodConsumption=false` continuam explícitos.

## 4. QA

`GaEconMarketDefault01eTests` testa fronteira antes/depois do primeiro fechamento, zero compromissos fora do ciclo, aprovação do lote e integridade dos materiais, múltiplos ciclos, checkpoint e replay idênticos em cada turno, garantia de fonte preservada, rejeição de save legado sem versão e recusas de envelope adulterado, snapshot externo isolado, estabilidade de hash entre linhas do tempo idênticas e inexistência de venda ou receita artificial.

**Gates:** FAST no HEAD, status de mergeabilidade e validação adicional de hash/Windows/Godot quando for promovido ao runtime real. Este PR não altera `Main.cs`, não habilita UI e não promove a identidade experimental a partida de produção.

## 5. Próximos marcos bloqueantes

- **01F:** validar adoção do opt-in no ciclo real do Godot e persistência de identidade no formato canônico oficial, com migração explícita e rollback seguro; não ativar por padrão antes de resolver a absorção física.
- **Clearings comerciais:** destino físico para a totalidade do volume ofertado, serviços de transporte realmente executados quando necessários, curva de receita marginal decrescente calibrada e titularidade fiscal real; sem `min(S,D)`, venda presumida ou aniquilação de bens.
- **Nutrição:** seis alimentos reais tipados, ingestão consumada, `Q=A(f)·V(...)` calibrada e efeito defasado. Nenhuma fração fictícia do alimento legado.

**Gate de produto:** formação automática de ofertas ≠ execução automática de vendas. A convergência não está concluída sem o clearing rastreável e a nutrição real.
