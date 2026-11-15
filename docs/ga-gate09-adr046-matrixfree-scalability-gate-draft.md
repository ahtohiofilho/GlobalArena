# Global Arena — ADR-046: escalabilidade do núcleo de equilíbrio sem reduzir a complexidade econômica

**Situação:** experimento de escalabilidade executável; GO/NO-GO depende dos resultados CI e dos certificados ADR-042.
**Data:** 15/11/2026. **Base:** ADR-045 PR #334. **Governança:** PR Draft, nenhuma mudança no motor C#/Godot, F-072, F-084F ou F-059.

## Diretriz de produto confirmada

O Global Arena foi concebido para **muitos milhares de nós**, não 42 ou 162.
A economia complexa, os custos de transporte dependentes do fluxo,
a curva de congestionamento quartica e as tarifas de propriedade são
parte do valor estratégico do jogo.

**NÃO simplificar a economia por mera presunção de custo.** Priorizar
redução da complexidade algorítmica, estruturas esparsas, reaproveitamento
de estados imutáveis dentro de cada avaliação e processamento em
ciclos econômicos, desacoplado de renderização do mapa. A tolerância
ECONÔMICA F-072 permanece global e pior produtor <=1e−4;
a tolerância de custo COMPUTACIONAL por ciclo pode ser mais generosa.
Qualquer nova política de congestionamento exige prova de necessidade,
análise de efeitos comerciais e decisão explícita de produto.

**Alerta:** 312/320 scans e 46/48 updates de 162 nós no ADR-045
foram limites artificiais de um laboratório. Não são medição de
saturação do hardware. O número de nós NÃO determina sozinho o
número de incógnitas do solver: importam produtores, mercados,
caminhos com fluxo e colunas ativas.

## Gargalo concretamente identificado no ADR-045

O kernel de viabilidade usa Jacobi F-B analítico J de tamanho
N×N onde N é o número de colunas comerciais ativas.
A cada Levenberg–Marquardt forma (JᵀJ+λI) e usa eliminação
gaussiana densa com custo aproximado O(N³) de operações
e O(N²) de armazenamento por passo, ignorando estrutura
de mercado/rota/aresta.

Em mapas grandes, o custo de caminho mínimo cresce com
grafo e pares (origem,vendedor); já o custo do sistema
linear cresce com o número de colunas. Ambos devem ser
reportados em unidades separadas.

## Incremento experimental efetivo ADR-046

Novo arquivo: experiments/gate09/adr046_sparse_mcp/ci/run_adr046.py

A análise diferencial usa EXATAMENTE a mesma economia F-072
do ADR-045 e a mesma função matemática de complemento:

    H_gc = [1+C_gc(B)]*sqrt(Q_m/D_m),
    F-B(x_gc, H_gc−mu_g) = 0.

Uma coluna comercial i toca UMA quantidade de mercado Q_m e
o conjunto de arestas do seu caminho. Dados k,j do vetor
independente, uma pequena mudança dZ desloca massa física
entre a coluna independente j e a coluna-âncora a do seu
produtor, sem violar a soma da oferta.

A derivada exata direcional do custo é:

    dH_i = sqrt(Q_i/D_i) * {
              Σ_(e na rota i) [4*c_free,e*(B_e/K_e)^3/K_e * dB_e]
              + [(1+C_i)/(2*Q_i)] * dQ_i
           }

    dF-B_i = da_i*dX_i + db_i*(dH_i−dmu_g).

Todos os termos de congestão e tarifa física permanecem
os originais. A implementação constrói o produto Jv
somando incrementos por mercado/aresta e Jᵀv
retropropagando adjuntos pelas MESMAS incidências.

**Nunca monta uma matriz J N×N nem o sistema normal
denso durante o caminho matrix-free**. Armazena
somente colunas, caminhos e somatórios de arestas,
mercados e multiplicadores: memória O(N+L+E),
onde L é a soma dos comprimentos das rotas ativas.

A direção LM resolve iterativamente
(JᵀJ+λI) δ = −JᵀF-B por gradiente conjugado
pré-condicionado, com limites explícitos de
iterações e produtos Jacobiano-vetor. O algoritmo
ainda usa busca física projetada, continuação de
suavização e decisões de PASS/NO-GO do ADR-045.

