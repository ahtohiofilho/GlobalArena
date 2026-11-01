# GA-VS70 — Poder de compra abstrato regional calibrado por produção física

**Baseline:** VS69 PR #176, `7067b1584d7535f7f27a297604b5fc8bc0549064`, 35/35 workflows aprovados. VS70 é incremental, em PR Draft empilhado, sem merge em `main`.

## Decisão

A diferenciação regional de poder de compra, antes congelada, passa a ter uma **implementação de laboratório** baseada na produtividade física corrente. O modelo canônico de poder de compra **continua abstrato**: números racionais são instrumentos de calibração e preflight, não dinheiro emitido, salários de cidadãos, contas privadas ou sistema de crédito.

Um mesmo trabalhador residente continua participando de ambas as dimensões, **produção e consumo**. Um povoamento é consequência de pessoas atuando em tiles, não uma fundação formal de cidade. Não foi introduzido estoque persistente nem receita futura para financiar compras.

### Derivação do fator

O baseline VS62 utiliza um fator de poder de compra uniforme por povoamento. VS70 deriva um multiplicador **por comprador/tile** a partir dos fluxos positivos reais de produção do mesmo snapshot:

```
score(tile) = Σ_{fluxos de produção no tile}
    quantidade_real_do_produto /
    (referência_explícita_da_mercadoria_atividade × residentes_no_tile)

fator(tile) = clamp(score(tile), minimo, maximo)
poder_de_compra_bruto(tile) =
    residentes × base_abstrata_por_residente
    × fator_uniforme_VS62 × fator_regional_VS70

H_protegido(tile) = residentes × H_por_residente
```

A **referência por commodity/atividade é obrigatória** e modela unidades heterogêneas: madeira, alimento e pedra **não** têm quantidades brutas diretamente somáveis. Somente seus quocientes adimensionais em relação a referências declaradas podem ser combinados. Os coeficientes, a soma, os limites `minimo/maximo` e a quantidade numérica de poder de compra são hipóteses explícitas de calibração, **não equivalências econômicas definitivas**. Os valores do mundo já gerados não mudam.

Para impedir exclusão automática de residentes que estejam produzindo pouco, o modelo exige um mínimo positivo, com máximo limitado a 16; se uma hipótese fizer o H maior que o bruto, o preflight existente falha fechado em vez de dar saldo negativo ou empréstimo.

### Arquitetura

- `CanonicalPrimitiveRegionalProductivityProjection.FromSites`: aceita apenas `CanonicalPrimitiveSiteDiscovery` vigente, referências exatas para todas e somente as commodities/atividades produtivas correntes, mínimo positivo e máximo explícito; cria fatores e scores em racionais exatas.
- `projection.ApplyTo(policy)`: cria **uma nova** `ExperimentalAbstractPurchasingPolicy` de laboratório vinculada à mesma sessão e sítios, preservando ID/versão, poder de compra base e H. Recusa compor duas vezes o mesmo fator.
- `ExperimentalAbstractPurchasingPowerProjector.Project`: se houver projeção regional, valida que ela é da sessão atual e cobre **todos** os compradores. Multiplica os fatores uniformes já existentes pelos regionais; nunca duplica o número de trabalhadores nem altera a demanda física.
- `CanonicalPrimitiveFreightPortfolio.CalibrateRegionalPurchasing` e `CompareRegionalPurchasing`: expõem calibração e confronto entre baseline VS69 (uniforme) e cenário regional usando **o mesmo** portfólio autenticado, preços, H, congestionamento e caminhos físicos. A comparação reutiliza VS68/67/66/65, inclusive auditoria independente por componente e reconciliação de frete.
- Provas regionais são **invalidadas** após mudanças de trabalhador, topologia, concessões, rede ou restauração de save; não podem ser aplicadas entre sessões.

### Evidência e critérios de promoção

Os testes VS70 incluem:
1. Produção física por trabalhador, referências por commodity/atividade, fator mínimo/máximo e conservação de residentes em dois mercados de rede;
2. Projeção regional do poder de compra sem alterar H, habitantes, demanda nem mover estoque;
3. Integração do fator com seleção de frete, custos de corredor e replay auditado dos cenários VS68;
4. Sensibilidade às referências declaradas sem alterar a produção/civilização física;
5. Rejeição de referências incompletas, duplicadas, inválidas, limites errados e provas obsoletas.

Benchmark `--vs70-regional-purchasing`: 8/32/128 **consultas** no cenário físico fixo e conectado, não 128 cidades. Faz projeção regional e comparação uniformizada/regional completa; mede consulta e alocação com aquecimento, sem assumir throughput linear por população.

Gate Linux: regressão direcionada VS40–70, benchmark Release, Godot Release. Gate Windows: suíte integral, benchmark Release, Godot Debug. Homologar **somente** quando todos os workflows do head final forem `completed/success`.

**Governança:** `CanAuthorizeRealSettlement=false`, `IsCommitted=false`. Não habilitar SETTLE, cartões, bancos, crédito, contratos futuros ou salários por pessoa. A calibração regional é uma hipótese transitória; poder de compra abstrato continua canônico.
