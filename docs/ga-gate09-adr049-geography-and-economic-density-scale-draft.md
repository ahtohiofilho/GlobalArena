# Global Arena — ADR-049: crescimento geográfico e densidade econômica máxima do R13

**Data:** 15/11/2026 · **Status:** laboratório executável / GO-NO-GO vinculado à CI
**Base:** ADR-048 PR #337, ADR-047 PR #336 e certificador ADR-042 original.
**Governança:** PR Draft empilhado em #337; sem merge, sem alteração do C#/Godot, F-072, F-084F, R13, tarifas, congestionamento ou F-059.

## Escopo e hipótese

Os experimentos anteriores demonstraram equilíbrio estático original em:
- Goldberg de 2.562 nós, 144 produtores, 48 mercados, no Ubuntu/Windows;
- preços originais P_m=sqrt(D_m/Q_m), custos c_free*(1+(B_e/K_e)^4) + tarifa, com gaps F-072 global e pior produtor <= 1e-4;
- cerca de dois minutos por ciclo NUMÉRICO completo no executor GitHub Actions Python nesse cenário, sem qualquer benchmark no C# real.

A próxima variável é o **número de nós**, mantendo a densidade comercial de 144/48 e o **mesmo orçamento de trabalho do ADR-048**. O resultado permite separar crescimento geográfico de densidade.

## Ressalva estrutural do domínio — trabalhador gera oferta E demanda

**Esclarecimento aprovado pelo usuário em 15/11/2026.** O
trabalhador é a unidade física econômica do Global Arena.
Sua atividade produtiva pode gerar OFERTA, mas o próprio
trabalhador gera DEMANDA mesmo quando estiver construindo,
em deslocamento, alocado a atividade militar, ou ocioso.
A localização econômica da demanda acompanha a presença do
trabalhador. No deslocamento, a demanda do ciclo é distribuída
proporcionalmente ao tempo efetivo de permanência em cada tile
atravessado, conforme a nova diretriz conceitual aprovada em
15/11/2026. A antiga regra provisória de atribuir toda a demanda
em trânsito ao último local de atividade fica SUBSTITUÍDA.

Para cada trabalhador w e ciclo de duração T, se t(w,n) for o
tempo de permanência atribuído ao nó n, a participação na demanda
do nó será t(w,n)/T, com soma_n t(w,n)=T e soma_n t(w,n)/T=1.
Assim, deslocar-se NÃO multiplica demanda total, e a parcela
da demanda recebida por cada tile deriva da presença física
no período. A regra de fronteira para instantes sobre arestas,
travessia exata, partidas e chegadas é um detalhe de execução
pendente de especificação e testes determinísticos.

Trata-se de CONTRATO CONCEITUAL PARA O FUTURO MOTOR DE TRABALHADORES,
não de código implantado: o laboratório R13 atual ainda gera
demanda sintética independente dos trabalhadores, e o C#/Godot
não calcula essa distribuição temporal.

Definindo apenas conjuntos de **nós físicos distintos**:

