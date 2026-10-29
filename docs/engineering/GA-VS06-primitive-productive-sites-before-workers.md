# GA-VS06 — Instalações primitivas explícitas antes dos trabalhadores

Data: 29/10/2026. **Incremento funcional do laboratório**, não
implementação de matemática de investimento, capacidade ou
produtividade definitiva.

## Motivo

O usuário estabeleceu que **oportunidades de trabalho surgem da
infraestrutura**, não da simples presença de trabalhadores.
Uma fazenda deve existir antes que seus trabalhadores possam
produzir, mas ainda não há modelo acordado de investimento físico,
insumos, vagas, capital ou dinheiro. O GA-VS05, que já possui
alocação e demanda no tile físico, foi preservado como contrato
de regressão e adaptado apenas onde isso melhora a prova funcional.

## Regras implementadas neste recorte

1. O modo `requirePrimitiveSites: true` na sessão
   `EconomicPhysicalTacticalWorkerSession` ativa a regra
   **instalação antes da alocação**. Sessões legadas VS03/VS05
   continuam com o atalho antigo quando o parâmetro não é
   informado, somente por compatibilidade de testes.
2. `TacticalPrimitiveProductiveSite(TileId, Activity)` representa
   uma oportunidade produtiva **em um tile físico canônico**.
   Não existe cidade, setor estratégico produtivo nem unidade
   econômica no centro de um hexágono estratégico.
3. `InstallPrimitiveSite(tile, atividade)` exige terra
   trabalhável e potencial local positivo do recurso da atividade.
   É uma ação explícita, de custo material **zero** para
   roçado primitivo, coleta de madeira e coleta de pedra.
   Não cria trabalhadores nem produz recursos automaticamente;
   locais iguais/duplicados e terrenos incompatíveis são
   rejeitados antes de mutação.
4. No cenário de início são **pré-instalados** no tile inicial
   os três tipos primitivos que sustentam os oito trabalhadores
   originais (4 agricultura, 2 madeira e 2 pedra).
   Esse estado é uma **condição de cenário**, anterior ao
   emprego, sem revisão econômica extra nem custo oculto.
5. `MoveWorkers` no novo modo exige `HasPrimitiveSite`
   para a atividade de destino ANTES de qualquer mutação.
   Se não existe oportunidade correspondente, o trabalhador
   não pode ser alocado ali. A soma da população permanece
   invariável; cada trabalhador continua produtor e consumidor.
6. Revisões aumentam separadamente por instalação e por
   transferência. `Preview` continua somente estimativa
   sem estoque/mercado/turno liquidado. Instalar site
   **não altera a produção** enquanto não houver gente.
7. O Godot original (`tools/GlobalArena.Godot`) agora exibe
   **1. ATIVAR INSTALAÇÃO PRIMITIVA (GRÁTIS)** e
   **2. ALOCAR TRABALHADORES NA INSTALAÇÃO**.
   O destino selecionado informa se a atividade escolhida
   já tem local de trabalho, se cabe a ativação gratuita ou
   se o terreno é incompatível. O botão de alocar só habilita
   quando já existe uma instalação correspondente.
   Os contornos visuais e a geometria plana são reutilizados
   da interface existente.

## Parâmetros propositalmente NÃO definidos

A etapa posterior terá de construir uma relação matemática
fundamentada, testável e equilibrada entre:

- `K`: investimento físico acumulado na instalação (materiais,
  eventualmente dinheiro), desgaste e evolução tecnológica;
- `L`: vagas geradas, limite de pessoal alocável, utilização
  efetiva e potenciais retornos marginais decrescentes;
- `G`: potencial do tile (solo, vegetação, minério, água,
  relevo, clima) e restrições naturais;
- `I`: disponibilidade de insumos e energia, quando
  requeridos pela atividade;
- `Q`: produção resultante, sensível aos fatores acima;
- custos explícitos de construção, manutenção e operação,
  sem introduzir crédito/bancos no MVP.

**Não foi escolhida nenhuma fórmula `Q=f(K,L,G,I)`,**
elasticidade, limite de vagas, produtividade adicional, custo
financeiro ou coeficiente de saturação. Em particular, a
instalação primitiva desse laboratório **ainda não impõe
capacidade de emprego** — a existência da oportunidade,
não o número de vagas, é o gate atual. Não confundir
essa lacuna com vagas infinitas como decisão de projeto.

As curvas de preço e logística existentes no motor não devem
ser reaproveitadas arbitrariamente para investimentos
agrícolas sem uma modelagem própria.

## Verificação

- Testes em G(2,0)→G(12,0) e G(3,0)→G(18,0).
- Alocar antes da instalação falha atomicamente;
  a instalação gratuita eleva revisão sem criar
  pessoas, oferta ou demanda.
- Transferir três trabalhadores somente após instalação
  mantém população 8=5+3 e demanda alimentar total.
- Um roçado não autoriza automaticamente trabalho florestal;
  atividade precisa de sua **própria instalação**.
- Tiles estrangeiros, incompatíveis e duplicações
  são rejeitados sem efeitos.
- Sessão legada continua reproduzível; nova sessão
  reexecuta o mesmo estado de maneira determinística.
- Build da interface Godot .NET original em Windows e
  Linux. **Teste visual manual Windows permanece pendente**
  até observar o fluxo em Godot 4.7.2.mono.

## Próximo ROI

Após validar a sequência **ativar instalação → alocar**
na interface original, preservar o trabalho no cliente
principal e avançar à integração canônica versionada
de trabalhadores, instalações, mercado/estoques e
turnos econômicos. Definir as relações matemáticas
de investimento/produtividade **no momento em que
elas passarem a determinar decisões de jogo**, com
testes de equilíbrio, sem expandir para HA, finanças,
bancos ou infraestrutura desnecessária.

**Marketing:** MKT-00 permanece planejamento leve;
este laboratório ainda não destrava MKT-01.
