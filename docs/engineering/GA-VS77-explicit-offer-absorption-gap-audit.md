# GA-VS77 — Certificado da lacuna entre oferta explícita e absorção

**Referências:** GA-ADR-ECON-006 (#181), plano VS77 (#186), VS76 PR #185 SHA homologado \`0c4b0b2d114728c71b03da82fc990424f577de68\` (42/42 CI).

## Decisão de fronteira

A diretriz econômica do GA é a absorção da **produção efetivamente colocada à venda** mediante preços marginais decrescentes, **não** a venda automática de toda produção fisicamente possível em qualquer tile. O laboratório VS76, porém, escolhe lotes limitados por demanda física, orçamento/H e rotas institucionais, com preços postados FIXOS. Portanto, **separar estes três conceitos** antes de inventar liquidação:

1. **Potencial físico**: capacidade produtiva local derivada dos trabalhadores e produtividade; não é estoque e não equivale a mercadoria extraída ou ofertada.
2. **Oferta explícita**: quantidade hipoteticamente disponibilizada à venda por \`(origem,commodity,activity)\` para avaliar a regra econômica; ainda não é decisão de extração do mundo.
3. **Atendimento VS76**: lotes locais e de frete escolhidos em um domínio finito, com orçamento H, demanda física, direitos e custos. **Não é venda executada**.

A VS77 não resolve o problema de preço marginal, nem muda o seletor, apenas mede a incompatibilidade observável.

## Contrato do diagnóstico

\`CanonicalPrimitiveIntegratedSelection.DiagnoseExplicitOfferAbsorption(offers)\` retorna \`CanonicalPrimitiveOfferAbsorptionDiagnostics\`, com \`Snapshot.Sources\`, \`Snapshot.Buyers\` e \`Snapshot.Commodities\`.

Para cada \`(origem, commodity, activity)\`:

\`\`\`text
0 ≤ alocado_VS76 ≤ ofertado_explicitamente ≤ potencial_fisico
ofertado_nao_alocado = ofertado_explicitamente - alocado_VS76
potencial_nao_ofertado = potencial_fisico - ofertado_explicitamente
\`\`\`

Para cada \`(comprador, commodity)\`, \`demanda_nao_atendida = demanda_fisica - alocado_VS76\`.

Para cada **mesma commodity**, soma-se o residual das origens e a demanda não atendida de todos os compradores:

\`\`\`text
demanda_adicional_necessaria_pelo_menos =
    max(0, soma_ofertado_nao_alocado - soma_demanda_nao_atendida)
\`\`\`

Quando positivo, o déficit demonstra um **impedimento físico incontornável** para absorver toda a oferta com os atuais compradores e limites de demanda, mesmo que os preços sejam reduzidos. Quando igual a zero, **NÃO** garante venda: o frete pode não existir, H pode ser insuficiente e o preço marginal não está calibrado. Não há comprador, salário, receita ou transporte novo criado para preencher a diferença.

Ofertas são propostas positivas, únicas, de fonte autêntica, limitadas pelo potencial do fluxo e obrigatoriamente suficientes para cobrir TODOS os lotes VS76 já selecionados naquela fonte. Quando a oferta declarada não cobre um lote previamente selecionado, a VS77 falha fechada **em vez de modificar a seleção**. Ausência de oferta significa potencial não ofertado, salvo se a seleção tiver lote positivo naquela origem, caso em que a auditoria rejeita a entrada.

## Auditoria e escopo

\`VerifyIndependent()\` chama auditoria VS76 e reconstrói integralmente todas as linhas e indicadores; \`RequireCurrent()\` rejeita sessões restauradas ou recibos obsoletos. O snapshot é ordenado de modo determinístico por identidades físicas, e a entrada da lista pode ser reordenada sem mudar o resultado.

A classe fixa \`CanAuthorizeRealSettlement=false\`, \`IsActualSale=false\`, \`IsActualInventory=false\`, \`IsMarginalPriceCurveCalibrated=false\` e \`IsCommitted=false\`.

**Sem** novos preços, operador, estrada, comprador fictício, saldos, banco, crédito, imposto, estoque ou mudança no estado físico; sem reutilização da economia invisível H como depósito de mercadorias.

## QA

Seis testes VS77 de reconciliação, excedente de oferta, zero-lote sem compradores artificiais, rejeição de proposta abaixo da quantidade já selecionada ou acima do potencial, permutação determinística, restore invalidando recibos. Benchmark 8/32/128 sobre sessão pequena; workflow dedicado Ubuntu/Windows, regressões, Godot Mono Release/Debug.

**Somente homologar com todos os workflows do SHA final \`completed/success\`.** PR Draft empilhada sobre VS76. Benchmark não prova escalabilidade geral ou equilíbrio de mercado.

## Próximo gate econômico: NÃO resolver implicitamente

Antes do mercado executável, será preciso uma ADR distinta que decida:
- Se a curva marginal altera preço único recebido por todos os lotes ou preserva múltiplos preços por segmentos da oferta;
- Como a absorção integral de **oferta efetiva** convive com demanda física finita e H protegido. Quando a demanda se esgota, redução de preço sozinha não cria consumidor físico;
- Se é necessária regra explícita para o produtor **não colocar toda sua produção potencial à venda**, sem estoques invisíveis;
- Como comparar receitas, preços e eficiência sem obrigar todo lote a produzir remuneração positiva ou inventar renda de outra rodada;
- Como incorporar a regra ao ciclo econômico do jogo sem criar um segundo mercado.

A VS77 demonstra o eventual gap, mas **não altera a diretriz canônica nem simula seu cumprimento**.