- \`P\`: nós em que algum trabalhador produz oferta;
- \`D\`: nós onde a demanda de trabalhadores/atividades é registrada;
- como o trabalhador que produz também demanda no local da
  atividade, **\`P ⊆ D\` e \`|P| ≤ |D|\`**.

A presença de trabalhadores não produtivos NÃO implica
necessariamente \`|P| < |D|\`: eles podem permanecer em nós
que já registram demanda. A desigualdade de cardinalidade
não substitui a demanda quantitativa nem suas ponderações
por trabalhador/atividade.

**Não comparar objetos de dimensões diferentes:**
\`Group\` do R13 é um grupo produtor de UM produto,
\`Market\` do R13 é um par (nó, produto) com demanda sintética,
enquanto \`P\` e \`D\` acima são conjuntos de NÓS físicos.
Múltiplos grupos produtores podem ocupar um mesmo nó,
e um nó pode conter demanda de vários produtos. Portanto
os valores **144 grupos produtores e 48 mercados (nó,produto)**
do laboratório não são uma violação aritmética de
\`|P| ≤ |D|\`, mas também NÃO validam a topologia de
trabalho/consumo do jogo.

Na implementação observada, o gerador R13 sintetiza
\`groups\` e \`markets\` em laços independentes e cria
demanda por produto sem derivá-la da população de
trabalhadores. Isso é um **recorte artificial de
benchmark**, suficiente para verificar a formulação
F-072 e custo dos algoritmos, mas não uma prova de
representatividade da economia final.

**Gate necessário ANTES de afirmar escala realista:**

1. Criar fixture vinculada a trabalhadores físicos,
   cuja alocação por nó/atividade determine
   simultaneamente a oferta (quando produtiva) e
   a demanda (mesmo quando não produtiva).
2. Testar o invariante espacial \`P ⊆ D\` em cada
   snapshot econômico, sem confundir grupos e
   pares (nó,mercadoria).
3. Preservar demanda positiva por item segundo o
   contrato por atividade, com multiplicadores de
   demanda por trabalhador, sem piso artificial
   fixo de compradores e sem duplicar consumo.
4. Testar situações com trabalhadores produtivos e não produtivos,
   inclusive deslocamento com demanda proporcional ao tempo de
   permanência no tile, ausência de duplicação entre tiles,
   mudança de atividade, e concentração de vários trabalhadores
   em um mesmo nó.
5. Só então dimensionar o solver com milhares
   de trabalhadores, lugares de atividade e
   mercados efetivamente demandantes; confirmar
   todos os certificados F-072 originais.

Essa exigência orienta as próximas provas de escala:
**NÃO** altera retrospectivamente o PASS matemático
de 5.762 nós no R13, nem muda preço, congestionamento,
tarifas, F-084F, F-059, C#/Godot ou a CI atual.

## Matriz e limites prévios

| Perfil | Goldberg | Nós | Arestas | Produtores | Mercados | Opções grupo×mercado |
|---|---|---:|---:|---:|---:|---:|
| ADR-048 comparação | m=16 | 2.562 | 7.680 | 144 | 48 | 1.152 |
| **geography (obrigatório)** | m=24 | **5.762** | **17.280** | **144** | **48** | **1.152** |
| maximum_density (opcional, separado) | m=16 | 2.562 | 7.680 | **240** | **120** | **2.880** |

A fixture maior reproduz as fontes R13 originais. O teste de 240/120 usa o teto NATIVO do R13 sem alterar o gerador; não é alegação de milhares de produtores. O catálogo de 10 produtos neste estresse é somente um arranjo de bens sintéticos da fixture, não definição de itens e épocas do jogo.

### Orçamento *geography* (idêntico ao ADR-048 144/48)

| Unidade | Teto |
|---|---:|
| H completas incluindo rejeições | 820 |
| Scans de menor caminho | 4.300 |
| Certificações originais ADR-042 | 6 |
| Colunas ativas | 1.150 |
| Gerações globais | 6 |
| Newton aceitos | 175 |
| Produtos Krylov Jv/Jtv | 56.000 |
| CG por direção | 160 |
| Coeficientes por blocos | 245.000 |

O perfil máximo 240/120 tem teto separado e explícito de 980 H, 6.500 scans, 6 certificados, 1.600 colunas, 6 gerações, 185 Newton, 58.000 produtos Krylov, 180 CG por direção e 248.000 coeficientes de blocos. Nenhum orçamento representa SLA do jogo.

## Implementação verificável

Novo driver \`experiments/gate09/adr049_geography_density/ci/run_adr049.py\` REUTILIZA o núcleo ADR-047 SEM MODIFICÁ-LO. Chama \`construct_scenario\` do R13 original, verifica que todas as ofertas reais têm caminhos válidos e todo mercado começa precificado, registra SHA do fluxo e fingerprint dos custos/titularidade, executa a busca por blocos, classifica resultados e salva evidências JSON+ZIP.

**Nunca aceita resultado por apenas diminuir erro:**
- PASS somente se o certificado independente ADR-042 original tiver sido emitido, os dois gaps globais/pior grupo F-072 forem <=1e-4, fluxos físicos finais existirem, preço e congestionamento originais forem preservados e o input continuar imutável.
- Em NO-GO, o campo final do candidato e seus gaps finais permanecem nulos. Um eventual último certificado do estado inicial ou fluxo PROVISÓRIO fica em campo separado, explicitamente sem homologação.
- CI do perfil geography é **gate de regressão comercial real**: se 5.762/144/48 não certificar, o job retorna erro e o marco permanece NO-GO; não faz CI verde significar apenas integridade dos testes.
- Perfil maximum_density é execução distinta, com custo alto; só será avaliado se o resultado geográfico justificar. Nunca herdar o certificado de 144 produtores para validar 240.

## Governança de tempo de processamento

Esta prova de conceito é Python sequencial sob orçamento numérico, NÃO um agendador de CPU e NÃO um runtime C#/Godot. A política acordada para o jogo é reservar capacidade para interface, guerra, movimentação, política e demais camadas. No futuro o solver poderá usar trabalho cooperativo e snapshots econômicos consistentes em ciclos separados do FPS. Nada disso é implementado aqui.

Para mundos de muitos milhares de nós, o número de trabalhadores/produtores importa tanto quanto a malha. A pergunta de ROI neste gate é **se o custo adicional do território cresce administravelmente com o mesmo número de produtores e mercados**. Após o resultado, priorizar o gargalo efetivo antes de modificar as regras de transporte.

**Decisão nesta etapa:** só após evidência; não pré-classificar GO ou NO-GO. Uma CI aprovada não autoriza integração em produção, merge, alteração de congestionamento ou fechamento F-059.
