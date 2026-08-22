# Plano de Implementação - SDD-07

> Gate: B - Plano de implementação
> Status: Aprovado pelo engenheiro
> Data: 2026-08-21
> Gate B aprovado em: 2026-08-21
> SDD: `SDD-07-EMISSAO-E-CONSISTENCIA-DISTRIBUIDA.md`
> Dependências: SDD-01 a SDD-06 e ADRs 004, 007 a 012 e 014

---

## 1. Objetivo

Conectar o aceite durável de `PrintInvoice` à baixa atômica de estoque e ao fechamento da invoice usando RabbitMQ, Outbox, Inbox, acknowledgments manuais, publisher confirms, retry limitado e DLQ.

O trabalho preservará as fronteiras aprovadas: somente Billing e Inventory acessam o broker; cada serviço acessa exclusivamente seu banco; Gateway, Identity e frontend não participam da mensageria.

## 2. Estado encontrado

- os quatro contratos de integração e o envelope versionado já existem em `Korp.Shared.Contracts`;
- Billing já confirma Invoice, InvoiceIssuanceProcess e `StockDeductionRequested` na própria Outbox antes do `202`;
- Inventory já possui o caso de uso de baixa atômica, concorrência otimista e idempotência lógica por movimentos da invoice;
- os dois bancos já possuem Inbox e Outbox com hash, lease, tentativas, próximo processamento e `xmin`;
- Billing já possui as transições internas `AwaitingStock`, `Completed`, `Rejected` e `ManualIntervention`;
- ainda não existem cliente RabbitMQ, topologia, dispatchers, consumers, retry/DLQ operacionais ou health checks de mensageria;
- o schema existente atende ao desenho aprovado. Nenhuma migration é planejada. Necessidade de alteração estrutural descoberta durante o código interromperá a implementação para revisão.

## 3. Dependência nova

Adicionar `RabbitMQ.Client` oficial, linha estável 7.x, ao gerenciamento central de pacotes e às infrastructures de Billing e Inventory.

Não serão adicionados MassTransit, CAP, Rebus, Polly, MediatR ou bibliotecas paralelas de retry. A implementação usará `BackgroundService`, `System.Text.Json`, `TimeProvider`, EF Core e health checks nativos.

## 4. Organização da implementação

### Marco 1 - Fundamentos e topologia

Arquivos afetados:

- `Directory.Packages.props`;
- projetos Infrastructure de Billing e Inventory;
- `Messaging/Configuration`, `Messaging/Topology` e `Messaging/RabbitMq` em cada Infrastructure;
- `DependencyInjection.cs` dos dois serviços;
- `appsettings.json` das duas APIs;
- testes unitários e de integração de mensageria.

Responsabilidades:

- opções fortemente tipadas e validação no startup;
- constantes fixas para exchanges, filas, routing keys, tipos e versões;
- conexão duradoura por serviço e canais exclusivos por função;
- declaração idempotente da topologia completa;
- falha observável e impeditiva diante de topologia incompatível;
- propriedades persistentes, `mandatory`, headers e metadados aprovados.

Evidências: TST-DST-001, TST-DST-002 e parte de TST-DST-004.

### Marco 2 - Outbox e publicação confirmada

Arquivos afetados:

- `Persistence/Messaging/OutboxMessage.cs` dos dois serviços, somente se necessário para operações já previstas;
- novos repositórios/units de reserva da Outbox nas duas Infrastructures;
- publisher RabbitMQ e dispatcher `BackgroundService` por serviço;
- composição nas APIs;
- testes de domínio técnico, PostgreSQL e RabbitMQ.

Responsabilidades:

- lote máximo de 50 com `FOR UPDATE SKIP LOCKED`;
- lease de 60 segundos e seleção ordenada;
- publicação sequencial, persistente, obrigatória e confirmada em até cinco segundos;
- `ACK` sem `BasicReturn` como única confirmação positiva;
- atualização local por `Id + LockId`;
- transição `Pending -> AwaitingStock` na confirmação da Outbox do Billing;
- backoff de Outbox `1, 2, 4, 8, 16, 30...` segundos;
- preservação indefinida da intenção não publicada;
- cancelamento sem novas reservas e recuperação por lease.

Evidências: TST-DST-004 a TST-DST-006 e CA-DST-01, 04, 05 e 20.

### Marco 3 - Consumer do Inventory

Arquivos afetados:

