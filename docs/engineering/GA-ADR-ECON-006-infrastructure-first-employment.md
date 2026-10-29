# GA-ADR-ECON-006 — Infraestrutura produtiva antes da ocupação de trabalhadores (SUPERADO)

> **HISTÓRICO — SUPERADO EM 29/10/2026. NÃO É DECISÃO VIGENTE.**
> Esta hipótese de exigir instalação produtiva antes de todo trabalho
> foi abandonada. Para agricultura rudimentar e coleta arcaica de
> madeira/pedra, **nenhuma infraestrutura é necessária**: basta
> alocar trabalhadores em tile físico tático adequado. Consulte a
> decisão canônica [GA-ADR-ECON-007](GA-ADR-ECON-007-primitive-work-without-infrastructure.md).
> O experimento GA-VS06 / PR #93 foi fechado sem merge; manter
> GA-VS05 / PR #92 como base. O conteúdo abaixo permanece apenas
> como registro histórico de uma opção rejeitada.

Data: 29/10/2026. **Decisão de direção canônica**, ainda **não implementada**
na sessão do laboratório GA-VS05. Compatível com os princípios do
GA-VS03/VS04 e com o MVP neolítico offline.

## Problema e correção conceitual

GA-VS03/VS05 oferecem um comando experimental
`MoveWorkers(origem, destino, atividadeAnterior, atividadeNova, quantidade)`.
Ele desloca e muda a atividade de trabalhadores já existentes e projeta
produção pela geografia. Isso foi útil para provar identidade física,
conservação de população, demanda e integração visual, mas NÃO deve se
tornar a mecânica definitiva de geração de produção.

**Decisão:** uma instalação/atividade produtiva existente no tile
físico tático cria **oportunidades de trabalho**; os trabalhadores
podem ser alocados nessas vagas. **Não** é a alocação de trabalhadores
que faz surgir automaticamente uma fazenda, mina ou serraria.

O fluxo pretendido é:

1. O tile físico tem condições/potenciais geográficos próprios;
2. O jogador (ou uma regra sistêmica futura) implanta uma infraestrutura
   produtiva admissível naquele tile, respeitando condições e insumos;
3. A infraestrutura determina sua atividade, capacidade de trabalho,
   produtividade e eventuais requisitos de operação;
4. Trabalhadores elegíveis são alocados nas vagas existentes;
5. A produção efetiva depende das pessoas **realmente trabalhando**,
   infraestrutura, condições físicas e insumos disponíveis;
6. As **mesmas pessoas** constituem a população que consome no tile
   onde reside, independentemente de sua atividade de trabalho.

**Não existe cidade como entidade obrigatória.** Uma concentração de
instalações, pessoas e infraestrutura é um fenômeno emergente.
A malha estratégica só agrega para visualização e navegação, sem
trabalhadores nem estruturas econômicas fictícias no centro do setor.

## Contrato mínimo futuro (quando promovido ao estado canônico)

Um registro físico de oportunidade produtiva deve ser associado a:
`PhysicalTacticalTileId`, tipo de instalação/atividade, capacidade
de vagas, trabalhadores alocados, estado operacional, tecnologia,
necessidades de construção, consumo de insumos/energia (se aplicável)
e manutenção (se aplicável).

Invariantes:

- Sem instalação válida e habilitada, não existem novas vagas daquela
  atividade no tile. A alocação não cria infraestrutura implicitamente.
- `trabalhadoresAlocados <= vagasDaInfraestrutura` e quantidade
  não negativa; vagas livres não produzem por conta própria.
- Uma pessoa não pode ocupar simultaneamente duas vagas nem gerar
  demanda em duplicidade. Trabalhadores produzem e consomem, como
  estabelecido.
- Instalações podem existir sem trabalhadores (ociosas), ou deixar
  de operar por falta de insumo, manutenção ou energia.
- Construção, contratação e **movimentação física** de pessoas são
  conceitos distintos. A instalação cria possibilidade de emprego,
  mas **não teletransporta trabalhadores**. A futura política de
  mobilidade definirá tempo, caminho, distância, travessias e custos.
- Mudança de atividade **dentro do mesmo tile** e viagem entre tiles
  devem ser ordens distintas quando houver turnos reais.
- A implantação deve ser atômica e auditável no estado canônico
  e em saves versionados, sem alterar a malha física por renderização.

## Neolítico: preservar a capacidade de começar e de recuperar

A decisão anterior sobre **agricultura primitiva sem insumo de
construção** e **coleta básica gratuita de madeira e pedra** continua
válida, para impedir o bloqueio circular "sem recursos não consigo
construir a infraestrutura que produz esses recursos".

Isso NÃO torna trabalhadores criadores de instalações:

- Um **roçado primitivo** pode ser implantado/ativado explicitamente
  em terra cultivável a **custo material zero**, criando vagas básicas;
- Um **ponto de coleta de madeira ou pedra** pode ser implantado/
  ativado onde existir potencial, igualmente sem custo material
  inicial. O recurso não é consumido de um estoque oculto antes
  de ser extraído;
- A implantação inicial pode fazer parte das condições de cenário
  ou de uma ação gratuita, **mas sempre precede logicamente** a
  alocação de pessoal;
- Fazendas, minas, oficinas e infraestrutura mais avançadas poderão
  exigir materiais, tempo, tecnologia, dinheiro ou outros requisitos
  quando o sistema correspondente existir. **Nenhum custo numérico**
  ou fonte de financiamento é estabelecido por este ADR;
- Zero custo de **implantação primitiva** não significa produção
  infinita, produtividade sem trabalhador ou manutenção gratuita
  de todas as instalações futuras.

## Sequência de implementação / gates de ROI

**Gate atual GA-VS05 (inalterado):** manter o comando de
alocação direta **somente como atalho de teste**, útil para
verificar o tile físico, a interface, conservação de pessoas e
prévia de consumo. Indicar claramente que não representa a
implantação definitiva de infraestrutura.

**Gate de estado canônico:** adicionar registros por tile físico
para instalações e vagas, sem cidades; separar implantação,
alocação e transferência. Os testes deverão rejeitar alocação
sem vaga, contratação superior à capacidade, criação
implícita de instalação e duplicação de pessoas; deverão
permitir instalações ociosas e atividades primitivas de
custo material zero. O componente de trabalho e o de
residência precisam continuar compatíveis com a decisão
de trabalhador único produtor+consumidor.

**Gate econômico seguinte:** ligar instalações à produção e
consumo físicos reais, insumos e estoque, custos de construção
e manutenção conforme avanço tecnológico, mercado e ciclo
econômico a cada seis turnos de movimento conforme planejamento;
depois ligar deslocamento de pessoas à política de mobilidade.
Não antecipar bancos, crédito e dívidas no MVP.

## Não objetivos neste momento

- Não reescrever a sessão de referência nem a interface validada
  do GA-VS05 nesta decisão documental;
- Não inventar novos preços, vagas, receitas, despesas ou limites
  financeiros antes de existir o respectivo modelo;
- Não inferir cidade/controle político por existência de fazenda;
- Não confundir a conclusão do laboratório com MVP jogável
  ou liberar marketing MKT-01 antecipadamente.

**Resumo canônico:** _a infraestrutura gera vagas; os trabalhadores
ocupam vagas, produzem e consomem. O laboratório atual abrevia
essa sequência provisoriamente, sem redefinir a regra do jogo._
