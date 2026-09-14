# Global Arena — V1 Definition of Done

**Versão:** 0.1
**Status:** Draft
**Milestone relacionado:** M0 — Project Baseline

---

# 1. Objetivo do V1

Global Arena V1 deverá oferecer uma experiência completa de estratégia planetária em que o jogador possa:

- gerar um planeta procedural;
- assumir o controle de uma civilização;
- explorar e compreender o mundo;
- desenvolver uma economia;
- produzir e movimentar unidades;
- estabelecer relações diplomáticas;
- estabelecer e utilizar rotas comerciais;
- entrar em guerra;
- resolver conflitos através da camada tática;
- conquistar e perder território;
- interferir na economia e logística de outras civilizações;
- salvar e continuar uma partida;
- jogar em single-player;
- jogar em multiplayer;
- interagir com todo o sistema através de um cliente 3D funcional e compreensível.

O V1 não precisa representar a versão definitiva de cada subsistema.

Ele precisa entregar o loop completo do jogo de maneira tecnicamente sólida, estável e extensível.

---

# 2. Core Loop

O V1 deverá suportar o seguinte ciclo:

Gerar mundo
→ estabelecer civilizações
→ produzir
→ comercializar
→ planejar
→ emitir ordens
→ resolver turno
→ movimentar
→ combater
→ alterar território
→ alterar economia
→ adaptar estratégia
→ próximo turno

O ciclo deverá poder se repetir indefinidamente enquanto a partida permanecer válida.

---

# 3. Mundo

Para considerar o V1 concluído:

- o planeta deverá possuir topologia fechada;
- deverá ser representado visualmente em 3D;
- deverá utilizar a estrutura Goldberg definida pelo projeto;
- deverá possuir camada estratégica;
- deverá possuir camada tática;
- territórios estratégicos deverão possuir adjacências válidas;
- arestas estratégicas deverão possuir representação própria;
- as fileiras compartilhadas de subtiles deverão funcionar;
- não deverão existir bordas artificiais no planeta;
- diferentes tamanhos/resoluções de planeta deverão ser suportados dentro dos limites definidos de performance.

---

# 4. Geração procedural

O V1 deverá conseguir gerar um planeta a partir de uma seed.

A mesma seed, utilizando a mesma versão do gerador, deverá produzir o mesmo mundo.

Deverão existir pelo menos:

- geometria;
- elevação;
- terra e água;
- clima;
- biomas;
- recursos;
- habitabilidade;
- localização inicial de civilizações.

O jogador deverá poder iniciar novas partidas sem depender de mapas feitos manualmente.

---

# 5. Civilizações

O V1 deverá:

- gerar ou posicionar múltiplas civilizações;
- sugerir uma quantidade de civilizações compatível com o planeta;
- permitir ao jogador escolher uma civilização;
- atribuir identidade visual distinguível;
- manter território;
- manter recursos;
- manter unidades;
- manter relações diplomáticas.

Não deverá existir um limite estrutural artificial causado por uma paleta fixa de cores.

---

# 6. Diplomacia

Estados mínimos do V1:

- Enemy
- Neutral
- Ally

A diplomacia deverá:

- existir entre pares de civilizações;
- afetar comportamento relevante da simulação;
- ser persistida em saves;
- ser sincronizada em multiplayer.

Sistemas diplomáticos avançados não são requisito do V1.

---

# 7. Economia

O V1 deverá possuir uma economia funcional e dinâmica.

Deverão existir:

- produção;
- consumo;
- recursos ou mercadorias;
- oferta;
- demanda;
- transporte;
- rotas comerciais;
- custos de transporte;
- limitações ou capacidades de rota;
- impacto de guerra e bloqueios.

Rotas estratégicas não deverão exigir pathfinding global sobre toda a malha tática.

Mudanças locais deverão poder invalidar somente as rotas afetadas.

A economia deverá possuir performance suficiente para os tamanhos de mundo aprovados para o V1.

---

# 8. Guerra

O V1 deverá permitir:

- criação ou existência de unidades militares;
- movimentação estratégica;
- posicionamento tático;
- ordens;
- ataque;
- defesa;
- destruição ou neutralização de unidades;
- conquista territorial;
- bloqueio de conexões;
- interação entre guerra, comércio e economia.

A resolução de comandos deverá utilizar o sistema de ordens simultâneas com resolução sequencial determinística definido pela arquitetura.

---

# 9. Turnos

O V1 deverá suportar:

- recebimento simultâneo de ordens;
- fechamento da janela de ordens;
- transformação de comandos em eventos;
- ordenação pseudoaleatória reproduzível;
- execução sequencial;
- revalidação de eventos no momento de execução;
- consolidação do estado;
- avanço para o turno seguinte.

