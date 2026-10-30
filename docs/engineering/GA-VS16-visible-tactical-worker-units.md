# GA-VS16 — Grupos produtivos selecionáveis e trajetos visíveis no tático

Data: 30/10/2026. Base: VS15 PR #112. PR incremental e draft, sem merge na main.

## Escopo de experiência
- Os **grupos produtivos existentes** aparecem como marcadores discretos no **mesmo tile físico 01C** do renderizador Godot. Verde: agricultura, marrom: madeira, cinza: pedra.
- Quando existem múltiplas atividades no mesmo tile, seus pontos são distribuídos dentro da célula. **Clique no ponto** seleciona a atividade como grupo de origem do comando já existente. Clique no resto da célula mantém seleção normal de tile. Não cria novo tipo de entidade canônica.
- O HUD registra a atividade selecionada e os trabalhadores disponíveis. O máximo numérico é limitado ao efetivo daquele grupo; o botão de ordem é desativado se não houver grupo válido ou destino adjacente selecionado.
- Cada viagem V13 é representada por uma faixa âmbar sobre a aresta entre origem e destino e um marcador âmbar que avança pela faixa em cada turno (0%, 50%; ao chegar, some e reaparece como grupo ativo no destino). Não é animação contínua no tempo real.
- Apenas geometria visível na janela tática é desenhada; a UI usa centros dos **polígonos já desenhados**, sem reprojetar nem inventar posição, mapa ou tile.
- `TacticalWorkerMarkerProjection` contém **somente um snapshot derivado** das alocações e viagens do estado físico, com uma checagem independente de conservação da população. Não altera WorldState, dinheiro, produção, caminho, relógio ou save.

## Persistência e arquitetura
- **Não há nova versão de save nem de hash.** O V13 permanece dono dos comandos, ordens e da duração de viagens; reconstruímos as posições visuais a partir da revisão reaberta.
- A via de seleção `WorkerGroupPicked` apenas atualiza o grupo ativo no controle Godot; emissão de ordem ocorre exclusivamente pelo botão (comando V13 existente). Nenhuma mutação por simples clique.
- Suprimento vinculado à origem e duração fixa de dois turnos do VS15 são preservados, ainda provisórios.
- Evitar sobrecarga gráfica: um pequeno marcador por grupo produtivo, não um ícone por pessoa. Grupos dispersos fora da janela não aparecem; quando um tile volta à visão, são regenerados.
- Demanda basal universal #103 e poder de compra endógeno #111 seguem congelados; não implementar salários nem sistemas financeiros extras.

## Verificação
1. Abrir VS16 no Windows. Iniciar laboratório e observar três marcadores diferenciados no tile inicial.
2. Clicar no marcador **verde**: origem Agricultura com 4 trabalhadores. Clicar no marcador marrom: Madeira com 2; conferir que a seleção não avança turno nem altera revisão.
3. Clicar num tile adjacente e ordenar **2 trabalhadores de Agricultura → Madeira**. Confirmar a faixa âmbar e grupo em trânsito 2, sem chegada instantânea.
4. Avançar um turno: marcador âmbar a meio caminho, ETA 1. Segundo turno: marcador desaparece da faixa e grupo Madeira aparece no destino.
5. Salvar durante o trajeto; reabrir V13; conferir que faixa, marcador e seleção recuperam do estado persistido (seleção UI não precisa ser serializada); sem duplicação.
6. Código compila/roda Linux e Windows; testes da projeção conferem identidade canônica, 3 grupos iniciais, progressão 0/0,5/chegada e replay V13. Homologação visual manual continua necessária e deve documentar eventuais defeitos de sobreposição/legibilidade.

## Fora de escopo
Não são sprites animados individualmente, seleção de unidades militares, filas de ordens, rotas multi-aresta ou combate. A issue #105 permanece parcialmente aberta. A interface definitiva continua acompanhada pela issue #107.
