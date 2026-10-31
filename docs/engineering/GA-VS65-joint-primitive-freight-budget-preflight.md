# GA-VS65 — Prévia conjunta do orçamento abstrato e do frete físico

**Baseline:** VS64 PR #171 / `4c02fe1807c618fa13411831712afe5def9fff04`,
homologada tecnicamente (45/45 workflows). Branch empilhada sem merge à
`main`.

## A lacuna que a VS65 resolve

Na VS64, um portfólio apresenta candidatos autenticados por produto e
atividade, mas cada mercadoria é apenas uma alternativa comercial:
somar os resultados de prévias independentes duplicaria, indevidamente,
o poder de compra do mesmo comprador e ignoraria o congestionamento
que lotes de produtos diferentes causam na mesma via.

A VS65 implementa `CanonicalPrimitiveFreightPortfolio.PreviewJointBudget`
como laboratório **read-only** de um lote hipotético conjunto,
na mesma janela econômica, reutilizando as provas institucionais VS63
e os candidatos da VS64. Cada quantidade é escolhida explicitamente
por quem consulta; ainda **não** é um motor de clearing/otimização
multicommodity, e a viabilidade não significa execução comercial.

## Fluxo de dados e segurança

1. Exigir o portfólio de sessão atual, uma política de compra com
   identificação/versão idênticas à proveniência institucional e de
   1 a 12 lotes positivos. Não aceita candidatos forjados, repetidos,
   fora da sessão ou acima da cota econômica declarada.
2. Revalidar, para cada lote, a rota efetivamente construída,
   o produto, a atividade, as autorizações e o direito de serviço
   já aprovados pela VS63. Sem licenças/infraestrutura implícitas.
3. Verificar simultaneamente quantidades agrupadas por
   (origem, mercadoria, atividade) e (comprador, mercadoria).
   Se duas transportadoras pretendem vender a **mesma**
   produção física duas vezes, a proposta inteira é rejeitada.
4. Converter cada quantidade bruta pela massa explícita do catálogo
   (microgramas) e somar as cargas por corredor físico/tecnologia,
   com `ExperimentalSharedFreightBatchCost.Evaluate`. Uma mesma
   estrada recebe carga conjunta de alimento, madeira e pedra;
   a parcela de congestionamento é calculada **uma só vez por
   corredor**, não por consulta de mercadoria.
5. Projetar os residentes compradores da sessão **uma única vez**
   por `ExperimentalAbstractPurchasingPowerProjector`, com
   a política explícita de orçamento/H. Validar cobertura de
   preços de **todas** as demandas correntes.
6. Debitar apenas hipoteticamente, na mesma avaliação
   `ExperimentalBuyerCashSnapshot.Evaluate`, preço de mercadoria e
   custo proporcional do frete. Se os gastos ultrapassarem o valor
   disponível após H, retornar `IsBudgetFeasible=false`, sem crédito,
   saldo negativo, venda, obrigação futura nem mutação da sessão.
7. Exigir igualdade exata entre custo agregado do frete físico
   e encargos somados dos compradores. Expor o resultado e as
   cargas imutavelmente; `RequireCurrent` invalida resultados
   após alterações do mundo/rede ou restauração de saves.

**Cautelas:** `CanAuthorizeRealSettlement=false`;
`IsCommitted=false`. O nome interno de um cálculo monetário de
laboratório não significa que dinheiro ou conta pessoal exista
no jogo. A unidade de preço e da tecnologia de frete continua
uma hipótese de calibração explícita comum; a prévia exige preços
externos para todo produto demandado, mas não inventa preços,
câmbio nem fatores de substituibilidade. A política H permanece
residual invisível e nunca se converte em conta bancária.

## Escopo deliberado

- Não seleciona automaticamente quais lotes oferecer: apenas
  verifica uma proposta conjunta finita, escolhida externamente.
- Não assegura capacidade máxima física de rodovias e portos
  inexistente no modelo: a competição por corredor entra no
  custo marginal, sem "limite de tonelagem" fictício.
- Não usa carteira real do save, depósito, crédito, dívida, empréstimo
  ou receita futura para custear despesas.
- Não confunde viabilidade financeira hipotética com liquidação,
  autorização final ou aumento garantido de renda.
- Não altera as regras estabelecidas de moradores/trabalhadores:
  uma pessoa produz e também demanda; povoamentos emergem
  diretamente de tiles ocupados, sem fundar cidades.

## Gates e escalabilidade

Testes GA-VS65 cobrem:
- três produtos e **um único** serviço/ativo rodoviário real;
  custo de corredor e H compartilhados entre todos;
- preços exorbitantes levando a insuficiência orçamentária
  sem lançar débito ou dívida;
- dois direitos registrados que tentam vender duas vezes
  a mesma capacidade física de uma origem;
- preços faltantes, lote duplicado, candidato forjado,
  divergência da política e quantidade acima do limite;
- reprodução de save, bloqueio de rede, prova obsoleta,
  ordenação canônica de lotes e valores racionais.

Benchmark `--vs65-joint-budget`: 8/32/128 consultas em
cenário fixo de dois sites, três mercadorias e corredor único,
com aquecimento/mediana sem thresholds de desempenho irrealistas.
As repetições são **número de consultas, não número de habitantes
ou pontos geográficos**.

Gate Linux: regressão dirigida VS40–65, benchmark Release e Godot
Release. Gate Windows: suíte completa, benchmark Release e Godot
Debug. Concorrência por PR cancela apenas versões antigas do gate
VS65; outros workflows herdados continuam independentes.

**Condição de promoção:** todos os workflows do **head final**
passam. O PR permanece Draft; a VS66 só deve tratar seleção
simultânea, liquidação e despacho depois de uma decisão de
escopo separada e nova auditoria. Não ativar SETTLE real.
