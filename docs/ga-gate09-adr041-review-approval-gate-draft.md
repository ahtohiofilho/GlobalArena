# Global Arena — ADR-041: pauta executiva e gate de aprovação

**Natureza:** somente documentação e matemática, sem código executável,
sem quarto solucionador e sem alterações nos PRs R20–R40.
**Referência técnica completa:**
docs/ga-gate09-adr041-equilibrium-complementarity-review-draft.md.

## Resultado da revisão e matriz de evidências

| Tema | Avaliação | Fundamento / consequência |
|---|---|---|
| R38–R40 esgotados | **NO-GO confirmado** | As três famílias falharam o contrato de passagem predeclarado. |
| Economia F-072 | **Preservar** | Preço P=sqrt(D/Q); score P/(1+C); congestão original; tarifas do vendedor; produção integral. |
| Descrição do equilíbrio | **Equivalência algébrica estabelecida** | Complementaridade por coluna e grupo equivale a gap R14 nulo em fluxos viáveis com preços finitos. |
| Rotas omitidas | **Precisam de separação global** | O melhor caminho por vendedor e destino do R14 permite localizar a melhor oportunidade sem enumeração exaustiva. |
| Domínio de preço | **Restrição aberta não resolvida em geral** | Mercado alcançável com Q=0 tem preço infinito; sementes preservam massas mas não provam existência global. |
| Existência de equilíbrio | **Aberta em geral** | Há exemplo explícito com equilíbrio; não há teorema global para redes arbitrárias. |
| Unicidade da alocação | **Falsa em geral** | Contraexemplo com dois produtores e dois destinos: infinitas alocações ótimas com mesmos Q, B e preços. |
| Convergência de melhor resposta | **Não garantida** | O equilíbrio simétrico existe, mas mover toda a massa ao vencedor de um estado assimétrico pode anular um mercado. |
| Cinco métricas por três passos | **Gate de dinâmica, não definição de equilíbrio** | Uma configuração de GAP=0 pode ser alterada por desempate de uma resposta pura; proposta de separar certificado estático e ensaio de robustez. |
| F-084F | **Conservação intacta** | Certificado de soma dos créditos não seleciona os beneficiários em empate. |
| F-059 | **Pendente e independente** | Necessita regra financeira específica; não é ratificado pela complementaridade. |
| Implementação C#/Godot | **Não autorizada** | Requer revisão e aprovação formal do contrato antes de código novo. |

## Perguntas de aprovação — NÃO PRESUMIR RESPOSTAS

**D1. Contrato do equilíbrio.** Adotar como definição técnica
de equilíbrio comercial F-072 o conjunto de fluxos
não negativos com oferta conservada, mercados
alcançáveis positivamente precificados, e
condições de complementaridade
  lambda_g >= S_gc(x), x_gc*(lambda_g-S_gc(x))=0
para TODAS as rotas válidas de cada produtor?

Recomendação do review: **SIM**, como especificação de
candidato de arquitetura, não como prova de existência
ou promessa de solução numérica.

**D2. Separação entre certificado estático e gate dinâmico.**
Permitir que um futuro solucionador reconheça um estado de
equilíbrio aproximado por GAP global e pior grupo
originais mais contrato físico/R14 e route-separation,
SEM exigir mover artificialmente um equilíbrio para gerar
três novos passos? Continuar executando replays,
perturbações de robustez e métricas de preço/carga/Q
como testes DINÂMICOS independentes.

Recomendação: **SIM, sob um novo gate a ser formalizado
e aprovado antes do desenvolvimento**.
O gate R24 anterior e os NO-GO R38/R39/R40 permanecem
historicamente válidos; nenhuma homologação passada
é revisada automaticamente.

**D3. Existência e singularidade de Q=0.** Autorizar
a análise teórica e numérica de condições de
existência e cobertura física dos mercados, sem
substituir P=sqrt(D/Q) por preços com piso ou criar
consumo obrigatório/estoques para fechar equações?

Recomendação: **SIM**. Se a análise apontar um
problema econômico estrutural real, apresentar
opções de regra com impactos de jogabilidade para
decisão específica, antes de implementá-las.

**D4. Retorno à engenharia.** Somente depois de D1–D3
formalmente aprovados, escolher uma arquitetura
de resolução de complementaridade (com geração de
colunas/oráculo de separação) e seus critérios de
aceitação. Isso NÃO é autorização automática
para uma quarta família tipo R38–R40, não é
decisão de algoritmo específico, nem merge.

Recomendação: **manter pendente** até aprovação do
contrato e revisão de hipóteses.

## Critérios para aceitar o documento ADR-041

1. O PR deve apontar como base apenas a branch Draft
   R40, sem alteração de qualquer arquivo econômico.
2. Revisão deve distinguir fatos observados em
   R20/R14, demonstrações matemáticas nos dois
   exemplos e hipóteses abertas de engenharia.
3. Nenhuma solução de VI, MCP, NCP, espelhamento,
   penalidade ou raiz é declarada implementada.
4. Nenhum limite numérico anterior é alterado
   silenciosamente, inclusive o gate de R24.
5. Aprovação do ADR não ratifica F-059,
   não autoriza produção, não prova existência
   ou unicidade e não move a branch main.
6. A futura proposta técnica deverá ser falsificável,
   reproduzível e compatível com o C# do jogo.

**Situação:** revisão documental publicada como Draft,
contrato proposto e D1–D4 AINDA NÃO APROVADOS pelo usuário.
O próximo checkpoint deve ser aprovação ou ajuste dessas
decisões; não abrir outro ensaio numérico por inércia.
