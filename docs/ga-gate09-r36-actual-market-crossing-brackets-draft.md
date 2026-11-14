# Global Arena — Gate 09 R36: intervalos de inversão entre mercados

Data: 14/11/2026. Base PR #324/R35, laboratório R20–R35 preservado.

## Pergunta

O R35 verificou derivadas locais em regiões suaves, mas não
observou troca de vencedor. Isso não prova que escolhas sejam
globalmente suaves. O R36 seleciona transições reais nas
quais um produtor muda seu mercado vencedor entre A e B.

## Métrica e execução

Reconstruir os fluxos no intervalo da atualização aceita:
   fluxo(t) = mix_original(fluxo_antigo, alvo_antigo, alfa*t).
t=0 representa o snapshot antigo e t=1 reproduz exatamente
o hash do fluxo novo. Os estados internos são exclusivamente
numéricos, não turnos, transações ou movimentos reais do jogo.

Reavaliar R14 com preço P=sqrt(D/Q), transporte físico original,
tarifas e menor rota por vendedor em cada ponto de bisseção.
Definir a vantagem comercial em dois destinos fixos:
   L(t) = ln(S_B(t)/S_A(t)); S_m=P_m/(1+C_m).

Manter L na borda esquerda <=0 e direita >=0; subdividir
24 vezes. A continuidade dos scores quando Q permanece positiva
e custos finitos fundamenta a existência de um cruzamento
NO INTERVALO, mas não sua unicidade e não uma relação causal.

Distinguir explicitamente:
- empate entre A/B no limiar original R14;
- empate competitivo no máximo global de TODOS os mercados;
- presença de um terceiro mercado acima do par A/B;
- eventual mudança de caminho mínimo canônico;
- flag original de empate/rota equivalente associado a F-059,
  cuja regra financeira não está ratificada.

## Custos e matriz

Dois tamanhos 42/162 nós, horizonte 24/12 atualizações,
dois regimes de demanda e oferta, capacidades x0,20,
x0,40 e x1, duas políticas numéricas R21 originais.
Matriz CI: 24 trajetórias em Ubuntu e Windows.

Selecionar até duas atualizações com trocas e até dois grupos
por atualização, segundo a proximidade dos scores A/B.
Amostragem proposital; não representa todos os eventos.

Cada grupo sondado custa 24 avaliações R14 mais uma
reanálise da melhor aproximação para paridade R13/R14
e conservação F-084F. Cobrar separadamente avaliações
da política numérica e scans adicionais das rotas.

Manifesto R20 16/16, oferta integral, preços positivos,
paridade de oráculos, replay R34/R21, cinco critérios
simultâneos <=1e-4 durante três snapshots preservados.
F-059, prova de equilíbrio, solver C# e produção
continuam pendentes. PR Draft sobre R35, sem merge.
