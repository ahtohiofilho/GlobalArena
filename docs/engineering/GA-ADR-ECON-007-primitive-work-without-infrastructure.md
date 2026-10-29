# GA-ADR-ECON-007 — Trabalho primitivo sem infraestrutura obrigatória

Data: 29/10/2026. **DECISÃO CANÔNICA VIGENTE.** Retificação da
tentativa GA-ADR-ECON-006/GA-VS06: aquela hipótese de instalar
explicitamente um roçado ou ponto de coleta antes do trabalho
rudimentar foi **rejeitada** por contrariar a diretriz anterior
do projeto. Preservar o GA-VS05 como referência funcional.

## Regra da fase arcaica/neolítica inicial

**Basta alocar trabalhadores ao tile físico tático apropriado e à
atividade primitiva para iniciar produção rudimentar.**

- **Agricultura primitiva:** pessoas cultivam diretamente solo
  utilizável, sem exigir uma fazenda ou roçado registrado como
  infraestrutura, sem custo de implantação.
- **Madeira:** trabalhadores coletam madeira disponível no tile
  sem ponto de coleta, serraria, construção ou estoque prévio.
- **Pedra:** trabalhadores coletam pedra disponível no tile
  sem mina, pedreira construída ou capital inicial.
- A produção depende da presença de trabalhadores, da atividade
  escolhida e dos potenciais físicos/geográficos do tile.
  Sem potencial admissível não há produção daquele recurso,
  mas não se cria uma instalação fictícia para habilitá-lo.
- **Os mesmos trabalhadores residem, produzem e geram demanda de
  consumo**. Não duplicar habitantes/residentes e produtores.
- **Não existem cidades como entidades do jogo.** A concentração
  futura de pessoas, infraestruturas e atividades é emergente.
- A malha estratégica não possui população nem economia
  independente: exibe agregações da única realidade física tática.

**Não implantar tampouco exigir uma estrutura invisível/gratuita
obrigatória para os três trabalhos primitivos.** Isso reintroduziria
a mesma pré-condição de forma disfarçada.

## Infraestrutura posterior

Infraestrutura produtiva terá significado **quando conferir
capacidades que o trabalho primitivo não consegue alcançar**,
como maior produtividade, operações complexas, processamento
de matérias-primas, transformação energética, qualidade,
economias de escala ou vagas/limites próprios.

Exemplos futuros: fazenda aprimorada, mina equipada, serraria
e oficina. A infraestrutura é **construída e localizada em um
tile físico**, não uma cidade ou uma propriedade do setor
estratégico. A implantação poderá consumir recursos e exigir
tecnologia/dinheiro/tempo conforme regras futuras.

Uma instalação construída pode gerar oportunidades especializadas,
mas **não revoga a possibilidade de trabalho rudimentar
independente**, quando o tile conservar condições naturais.
O trabalhador não gera uma fazenda por estar presente;
ele simplesmente pode produzir rudimentarmente sem ela.

### Modelo matemático: pendência deliberada

Em momento posterior, especificar, justificar e testar
as relações entre:

- `L`: trabalhadores ativos e sua distribuição;
- `G`: produtividade natural do tile e disponibilidade
  do recurso;
- `K`: investimento físico efetivo e tipo de instalação;
- `I`: insumos e energia;
- `T`: tecnologia, capacidade e manutenção;
- `Q`: quantidade produzida.

A função rudimentar poderá operar **com `K=0`**, quando
`G` permitir. A infraestrutura posterior pode alterar
`Q`, os limites e as atividades disponíveis. **Nenhum
coeficiente, limite de vagas, preço, orçamento ou fórmula
para aumento de produtividade foi decidido aqui.**

A condição de possibilidade de produzir deve preservar a
recuperação de uma civilização sem madeira/pedra estocadas;
não criar loops circulares de pré-requisitos. Evitar
bancos, empréstimos e crédito no MVP, como já definido.

## Situação de implementação e roadmap

- **GA-VS05 / PR #92:** comportamento funcional a preservar.
  Trabalhadores são alocados diretamente em tiles físicos
  táticos e geram oferta preliminar e demanda. Ainda é um
  laboratório em memória, sem tempo/custo de deslocamento,
  mercado liquidado, estoque ou save canônico.
- **GA-VS06 / PR #93:** **fechado sem merge / não integrar**.
  O requisito de instalação arcaica explícita era um desvio
  conceitual e não deve virar regra nem baseline futuro.
  Não há necessidade de testar esse build no Windows.
- **Próximo ROI:** validar completamente VS05 e integrar
  trabalhadores aos estados/turnos canônicos; avançar à
  produção/consumo e estoques reais. Quando instalações
  aprimoradas entrarem no ciclo econômico, definir e testar
  sua modelagem matemática antes de implementá-las.
- **Marketing:** MKT-00 preparação leve, MKT-01 ainda
  depende de um vertical slice jogável e apresentável.

**Regra de governança:** não reapresentar a ativação gratuita
obrigatória de roçado/ponto de coleta como condição de emprego
primitivo. Se a regra for reconsiderada, exigir uma decisão
explícita que substitua este ADR.