- portas e caso de uso de baixa em `Korp.Inventory.Application`;
- unidade transacional e adapters em `Korp.Inventory.Infrastructure`;
- consumer, validador de envelope/hash e composição da API;
- testes unitários, PostgreSQL e RabbitMQ do Inventory.

Responsabilidades:

- validar envelope, contrato, propriedades e SHA-256 dos bytes recebidos;
- distinguir duplicidade técnica, violação de integridade e nova mensagem;
- confirmar Products, StockMovements, Inbox e Outbox de resultado na mesma transação;
- produzir Completed ou Rejected sem baixa parcial;
- preservar idempotência lógica por invoice;
- encaminhar divergência lógica para diagnóstico sem nova baixa;
- enviar `ACK` somente após commit ou encaminhamento confirmado.

O handler atual será ajustado para que o commit não aconteça antes da criação de Inbox e Outbox. Regras de saldo continuam na Application/Domain; RabbitMQ permanece na Infrastructure.

Evidências: TST-DST-007 a TST-DST-012 e CA-DST-06 a 11.

### Marco 4 - Consumer de resultados do Billing

Arquivos afetados:

- portas e transições de emissão em `Korp.Billing.Application`;
- unidade transacional e adapters em `Korp.Billing.Infrastructure`;
- consumer, validador de envelope/hash e composição da API;
- testes unitários, PostgreSQL e RabbitMQ do Billing.

Responsabilidades:

- consumir Completed, Rejected e ProcessingFailed na mesma fila;
- validar vínculo entre evento, invoice e processo;
- confirmar Inbox, Invoice e InvoiceIssuanceProcess atomicamente;
- tolerar resultado antes da marcação `AwaitingStock`;
- consumir resultado terminal equivalente sem novo efeito;
- impedir resultado contraditório de alterar terminal;
- classificar concorrência reavaliando o estado antes de decidir retry ou ACK.

Evidências: TST-DST-013, TST-DST-014 e CA-DST-12 a 14.

### Marco 5 - Retry, DLQ e falha técnica terminal

Arquivos afetados:

- classificadores de falha e publicadores de encaminhamento nos dois serviços;
- consumers e metadados de retry/DLQ;
- testes de integração com RabbitMQ real.

Responsabilidades:

- falha transitória percorre somente 5, 30 e 120 segundos;
- falha determinística segue diretamente à DLQ;
- encaminhamento é confirmado antes do ACK da entrega original;
- corpo e identidade originais são preservados;
- falha no encaminhamento usa `NACK requeue=true` com proteção contra loop agressivo;
- Inventory só produz ProcessingFailed quando comprova ausência de efeitos;
- resultado inconclusivo permanece na DLQ sem inventar estado funcional;
- Billing não possui redrive ou consumer automático de DLQ.

Evidências: TST-DST-015 a TST-DST-019 e CA-DST-15 a 18.

### Marco 6 - Saúde, observabilidade e encerramento

Arquivos afetados:

- `Program.cs` e observabilidade das APIs Billing e Inventory;
- health checks e estado operacional na Infrastructure;
- testes de API, segurança e ciclo de vida.

Responsabilidades:

- `/health/live`, `/health/ready` e `/health/dependencies` com semânticas distintas;
- RabbitMQ degradado não invalida automaticamente readiness HTTP quando a intenção ainda pode ser persistida;
- logs estruturados com allowlist e sem payload, token, credencial ou connection string;
- métricas de baixa cardinalidade definidas no SDD;
- parada em até 30 segundos sem confirmação artificial.

Evidências: TST-DST-021 a TST-DST-023 e CA-DST-20 a 22.

### Marco 7 - Prova distribuída e Gate C

Arquivos afetados:

- `compose.yaml`, limitado à infraestrutura de teste necessária ao SDD-07;
- projetos de integração existentes e, se a separação for necessária, novo projeto `tests/Distributed/Korp.Distributed.IntegrationTests`;
- testes de arquitetura;
- matriz, índice e relatório do SDD-07.

Responsabilidades:

- RabbitMQ real isolado para testes;
- fluxo Billing -> RabbitMQ -> Inventory -> RabbitMQ -> Billing;
- broker, Inventory e Billing indisponíveis e posteriormente recuperados;
- concorrência sobre saldo unitário;
- regressão integral, cobertura por assembly e evidências reproduzíveis;
- relatório sem promessa de exactly-once.

