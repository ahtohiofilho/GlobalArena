# Global Arena - Gate 09 R28: progresso economico por consulta ao oraculo

Data 13/11/2026. Base #316/R27, com R20-R26 preservados em Draft.

## Contexto e objetivo

O R27 reduziu consultas do oraculo por historico de alfas, mas aceitou
passos numericamente inofensivos que nao entregaram progresso comercial
suficiente. A economia computacional isolada nao justifica a promocao.

O R28 avalia uma busca *com salvaguarda de progresso*: primeiro ensaiar
o coeficiente do decaimento R21, mantendo a busca F-072 original por
mercados/rotas e recalculando o custo/precificacao no estado novo.
Aceitar cedo apenas se houver reducao de pelo menos 1% do gap agregado,
com tolerancia matematica de 1e-12; se ja abaixo de 1e-4, aceitar
nao aumento. Nao se altera o criterio de convergencia economico.

No metodo conjunto, adicionalmente exigir que o pior grupo nao piore.
Em controle de ablacacao, experimentar os mesmos filtros mas testar
alfas fixos decrescentes (sem primeira proposta de decaimento).

Se nada passa, selecionar somente dentre candidaturas auditadas:
menor gap agregado ou menor pior razao conjunta conforme a politica.
Qualquer fallback e registrado, inclusive se o resultado piorar.
Nenhum preco, custo ou rota anterior pode servir de substituto a
avaliacao R14 do novo estado.

## Matriz e metrica economica por trabalho

Controles: R21 decaimento/backtracking, R26 progressive_global,
R27 warm_global. Todas as consultas de oraculo sao cobradas.
Comparar em orcamento nominal 25/49/97/193/385 e relatar tanto
gap final quanto pior produtor. A metrica complementar de gap
acumulado por oraculo contabiliza cada avaliacao ao estado comercial
anterior ate que um novo passo seja aceito, impedindo atribuir
ganhos de candidaturas descartadas.

Cenarios CI: 42 nos, 96 updates, 385 consultas; 162 nos, 48 updates,
193 consultas. A iteracao inicial contabiliza uma consulta.
Tempos de CPU Python nao equivalem a performance integrada do C#.

## Gates e decisao

- Manifesto R20 16/16, replays de paridade R21 e F-072 R13/R14.
- Venda 100% da producao e igualdade bruta/creditos F-084F.
- Verificar prefixos, custo efetivo, preco positivo, origem dos
  candidatos, rejeicoes, fallback e truncamento por orcamento.
- Cinco criterios simultaneos <=1e-4 (gap global, pior produtor,
  mudanca de precos, cargas e quantidades) durante tres snapshots
  consecutivos. Mesmo obtidos, nao demonstram equilibrio global.
- Se houver apenas reducao de consultas com pior gap, nao promover.
  Se o ganho aparecer em um cenario apenas, prolongar laboratorio
  antes de produzir. Preservar F-059 como questao aberta.

Governanca: PR R28 Draft empilhado sobre R27, sem merge e sem alteracao
do motor C#, UI, tesouros, transporte ou journal.
