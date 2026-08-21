# SDD-12 - Documentação, Vídeo e Entrega

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-20
> Gate A: aprovado em 2026-08-20
> Dependências: SDD-01 a SDD-11 e SDD-13

---

## 1. Objetivo

Especificar de forma enxuta os artefatos que apresentarão o projeto: README público, detalhamento técnico, vídeo demonstrativo e checklist de consistência da entrega.

Conteúdo dependente da implementação permanecerá marcado para preenchimento com evidência real. O documento não autoriza inventar comandos, resultados, percentuais, screenshots ou funcionalidades antes de existirem.

---

## 2. Requisitos rastreados

- `APR-001` a `APR-011`;
- apresentação das telas, funcionalidades e detalhamento técnico exigidos pelo PDF;
- `DIF-001` a `DIF-010`, somente quanto à demonstração fiel;
- `QLT-003`, `QLT-004`, `QLT-006` a `QLT-008`, quanto a evidências e execução documentada.

---

## 3. Escopo previsto

- responsabilidades e relação entre os artefatos;
- estrutura do README raiz;
- conteúdo do detalhamento técnico;
- roteiro e evidências do vídeo;
- referências a arquitetura, testes, cobertura e limitações;
- critérios de consistência antes da entrega.

---

## 4. Fora do escopo

- enviar e-mail ou mensagem externa;
- publicar repositório ou vídeo;
- escolher conta, nuvem ou destinatário;
- controlar o prazo de sete dias;
- executar commit, push ou tornar repositório público;
- repetir integralmente todos os SDDs no README;
- afirmar resultado ainda não obtido.

As orientações administrativas do PDF serão tratadas pelo engenheiro no momento da submissão e não ampliam o escopo de desenvolvimento.

---

## 5. Blocos de decisão

1. artefatos, responsabilidades e fonte da verdade;
2. estrutura do README final;
3. detalhamento técnico exigido;
4. roteiro enxuto do vídeo;
5. placeholders, evidências, critérios e checklist final.

---

## 6. Decisões herdadas

- README ensina executar e compreender sem duplicar os SDDs;
- documentação e vídeo descrevem somente o candidato validado;
- backend e arquitetura são apresentados em C#/.NET;
- frontend é Angular com Angular Material;
- concorrência e idempotência são diferenciais obrigatórios no projeto;
- IA permanece excluída;
- nota fiscal é uma feature simplificada de saída de estoque, não solução fiscal completa;
- impressão usa HTML e navegador, sem PDF;
- limitações e trade-offs são declarados;
- comandos finais usam Docker e Docker Compose;
- percentuais, versões, links e resultados reais só são preenchidos depois da validação.

---

## 7. Decisões em elaboração

Os cinco blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Artefatos, responsabilidades e fonte da verdade

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### README raiz

`README.md` atende o avaliador técnico: explica objetivo, atendimento ao desafio, arquitetura resumida, configuração, comando oficial, fluxo, testes, cobertura, diferenciais, limitações e links para detalhes. Não duplica regras integrais dos SDDs.

#### Detalhamento técnico

`docs/DETALHAMENTO-TECNICO.md` responde diretamente lifecycle Angular, RxJS, bibliotecas, componentes visuais, frameworks C#, erros e exceções e uso de LINQ. Também resume arquitetura, persistência, mensageria, concorrência, idempotência, testes e Docker quando esses elementos ajudarem a compreender a solução real.

#### Roteiro

`docs/ROTEIRO-VIDEO.md` organiza demonstração e explicação, liga trechos a requisitos, inclui falha, recuperação, concorrência e idempotência e impede promessas sem implementação. Durações serão definidas depois dos ensaios.

#### Evidências

Resultados pertencem a `docs/RELATORIO-QA-FINAL.md` e `artifacts/`. README e detalhamento resumem apenas valores comprovados e apontam para a evidência correspondente.

#### Hierarquia

```text
PDF e decisões -> ADRs/SDDs -> implementação -> testes/QA -> apresentação
```

README, detalhamento e vídeo não alteram requisito ou prevalecem sobre especificação.

#### Placeholders

Durante implementação podem existir marcadores explícitos como `[PREENCHER APÓS QA: cobertura obtida]`. Nenhum permanece no candidato final e estimativa não substitui evidência.

### 7.2 Bloco 2 - Estrutura do README final

> Estado: Aprovado pelo engenheiro em 2026-08-20

O README raiz segue esta ordem:

1. nome, descrição, estado e limite de nota simplificada;
2. objetivo, fluxo e tabela de obrigatórios, opcionais A/C, diferenciais e exclusões;
3. diagrama de Angular/Nginx, Gateway, três serviços, três bancos e RabbitMQ;
4. stack e versões reais com finalidade das bibliotecas principais;
5. Docker e Compose como únicos pré-requisitos;
6. criação segura de configuração e secrets locais;
7. comandos copiáveis de subir, acessar, parar e limpar;
8. uso: login, Product, Invoice, itens, emissão e impressão;
9. falhas, Outbox, retry, DLQ, recuperação e ManualIntervention;
10. testes, quantidades, cobertura, data e relatório de QA reais;
11. health, OpenAPI, RabbitMQ Management, correlação, logs e métricas;
12. árvore curta do repositório;
13. decisões e diferenciais;
14. limitações;
15. links para documentação especializada.

O diagrama distingue HTTP, mensageria e persistência e nunca liga Gateway ou banco ao RabbitMQ.

Configuração não exibe senha. Limpeza destrutiva possui aviso e alvo local exato. Comandos são copiados somente depois de validados em ambiente limpo.

O README não usa badge externo como substituto de evidência, não lista dependência transitória irrelevante e não apresenta a feature como ERP completo ou solução fiscal homologada.

