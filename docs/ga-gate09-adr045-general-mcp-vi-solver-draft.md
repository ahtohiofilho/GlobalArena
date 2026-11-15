# Global Arena — ADR-045: solucionador MCP/VI multigrupo e geração global de rotas

**Estado:** prova de conceito experimental em Python; sem homologação C#/Godot. **Data:** 15/11/2026.
**Baseline:** ADR-044 PR #333, ADR-043 PR #332, certificador estático ADR-042 PR #331. Todas as regras econômicas originais permanecem intactas.

## Implementação concreta — não mais um diagnóstico

O núcleo encontra raízes de complementaridade para vários produtores, mercados e rotas, em vez de repetir as atualizações de melhor resposta R38–R40. Usa H_gc(x) = (1+C_gc(x))*sqrt(Q_m(x)/D_m) e oferta física conservada por produtor.

Para cada grupo g, uma coluna âncora recebe o saldo de sua oferta. As outras quantidades são variáveis independentes. Para cada rota i e multiplicador de grupo mu_g, a equação suavizada de Fischer–Burmeister é:

    slack_i = H_i(x) - mu_g
    FB_i = sqrt(x_i² + slack_i² + 2*epsilon²) - x_i - slack_i = 0.

O número de equações coincide com as incógnitas de fluxo independente mais multiplicadores por grupo. As derivadas do Jacobiano são analíticas e incluem os efeitos cruzados de quantidade/mercado, custo de congestionamento quartico e tarifa fixa por vendedor. A correção usa Levenberg–Marquardt regularizado, sistema linear com pivoteamento e busca de passo que rejeita perda de oferta, fluxo negativo, preço indefinido e ausência de melhora do resíduo. Continuação de suavização declarada: epsilon = (1e-3, 1e-6, 1e-9). Convergência NÃO presumida.

## Geração de colunas e Q=0

A cada avaliação de rotas, uma varredura por menor caminho específico do vendedor considera todos os mercados compatíveis alcançáveis. O conjunto ativo inclui rotas com produção efetiva, inclusive não-canônicas, mais as rotas canônicas. Após resolver o MCP restrito, refaz a varredura no estado novo; qualquer rota omitida com custo H significativamente inferior é adicionada e o MCP é novamente resolvido. Não apagar rotas financiadas.

Um mercado inicialmente alcançável com Q=0 recebe somente uma redistribuição de até 1e-6 da própria oferta de um grupo viável, subtraída da rota doadora. É inicialização NUMÉRICA fisicamente conservadora, nunca renda fictícia, preço artificial ou aceitação de equilíbrio com Q=0. O certificado final continua exigindo preços P=sqrt(D/Q) originais e Q>0.

## ÚNICO critério de aprovação econômica

Mesmo quando o resíduo MCP cai, isso NÃO é aprovação. Um resultado PASS precisa do certificador estático ADR-042 independente sobre a alocação final: oferta totalmente conservada, preços originais finitos, separação de todas as oportunidades, R13/R14, F084F, e gaps comerciais originais global e do pior produtor <=1e-4. F-059 continua pendente; nenhum resultado altera C#/Godot nem main.

## Limites predeclarados

| Unidade de trabalho | Limite padrão |
|---|---:|
| Avaliações H totais (inclusive rejeitadas) | 1200 |
| Scans de menor caminho | 320 |
| Certificações ADR-042 completas | 6 |
| Colunas ativas | 192 |
| Gerações de rotas | 4 |
| Atualizações Newton aceitas | 60 |
| Testes de backtracking por correção LM | 14 |

Todos os custos são contabilizados. Falha, teto excedido, preço inválido ou certificado independente FAIL causam NO-GO, jamais publicação de um estado econômico não aprovado.

## Validação e estratégia de escalabilidade

- Controles analíticos: dois grupos e dois mercados inicialmente desequilibrados, dois grupos em equilíbrio existente, um produtor com três mercados, dois grupos com rota alternativa antes não financiada e começo com mercado vazio.
- Cenários reais do laboratório: 42 nós original e 42 nós com demanda/oferta heterogêneas e capacidade K×0,20, com resultados PASS ou NO-GO explícitos. Perfil extended inclui 162 nós com orçamento de passos reduzido. Nenhum resultado desfavorável pode ser omitido.
- Gate inicial: a CI deve validar integridade e solução efetiva dos DOIS cenários controlados de múltiplos grupos (incluindo um inicialmente desequilibrado). Isso não substitui o benchmark de escalabilidade.
- Gate de produto posterior: desempenho em mapa médio, determinismo, paridade C#, integração Godot e política tarifária financeira F-059 decidida separadamente.

**Governança:** PR Draft empilhado sobre #333, sem merge. Contratos econômicos e provas históricas R20–R44 inalterados. A aprovação da POC não equivale a autorização de produção.
