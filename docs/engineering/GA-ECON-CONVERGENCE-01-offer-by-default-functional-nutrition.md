# GA-ECON-CONVERGENCE-01 — Convergência entre oferta automática e nutrição funcional

**Data:** 06/11/2026  
**Base:** PR #278, HEAD `8c7dba7dd395b3bff288301bcd1eb83566769a9b`; contrato vinculante `GA-CANON-DIRECTIVES-01`; `GA-NUTR-ARCH-01`, `GA-NUTR-INTEGRATION-01`, `GA-ECON-OFFER-AVAIL-01` e `GA-ECON-OFFER-COMMIT-01`.  
**Status:** PRIMEIRO INCREMENTO (diagnóstico/projeção read-only + redação fiel no HUD), **não** ativação do novo motor econômico. PR Draft empilhado sobre #278, sem merge.

## 1. Decisão reafirmada

A economia de destino deve satisfazer **simultaneamente**:

1. **Oferta por padrão.** Depois da produção e dos usos/obrigações físicos legítimos, toda mercadoria não retida deliberadamente integra o circuito comercial automaticamente; o jogador não emite uma ordem de venda a cada ciclo. Reserva voluntária e compromisso de insumo/obra/expedição não são oferta. O estoque físico enquanto a execução aguarda não configura decisão de retenção. Mercadoria já ofertada não pode voltar a ser ofertada ou consumida duas vezes.
2. **Absorção sem teto exógeno de quantidade.** Todo volume disponibilizado à venda deve receber destino econômico no fechamento pertinente; `D=P*N` é pressão abstrata de demanda/preço, **não** quantidade máxima comercializável ou ingerida. A receita marginal cai ao aumentar a oferta, mas não impor `min(S,D)`, não extinguir mercadorias sem registro físico e não converter oferta em venda por declaração.
3. **Nutrição sem quota obrigatória.** Não existe «necessário 0,5 alimento/pessoa» no modo-alvo, nem «falta 0,45» ou «atendimento 88,75%». Quantidade e variedade efetivamente ingeridas geram estado nutricional funcional; eficiência é saída de uma função contínua/não linear calibrada, não uma multiplicação por quota de consumo.
4. **Evidência executada.** Uma venda executada requer destinação material legítima (consumo físico registrado, uso produtivo, custódia/entrega, destino final autorizado e rastreável), origem de produção real, controle de transporte e direitos/titularidade; receita real exige liquidação autenticada. Oferta, intenção, cotação e preço de referência não constituem venda nem ingresso de caixa.
5. **População e compra abstratas.** Os moradores reais são consumidores e produtores; `N` não é saldo de carteira, não há emissão de moeda ficcional nem crédito. Não distribuir mercadoria entre tiles sem serviço físico de transporte.
6. **Compatibilidade explícita.** O modo histórico `LegacyDirectConsumption` e `LocalCashSettlement`, o contador de referência `FoodUnmetRaw`, a quota `FoodDemandRawPerWorker=500_000`, os saves V9–V34 e seus hashes/replays continuam intactos *até a introdução de um modo canônico novo, versionado e testado*. O novo modo não reutilizará esses indicadores como dados nutricionais.

## 2. Separação dos domínios

| Conceito | Unidade / evidência | Interpretação legítima |
|---|---|---|
| Produção executada | Quantidade por mercadoria, tile, ciclo e trabalhador | Aumenta bens físicos, não receitas automaticamente |
| Livre local | Estoque em custódia local, não comprometido | Pode ser candidato à oferta; não é «retido deliberadamente» |
| Oferta explícita em custódia | Compromisso canônico `MarketOffer` | Não é venda executada |
| Retenção deliberada | Compromisso `DeliberateRetention` | Excluída do circuito comercial |
| Potencial elegível ao mercado | Saldo local livre + ofertas explicitamente vinculadas da mesma mercadoria | Candidato read-only à política de oferta automática; não atesta venda |
| Absorção executada | Transferência/destino físico e liquidação autorizados no journal | Pode atestar venda e receitas, se as regras de propriedade estiverem satisfeitas |
| Ingestão executada | Uso alimentar final por tipo, local/expedição, população e ciclo | Input nutricional; não confundir com compra sem ingestão |
| Qualidade nutricional | Função de ingestão total **e** variedade fisicamente atestadas | Só após calibração e ledger real de alimentos |
| Eficiência produtiva | Efeito de estado nutricional aprovado **no ciclo posterior** | Sem causalidade circular |

### Nota matemática obrigatória

O contrato histórico de referência `D=P*N, x=S/D, p=f(x), R_ref=p*D` **permanece válido como cotação abstrata**, não como realização financeira nem como cálculo final da absorção executada. A receita efetiva será definida por uma **função de absorção com retornos marginais decrescentes**, calibrada e sem limite artificial de quantidade, por exemplo um funcional `R_exec(S; estado do mercado, acesso, custos, direitos)`. A expressão ilustrativa `∫_0^S p_marginal(q) dq` é estrutura conceitual, não curva aprovada: parâmetros, positividade, conservação e autorização fiscal precisam de gate independente. Nenhum preço, receita ou imposto novo é ativado por este PR.

