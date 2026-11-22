# GA-Next R166 — tempo de deslocamento sensível à infraestrutura

## Origem dos parâmetros

A malha tática Goldberg já define a **aresta como unidade lógica de percurso**, sem quilômetros físicos ou comprimento variável introduzido artificialmente. A R165 já reutiliza o validador autoritativo `PhysicalTacticalWorkerMoveEventExecutor.CheckEdge`, inclusive suas exclusões da superfície física assinada.

A infraestrutura existente (R112) já registra por aresta a cotação física natural `C(0)`, o investimento monetário confirmado `I` e o preço físico por unidade de mercadoria `C(I)`, com retorno marginal decrescente. Esse preço é usado para **frete**, mas NÃO constitui tempo em turnos, velocidade, pedágio ou distância.

## Regra candidata explícita para tempo

A R166 reutiliza apenas a **proporção física de melhoria** que a infraestrutura já produz:

```
tempo(I) = max(1 microturno,
               ceil(tempo_natural * C(I) / C(0)))
```

`tempo_natural` vem da política `FoundationGroundEdgeTravelTimePolicy`, obrigatoriamente configurada. A R166 não escolhe uma velocidade inicial oculta: um teste pode solicitar 1,4 turno como exemplo, mas esse não é o tempo universal de todas as travessias. A fórmula da infraestrutura também não depende de carga transportada, dinheiro de pedágios, propriedade ou preço de mercado.

Esse é um **primeiro contrato de acoplamento entre melhoria física e tempo**, não a homologação dos valores finais de deslocamento do jogo. O efeito é determinístico, em inteiros exatos e não cresce com investimento. No futuro, a calibração da política de tempo poderá respeitar modificadores já aprovados para tipos de unidade, sem inventar distâncias cartográficas.

## Implementação

- `FoundationGroundEdgeTravelTimeProjector.Quote` consulta uma aresta **real** no catálogo de transportes do `WorldState`, depois de validar a mesma aresta pelo executor tático autoritativo e rejeitar travessias proibidas.
- `FromInfrastructure` isola a conversão inteira da proporção física existente para tempo de travessia, permitindo validar monotonicidade e independência dos pedágios sem alterar o tesouro.
- `Start` confirma que o trabalhador pertence ao estado canônico fornecido e transmite o tempo calculado ao progresso R165. A R165 continua responsável pelo tempo restante a cada turno, pela chegada e pelas fatias R164 de esforço, presença e demanda.
- O teste integrado utiliza trabalhador e aresta reais, confirma geração de progresso, ausência de mutação no hash e redução de tempo sob investimento em casos analíticos sem mudar o estado autoritativo.

## Limites deliberados

R166 é **pré-commit**: não modifica o `WorldState`, não debita dinheiro, não implanta trabalhador em movimento, não registra o progresso no diário e não substitui a ordem de movimento v10/v11 já autorizada. O valor natural de duração continua sendo uma política de deslocamento **explícita**, e não é inferido da moeda. Também não há custo de congestionamento ou logística sombra na regra de tempo.

Próximo incremento natural: registrar progresso entre turnos e integrá-lo ao resolvedor de movimento, exigindo evento journaled e replay; depois, consumir as fatias resultantes na economia autoritativa. Nascimentos efetivos permanecem separados da projeção demográfica física da R164.
