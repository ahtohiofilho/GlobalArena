# GA-Next — direção visual: contagem, identidade, cidade e inglês

**16/11/2026 · PR #378 · incremental e reversível**

## Implementação deste checkpoint

- **Números sempre de cabeça para cima:** o desenho sete-segmentos
  das bolinhas VS16 foi corrigido com eixos no plano da superfície
  alinhados a *screen-right/screen-down*, calculados pela orientação
  real da câmera ortográfica inclinada. O antigo "3" parecia "E"
  por causa de uma inversão simultânea nos dois eixos. A correção
  NÃO introduz Label3D flutuante, muda a câmera ou altera os tiles.
  A malha dos números se recompõe na rotação aprovada e no
  `NORTH/RESET`. Um disco sem número representa um indivíduo,
  como antes.
- **Cor da civilização:** paleta de teste provisória **RGB primário**:
  CIV 1 = vermelho, CIV 2 = azul, CIV 3 = verde. A terceira é
  apenas um token, **não cria uma nova civilização** na simulação.
  Um aro **externo** identifica a civilização de cada grupo;
  o **interior** continua indicando ocupação:
  Agricultura verde, extração vegetal marrom, mineração cinza.
  Aro âmbar de deslocamento e halo branco de seleção continuam
  canais separados. A janela do tile mostra também CIV-ID,
  nome da cor e faixa cromática, evitando depender só das cores.
  A paleta é ajustável conforme o mapa e a acessibilidade.
- **Interface atual preservada:** clique no tile habitado, clique
  na atividade, clique na pessoa, Fechar/Esc; nenhuma expansão
  por hover. O valor do censo, o número seis por civilização do
  cenário, as três ocupações 2/2/2 e os eventos do turno não mudam.

## Direção futura — ainda HIPÓTESE de interface

### Organização em espaços / grupos

O painel local poderá apresentar três zonas visuais
(`Agriculture`, `Forestry`, `Mining`), cada uma com ícone
e contagem, e "bonequinhos"/fichas individuais com ID, tarefa,
status e, quando aplicável, capacidade. Não renderizar ficções
econômicas: cada peça representa `PhysicalWorkerId` real;
se não houver ninguém num setor, mostrar espaço vazio e 0.

### Arrastar e soltar trabalhadores

O gesto proposto é:
1. Selecionar/arrastar uma peça de trabalhador de um setor
   para outro, mantendo o mapa e a localização do tile.
2. Mostrar **preview** (por exemplo, mudança 2→1 e 2→3),
   claramente marcado como **ordem pendente**, não censo
   efetivamente atualizado.
3. Soltar chama a **mesma intenção** de realocação v11 que
   os botões A/V/N; o núcleo valida proprietário, ordem
   existente, papel e período. Não permitir mover trabalhador
   de outra civilização.
4. **X** descarta pedido; **E** efetiva turno coletivo e
   somente então publica novos números do censo.
5. O gesto precisa ter alternativa por clique/teclado para
   mobilidade, acessibilidade e operação de grandes grupos.

Apenas desenhar uma peça noutra caixa não muda `WorldState`.
O modelo não deve permitir que a UI execute produção, transporte
ou imposto em separado do journal.

### Identidade civilizacional vs. ocupação

Civilizações são entidades persistentes com ID autoritativo.
**Cores e nomes são apenas skin de cliente**, incluindo seleção
de contraste, acessibilidade e jogos com mais que três
civilizações. Não usar cor RGB como código que participe do
hash, economia ou posse. Se um jogador ativar modo daltônico,
a identidade numérica/textual deve sobreviver.

### Nomenclatura e internacionalização

**Inglês é o idioma de produto objetivo** (exemplos:
`Population`, `Agriculture`, `Forestry`, `Mining`,
`Local Treasury`, `Orders`, `End Turn`, `Workers`).
Texto português é aceitável temporariamente no protótipo.
Ao escalar a interface, centralizar as strings em chaves
substituíveis, em vez de traduzir cada arquivo na mão.
As versões locais e de rede devem apresentar os mesmos
identificadores e números, independente do idioma escolhido.

### Gestão do tile habitado

Por enquanto, o clique seleciona/abre o menu; **a Cidade**
é um nome de trabalho para núcleo habitado, não um objeto
do mundo. A futura tela administrativa poderá dispor:
população/trabalho, produção física, tesouro/infraestrutura
e controle, em área lateral que redimensiona o mapa.
Não criar hoje uma cidade autônoma por cada tile físico.

## Gates de aceitação futuros

- Os numerais 2, 3, 4 e 12 são inequívocos em pelo menos
  duas orientações de câmera e diferentes níveis de zoom.
- Paleta de civilização se distingue da paleta de atividade,
  do mar e da seleção; conservar ID/nome junto à cor.
- Drag/drop muda somente ordem pendente até E, sem duplicar
  trabalhador e com replay igual ao atalho A/V/N.
- A janela de cidade opera com clique estável, não hover,
  e funciona para populações muito maiores que seis
  via agregação/paginação.

**Governança:** a direção "plataforma + clientes" está
documentada em `ga-next-authoritative-platform-civilization-client-contract.md`.
A paleta RGB e a interface de arrastar e soltar são
hipóteses visuais, não decisões irrevogáveis.
