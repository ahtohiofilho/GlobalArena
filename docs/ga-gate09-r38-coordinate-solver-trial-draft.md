# Global Arena — Gate 09 R38: primeiro experimento de SOLUÇÃO numérica

Data: 14/11/2026. Base PR #326/R37. Regras R20–R37 preservadas.

## Mudança deliberada de estratégia

A investigação dos motivos das inversões de mercados foi
formalmente encerrada no R37. R38 não busca novas taxonomias
de oscilação: implementa e mede um método novo para resolver
o mesmo problema simultâneo F-072, sem tocar na economia.

## Candidato testado

Método: troca coordenada de fluxo comercial por produtor
com conservação integral da oferta e busca de passo.

Em cada snapshot, selecionar até duas fontes físicas de
maior contribuição ao arrependimento ponderado, comparar
sua rota existente com a melhor coluna original do R14
e transferir provisoriamente 1/2, 1/4, 1/8 ou 1/16
da quantidade física existente para a coluna preferida.

Para CADA proposta, recalcular integralmente:
P=sqrt(D/Q), carga B, custo c_free*(1+(B/K)^4),
tarifa do vendedor, menor rota e arrependimento F-072.

O critério de ESCOLHA NUMÉRICA e:
  M = gap_global_F072 + 0,30*gap_pior_grupo.
Não é regra de consumo, carteira de comprador, preço
artificial, preferência regional ou pagamento.
Aceitar somente redução estrita de M >1e-12.
Se todas as opções piorarem, parar declarando
ESTAGNAÇÃO, nunca falsificar convergência.

## Controles, matriz e limites de trabalho

Confrontar com políticas ORIGINAIS R21 decaimento,
R21 backtracking e R26 progressiva global. São
quatro métodos por fixture, sob o mesmo teto duro
de chamadas R14.

Casos: mapas 42/162 nós, D/oferta original e
heterogênea, capacidade x1 e x0,20.
32 percursos por plataforma, limite de chamadas:
129 (42 nós), 97 (162 nós). Todas as propostas
rejeitadas devem ser contabilizadas. Métodos que
pararem cedo devem informar o trabalho não utilizado.

## Gate vinculante para decidir o futuro do método

Condições originais (inalteradas):
- gap global <=1e-4;
- gap do pior grupo <=1e-4;
- variação relativa de preços <=1e-4;
- variação de carga <=1e-4;
- variação de quantidades <=1e-4;
todos simultaneamente por três checkpoints
consecutivos COM atualização efetiva.

Para obter GO apenas para novas validações, o
candidato deve satisfazer todas essas condições
em TODAS as fixtures e superar o melhor
controle em gap global E no pior grupo.
Caso contrário, NO-GO sob orçamento predefinido.

Um NO-GO não autoriza 20 rodadas de recalibração
arbitrária: a próxima família deve ser
matematicamente distinta (R39). Após no máximo
três famílias (R38, R39 e R40), emitir decisão
de arquitetura, inclusive eventual necessidade
de reformular o problema, se necessário.

## Contratos inegociáveis

C# e Godot inalterados. Oferta de cada produtor
100% alocada, mercado com preço sempre finito,
manifesto SHA256 R20, paridade R13/R14,
conservação monetária F084F, juros/estoques
não introduzidos e regras de tarifa e propriedade
intactas. F059 continua pendente em outra
frente; sucesso numérico jamais será automaticamente
homologação financeira nem prova global.

PR Draft, sem merge. R38 é uma prova de conceito
laboratorial com decisão objetiva de prosseguir
ou descartar.
