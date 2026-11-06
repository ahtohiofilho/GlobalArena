# GA-ECON-MARKET-DEFAULT-01D — Formação automática de ofertas físicas, em lote isolado

**Data:** 06/11/2026.  
**Base:** PR #283 (GA-ECON-MARKET-DEFAULT-01C), após FAST aprovado; hierarquia GA-CANON-DIRECTIVES-01.  
**Estado:** executor opt-in *staged* de custódia; não integrado ao turno do Godot e não equivale à execução de vendas. PR Draft sem merge.

## Entrega e ganho operacional

O novo `CanonicalStagedAutomaticMarketOfferBatch.Prepare(source)` elimina ordens **individualizadas** para colocar cada mercadoria física livre em compromisso de mercado. O módulo:

1. Exige ciclo econômico já encerrado no turno atual. Não executa mercado em turno anterior à produção ou com fechamento desatualizado.
2. Exporta o snapshot canônico e restaura em **sessão isolada**; compara hash e save byte a byte. A origem de jogo do usuário não é alterada.
3. A partir de `CanonicalDefaultMarketPoolPreflight`, percorre todos os tiles e mercadorias `1/2/3` com saldos livres positivos, em ordenação determinística. Só prossegue em tiles com pessoas residentes de verdade.
4. Para cada saldo livre, executa `CommitPhysicalStock(... MarketOffer, raw)` **de verdade** no clone. Cada operação retira a quantidade do saldo físico livre e registra custódia exclusiva/atribuição FIFO, ação no journal e hash versionado V31/V32. Ofertas anteriormente comprometidas não são comprometidas outra vez. `DeliberateRetention` permanece fora da oferta.
5. Concilia `livre + oferta + retenção` antes/depois em `BigInteger`; assegura que todo saldo anteriormente livre virou custódia `MarketOffer`, sem aumentar nem diminuir quantidade material.
6. Exporta e restaura o clone resultante, verificando bit-exatidão de save/hash. Falhas intermediárias só atingem clone descartável: não há meio-lote reservado no jogo fonte.
7. Devolve `PreparedSession` e a lista de novas ofertas; o chamador ainda **não** está autorizado a substituir automaticamente a sessão viva. A troca/ativação requer milestone próprio de integração e identidade de regra de jogo.

Esta entrega é avanço **executado** de estado físico, não um terceiro relatório read-only. Porém ela executa apenas **formação de oferta**, não destinação comercial. A receita marginal e a absorção da venda permanecem fora do escopo. O módulo não deve ser invocado automaticamente no ciclo atual do Godot sem política de ciclo, transporte, recebimento e compatibilidade de saves aprovada.

## Gates de segurança

`GaEconMarketDefault01dTests`: todos os bens livres viram oferta no clone; origem intacta; soma original/retenções/oferta conservada; ofertas explícitas não duplicadas; retenção parcial preservada; segunda execução idempotente; release de retenção reabre somente quantidade legítima; recusa sem ciclo corrente; journal/recibos e replay byte a byte; nenhuma execução fictícia de venda, dinheiro, imposto, N, nutrição ou frete.

As três mercadorias continuam sendo 1 alimento agregado legado, 2 pedra e 3 madeira. `CommodityId(1)` oferecido NÃO vira ingestão nutricional nem seis variedades; `FoodDemandRawPerWorker` legado não é reintroduzido como objetivo de nutrição.

## Próxima promoção

GA-ECON-MARKET-DEFAULT-01E deverá formalizar a **adoção versionada/atômica** dos resultados do batch após cada fechamento econômico real, com journal e eventos autenticados e sem alterar replays anteriores. Depois disso, avançar na execução de destinação e transação econômica para todo volume elegível, com acesso/frete/consumo real e curva de receita marginal. Nenhum estoque ofertado é dado como vendido só por ter sido bloqueado em custódia.

**Critério decisivo:** não declarar a regra de «toda produção será vendida exceto retida» cumprida até existir fechamento de absorção com execução e destino rastreáveis. Não confundir remover a necessidade de clicar em `RESERVAR OFERTA` com completar venda.