A composição completa para uso final continua no SDD-11; este marco adiciona somente o ambiente necessário para provar o SDD-07.

Evidências: TST-DST-003, TST-DST-020, TST-DST-024 e TST-DST-025.

## 5. Mapeamento dos critérios

| Critérios | Destino principal | Evidência principal |
|---|---|---|
| CA-DST-01 a 05 | topologia, publisher e Outbox | integração PostgreSQL/RabbitMQ e recuperação |
| CA-DST-06 a 11 | consumer e transação do Inventory | baixa/rejeição/duplicidade reais |
| CA-DST-12 a 14 | consumer e transação do Billing | fechamento, rejeição e eventos tardios |
| CA-DST-15 a 18 | retry, DLQ e ProcessingFailed | filas TTL e encaminhamento confirmado |
| CA-DST-19 e 20 | dispatchers, consumers e lifecycle | resiliência e shutdown |
| CA-DST-21 e 22 | health, logs e métricas | integração e inspeção com sentinelas |

## 6. Estratégia de testes

- unitários: validação, hash, equivalência, classificação, backoff, metadados e transições;
- integração PostgreSQL: reserva concorrente, transações Inbox/Outbox e conflitos;
- integração RabbitMQ: topologia, confirms, returns, retries, DLQ, ACK/NACK e redelivery;
- distribuídos: fluxo completo e indisponibilidades reais;
- arquitetura: nenhuma referência do Domain/Application ao RabbitMQ e nenhum acesso entre bancos;
- cobertura: mínimo de 80% de linhas manuais aplicáveis por assembly de produção; branch coverage publicada;
- regressão: toda a solution dentro do Docker.

Mocks não substituirão as provas em que o comportamento de PostgreSQL ou RabbitMQ é parte do critério.

## 7. Pontos-chave para commits do engenheiro

1. `feat: add rabbitmq topology and reliable outbox dispatchers`;
2. `feat: process stock deduction messages idempotently`;
3. `feat: apply billing issuance results idempotently`;
4. `feat: add messaging retry dlq and operational health`;
5. `test: validate distributed issuance quality gates`.

Cada ponto somente será sugerido depois de testes do bloco e worktree revisada. O agente não realizará commits ou pushes.

## 8. Riscos e controles

| Risco | Controle |
|---|---|
| escopo grande ocultar defeitos | implementação por sete marcos e parada nos cinco pontos-chave |
| ACK prematuro | ACK somente depois de commit ou republish confirmado |
| efeito duplicado | Inbox, hash, constraints e idempotência lógica |
| publish confirmado e banco falhar | mesmo MessageId, lease e republicação prevista |
| loop de retry | três estágios fixos, contador validado e DLQ |
| estado funcional falso | ProcessingFailed apenas com ausência comprovada |
| dependência RabbitMQ contaminar regras | pacote restrito à Infrastructure/API composition |
| teste lento ou instável | isolamento, espera por condição e ausência de sleeps arbitrários |
| vazamento em telemetria | allowlist e testes com valores sentinela |

## 9. Limites explícitos

Não serão implementados acesso entre bancos, mensageria no Gateway, exactly-once, two-phase commit, retry infinito, redrive automático, painel de DLQ, reconciliação automática ou funcionalidade fiscal adicional.

## 10. Condição para conclusão

O Gate C somente será recomendado quando os 22 critérios tiverem evidência objetiva ou deferimento já autorizado pelo SDD, os 25 testes planejados tiverem destino verificável, a regressão estiver aprovada, a cobertura mínima estiver atendida e as limitações operacionais estiverem documentadas.

## 11. Acompanhamento da implementação

### Marco 1 - Concluído em 2026-08-22

- `RabbitMQ.Client` 7.2.2 fixado centralmente;
- configuração validada e conexão recuperável isoladas nas Infrastructures;
- quatro exchanges, duas filas principais, seis filas de retry e duas DLQs declaradas com nomes e argumentos aprovados;
- inicialização idempotente e degradação sem derrubar as responsabilidades HTTP;
- topologia incompatível interrompe o componente sem apagar ou recriar recursos;
- RabbitMQ 4.1.4 real incorporado ao perfil de testes;
- TST-DST-001 e TST-DST-002 aprovados;
- regressão integral: 153 testes aprovados, 0 falhas e 0 ignorados.

Próximo marco: Outbox e publicação confirmada.
