# Auditoria Documental 002 - Baseline para Implementação

> Status: Aprovada
> Data: 2026-08-20
> Escopo: PDF do desafio, documentos fundamentais, ADR-001 a ADR-016 e SDD-01 a SDD-13
> Aprovação da baseline: 2026-08-20

---

## 1. Objetivo

Esta auditoria verifica se a especificação integral está coerente, rastreável e suficientemente determinada para orientar a implementação funcional um SDD por vez. Ela não valida código ainda inexistente e não substitui os Gates B e C de cada atividade.

Foram verificados:

- cobertura dos requisitos do PDF;
- separação entre requisito obrigatório, opcional adotado, diferencial e item não aplicável;
- coerência entre Visão Geral, Convenções, Glossário, ADRs e SDDs;
- propriedade de dados e fronteiras HTTP, RabbitMQ e persistência;
- estados, idempotência, concorrência e recuperação de falhas;
- dependências entre SDDs;
- unicidade dos identificadores de critérios e testes;
- existência dos destinos de links Markdown;
- estados documentais e pendências de implementação.

---

## 2. Resultado executivo

A baseline documental está **aprovada para orientar a implementação**.

- os 22 requisitos obrigatórios do desafio possuem destino, critério e prova planejada;
- os 11 requisitos de apresentação e detalhamento possuem destino, inclusive Golang como não aplicável;
- concorrência e idempotência permanecem como opcionais do PDF deliberadamente adotados;
- inteligência artificial permanece deliberadamente excluída;
- os 10 diferenciais estão identificados sem serem atribuídos ao PDF;
- todos os SDDs possuem Gate A aprovado;
- o SDD-01 é o único SDD funcionalmente validado, pois possui implementação e Gate C;
- não foi encontrada decisão de negócio, arquitetura ou segurança ainda aberta que impeça o início da implementação;
- não foram encontrados links quebrados, IDs reutilizados entre SDDs ou requisito referenciado sem definição na matriz.

A aprovação desta auditoria autoriza somente a passagem para o planejamento Gate B do próximo SDD. Ela não autoriza implementar todos os SDDs de uma só vez.

---

## 3. Cobertura do desafio

| Área do PDF | Destino principal | Situação documental |
|---|---|---|
| Aplicação Angular | SDD-09 | Coberta |
| Cadastro de Product com código, descrição e saldo | SDD-02, SDD-03, SDD-05 | Coberta |
| Invoice sequencial, aberta/fechada e com vários itens | SDD-02, SDD-03, SDD-06 | Coberta |
| Botão de impressão e indicação de processamento | SDD-03, SDD-06, SDD-07, SDD-09 | Coberta |
| Fechamento e baixa de estoque | SDD-05, SDD-06, SDD-07 | Coberta |
| Mínimo de dois microsserviços | SDD-01, SDD-05, SDD-06 | Coberta por Inventory e Billing |
| Banco de dados real | SDD-02, SDD-11 | Coberta por PostgreSQL isolado por serviço |
| Falha, feedback e recuperação de serviço | SDD-07, SDD-09, SDD-10, SDD-11 | Coberta |
| Concorrência opcional | SDD-02, SDD-05, SDD-07, SDD-10 | Adotada |
| Idempotência opcional | SDD-02, SDD-03, SDD-06, SDD-07, SDD-10 | Adotada |
| IA opcional | ADR-002 e matriz | Excluída conscientemente |
| Vídeo e detalhamento técnico | SDD-12, SDD-13 | Estrutura aprovada; conteúdo factual posterior ao código |

As orientações logísticas de envio permanecem fora da arquitetura e da implementação por decisão explícita do engenheiro. O vídeo foi adiado, mas não removido do Gate C final.

---

## 4. Coerência arquitetural confirmada

### 4.1 Fronteiras

- o frontend acessa somente a superfície publicada pelo Nginx e pelo Gateway;
- o Gateway encaminha HTTP para Identity, Inventory e Billing;
- o Gateway não possui banco e não se conecta ao RabbitMQ;
- Billing consulta Inventory diretamente por HTTP interno apenas para validar Product e obter snapshot ao incluir item;
- essa chamada interna propaga o bearer do usuário e é novamente autorizada por Inventory;
- somente Inventory e Billing publicam e consomem eventos;
- RabbitMQ transporta mensagens entre aplicações e nunca acessa bancos;
- cada serviço acessa exclusivamente seu próprio PostgreSQL.

### 4.2 Estado e consistência

