# GA Gate 09 R31 — decomposição de inversão entre mercados

Data: 14/11/2026. Base #319/R30; motor e experimentos R20–R30 intactos.

## Problema identificado

O R29 registrou reversões A→B→A nas melhores respostas.
O R30 revelou que, nos snapshots de chegada, somente sete de
915 trocas entre mercados tinham margem pequena <=1e-4 no
ranking de mercados, considerando oito trajetórias. É necessário
ver o ranking ANTES, DEPOIS e em estados numéricos intermediários,
pois uma margem já pode ter crescido após cruzar o zero.

## Critério estritamente observacional

Para um grupo que troca destino A por B, registrar o preço de A e B,
as respectivas quantidades, os menores custos de transporte
generalizados por vendedor/mercado e os scores S=P/(1+C)
na origem e no destino da atualização.

Aplicar identidade algébrica (sem alterar a física):
    delta ln(S_B/S_A)
      = delta ln(P_B/P_A)
      - delta ln[(1+C_B)/(1+C_A)].

A segunda parcela separa o efeito de atualizar o custo das rotas
canônicas antigas com novas cargas e a diferença decorrente de
reotimizar as rotas canônicas com essas cargas.

Validar a identidade da curva original:
    delta ln(P_B/P_A) = -1/2 delta ln(Q_B/Q_A)
quando D de cada mercado é constante. Essa é relação matemática,
não demonstra a causa de uma troca comercial.

Definir "domínio de preço" somente pela magnitude absoluta de
sua contribuição logarítmica comparada à do transporte, sem
inferir causalidade. Margens sinalizadas nunca modificam F-072.

## Sondagens numéricas

Em passos selecionados de cada trajetória imutável, recalcular
o mesmo oráculo nos alfas 25%, 50% e 75% do passo realmente
aceito, sem aplicar essas propostas ao estado. Para cada mercado
antigo/novo, registrar em quais intervalos os scores trocam sinal.
Isso identifica cruzamentos amostrados, não a unicidade de zero.

Contabilizar 3 avaliações de R14 por passo sondado, além
das consultas da política e dos scans independentes para
reconstrução de caminhos. Nunca confundir custos de laboratório
com custo real do kernel C#.

## Invariantes de governança

- Manifesto congelado R20 SHA-256, 16/16.
- Reproduzir ranking e empate R14 por grupo em snapshots.
- Paridade R13/R14, replay R21/R30 e R26, conservação
  monetária F-084F e venda integral.
- Estado de mercado, fluxo e escolha original preservado.
- Estabilidade numérica requer cinco critérios <=1e-4
  por três verificações consecutivas. Nenhum relaxamento.
- F-059, prova de equilíbrio global, motor C# integrado
  e produção continuam sem ratificação.

O R31 deve apenas orientar a seleção do experimento R32:
se a variação de preço relativa prevalecer, estudar
sensibilidade/realimentação Q→P; se prevalecer transporte,
estudar a reação de carga e saturação. Não atribuir causalidade
sem ensaio independente. PR em Draft sobre R30, sem merge.
