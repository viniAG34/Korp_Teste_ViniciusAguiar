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

### Marco 2 - Concluído em 2026-08-22

- dispatchers independentes para as Outboxes de Billing e Inventory;
- reserva de até 50 registros com `FOR UPDATE SKIP LOCKED`, lease único por lote e `xmin` explícito;
- canal exclusivo e duradouro por publisher, com confirms rastreados, `mandatory = true` e timeout de cinco segundos;
- payload persistido publicado sem reconstrução, com metadados e headers validados;
- `ACK` do broker marca a Outbox do Billing e `Pending -> AwaitingStock` na mesma transação local;
- `BasicReturn/NO_ROUTE`, timeout, NACK e falhas técnicas não marcam publicação como concluída;
- backoff de Outbox validado em `1, 2, 4, 8, 16, 30...` segundos;
- ciclo do dispatcher retoma após indisponibilidade temporária de banco ou broker;
- divergência anterior de `producer = billing-service` corrigida para o contrato aprovado `billing`;
- TST-DST-004 e TST-DST-005 possuem evidência parcial direta; janelas de crash e NACK explícito permanecem para a prova de resiliência;
- regressão integral: 163 testes aprovados, 0 falhas e 0 ignorados.

Próximo marco: consumer idempotente do Inventory e transação conjunta de saldo, movimentos, Inbox e Outbox.

### Marco 3 - Concluído em 2026-08-22

- consumer do Inventory com prefetch 1, despacho sequencial e acknowledgment manual;
- validação de JSON, envelope, versão, produtor e propriedades RabbitMQ antes do caso de uso;
- SHA-256 calculado sobre os bytes exatos recebidos;
- mesma combinação `MessageId + hash` consumida sem novo efeito ou nova resposta;
- mesmo `MessageId` com corpo diferente classificado como violação determinística;
- conclusão e rejeição confirmam Products, StockMovements, Inbox e Outbox na mesma transação `ReadCommitted`;
- duplicidade lógica equivalente cria nova Inbox e nova conclusão sem reduzir saldo novamente;
- conteúdo lógico divergente não produz efeito e é classificado para DLQ;
- ACK somente após commit ou encaminhamento confirmado;
- encaminhador mínimo de retry/DLQ antecipado como dependência do ACK seguro; TTL, estágios, esgotamento e `ProcessingFailed` permanecem no Marco 5;
- TST-DST-007 a TST-DST-012 possuem evidências diretas ou parciais em PostgreSQL e RabbitMQ reais;
- regressão integral: 167 testes aprovados, 0 falhas e 0 ignorados.

Próximo marco: consumer de resultados no Billing.

### Marco 4 - Concluído em 2026-08-22

- consumer único do Billing para `StockDeductionCompleted`, `StockDeductionRejected` e `StockDeductionProcessingFailed`;
- validação estrita de propriedades, envelope, versão, produtor, correlação, payload e SHA-256 dos bytes recebidos;
- Inbox, Invoice e InvoiceIssuanceProcess confirmados na mesma transação local;
- resultados em `Pending` são aplicados sem depender da marcação posterior `AwaitingStock`;
- conclusão fecha e desbloqueia; rejeição mantém aberta e desbloqueia; falha técnica terminal mantém aberta e bloqueada;
- duplicata íntegra recebe ACK sem novo efeito e identificador com hash divergente segue como falha determinística;
- resultado terminal equivalente registra nova Inbox sem repetir efeito; resultado contraditório preserva o terminal e segue para DLQ;
- conflitos otimistas descartam o contexto e são reavaliados até três vezes antes da classificação transitória;
- acknowledgment manual ocorre somente após commit ou encaminhamento confirmado;
- TST-DST-013 e TST-DST-014 possuem evidência direta em PostgreSQL real; a prova ponta a ponta pelo broker permanece no Marco 7;
- regressão integral: 170 testes aprovados, 0 falhas e 0 ignorados; o projeto Gateway permanece sem testes descobertos.

Próximo marco: retry completo, DLQ e produção segura de `StockDeductionProcessingFailed`.

### Marco 5 - Concluído em 2026-08-22

- contador de retry ausente tratado como zero e valores malformados, negativos ou superiores a três enviados diretamente à DLQ;
- sequência limitada às filas TTL de 5, 30 e 120 segundos, sem retry funcional ou infinito;
- encaminhamento preserva corpo e propriedades, usa mensagem persistente, `mandatory` e publisher confirms;
- DLQ recebe exchange, routing key, contador, código estável, instante UTC e consumer, sem exception message ou payload;
- falha do encaminhamento mantém a entrega original por `NACK requeue=true` e aplica pausa antes da redelivery;
- Billing esgotado preserva invoice e processo e envia o resultado à sua DLQ;
- Inventory esgotado reabre transação e somente produz `StockDeductionProcessingFailed` quando Inbox e movimentos estão ausentes;
- Inbox da solicitação e Outbox de falha técnica são confirmadas juntas, com causalidade apontando para a solicitação original;
- movimentos existentes, estado já processado ou acesso inconclusivo não produzem resultado técnico falso;
- TST-DST-016 e a regra central de TST-DST-018 possuem evidência direta; percurso temporal completo e falha dirigida de encaminhamento permanecem para a prova de resiliência do Marco 7;
- regressão integral: 173 testes aprovados, 0 falhas e 0 ignorados; builds finais de Billing e Inventory sem erros ou avisos.

