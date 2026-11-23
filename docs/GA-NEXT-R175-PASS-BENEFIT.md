# GA-Next R175 — Valor econômico real por passagem do coordenador

## Escopo
A R174 demonstrou ganho líquido de 0,71% e 2,35% após estabilização, com travessias físicas reais. A R175 observa separadamente o estado inicial (passagem zero) e o estado após **cada passagem completa**, sem mudar o water-filling, o ponto fixo, o limite de passagens ou os preços de produção.

## Instrumentação
Uma entrada **interna e somente diagnóstica** do coordenador entrega leituras imutáveis quando há observador explicitamente configurado; no caminho canônico, o observador é nulo e não se constrói nenhum snapshot adicional.

Cada observação contém:
- índice da passagem (0 = distribuição histórica inicial);
- quantidades por mercado físico e mercadoria;
- custo de frete físico calculado das mesmas árvores de caminho por vendedor e da conversão canônica de carga;
- quantidade transferida naquela passagem.

Os testes R174 reconstroem a receita bruta com a função canônica p = floor(1e6 sqrt(D/Q)) para cada estágio, subtraem o custo de frete e verificam que o primeiro e o último coincidem exatamente com os planos auditados existentes. Não são criadas novas mercadorias nem mercados e nenhum registro financeiro é publicado.

## Guardrails
- Não há congestionamento, nova equação de preços, parada antecipada, comparação monetária aproximada ou redução de tolerância.
- Nenhuma decisão de realocação depende do observador; somente a medição usa tempo e snapshots.
- Uma passagem final sem movimentação é uma certificação do ponto fixo; o ganho dela deve ser zero.
- Os dois cenários medidos ainda são pequenos. A simplificação da economia dependerá de considerar o ganho marginal por passagem também em casos maiores, não apenas de um número isolado.
- Windows e Linux continuam obrigatórios. macOS permanece auditoria opt-in. PR Draft sem merge.