Para seis alimentos **reais e tipados** `c_j`, `z_j=c_j*k_j/P` e `f=Σz_j`, a arquitetura candidata conserva `Q=A(f)×V(c_1,…,c_6)`; `A`, `k_j`, `sigma` e intensidade de variedade requerem aprovação. A ingestão não é limitada por `D` ou `FoodDemandRawPerWorker`. A ausência do ledger por variedade **invalida** a medição de `Q`; não autoriza presumir seis quantidades iguais ou seis zeros.

## 3. Conclusão do diagnóstico executado no PR #278

O `CanonicalPhysicalTacticalSession` ainda calcula `demand=workers*FoodDemandRawPerWorker`, consome `min(available,demand)` no modo direto (ou executa compra limitada por demanda/carteira no modo cash) e registra `FoodUnmetRaw=demand-delivered`. Esse campo é **diferença de quota histórica**, não déficit fisiológico medido.

O `CanonicalTacticalPhysicalFlowProjection` antes apenas apresentava `FreeWoodRaw`, `FreeStoneRaw`, compromissos de oferta/retencão e `LastGlobalFoodUnmetRaw`. Os valores observados `1,796` e `0,534` são estoque local realmente **não vendido**, sem retenções deliberadas: `retenções=0` na captura. Não há escoamento automático executado no modo atual.

## 4. Entrega do primeiro incremento neste PR

- `CanonicalTacticalPhysicalFlowReport.PotentialMarketRaw(commodity)` projeta **somente leitura** a quantidade elegível: saldo físico livre + ofertas `MarketOffer` já em custódia; exclui retenções reais, custódia de expedições e qualquer mercadoria inventada. Restringe a três mercadorias físicas conhecidas e não certifica vendas.
- `ProvesAutomaticMarketSale=false` e `ProvesNutritionalEfficiency=false` tornam os limites inequívocos no read model. Nenhum hash, save, preço, lote ou comando é alterado.
- No HUD do jogador, a quota histórica `falta` deixa de ser apresentada como «déficit alimentar»; exposição conservadora: ingestão **física** agregada, nutrição «indisponível» e oferta automática «não executada» no modo atual. O saldo local é rotulado «não vendido» em vez de ser interpretado como retenção.
- `GaUiEconFlow01Tests` valida limites de evidência, inclusão de estoque livre e ofertas já segregadas, exclusão de retenções reais, igualdade antes/depois das leituras, sem venda presumida. `UiE2E.gd` valida no Godot real que o ciclo avançou sem exibir «falta» como estado nutricional ou afirmar venda automática.
- **Não há promoção para o modo canônico-alvo neste PR.** A correção do motor não pode consistir em apenas apagar o indicador `FoodUnmetRaw` ou zerar estoques para parecer que houve absorção. Esses atos falsificariam a simulação.

## 5. Próximos incrementos obrigatórios (mesmo checkpoint de produto)

**A. GA-ECON-MARKET-DEFAULT-01 — execução comercial versionada.** Introduzir configuração nova para jogos novos, sem afetar restore histórico; registrar destinação automática da produção e do estoque elegível, retenções opt-in, origem física, destino real, transporte quando necessário, unicidade de custódia e absorção completa sem teto `D`. Não remover lotes sem registrar seu uso. Receitas/taxas só após escrituração real e curvas calibradas. Gates: múltiplos tiles, sem comprador fictício, zero retenção no startup, retenção integral/parcial, sem dupla venda, replay/hash e casos em que `S≫D`.

**B. GA-NUTR-VARIETY-LEDGER-01 — ingestão por variedade.** Migrar as seis identidades alimentares para produção, mercado, consumo e jornadas com conservação e proveniência; proibir índice escalar de consumo e fracionamento fictício de `CommodityId(1)`. O modo novo não define quantidade obrigatória por pessoa; estabelece ingestão endógena a partir da absorção/decisão econômica. Gates: monovariedade, escassez, diversidade e alta abundância, igualdade de saída e ingresso físicos, expedições, falha fechada para dados incompletos.

**C. GA-NUTR-EFFICIENCY-01 — curva e efeito posterior.** Calibrar `A(f)`, `V` e conversões de unidade com testes de monotonicidade controlada, ganhos marginais decrescentes e não-linearidade, sem meta fixa; só então aplicar produtividade no próximo ciclo, com save/journal versionado. Zero bônus antes de evidência e aprovação.

**D. Gate cruzado e aceite visual.** Confirmar que todas as mercadorias não retidas possuem destino, que não existem quotas fisiológicas na nova modalidade, que receita não é `preço×quantidade` usado como axioma mecânico, que bens não se teletransportam, e que o HUD mostra ingestão/qualidade corretamente. Aplicar testes unitários, FAST, hash/kernel/regressão multiplataforma e uma execução Godot Debug Windows. Nenhum merge antes desses gates.

## 6. Critério de avanço do projeto

Este checkpoint mantém a prioridade econômica sobre novos controles de UI e frete. O usuário homologou **a direção conceitual**, não a ativação silenciosa de algoritmos não calibrados. O fechamento só ocorrerá após A+B+C+D, sem apagar saves históricos.
