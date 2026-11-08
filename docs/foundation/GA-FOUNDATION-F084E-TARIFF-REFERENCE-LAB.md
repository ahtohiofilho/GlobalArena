# GA-FOUNDATION-F084E — Peso tarifário local sob referência natural e copropriedade

**Data:** 2026-11-08.  
**Estado:** laboratório executável isolado; **fórmula de intensidade F-073 NÃO RATIFICADA**.  
**Base:** PR #305 (F-084D), sobre #304 (investimento), #303 (movimento), #302 (circulação natural) e #298 (fundação).  
**Agricultura:** PRs #299–#301 preservados em linha independente.  
**Contratos relevantes:** F-024/F-025/F-031/F-034/F-035/F-060/F-062/F-066/F-070/F-072/F-073/F-074/F-081/F-082/F-084.

## 1. Escopo da hipótese F-073

Existe uma única aresta física-logística com distância lógica 1. A F-084D produz impedância de referência em uma carga equivalente B; a F-084C produz condições de livre fluxo e capacidade depois de receber o **capital já incorporado**. O peso tarifário candidato deve depender somente da própria aresta e não de preço, origem, destino, carga endógena do período ou tamanho aparente do polígono.

**Avaliação para uma carga fixa e exógena da mesma aresta, K_natural:**

```text
B_ref  = K_natural,e
c0     = C_e(B_ref, I=0)
cI     = C_e(B_ref, I=I_e)
delta  = max(0, c0 - cI)
w_e    = delta / (1 + delta/c0)
       = (delta * c0) / (c0 + delta)       se delta>0
w_e    = 0                                 se delta=0
```

A carga `B_ref` **não** varia quando a obra modifica K. Usar a carga corrente, ou atualizar `w` a cada variação transitória de B, introduziria realimentação tarifária acidental. A sensibilidade ao expoente p=3/4/5 permanece aberta.

- `w` é **índice adimensional de intensidade** local, não dinheiro nem percentual diretamente cobrado. Na comparação comercial ratificada F-072, o índice pode entrar como `g[e,c]=C[e]+w_e*(1-s[e,c])`; manter C e w conceitualmente distintos.
- Somente capital efetivamente incorporado pela obra F-035/F-066 define H e I: `I_e=Σ_c H[e,c]`. A conta pagadora/tempo do trabalhador não é modificada.
- O peso `w` é o mesmo para todos os produtos, e só depende da melhoria física total. Nenhuma civilização ganha exclusividade ao investir primeiro e nenhum voto para ingresso do novo investidor.
- `I=0` obriga `w=0`; a infraestrutura natural funciona mesmo sem titulares.
- Sob redução de impedância e/ou aumento de capacidade, `0≤w≤delta` e `w≤c0/2` no candidato. A fórmula não fixa arbitrariamente um percentual de tarifa: os números resultam da escala física de C e da intensidade de sua melhoria.

## 2. Participações e remuneração, SEM lançamentos financeiros

Os titulares possuem capital `H[e,c]` e quotas `s[e,c]=H[e,c]/I_e`. Para uma civilização vendedora c, peso **externo** na aresta `w_e(1-s[e,c])`, em ponto fixo, calculado sem carteira/registro de venda.

- Vendedora titular integral (100%): peso externo 0.
- Vendedora cotitular: peso externo proporcional às participações dos demais.
- Vendedora não titular: peso externo igual ao peso total `w_e`.
- Capital zero: peso total zero, nenhuma quota positiva.

A função `OwnerExternalComponentRaw` expõe as partes **não monetárias** de cada investidor externo. Arredondamentos independentes de quotas geram resíduos unitários; NÃO usar diretamente essas frações arredondadas para lançar pagamentos. Quando houver liquidação F-060/F-071, o journal deverá definir distribuição de resíduos determinística, atribuição por produto/destino e conservação exata de fundos.

Uma rota comercial existe apenas transitoriamente no cálculo de fluxo. Sua soma de pesos externos `W=Σ_e w_e(1-s[e,c])` nunca cria entidade persistente de transporte ou copropriedade de percurso.

**F-060 já ratificou o rateio:** numa venda com receita bruta V, o vendedor recebe V/(1+W), titulares externos recebem juntos VW/(1+W). O laboratório expõe **somente as frações** indicativas, que somam 100% após quantização fixed-point; não simula tesouro, compra, preço, tributos, comprador com carteira, transferência monetária ou lucro. O rateio não é um segundo custo físico C.

## 3. Resultados numéricos ilustrativos