**Não é um método econômico novo**, mas implementação
numérica mais econômica da mesma raiz NCP; a
prova de convergência global ainda NÃO existe.
O condicionamento de mercados quase vazios e
congestionamento severo pode aumentar iterações
CG, mesmo com economia correta.

## Matriz e critérios objetivos

### Paridade da álgebra

- Jv matrix-free ≡ J denso analítico original ADR-045,
  até erro de arredondamento comprovado em suíte.
- Jᵀv matrix-free ≡ Jᵀ denso (adjunto correto).
- Derivada direcional versus diferença finita.
- Equação normal SPD esparsa versus sistema
  denso da mesma formulação em caso pequeno.
- Repetibilidade Windows/Ubuntu nos valores
  comerciais finais e na quantidade de trabalho.
- Nenhuma alteração das fontes originais R20–R45.

### Gate econômico obrigatório

O método só pode receber PASS quando o certificador
estático ADR-042 independente confirmar:
- todo produtor vende sua oferta integral;
- todo mercado fisicamente alcançável possui Q>0;
- P=sqrt(D/Q) original, c_free*(1+(B/K)^4),
  tarifas específicas por vendedor;
- gap global e do pior grupo F-072 <=1e−4;
- auditoria independente R13/R14, separação de
  rotas comerciais, conservação F-084F.
- F-059 permanece decisão FINANCEIRA aberta.

Em qualquer outra condição: NO-GO explícito.
Nenhuma tolerância econômica será relaxada para
fazer uma variante passar.

### Medições predeclaradas

- Casos controlados de 2 produtores / 2 mercados,
  inclusive rotas alternativas e referência
  ao algoritmo DENSO original ADR-045.
- Mapa 42 nós original; 162 nós heterogêneo com K×0,20.
- **Mapa 2.562 nós e 7.680 arestas** produzido
  pelo construtor GOLDberg original R13:
  4 produtos, 48 grupos produtores e 24 mercados,
  com cerca de 288 colunas canônicas potenciais.
  IMPORTANTE: milhares de nós **não são**
  milhares de produtores; é uma etapa de escala,
  não prova de densidade máxima do futuro jogo.
- Perfil extended opcional para matriz mais densa,
  definido em PR futuro, somente após resposta
  numérica desta primeira execução.

Para o mundo de 2.562 nós: limite experimental
de 240 avaliações H, 600 scans de rotas,
2 certificações ADR-042, 440 colunas,
32 updates Newton, 12.000 produtos Krylov
e até 100 iterações CG por direção. Esses
limites são instrumentos de medição, não
orçamento definitivo da economia do jogo.

A CI poderá concluir com NO-GO específico em
2.562 nós **sem ocultar o resultado**: o
primeiro gate é a paridade exata do operador
e a integridade econômica nos casos controlados.
Somente o certificado independente valida
o sucesso para a escala maior.

### Metas de desempenho e próximos gargalos

1. Registrar separadamente nós, produtores,
   mercados, colunas e incidências rota-aresta.
2. Medir wall time do mesmo executor Python
   apenas como evidência observacional.
   Não generalizar diretamente ao C#/Godot
   ou a outros computadores.
3. Contabilizar H inclusive descartadas,
   Jv e Jᵀv, varreduras de menor caminho,
   iterações CG, linhas rejeitadas e
   certificações completas.
4. O jacobiano deixa de ocupar N²; ainda
   permanecem os custos de cálculo de H,
   caminhos canônicos e auditoria F-084F.
5. Avaliar otimização posterior das rotinas
   de caminho, agrupamento por vendedor,
   precondicionamento e exploração temporal,
   só depois de medir cenários grandes.
6. Reservar capacidade computacional para
   futuras camadas do jogo. O ciclo econômico
   NÃO precisa coincidir com os frames Godot.

**Conclusão só após resultados:** GO para
prosseguir se paridade e contratos de oferta/
preço/finança forem aprovados; GO operacional
de milhares de nós somente se o certificado
ADR-042 independente for atingido com custo
estabelecido; caso contrário, NO-GO operacional
de escala, com próxima intervenção dirigida
ao gargalo observado, SEM reduzir exponente.