- `InvoiceStatus` permanece limitado a `Open` e `Closed`;
- `Pending`, `AwaitingStock`, `Completed`, `Rejected` e `ManualIntervention` pertencem ao processo técnico;
- aceite da emissão é definido pelo commit de Billing com processo e Outbox;
- baixa de todos os itens é atômica em Inventory;
- Inbox, Outbox, chave HTTP e unicidades tornam redelivery e repetição seguros;
- concorrência do saldo usa `xmin`, reavalia a regra e impede baixa parcial ou saldo negativo;
- indisponibilidade transitória preserva a intenção e permite retomada automática;
- resultado tecnicamente incerto não é convertido artificialmente em sucesso ou rejeição.

### 4.3 Segurança e execução

- Identity emite JWT de 15 minutos e não implementa refresh token;
- Gateway e APIs aplicam defesa em profundidade;
- Angular persiste a sessão somente em `sessionStorage`, com limitações de XSS registradas;
- código, execução, migrations e testes são Docker-first;
- somente frontend/Nginx é exposto por padrão no host;
- OpenAPI é limitado a Development e servido pela superfície aprovada;
- a meta de cobertura é de pelo menos 80% de linhas por assembly de produção aplicável, com branches publicadas e fluxos críticos testados.

---

## 5. Dependências entre SDDs

Não foi encontrada dependência circular de implementação.

O aparente ciclo entre SDD-12 e SDD-13 é temporalmente separado:

1. o SDD-13 orienta a validação do candidato que alimentará a documentação factual;
2. o SDD-12 materializa README, detalhamento e roteiro sobre esse candidato;
3. o SDD-13 executa então o Gate C final, que inclui a documentação.

Essa relação não bloqueia a implementação dos SDDs 02 a 11 nem permite produzir evidência hipotética antes do código.

---

## 6. Inconsistências documentais corrigidas

| ID | Inconsistência | Correção aplicada | Mudança de comportamento |
|---|---|---|---|
| AUD2-001 | SDD-03 a SDD-13 estavam como `Validado` apesar de possuírem apenas Gate A | Estado normalizado para `Aprovado` | Não |
| AUD2-002 | Plano do SDD-01 ainda aguardava aprovação após execução e Gate C | Estado alterado para `Executado`, com referência ao Gate C | Não |
| AUD2-003 | Introdução e evidências iniciais da matriz ainda refletiam a fase anterior aos SDDs | Baseline atualizada para distinguir prova planejada de evidência real pendente | Não |
| AUD2-004 | `APR-011` estava definido, mas ausente de intervalos consolidados | SDD-12, testes, QA e consolidação passaram a incluir o item não aplicável | Não |

As correções propagam decisões já aprovadas e não criam requisito, dependência ou regra nova.

---

## 7. Verificações estruturais

| Verificação | Resultado |
|---|---|
| Links Markdown locais | Nenhum destino inexistente |
| IDs de requisito usados pelos SDDs | Todos definidos na matriz |
| IDs `CA-*` e `TST-*` entre SDDs | Nenhuma reutilização entre documentos |
| Estados canônicos de Invoice e emissão | Coerentes entre Glossário, ADRs e SDDs |
| .NET e frontend | .NET 10 LTS e Angular 21 coerentes com a baseline |
| Gateway, RabbitMQ e bancos | Fronteiras coerentes e sem ligação indevida |
| Decisões DEC-P01 e DEC-P02 da auditoria anterior | Fechadas nos SDD-03 e SDD-09 |
| Evidências de código futuro | Mantidas como pendentes, sem alegação fictícia |

---

## 8. Riscos residuais controlados

- versões patch e digests de imagens serão fixados no Gate B da infraestrutura, sem trocar majors aprovadas;
- README final, detalhamento e roteiro dependem do candidato implementado e não devem antecipar resultados;
- cobertura de 80% não substitui testes dos ramos críticos nem validação distribuída real;
- `ManualIntervention` não possui reconciliação administrativa automática nesta entrega;
- assinatura JWT simétrica e `sessionStorage` são decisões proporcionais ao desafio, com limitações registradas;
- a amplitude dos diferenciais exige implementação estritamente faseada para não ameaçar o fluxo obrigatório.

Nenhum desses riscos exige nova decisão antes do plano de implementação do próximo SDD.

---

## 9. Condição de aprovação da baseline

Com a aprovação explícita do engenheiro em 2026-08-20:

1. esta auditoria está `Aprovada`;
2. a matriz constitui a baseline documental aprovada;
3. o projeto entra na macroetapa de implementação;
4. o próximo trabalho é o Gate B do SDD-02, com auditoria do código existente, mapa critério → arquivo → teste, riscos e plano de alterações;
5. nenhum código do SDD-02 será escrito antes da aprovação desse plano.
