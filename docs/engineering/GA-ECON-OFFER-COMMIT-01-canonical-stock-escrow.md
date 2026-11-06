# GA-ECON-OFFER-COMMIT-01 — custódia física canônica de ofertas e retenções

**Data:** 06/11/2026. **Base:** PR #270; **modelo de QA:** FAST incremental + checkpoint a cada 3–5 incrementos.

## Escopo implementado

O jogo registra **duas formas distintas de compromisso físico**: `MarketOffer` (estoque separado para futura comercialização) e `DeliberateRetention` (estoque deliberadamente preservado fora do mercado). Ambas são operações efetivas no estado canônico, não hipóteses numéricas. Uma nova ação `commit_physical_stock` reduz estoque local livre e transfere seus lotes físicos para custódia exclusiva; `release_physical_stock` devolve a mesma quantidade e os mesmos lotes ao tile original. Não há duplicação, consumo, pagamento, preço, imposto, crédito, bônus de produtividade ou movimentação fictícia.

- `CanonicalPhysicalTacticalSession.CommitPhysicalStock(tile, commodity, kind, raw)`: exige estoque existente, quantidade positiva, habitantes no tile e validação integral de custódia. ID do compromisso = ordinal da ação canônica.
- `ReleasePhysicalStockCommitment(ordinal)`: liberação integral e única, sem reemitir mercadorias; o registro original continua no journal como histórico.
- `CanonicalPhysicalStockCommitmentWitness`: snapshot do mesmo hash e journal, quantidade aberta em oferta/retencão; não é recibo de venda.
- `CanonicalPhysicalLotLineageTracker`: custódias novas `MarketCommitment` e `DeliberateRetention`, preservando FIFO/vintage; lotes da oferta não podem ser consumidos nem usados em instalações ou viagens enquanto bloqueados.
- `VerifyPhysicalStockCommitments()`: reconcilia lotes vinculados com exatamente um compromisso canônico ativo e rejeita liberação duplicada, registro órfão ou quantidade não conservada.

## Save e determinismo

Novos formatos de envelope **31 (sem tesouro) e 32 (com tesouro)**, selecionados somente se o journal contém ações reais de compromisso. Ações `commit_physical_stock`/`release_physical_stock` carregam identidade de mercadoria, quantidade, tipo e ordinal; todos os novos campos de ação são opcionais, omitidos quando não utilizados. O payload de compromisso inclui um resumo auditável, mas o replay reconstrói a autoridade exclusivamente das ações e do estado físico. Checagens de SHA256, hashes por ação e igualdade byte a byte entre resumo persistido e reconstruído impedem inventar reservas. O hash `WorldState` muda **legitimamente**, já que o estoque físico livre é debitado ou devolvido.

Saves anteriores **9–30 continuam suportados** e preservam bytes/hash históricos. O novo formato também aceita, sob gates explícitos, outras funcionalidades que precedam a primeira reserva (material_use, assemble_primitive, instalações, tesouro). Nada é migrado silenciosamente.

## Limite explícito do produto

O compromisso de oferta é **estoque físico reservado ao mercado**, não entrega, venda, transporte ou absorção já concluída. A prova de `ActualMarketOfferCommitment` não é prova de `ExecutedSale`. A política aprovada continua `D=P*N`, `x=S/D`, `p=f(x)`, `R_ref=p*D`; nesta fase `N` e a curva de preços **não** são usados para movimentar mercadorias. A guarda contra saldo fictício permanece. A política cronológica `AdaptiveThirty` sobrevive apenas como compatibilidade; a diretriz-alvo de cadência vinculada à mobilidade segue congelada para desenho/versionamento.

## QA

`GaEconOfferCommit01Tests`: trava física de madeira; devolução e segunda liberação negada; retenção real de pedra; duas propostas sobre o mesmo estoque rejeitadas; custódias exclusivas; reserva alimentar; replay de commit/liberação e formatos antigos; persistência entre ciclos; coexistência com modo cash sem alterar contas; rejeição de payload adulterado até com checksum recalculado; parâmetros inválidos preservam hash e save.

**Próximo incremento:** registrar destinação e liberação comercial executada no journal de forma autêntica, com logística, consumo final ou retenção pós-liquidação, preços sem teto por carteiras e receita institucional apenas mediante titularidade/autoridade. Ainda não existe autorização para zerar todo estoque ofertado e declarar 'vendido' sem um destino material real.
