# GA-VS61 — Oferta e demanda primitivas derivadas da sessão física

Base VS60 PR #167, commit `9dd3c13af34c44aab0ee038af22c684fc905543f`.
PR Draft empilhado; nenhum merge na main e nenhuma autorização de SETTLE real.

## Entrega

`CanonicalPhysicalTacticalSession.PreviewLivePrimitiveTrade(scenario)` reconstrói,
**em cada chamada**, a oferta produtiva a partir das alocações residentes
canônicas e de `PrimitiveYieldAt(tile, activity)`, a mesma produtividade
usada na execução física do ciclo econômico. Agricultura produz a mercadoria
alimentar do mapeamento explícito, floresta produz madeira e mineração produz
pedra. Rate zero / ausência de trabalhador significa **nenhuma linha de oferta**.
Não se inventa estoque nem construção.

A demanda é a multiplicação exata e com overflow checado entre moradores
atuais do tile e as taxas por morador declaradas no cenário (para cada item,
inclusive madeira/pedra caso configurados). Residentes e trabalhadores são a
**mesma população**; não existe categoria paralela. Um trabalhador em trânsito
não produz e não aumenta a demanda residencial do destino antes de chegar.

O host informa identidades econômicas associadas a tiles, política de poder de
compra abstrato/H, taxas base de consumo, catálogo/mapeamento de itens, preços,
serviços e rotas já construídos. **Não informa quantidades prontas de oferta e
demanda nem recibos autoritativos.** A rota `CanonicalPrimitiveTradeLane` leva
somente identidades e opções de serviço: as linhas econômicas são religadas
aos valores atuais antes da seleção.

A consulta executa integralmente o pipeline existente VS52 -> VS57 -> VS58 ->
VS59 -> VS60 e retorna `CanonicalLivePrimitiveTradePreview` com `Flows`
e o `Trade` auditado. `RequireCurrent(session)` rejeita resultados
antigos ou pertencentes a outras sessões. Mudança de atividade, migração,
chegada, alterações de rede/direitos e restauração exigem nova consulta.
Todas as coleções de cenário são copiadas, sem depender de enumeráveis mutáveis.

## Contratos e limites

- Oferta reflete **capacidade primitiva instantânea por ciclo**, não o
  estoque disponível, o saldo físico vendido nem a produção já liquidada.
- A modelagem cobre **apenas os locais e as linhas de frete explicitamente
  vinculados**. Não descobre mercados, não funda cidades, não reconstrói
  automaticamente ofertas de serviço, não recalibra políticas/preços por conta
  própria e não produz a curva final de preços do jogo.
- Local produtor ou comprador atualmente desabitado, ou rota cujo item
  deixou de ser produzido/demandado, **falha fechado**. Não é permitido
  reutilizar o valor antigo como fallback.
- Os limites de busca e desempate permanecem exatamente os da VS58/VS59.
  A política de poder de compra numérica é um suporte de cálculo para o
  **poder de compra abstrato**, com reserva invisível H protegida; não se
  materializam carteiras, renda salarial, banco, crédito ou obrigação.
- Somente leitura: não muda save, turnos, trabalhadores, estoque, dinheiro,
  contas, direitos institucionais, reservas ou fretes. `CanAuthorizeRealSettlement`
  permanece falso de forma incondicional.

## QA e benchmark

`GaVs61LivePrimitiveTradeTests` cobre derivação pela produtividade física,
realocação de atividade, trânsito/chegada, oferta esgotada (sem volume
fantasma), reuso de cenário sem quantidades envelhecidas, cópia das cotações,
ausência de rede, fechamento de via, restauração e invariância do save.

`--vs61-live-primitive` mede, em Release, 8/32/128 consultas completas
com cenário físico e direito institucional fixos. **Não representa 128
povoamentos** e não usa esse número como escala de população. No gate Linux:
regressões dirigidas VS40–VS61 + benchmark + Godot Release; no Windows:
testes integrais + benchmark + Godot Debug.

## Próximo incremento

VS62: introduzir descoberta/binding incremental de povoamentos e mercados
a partir das atividades observadas, sem depender de lista manual de sites.
Manter a separação entre a economia *derivada* e a decisão institucional
auditada de transporte. Avaliar desempenho em mapas maiores antes de
desbloquear liquidação real.