Aresta neutra de laboratório `C_livre,nat=1`, `K_nat=1`, `I=I0`. F-084C fornece provisoriamente `C_livre,I=0,5`, `K_I=2`. A carga de referência natural permanece B=1:

| Expoente de congestão | c0 | cI | Delta | Peso `w` aproximado |
| --- | ---: | ---: | ---: | ---: |
| p=3 | 2,000000 | 0,562500 | 1,437500 | 0,836363 |
| p=4 | 2,000000 | 0,531250 | 1,468750 | 0,846846 |
| p=5 | 2,000000 | 0,515625 | 1,484375 | 0,852017 |

Valores em fixed-point truncados ao milionésimo, portanto a aproximação difere da avaliação ideal em ponto flutuante. **Nenhum dos expoentes, p, I0, forma logarítmica, escala de K ou peso foi ratificado.**

Exemplo de **copropriedade** no perfil p=4: capital A=25%, B=75%. Para a vendedora A, seu peso externo é aproximadamente `0,846846×0,75≈0,635134`; B recebe o direito potencial correspondente. Para a vendedora B, seu peso externo é `≈0,211711`. Vendedora C sem participação: peso `≈0,846846`. São índices, não transferências monetárias efetivas.

Uma vendedora totalmente externa, atravessando apenas essa aresta, teria pelo rateio F-060 uma fração bruta indicativa destinada aos titulares de aproximadamente **45,85%**. Esse percentual é alto para muitas experiências de jogo: constitui um **alerta de calibração, não uma tarifa aprovada**. Não fixar teto artificial para ocultar o risco e não prometer ROI antes de avaliar a resposta endógena dos produtores.

## 4. QA matemático e limites

- Pesos nulos sem investimento para p=3/4/5; mar/costa/terra seguem mesma fórmula sem classes de obra.
- Capacidade melhorada com impedância livre inalterada também pode produzir peso positivo: F-073 remunera melhora física na referência, não apenas redução de `C_livre`.
- A carga de referência permanece `K_natural` mesmo quando o tráfego corrente ou `K_investida` mudar; não criar nova tarifa a cada ciclo por volume.
- Manter `w≥0`, `w≤delta`, `w≤c0/2`, crescente com capital nas condições testadas; mudança de propriedade altera apenas peso externo, não desempenho físico.
- Peso externo da coproprietária isenta exatamente sua própria quota, nunca a quota alheia.
- Soma dos pesos W independe da ordem em que as arestas são percorridas matematicamente; não associar direito patrimonial ao trajeto computacional.
- Subdivisão **matemática homogênea** de uma aresta física idêntica divide também c0, cI e w, mantendo soma. **Não usar esse teste para exigir invariância entre grafos LÓGICOS distintos**, rejeitada por F-081.
- Testar C=0 apenas quando houver definição física; este laboratório exige C e K naturais positivos.
- Rejeitar capital total divergente da soma das quotas, parâmetros inválidos, situação com capital zero mas capacidade melhorada, e tentativas de degradação física rotuladas investimento.
- Testes com checksum `49_949_188_417` para 4.096 casos, execução Linux/Windows/macOS e benchmark isolado em 10 mil/100 mil arestas com p=3/4/5.

## 5. O que NÃO está aprovado ou implementado

1. **w de F-073 permanece PROPOSTO**, apesar de seus invariantes estruturais. O preço do mercado, escala física, balanço de incentivos e global solver ainda não têm calibração final.
2. Nenhuma cobrança monetária é realizada. Frações são apenas uma leitura local do contrato de rateio **já ratificado F-060**, e arredondamentos monetários exigem mecanismo próprio.
3. Não há solver de preços e volumes, demanda endógena, impostos, transferência de dinheiro, plano persistente de rota, pessoal embarcado ou motor de eventos.
4. Não há alteração do tempo do trabalhador F-084B, nem implantação real de obras F-035, nem worldgen novo integrado.
5. Nenhuma reintrodução da lógica logística, estoques materiais ou biomas discretos do motor anterior.

## 6. Próximo gate: F-084F / F-071

Testar **atribuição transitória de preço e receita bruta por destino do fluxo matemático** e conciliação do rateio F-060, sem caminho como entidade física. Reconciliar pagamentos sob várias arestas e vários coproprietários, com arredondamento determinístico. Só depois integrar solver iterativo com carga e preço simultaneamente endógenos e testar se as tarifas alteram o fluxo/ROI no equilíbrio.

Preservar a nova fundação PR #298, a frente agrícola #299–#301 e a cadeia de circulação #302–#305, com este incremento como continuação isolada, sem merge automático.
