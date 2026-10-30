# GA-VS14 — Liquidação alimentar à vista por tile físico (primeiro contrato canônico)

Data: 30/10/2026. Base: GA-VS13B/PR #108. Incremento isolado, sem merge na main.

## Objetivo
Transformar uma cotação alimentar que não modificava o estado em **compra e consumo físicos reais**, pagos à vista com dinheiro existente, sem inventar salários, crédito, bancos ou mercadorias.

## Modelo explícito e provisório
- Novo modo opt-in LocalCashSettlement. Iniciar no laboratório VS14 habilita a variante **save V12, hash físico 12**. V9/V10/V11 mantêm exatamente sua regra antiga de consumo direto.
- Dotação inicial fixa **40 microcréditos de escala monetária por trabalhador, equivalente a 40 créditos por trabalhador**; a unidade interna é 1/1.000.000 de crédito. É capital de teste **uma vez só**, não receita periódica ou emissão em cada turno. Oito trabalhadores iniciam com total 320 créditos.
- Para cada tile, um livro de caixa registra duas contas distintas: **famílias consumidoras** e **produtores locais**. Compradores pagam produtores no próprio tile. A soma de todas as contas é idêntica à dotação inicial.
- Em cada ciclo econômico, primeiro há produção física. Depois as famílias apresentam orçamento efetivo disponível. O resolvedor endógeno GA-PROP-001 determina preço e quantidade pretendida; o contrato reduz a quantidade ao alimento disponível e à necessidade alimentar, e arredonda para unidades inteiras de precisão 1e-6.
- O pagamento é calculado como inteiro da quantidade × preço endógeno quantizado (arredondamento bancário do preço; piso do total). Antes de publicar, exige saldo suficiente. **O mesmo WorldState** recebe as novas contas, o recibo local e a baixa física do alimento consumido.
- Só se consome alimento efetivamente comprado/transferido. O alimento não comprado permanece como estoque físico local; não há estoque invisível nem transporte entre tiles.
- Um trabalhador movido no laboratório leva uma fração da carteira familiar existente, proporcional ao seu grupo. A receita do produtor já acumulada na origem permanece com os produtores dali. O ato de mover não cria dinheiro.
- Não existe circulação salarial automaticamente inventada: sem entradas posteriores, o dinheiro familiar eventualmente se esgota. Essa limitação é **intencional neste primeiro contrato**, permitindo testar a liquidação à vista antes de integrar salários, Estado e fluxos produtivos. O orçamento hipotético do VS13 continua sendo uma ferramenta separada e **não constitui caixa real**.
- O preço e a quantidade nas notas de liquidação utilizam unidades com escala fixa; se o preço de mercado for zero, pode ocorrer distribuição de comida local a preço zero, sem inventar receitas. Isso permanece cenário a revisar no futuro.

## Compatibilidade e auditoria
- Estado monetário, contas e recibos são parte da mesma estrutura física imutável; o hash V12 inclui tudo, além dos dados do hash V11. Cada turno gera exatamente uma revisão; replay verifica hashes antes/depois e resumo reproduzido.
- Save V12 exclusivo: user://ga-vs14-cash-food.json. Ao carregar: V12 → V11 → V10 → V9 → VS07; nunca fazer fallback silencioso se o mais recente está corrompido.
- O construtor do ledger rejeita contas duplicadas, saldo negativo/estourado, soma diferente do capital inicial e recibos inválidos/desordenados.
- Regra de mercado V12 não é aplicada retroativamente aos saves V9–V11. Não existe migração financeira silenciosa.

## Limites
Este primeiro passo testa e valida **liquidação local de um alimento** com comprador agregado por tile. Não é ainda economia operacional completa: salários, propriedade privada por trabalhador, orçamento da indústria, impostos, mercado multicommodity, transporte pago, contratos simultâneos, reservas e reclearing estão em outros gates. O modelo de saldo inicial também não representa ainda uma circulação monetária estacionária. Em particular, não promover como solução final de distribuição de renda.

Demanda basal universal segue congelada na issue #103. Movimento individual com duração real e consumo em trânsito segue issue #105. Redesenho de produção UI segue #107.

## Testes
- V12 recém-iniciado: oito trabalhadores, 320 créditos exatos, contas zero-sum e hash 12.
- Primeiro ciclo: baixa do alimento comprado + débito comprador e crédito vendedor na **mesma revisão**.
- Movimento: partilha do dinheiro já existente, conservação dos oito trabalhadores, nenhuma venda em tile sem produção alimentar.
- V12 replay após vários ciclos e movimento mantém hash, registro de compra, dinheiro, estoques e bytes de save.
- Rejeitar save cuja contabilidade foi falsificada, mesmo com checksum SHA256 recomputado.
- Regressão de save V11 exato, testes VS10–VS13, Windows/Linux, compilação Godot e gate visual no Windows Intel UHD.
