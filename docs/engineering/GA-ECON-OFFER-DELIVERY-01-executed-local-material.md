# GA-ECON-OFFER-DELIVERY-01 — Uso material local realmente executado de uma oferta comprometida

**Data:** 2026-11-06. **Base:** PR #271 (checkpoint Linux, Windows e macOS Intel aprovado). **Status:** incremento de execução física, não liquidação econômica.

## Decisão canônica

Uma oferta reservada em `GA-ECON-OFFER-COMMIT-01` não pode ser considerada vendida sem destinatário e sem a entrega material. O primeiro passo executável **não promete comprador comercial fictício**: permite demonstrar a retirada integral de madeira/pedra da custódia exclusiva **MarketOffer** e a aplicação a uma finalidade material local já prevista pelo motor (`FinalPhysicalUse` ou `IntermediateInputUse`).

A quantidade total foi retirada de `State.Stocks` na abertura do compromisso. Na execução, não é debitada novamente. O comando `UseCommittedLocalMaterial(ordinal, kind)` retira os lotes exclusivos associados ao ordinal original e insere uma ação `use_committed_material` com o uso físico identificado, revisão e hashes canônicos. A execução não pode ser repetida ou liberada: o compromisso passa de `IsLive` a `Consumed`, jamais `Released`. Outros materiais não comprometidos continuam disponíveis em `material_use`.

**A decisão vale somente para o uso integral de madeira ou pedra no mesmo tile, onde existem trabalhadores.** Não admite alimentos, retenção deliberada, consumo nutricional, frete, venda a outro tile, nova construção ou novos recursos invisíveis. Não há cliente/beneficiário comerciante certificado, salário, imposto, preço, receita, fatura, carteira ou crédito.

## Contratos e evidência

- `CanonicalExecutedCommittedMaterialUse` preserva o ordinal da ação original e da ação executada, tile, mercadoria, quantidade, classificação e hashes antes/depois; atribuições de lotes FIFO reconstituíveis. Flags `ProvesMonetarySale`, `ProvesDeliveryToFinalEconomicBuyer`, `HasExecutedTransport` e `AuthorizesPurchasingPowerIndexN` são todas falsas.
- `CanonicalPhysicalStockCommitment` adiciona `Consumed=false` por compatibilidade. `IsLive = !Released && !Consumed`. O verificador reconcilia cada conclusão com exatamente uma ação `release_physical_stock` **ou** `use_committed_material`, nunca ambas.
- `CanonicalPhysicalLotLineageTracker.ConsumeCommittedMarketMaterial` consome exclusivamente lotes de custódia MarketCommitment, sem acesso a estoques livres ou ração de expedição.
- `VerifyPhysicalStockCommitments` reprova liberação/uso duplicado, recibos sem ação, material de origem incoerente, perda de conservação ou divergência de quantidade/identidade.

## Save / replay determinístico

O formato de envelope é **33** para save com uso material executado e **34** para o mesmo acompanhado de tesouro dormente. Formatos 9–32 continuam aceitos; saves 31/32 usam `Consumed=false` por omissão, preservando o mesmo JSON na ausência de novo uso. O replay reexecuta compromissos, liberação e uso material pela ordem real do journal; reconstrói a custódia com o mesmo hash e confere byte a byte o payload incluindo resumo. Forjar a flag `Consumed`, inventar quantidade ou rebaixar o envelope provoca falha segura.

## Diretrizes inalteradas

O modelo futuro continua `D=P*N; x=S/D; p=f(x); R_ref=p*D`, com N abstrato e curvas ainda não aprovadas. Nada aqui impõe limite `min(S,D)` ou usa saldos cash. A política de cadência por mobilidade permanece a direção final; `AdaptiveThirty` é compatibilidade. Seis alimentos físicos seguem alvo, sem chamar madeira ou pedra de alimentos.

## QA

FAST executa os testes novos de `GaEconOfferDelivery01Tests` + baseline do motor e compila Godot. O checkpoint multiplataforma anterior é evidência apenas do HEAD #271; o #272 permanece parcialmente validado até novo checkpoint ou `ga-ci-full` pré-merge.

## Próximo incremento

Autenticar beneficiário, local real de entrega e transferência física, com transporte e escalas que respeitem o custo; registrar venda/liquidação abstrata somente depois dos comprovantes de destinação. Alimentos precisam de contabilização nutricional de seis itens antes de absorção executada. Nenhum atalho pode classificar o novo uso material como uma venda.
