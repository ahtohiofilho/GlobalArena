# GA-Next — Roadmap incremental após R139

Status: planejamento de produto; **não autoriza implementação antecipada**. Registrado em 21/11/2026, após aceite visual provisório da R139.

## Sequência natural de desenvolvimento

1. **Ciclo corrente — jogabilidade vertical.** Consolidar a interação do jogador com localidades/nós físicos, trabalhadores, atividades, investimentos e resultados dos ciclos econômicos com dados autoritativos. Manter o mapa tático e o atlas como navegação, sem retrabalho cosmético sem demanda concreta.
2. **Tela da cidade (ciclo de gestão local).** Implementar quando o núcleo tiver contratos estáveis de localidade, trabalhadores presentes, atividades, produção, demanda, receitas e ações locais. A cidade é uma localidade/nó físico canônico, sem gerar uma segunda simulação nem dados fictícios. A tela deve permitir leitura e decisões locais (alocação e investimentos existentes) e ser acessível a partir do ponto da cidade no mapa. A nomenclatura definitiva de cidade/localidade será definida antes de cristalizar a UI.
3. **Tela da civilização (ciclo de gestão agregada).** Implementar quando houver contratos maduros de caixa, receitas, impostos, trabalhadores, territórios, infraestrutura/propriedade, forças e decisões disponíveis. Separar indicadores agregados dos comandos locais e manter vínculo de navegação entre civilização, cidades e mapa.
4. **Integração e refinamento visual.** Garantir interoperabilidade entre ambas as telas e as visualizações tática/planetária, sem perder foco geográfico, zoom ou estado da simulação. Otimizar ergonomia, apresentação e desempenho a partir do jogo operável e de testes visuais, sem calibragem visual ou econômica final antecipada.

## Gates de aceite das telas

- Somente informações provenientes do estado canônico da simulação; sem estoque físico inventado, carteiras de compradores ou dinâmica econômica paralela.
- Decisões encaminhadas aos mesmos comandos/autoridade da jogabilidade existente; nenhuma mutação direta de dados pela UI.
- Fluxo de abrir/fechar e voltar ao mapa consistente; rolagem acessível em painéis verticais.
- Testes automatizados de integração + validação visual. Nenhuma tela extensa antes de estar respaldada pelas mecânicas que expõe.

## Escopo imediato

Prosseguir naturalmente na jogabilidade e no ciclo econômico. As telas de cidade e civilização são compromissos do roadmap, **não tarefas do próximo PR**.