O resultado de uma resolução deverá poder ser reproduzido a partir do mesmo:

- estado inicial;
- conjunto de comandos;
- ruleset;
- seed.

---

# 10. Inteligência Artificial

O V1 deverá possuir IA capaz de participar de uma partida completa.

No mínimo deverá conseguir:

- administrar sua civilização;
- produzir;
- utilizar recursos;
- movimentar unidades;
- reagir a ameaças;
- atacar;
- defender;
- utilizar diplomacia básica;
- participar da economia.

A IA não precisa ser ótima.

Ela precisa ser funcional, coerente e suficientemente competitiva para permitir single-player.

---

# 11. Multiplayer

O V1 deverá permitir partidas multiplayer funcionais.

A arquitetura deverá utilizar autoridade de servidor.

O multiplayer deverá suportar pelo menos um modelo de avanço temporal aprovado para o V1.

Possibilidades arquiteturalmente suportadas:

- turnos com duração automática;
- turnos por confirmação;
- correspondência.

Não é requisito que todos esses modos estejam disponíveis no V1.

O núcleo de simulação utilizado pelo multiplayer deverá ser o mesmo utilizado no single-player.

---

# 12. Persistência

O V1 deverá permitir:

- criar save;
- carregar save;
- continuar partida;
- persistir o estado necessário da simulação;
- identificar versão do save;
- identificar versão do world generator;
- identificar versão das regras.

Falhas ou incompatibilidades deverão ser detectadas de forma controlada.

---

# 13. Replay e diagnóstico

O V1 deverá registrar informação suficiente para:

- reproduzir resoluções relevantes;
- investigar divergências;
- diagnosticar bugs de determinismo;
- identificar seeds;
- identificar turnos;
- identificar comandos executados.

Replay visual completo poderá ser classificado separadamente.

---

# 14. Cliente 3D

O V1 deverá permitir:

- visualizar o planeta;
- rotacionar e navegar pela esfera;
- alterar nível de observação;
- selecionar territórios;
- selecionar unidades;
- consultar civilizações;
- visualizar informações econômicas;
- visualizar diplomacia;
- visualizar guerra;
- emitir ordens.

A apresentação gráfica não será fonte de verdade da simulação.

---

# 15. Interface

A UI deverá permitir ao jogador compreender o estado global sem depender de informações externas.

Deverá existir navegação clara entre pelo menos:

- planeta;
- território;
- civilização;
- economia;
- comércio;
- unidades;
- guerra;
- diplomacia;
- turno.

O objetivo é permitir que grande complexidade sistêmica continue navegável.

---

# 16. Performance

Antes do V1 deverão ser definidos budgets mensuráveis para:

- tempo de geração de planeta;
- memória;
- duração da resolução de turno;
- economia;
- pathfinding estratégico;
- carregamento;
- save;
- multiplayer;
- renderização.

Os valores exatos serão estabelecidos por benchmarks durante o desenvolvimento.

Nenhum subsistema crítico poderá escalar de maneira incompatível com os tamanhos de mundo definidos para lançamento.

---

# 17. Estabilidade

Para V1:

- não poderão existir crashes conhecidos de alta severidade;
- saves não poderão ser corrompidos em fluxo normal;
- não poderão existir desyncs conhecidos reproduzíveis no multiplayer aprovado;
- os principais loops deverão possuir testes automatizados;
- invariantes críticos da simulação deverão estar protegidos por testes.

---

# 18. Fora do escopo obrigatório do V1

Salvo mudança explícita de escopo, não são requisitos obrigatórios:

- religião;
- política interna complexa;
- espionagem avançada;
- sistema financeiro altamente detalhado;
- centenas de tipos de tratados diplomáticos;
- simulação populacional individual;
- milhares de modelos visuais exclusivos;
- editor completo de mapas;
- modding completo;
- campanha narrativa;
- sistemas sociais externos ao core do jogo.

Esses itens poderão ser classificados como:

- V1 OPTIONAL;
- POST-V1;
- EXPERIMENTAL.

---

# 19. Critério final

Global Arena V1 estará concluído quando:

1. o core loop completo estiver funcional;
2. todos os requisitos V1 REQUIRED estiverem atendidos;
3. os budgets mínimos de performance forem cumpridos;
4. os critérios de estabilidade forem cumpridos;
5. single-player estiver funcional;
6. multiplayer aprovado para V1 estiver funcional;
7. o cliente 3D permitir controlar integralmente a partida;
8. o jogo puder ser instalado, iniciado, jogado, salvo, retomado e concluído sem ferramentas de desenvolvimento.

---

# 20. Status

Status atual:

**Draft 0.1**

Este documento ainda será refinado durante o M0.

A conclusão do M0 congelará o primeiro baseline oficial do escopo V1.
