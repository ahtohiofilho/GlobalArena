# GA-VS13 — Primeiro gate de cotação alimentar local (não liquidante)

## Por que agora
Após validação manual do VS12: trabalho, terreno físico 01C, estoque, ciclos adaptativos e save V11 funcionam, mas o consumo direto de alimento ainda não constitui compra com preço e orçamento. O próximo marco do roadmap é **fechar o turno econômico**. Antes de publicar transferências monetárias, precisamos expor e testar o preço e a oferta física da mesma malha.

## Incremento
- Resolver uma cotação local, para um só alimento, de um único tile com trabalhadores residentes. A oferta é o **estoque de alimento já existente + a colheita projetada para o próximo ciclo**, calculada pelas próprias alocações físicas e pela geografia ativa (V1 legado ou 01C).
- Reutilizar EconomicSingleVarietyOriginClearing / GA-PROP-001, com frete = 0 **apenas porque origem e destino são o mesmo tile**. Nenhuma conexão entre tiles é inferida.
- População compradora é o total dos trabalhadores naquele tile, sem duplicação.
- Orçamento de referência por trabalhador é fornecido explicitamente pelo usuário no laboratório. Valor padrão ilustrativo = 40 créditos, **não é renda canônica nem emissão de dinheiro**. Os parâmetros da preferência (sigma=1, nu=0, lambda=0) são uma hipótese de cotação alimentar monovariedade, não regra definitiva.
- Mostrar no Godot o preço endógeno, oferta local projetada, vendas hipotéticas, possível sobra, gasto e resíduo monetário H. Nunca tratar esses resultados de referência em double como centavos contábeis nem como fato liquidado.
- O botão **COTAR MERCADO ALIMENTAR LOCAL (SEM LIQUIDAR)** não modifica WorldState, revision, estoque, saves, eventos ou políticas; mudanças de estado invalidam a cotação anterior.

## Gate
1. A mesma seed/estado/orçamento produz a mesma cotação e o mesmo hash.
2. A oferta cotada vem somente do tile e da produção local, não de transporte implícito.
3. Nenhuma compra acima de oferta, nenhum orçamento excedido, resíduo H estritamente positivo quando há oferta.
4. Com excesso de oferta, preço deve poder cair sem crédito ou renda fabricada.
5. Em tile sem alimento/oferta, cotação informa nenhuma venda/receita, mesmo havendo demanda.
6. Save/replay V11, V10, V9 e simulação física anterior permanecem intactos.
7. Testes automatizados Linux/Windows e build Godot; inspeção visual posterior.

## Fronteira de promoção — não declarar mercado efetivo antes desta etapa
Ainda faltam contrato de **orçamento canônico real**, titulares da mercadoria, titular do pagamento, persistência monetária, unidade e arredondamento auditáveis, liquidação atômica à vista, preço e estoque registrados no snapshot, e replay versionado. Não converter números hipotéticos do solver (double) diretamente em saldo. No próximo gate, integrar essas responsabilidades por transação canônica sem simular bancos, crédito ou renda invisível.

## Proposta congelada
A hipótese de **demanda basal universal + demanda industrial + reservas** continua congelada na issue #103. Não é requisito para testar mercado **alimentar** e não foi ativada. Nem a existência de urânio, nem o estoque de madeira/pedra deve forçar implementação precoce.

Sem merge na main; esta PR é empilhada sobre o VS12/PR #102. Sem alteração de MKT-01.
