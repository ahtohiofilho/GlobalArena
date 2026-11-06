# GA-ECON-MARKET-DEFAULT-01L — identidade econômica explícita vinculada à custódia real

**Data:** 06/11/2026  
**Base:** PR #291 / GA-ECON-MARKET-DEFAULT-01K, HEAD `12c4429b1771b836f998672e31539bdea277603f`, com FAST, benchmark, kernel e hash aprovados.  
**Status:** preflight somente leitura, laboratório opt-in, PR Draft. Nenhum merge.

## Problema e decisão

As entregas físicas locais dos PRs #290/#291 identificam **tile, atividade, quantidade e trabalhadores reais**. Isso não identifica automaticamente uma instituição proprietária nem um comprador com título econômico. Fazer `workgroup == buyer` implicaria inventar a identidade patrimonial a partir do mundo físico.

O novo `CanonicalLocalEconomicRecipientIdentityPreflight.FromCurrent(session, declarations)` é a fronteira **entre custódia física realmente executada e contraparte institucional expressamente declarada**. Ele aceita intenções de acordo por lote, mas só reconhece a existência física do handoff, não a validade legal de um contrato.

## Modelo de atribuição

`CanonicalLocalEconomicCounterpartyDeclaration` exige:
- `AgreementId` único e `AgreementVersion>0`;
- ordinal da ação real de transferência e `PhysicalTransferAfterWorldHashHex` do evento 01J;
- tile, commodity (madeira/pedra), quantidade, atividade e número de trabalhadores da transferência, **todos comparados aos recibos autênticos**;
- `DeclaredSourceInstitution` e `DeclaredReceivingInstitution` **explicitamente fornecidos**, com identidade textual estável e espécie institucional tipada do catálogo existente: PublicAuthority, Community, Cooperative ou CommercialOperator;
- duas instituições distintas, sem identidade institucional vazia, tipo Unspecified, colisão de `AgreementId`, lote declarado duas vezes ou mesmo `InstitutionId` com tipos divergentes em um conjunto.

Nenhuma instituição é inferida de geografia, modo econômico ou quantidade de trabalhadores. Uma transação entre a mesma instituição e ela própria não é rotulada como venda por meio dessa proposta.

Cada `CanonicalLocalEconomicRecipientAttribution` expõe o ordinal original do compromisso, o evento físico de entrega, os participantes declarados e, se houver, o evento 01K de **devolução ou consumo físico**. O status histórico da entrega permanece auditável depois do encerramento físico. No caso de declaração parcial, `UnattributedPhysicalTransfersCount` permanece positivo; ninguém é adicionado artificialmente aos demais recebimentos.

## Segurança e reconciliação

- O revisor reconstrói o `CanonicalMarketExecutedPhysicalCoverage` antes de atribuir identidades e verifica novamente os recibos `CanonicalExecutedLocalRecipientCustodyTransfer` e `CanonicalExecutedLocalRecipientCustodyDisposition`.
- A origem do preflight é fixada por sessão, hash e contagem de eventos; mutações do jogo tornam a fotografia obsoleta. `VerifyIndependent` recompõe a atribuição sem confiar na instância de resultado.
- Quantidades atribuídas são limitadas pela soma de entregas reais. Separa-se **volume histórico**, **custódia ainda ativa**, **material devolvido** e **uso material realizado**, sem dupla contabilização.
- A origem continua sendo `WorldState` / journal / lot lineage reais; **não há alteração de saves V9–V40**, nem novo evento, nem mudança de UI ou de cadência.
- O resultado é uma **declaração de partes**, jamais uma fonte autônoma de direito legal. `AuthenticatesInstitutionalLegalTitle=false`, `AuthenticatesBindingSaleContracts=false`, `AuthenticatesExecutedBuyerPayment=false`, `AuthorizesMonetaryRevenueOrTax=false` e `GrantsAbstractPurchasingPower=false`.

## QA

`GaEconMarketDefault01lTests` valida:
1. vínculo de uma entrega real a contrapartes explicitamente nomeadas; hash, composição de origem, workers e save imutáveis;
2. mistura de entregas devolvidas/usadas, mudança de estado do preflight e replay determinístico;
3. ausência de contraparte presumida para handoffs sem declaração;
4. recusa de falsificação de tile, commodity, quantidade, atividade, trabalhadores, hash ou ordinal;
5. duplicidade de transferência ou contrato, ator nulo/não tipado, colisões institucionais, mesmo titular dos dois lados e sessão estrangeira.

## Próximo gate

Para transformar a declaração em direito, é indispensável uma **autoridade de titularidade externa ao mero journal físico**, com instrumento/versão, vínculo ao estado e prova de autorização legítima, e posteriormente uma ação canônica de venda/transferência de titularidade devidamente liquidada. Nunca promover declaração em contrato assinado por conveniência; tampouco inferir liquidação do simples uso de madeira/pedra.

Transporte remoto requer prova de itinerário realmente executado. A curva de preço marginal existente é índice, e não saldo bancário. Sem moeda fictícia da população, crédito, emissão ou tributo sem fato gerador. Alimento legado agregado não deve ser fragmentado em seis tipos fictícios.

## Relação com a modelagem do transporte

O armazenamento geográfico existente usa uma `StrategicEdgeId` para representar a **ligação entre células adjacentes**. O traçado exibido no mapa deverá cruzar o limite entre células aproximadamente na direção centro-a-centro; a aresta do grafo não significa desenhar a estrada ao longo da fronteira dos hexágonos/pentágonos. A rede de transporte possui ainda nós e ligações lógicas explícitos para permitir etapas e modais mais avançados, sem impor geometria literal para cada serviço.