Próximo marco: health checks, observabilidade e encerramento controlado.

### Marco 6 - Concluído em 2026-08-22

- estado operacional thread-safe compartilhado por topologia, dispatcher e consumer em Billing e Inventory;
- `/health/live` limitado ao processo, sem consulta a banco ou RabbitMQ;
- `/health/ready` valida configuração, banco próprio e migrations pendentes sem tornar RabbitMQ requisito HTTP;
- `/health/dependencies` apresenta separadamente banco, RabbitMQ, topologia, dispatcher e consumer;
- respostas de health sanitizadas, contendo somente nome e estado de cada check;
- shutdown do host configurado explicitamente para 30 segundos, preservando cancelamento e descarte natural de entregas sem ACK e leases não concluídos;
- logs mínimos de recebimento, processamento, retry, DLQ, duplicidade e violação de integridade usam campos permitidos e códigos estáveis;
- catálogo de métricas implementado com labels de baixa cardinalidade, incluindo snapshot real da Outbox e estado da conexão;
- TST-DST-022 possui evidência direta nas duas APIs e TST-DST-023 inspeciona instrumentos e labels dos dois serviços;
- TST-DST-021 confirma o prazo configurado; a interrupção dirigida durante handler/publicação permanece para o Marco 7;
- build completo sem erros ou avisos e regressão serializada: 179 testes aprovados, 0 falhas e 0 ignorados; o projeto Gateway permanece sem testes descobertos.

Próximo marco: prova distribuída, falhas dirigidas, cobertura e Gate C.

### Marco 7 - Concluído em 2026-08-22; Gate C aprovado

- projeto `Korp.Distributed.IntegrationTests` separado dos serviços de produção;
- runner `distributed-tests` incorporado ao perfil de persistência do Docker Compose;
- dois hosts independentes usam exclusivamente seus bancos próprios e RabbitMQ para integração;
- fluxo Billing -> RabbitMQ -> Inventory -> RabbitMQ -> Billing comprovado com invoice fechada, saldo reduzido e movimento único;
- janela de crash após publish e antes de `PublishedAtUtc` simulada por perda da confirmação local;
- republicação com o mesmo `MessageId` confirmada sem segunda baixa ou segunda Inbox técnica;
- TST-DST-006 e TST-DST-025 possuem evidência direta no ambiente distribuído real.
- TST-DST-015 comprova retorno após TTL real, preservação da identidade e definições fixas de 5, 30 e 120 segundos;
- TST-DST-017 remove a rota de retry, exige falha confirmada do publish e comprova a entrega original recuperável após `NACK requeue=true`;
- TST-DST-021 interrompe o consumer com transação bloqueada no PostgreSQL e comprova rollback, ausência de efeitos e redelivery sem ACK;
- suíte distribuída atual: 4 testes aprovados, 0 falhas e 0 ignorados;
- regressão serializada instrumentada: 179 testes aprovados, 0 falhas e 0 ignorados;
- a consolidação normaliza raízes distintas dos arquivos Cobertura e exclui somente migrations, `obj`, factories de design e bootstrap declarativo já autorizados;
- cobertura manual aplicável: Billing API 87,86%, Application 90,88%, Domain 96,86% e Infrastructure 80,09%; Inventory API 86,02%, Application 97,55%, Domain 96,64% e Infrastructure 80,48%;
- teste direcionado da falha de Outbox confirma liberação do lease, tentativa, erro estável e backoff persistido;
- indisponibilidade de Inventory e Billing preserva as mensagens nas filas e o fluxo conclui quando cada serviço inicia;
- interrupção das conexões pelo próprio RabbitMQ revelou e passou a cobrir a recriação do ciclo dos consumers após recuperação;
- regressão final serializada: 180 testes aprovados, 0 falhas e 0 ignorados; suíte distribuída: 7 aprovados.

Relatório de implementação: os critérios aplicáveis do SDD-07 possuem provas unitárias, de integração com PostgreSQL/RabbitMQ reais, de arquitetura e distribuídas. TST-DST-004 e TST-DST-005 permanecem classificados como evidência parcial porque suas garantias estão divididas entre provas específicas, sem um teste monolítico adicional. Todos os oito assemblies backend aplicáveis superam 80% de line coverage; branch coverage foi publicada nos relatórios Cobertura. O projeto Gateway continua sem testes descobertos, condição preexistente e fora do escopo do SDD-07. Não foram adicionadas dependências de produção.

Gate C aprovado pelo engenheiro em 2026-08-22. Evidências e limitações consolidadas em `RELATORIO-IMPLEMENTACAO-SDD-07.md`.

Próximo passo: elaborar o Gate B do SDD-08 - API Gateway.
