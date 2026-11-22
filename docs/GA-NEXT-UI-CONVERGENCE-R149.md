# GA-Next — Diretriz de convergência da interface (R149)

**Status:** referência estrutural aprovada; tema e desenho visual permanecem abertos. Complementa `GA-NEXT-ROADMAP-POST-R139.md`, sem substituir gates econômicos e de jogabilidade.

## Norte de produto

A interface principal é o **mapa**. Quando o painel operacional direito está fechado (TAB), o mapa ocupa praticamente todo o espaço útil e mantém somente controles essenciais de navegação e estado. Com TAB aberto, uma **coluna à direita** auxilia as operações iniciadas no mapa: selecionar indivíduos/nós/arestas, escolher ações elegíveis, preparar ordens, mostrar pendências, cancelar e encerrar turno. Ela **não é** um dashboard de dados econômicos extensos.

As superfícies de navegação previstas são:
1. **Mapa tático** (área central da experiência).
2. **Coluna operacional contextual** à direita (TAB; prioritariamente comandos, não relatórios).
3. **Menu da partida** (opções de sessão e sistema).
4. **Planeta físico em perspectiva** (visão global/estratégica, acesso próprio).
5. **Janela da civilização**, com abas e indicadores agregados.
6. **Janela da cidade/localidade**, com abas e decisões locais.
7. **Janelas especializadas somente quando justificadas**, por exemplo rota/unidade/batalha/infraestrutura.

Planeta, cidade, civilização e menu são superfícies distintas da coluna operacional. Seus indicadores não devem crescer indefinidamente dentro da coluna. Nomenclatura final de cidade/localidade ainda não foi canonizada.

## Comportamento vinculante da interface

- O mapa e a seleção contextual comandam o fluxo operacional; a coluna dá suporte ao comando em elaboração.
- O painel direito tem rolagem utilizável em telas menores e pode ser fechado sem perder contexto de jogo ou mutar o mundo.
- Toda ordem chama o **host autoritativo** existente, permanece pendente antes do fechamento coletivo e pode ser cancelada conforme a mecânica vigente.
- Toda informação exibida sobre trabalhadores, localização, caixa, investimentos, consumo e frete vem de estado ou recibos reais, nunca de dados ilustrativos do mockup.
- A representação visual do mockup é **referência conceitual**, não contrato de cores, ícones, proporções, números, datas, cidades ou topologia do jogo.
- Não duplicar a mesma operação em várias telas com regras divergentes; ações podem ser acessíveis de superfícies diferentes, mas compartilham a mesma autoridade.
- A ordem de trabalho continua sendo: **mecânica → integração → verificação → refinamento visual**, evitando redesenho cosmético antes de existir função.

## Trilha incremental

**R149 (atual):** acrescentar à coluna operacional de debug ações reais de designação LAND/PORT e retorno à produção para o trabalhador selecionado, com feedback de ordem pendente e sem criar uma nova janela. Preservar a leitura do mapa, o TAB e o fechamento por E.

**Próximas etapas:** trabalho operacional afetando efetivamente a produtividade logística somente após fórmula aprovada; harmonizar controles de trabalhador/rota/obra em operações contextuais; reduzir o texto de diagnóstico na coluna; implementar janelas de civilização e cidade quando seus contratos de dados e comandos estiverem prontos.

Nenhuma mudança do tema gráfico ou abertura de janela plena de civilização/cidade é requisito da R149.
