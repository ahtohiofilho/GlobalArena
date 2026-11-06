# GA-ECON-MARKET-DEFAULT-01I — execução atômica de múltiplos usos materiais locais

**Data:** 06/11/2026.  
**Base:** GA-ECON-MARKET-DEFAULT-01H / PR #288 (FAST success no HEAD 52107dd43b7514075476505c8523b077cff30614).  
**Status:** implementação CANÔNICA **experimental/opt-in** para registrar vários usos físicos de madeira/pedra por grupos produtivos comprovados, sem alteração de regras antigas, formato do save ou vendas comerciais. PR Draft sem merge.

## Problema resolvido

A etapa 01C já executava uma destinação por vez, sempre com recibo real de `use_committed_material_for_workers`. A etapa 01H passou a reconciliar as ofertas pendentes, os usos executados e as retenções, mas não havia um boundary de execução em lote com **atomicidade diante de erro**. A execução sequencial direta poderia comprometer a primeira oferta e depois falhar no segundo destino.

## Entrega

`CanonicalStagedLocalMaterialUseBatch.Prepare(source, instructions)` recebe ordens **explícitas** `CanonicalLocalWorkforceMaterialUseOrder(commitmentOrdinal, beneficiaryActivity, useKind)`. Cada ordem deve referenciar exatamente **uma** reserva `MarketOffer` viva e existente no journal, de madeira (3) ou pedra (2), na íntegra, com atividade e trabalhadores reais no mesmo tile. **NÃO** há seleção automática de quantidades consumidas, destinatários sem evidência ou unidades artificiais.

A operação:

1. Valida todo o lote antes de qualquer ação (ids válidos, sem duplicação de compromisso, atividades reais, tipos físico-final/intermediário, mercadoria tipada, custódia ativa, grupo de destino).
2. Serializa a sessão original e restaura clone bit-idêntico. **Nunca** executa mutações na sessão fonte.
3. Ordena as operações por ordinal, reconstitui um `CanonicalLocalMarketDestinationPreflight` **novo** antes de cada uso e invoca o executor 01C, que cria a ação canônica existente `use_committed_material_for_workers` e o recibo `CanonicalExecutedCommittedMaterialUse` com atribuição FIFO real.
4. Concilia por `CanonicalMarketExecutedPhysicalCoverage` (01H) as transições físicas exatas `pendente_before - utilizado = pendente_after`, `uso_after = uso_before + utilizado`, `elegível_after = elegível_before - utilizado`, com estoque livre e retenção deliberada inalterados.
5. Executa exportação e restauração do **save inteiro** para validar hash, eventos e recibos. Se qualquer etapa falhar, a sessão fonte segue intocada e o clone parcial é descartado.
6. Devolve `PreparedSession`, recibos e auditoria física; não altera automaticamente o jogo. A integração `CanonicalAutomaticMarketOfferTimeline.ApplyExplicitLocalMaterialUses` permite que o laboratório opt-in 01E adote atomicamente o resultado, com clone final adicional, sem permitir que um consumidor altere a sessão interna ao editar o retorno.
7. Preserva identidades e ações V31–V34 e não introduz novo tipo de evento, schema do save nem obrigação de retirar material automaticamente.

## Limites inegociáveis

- A operação é **uso físico atestado por trabalho**, e **não** venda comercial: não há prova de titularidade, contrato de compra, transferência monetária, preço calibrado, tributação ou comprador jurídico.
- A finalidade consumidora deve ser **explicitamente especificada**. Não converter todo o estoque de pedra/madeira em consumo indiscriminado só para parecer absorção integral.
- Ofertas sem destino indicado permanecem intactas e pendentes. A operação não inventa demanda/capacidade de uso, comprador remoto, transporte ou entrega.
- Alimento legado (commodity 1) é proibido nesse executor. Não criar ingestão fictícia, nem fatiar alimento agregado em seis alimentos.
- O laboratório existente no Godot não recebe controle novo; a API fica na linha de tempo experimental para montagem futura de regras de produção, construção e consumo autenticamente sustentadas.
- `TotalUsedRaw` é **volume físico executado**, e não receita, venda, imposto, aumento do produto acabado ou `N`.

## Homologação

`GaEconMarketDefault01iTests`: dois lotes em ordem arbitrária, recibos reprodutíveis, grupos produtivos reais, retenção e oferta não selecionadas, rejeição de duplicatas/compromisso fictício/retido/alimento/atividade inválida, preservação da origem, replay bit-idêntico, checkpoint da timeline após dois usos, isolamento do resultado retornado e falha fechada sem adoção parcial.

FAST no HEAD. Outros gates de compilação/execução e validação visual serão exigidos antes de qualquer promoção para o sistema jogável.

## Próximo objetivo comercial

A evolução do mercado exige introduzir **destinatários econômicos autênticos e saldo físico de titularidade/custódia** no destino, comprovando transporte quando remoto; depois, recibos distintos de transferência, liquidação e receita indexada ao preço marginal devidamente calibrado. Uma execução de uso produtivo **não pode** ser renomeada como transação comercial, tampouco um `MarketOffer` é venda automática. Continuidade com a nutrição de seis alimentos reais continua obrigatória.