### 7.3 Bloco 3 - Detalhamento técnico exigido

> Estado: Aprovado pelo engenheiro em 2026-08-20

`docs/DETALHAMENTO-TECNICO.md` será preenchido depois da implementação e conterá:

1. visão da solução e fluxo distribuído;
2. lifecycle Angular realmente utilizado, com componente e justificativa;
3. RxJS e Signals, com operadores, localização e responsabilidade reais;
4. bibliotecas, versões, finalidade e local de uso;
5. frameworks C#/.NET, DI e Clean Architecture;
6. erros HTTP, exceções, rejeições e falhas distribuídas;
7. LINQ real, distinguindo tradução PostgreSQL e execução em memória;
8. gerenciamento NuGet central, npm lockfile e Docker;
9. concorrência, idempotência, Inbox, Outbox, Process Manager, retry e DLQ;
10. testes, cobertura, trade-offs e limitações comprovados.

O item Golang será marcado como não aplicável. Hooks, operadores, consultas, bibliotecas, resultados e referências de código não serão inventados antes da implementação. O texto usa links e trechos pequenos, sem copiar classes ou SDDs completos.

### 7.4 Bloco 4 - Roteiro do vídeo diferido

> Estado: Estrutura aprovada; preenchimento deliberadamente diferido pelo engenheiro em 2026-08-20

A prioridade atual é a implementação. `docs/ROTEIRO-VIDEO.md` não será criado durante a baseline apenas para conter uma demonstração hipotética.

Depois do candidato funcional e do QA, o roteiro deverá cobrir, no mínimo:

- telas desenvolvidas;
- funcionalidades implementadas;
- fluxo principal completo;
- falha de serviço, feedback e recuperação;
- concorrência e idempotência;
- arquitetura e limites entre Gateway, serviços, bancos e RabbitMQ;
- lifecycle Angular, RxJS e biblioteca visual;
- C#/.NET, erros, exceções e LINQ;
- testes e cobertura reais;
- limitações e decisões deliberadas.

Ordem, duração, dados de demonstração, falhas induzidas, falas e links serão definidos somente depois de ensaio sobre o candidato validado. O vídeo não é condição para iniciar o código, mas é condição para o Gate C final do SDD-13.

### 7.5 Bloco 5 - Placeholders, evidências, critérios e checklist

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Critérios e provas planejadas

| Critério | Resultado verificável | Prova |
|---|---|---|
| `CA-DOC-01` | README permite compreender e executar sem ler todos os SDDs | `TST-DOC-001` |
| `CA-DOC-02` | Todos os comandos publicados foram executados no candidato | `TST-DOC-002` |
| `CA-DOC-03` | Artefatos e evidências não expõem segredo | `TST-DOC-003` |
| `CA-DOC-04` | Diagrama representa corretamente HTTP, bancos e RabbitMQ | `TST-DOC-004` |
| `CA-DOC-05` | Requisitos, diferenciais e limitações estão separados | `TST-DOC-005` |
| `CA-DOC-06` | Detalhamento responde todos os itens técnicos do PDF | `TST-DOC-006` |
| `CA-DOC-07` | Lifecycle, RxJS, bibliotecas e LINQ usam exemplos reais | `TST-DOC-007` |
| `CA-DOC-08` | Testes, quantidades e cobertura correspondem ao QA | `TST-DOC-008` |
| `CA-DOC-09` | Roteiro posterior cobre telas, funções e técnica exigidas | `TST-DOC-009` |
| `CA-DOC-10` | Nenhum placeholder permanece no candidato entregue | `TST-DOC-010` |
| `CA-DOC-11` | Documentação, vídeo e evidências pertencem à mesma versão | `TST-DOC-011` |
| `CA-DOC-12` | Limitação não oculta requisito não atendido | `TST-DOC-012` |

#### Checklist da baseline

- [x] estrutura dos três artefatos definida;
- [x] cada tópico técnico do PDF possui destino;
- [x] roteiro detalhado explicitamente diferido;
- [x] resultado hipotético não é tratado como real;
- [x] orientação administrativa permanece fora do escopo técnico.

#### Checklist anterior ao Gate C

- [ ] README criado e validado;
- [ ] detalhamento preenchido com exemplos reais;
- [ ] roteiro criado depois do ensaio;
- [ ] comandos reexecutados;
- [ ] cobertura atualizada;
- [ ] screenshots pertencem ao candidato;
- [ ] links válidos;
- [ ] placeholders removidos;
- [ ] relatório de QA referenciado;
- [ ] vídeo fiel à aplicação.

#### Riscos

| Risco | Mitigação |
|---|---|
| Documentação obsoleta | Validar contra o candidato final |
| Vídeo prometer comportamento inexistente | Gravar somente após QA |
| Números divergentes | Usar relatório final como fonte |
| README excessivo | Resumir e apontar aos SDDs |
| Segredo em captura | Ambiente e credenciais sintéticas |

---

## 8. Condição para Gate A

O SDD poderá atingir o Gate A quando:

- os cinco blocos estiverem aprovados;
- cada item de apresentação do PDF possuir destino;
- README, detalhamento e vídeo não duplicarem conteúdo desnecessário;
- placeholders não puderem ser confundidos com evidência real;
- critérios exigirem fidelidade ao candidato validado;
- índice e matriz estiverem atualizados.

A condição foi atendida em 2026-08-20: os cinco blocos foram aprovados, todos os tópicos do PDF possuem destino e o roteiro detalhado foi deliberadamente diferido sem criar evidência hipotética.

A aprovação define a estrutura dos artefatos. Seu preenchimento final ocorre depois da implementação e antes do Gate C do SDD-13.
