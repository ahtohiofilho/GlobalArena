# GA-Next R161 — ganho marginal de um trabalhador novo

## Contexto e objetivo

A R160 mediu a contribuição de uma atividade existente mantendo a pessoa no censo e sua demanda de consumo. **A R161 mede uma situação distinta:** a adição efetiva de um indivíduo ao censo experimental, cuja produção física e consumo econômico entram simultaneamente no mercado.

Esta implementação faz a ponte com o crescimento populacional proposto, mas não decide onde os nascimentos ocorrerão nem modifica uma partida.

## Experimento autoritativo apenas como referência

1. Usar um único mundo físico e estado canônico, com mercados reais, trabalhadores e infraestrutura natural autorizada.
2. Receber uma lista pequena e explícita de **arquétipos produtivos existentes**, cada um com localização física, civilização, mercadoria, rendimento micro, ocupação e multiplicador de consumo.
3. Resolver uma vez o mercado basal com frete físico do ciclo e preços universais.
4. Para cada alternativa, **copiar o perfil do arquétipo para um novo ID** reservado somente ao cenário contrafactual; todos os trabalhadores anteriores permanecem idênticos e o número de trabalhadores cresce exatamente em um.
5. Verificar a produção nova `deltaGoods=outputMicroPerTurn` **e** a demanda positiva adicional em todas as mercadorias do catálogo explícito. O mesmo novo indivíduo gera ambas.
6. Resolver um mercado completo por candidato, a partir da mesma base e com demanda atualizada. O saldo agregado é `cash_with_new_worker - cash_original`, onde `cash = grossSales - physicalFreight`. Não é receita individual da cidade nem benefício privado de um tesouro.
7. Enviar essas leituras ao indexador R158 como `NewWorkerAddition`, com `ExistingWorkerId=null` e `SourceTile=null`.

A avaliação custa estruturalmente **1+n soluções de mercado** para `n` alternativas. O tempo é registrado no workflow próprio, sem extrapolar a escala de um mundo pequeno.

## Limites do contrato

- Na R161, a proposta é apenas para **produções comprovadamente existentes na mesma localização física de um trabalhador produtivo real**. O novo indivíduo herda o perfil explícito da economia já implementada. Não há previsão de produtividade hipotética de um recurso ainda não modelado naquele nó, nem enumeração de todas as profissões e cidades.
- O mesmo ID novo pode representar alternativas **mutuamente exclusivas**; não existe adição simultânea de uma pessoa por alternativa.
- Sem mecanismo autoritativo de nascimento, distribuição de crescimento, fundação de cidades, portos operados por recém-nascidos ou migração. Para opções logísticas, será necessário distinguir o ganho no frete do efeito de demanda de uma pessoa nova, sem retirar produção de terceiros.
- Sem ajustes na fórmula de preços, transporte, pedágios, impostos, consumo, construção, turnos, hashes ou UI. Não há ordens emitidas nem mutações duráveis.
- O crescimento acordado (um trabalhador novo por civilização sobrevivente no total mundial a cada turno, redistribuído entre cidades proporcionalmente à população) permanece um **contrato posterior**, não uma feature disfarçada da R161.

## Critérios de aceite

O teste integrado demonstra que, ao adicionar um novo produtor em mundo real, a contagem cresce de `N` para `N+1`, oferta aumenta pela capacidade física explicitada, demanda total cresce pelas intensidades das mercadorias, a identidade canônica do baseline é preservada e a classificação é exclusivamente `NewWorkerAddition`. O teto de avaliações e candidatos distintos é obrigatório. A saída JSON disponibiliza valores monetários escalados exatos, demanda e produção em micro-unidades e tempo observado.
