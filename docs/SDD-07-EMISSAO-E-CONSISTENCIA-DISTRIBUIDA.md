# SDD-07 - Emissão e Consistência Distribuída

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-19
> Gate A aprovado em: 2026-08-19
> Dependências: SDD-01 a SDD-06, ADR-004, ADR-007, ADR-008, ADR-009, ADR-010, ADR-011, ADR-012 e ADR-014

---

## 1. Objetivo

Especificar a integração assíncrona que conecta `PrintInvoice` à baixa atômica de estoque e ao fechamento da invoice, utilizando RabbitMQ, Transactional Outbox, Inbox, consumers idempotentes, retry e DLQ.

Este documento completa tecnicamente o principal fluxo distribuído do desafio e define garantias reais, estados recuperáveis, falhas demonstráveis e limites operacionais sem alegar exactly-once ou reconciliação automática inexistente.

---

## 2. Requisitos rastreados

- `OBR-011`, `OBR-012`, `OBR-014` e `OBR-019` a `OBR-021`;
- `OPA-001` e `OPA-002`;
- `DIF-004`, `DIF-005`, `DIF-007` e `DIF-009`;
- `QLT-001` a `QLT-008`;
- `APR-003` e `APR-009`.

---

## 3. Escopo previsto

- fluxo distribuído ponta a ponta;
- responsabilidades de Billing, Inventory e RabbitMQ;
- exchanges, filas, routing keys e dead-lettering;
- propriedades e persistência das mensagens;
- dispatchers de Outbox e publisher confirms;
- consumer de `StockDeductionRequested`;
- consumers dos resultados em Billing;
- Inbox, hash e acknowledgment manual;
- retry técnico com TTL e dead-letter routing;
- DLQs e `StockDeductionProcessingFailed`;
- concorrência, shutdown e recuperação;
- observabilidade, health checks específicos e testes;
- critérios de aceite e evidências do cenário de falha.

---

## 4. Fora do escopo

- regras de cadastro e saldo já definidas no SDD-05;
- regras HTTP e de Invoice já definidas no SDD-06;
- Kafka, service bus gerenciado ou plugin RabbitMQ adicional;
- transação distribuída, two-phase commit ou exactly-once;
- acesso do broker a qualquer banco;
- fechamento por timeout;
- redrive automático infinito;
- reconciliador ou painel administrativo de DLQ;
- deploy em nuvem;
- comportamento visual do Angular;
- criação de funcionalidades fiscais adicionais.

---

## 5. Blocos de decisão

1. fluxo ponta a ponta e limites de responsabilidade;
2. topologia RabbitMQ e propriedades das mensagens;
3. dispatchers de Outbox e publisher confirms;
4. processamento da solicitação no Inventory;
5. processamento dos resultados no Billing;
6. retry, DLQ e intervenção manual;
7. ciclo de vida, configuração, observabilidade e health;
8. critérios de aceite, testes, riscos e marcadores.

Nenhuma implementação funcional será iniciada durante esta macroetapa documental.

---

## 6. Decisões herdadas

- Gateway e frontend não acessam RabbitMQ;
- RabbitMQ não acessa bancos;
- somente Billing e Inventory participam da mensageria;
- `PrintInvoice` confirma processo, bloqueio e Outbox antes de retornar `202`;
- Billing publica `StockDeductionRequested`;
- Inventory publica `StockDeductionCompleted`, `StockDeductionRejected` ou, quando possível, `StockDeductionProcessingFailed`;
- cada serviço lê e grava somente seu banco;
- Outbox e Inbox são locais e confirmadas com efeitos de negócio;
- entrega é at-least-once e duplicidade é esperada;
- mensagem persistente, acknowledgment manual e publisher confirm são obrigatórios;
- falha de negócio não recebe retry;
- falha técnica transitória recebe retry limitado;
- retry usa TTL e dead-letter routing nativos;
- estado atrasado não é falha nem decisão de negócio;
- terminais não regridem;
- nenhuma mensagem em DLQ altera banco diretamente;
- contratos e versões seguem o SDD-03;
- persistência e constraints seguem o SDD-02;
- regras locais seguem SDD-05 e SDD-06.

---

## 7. Decisões aprovadas

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Fluxo ponta a ponta e limites de responsabilidade

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Sequência principal

```text
1. Angular chama PrintInvoice
2. Billing confirma Invoice + Process + Outbox
3. Billing retorna 202
4. Billing Dispatcher publica StockDeductionRequested
5. RabbitMQ entrega a solicitação ao Inventory
6. Inventory confirma Inbox + saldos/movimentos + Outbox de resultado
7. Inventory envia ack
8. Inventory Dispatcher publica o resultado
9. RabbitMQ entrega o resultado ao Billing
10. Billing confirma Inbox + Invoice + Process
11. Billing envia ack
12. Angular observa o estado por polling
```

Nenhuma requisição HTTP permanece aberta aguardando esse ciclo.

#### Responsabilidades

| Componente | Responsabilidade |
|---|---|
| Billing API | Aceitar `PrintInvoice` e expor consulta do processo |
| Billing Database | Preservar invoice, processo, Inbox e Outbox |
| Billing Dispatcher | Publicar mensagens persistidas e registrar publisher confirm |
| RabbitMQ | Transportar e reter mensagens sem acessar bancos |
| Inventory Consumer | Validar solicitação e executar a baixa |
| Inventory Database | Preservar produtos, movimentos, Inbox e Outbox |
| Inventory Dispatcher | Publicar resultado persistido |
| Billing Consumer | Aplicar resultado ao processo e à invoice |
| Angular | Consultar Billing, sem acessar RabbitMQ |

#### Aceite inicial

```text
transaction billing_db
    -> Invoice.IsIssuanceInProgress = true
    -> InvoiceIssuanceProcess = Pending
    -> Outbox = StockDeductionRequested
    -> commit
    -> 202 Accepted
```

O commit local define o aceite. Broker indisponível mantém processo `Pending`, invoice aberta e bloqueada e Outbox pendente. Falha do banco antes do commit retorna `503`, sem solicitação aceita.

#### Publicação da solicitação

```text
reservar Outbox
    -> publicar mensagem persistente
        -> aguardar publisher confirm
            -> transaction billing_db
                Outbox.PublishedAtUtc
                Pending -> AwaitingStock, se ainda aplicável
```

Crash depois do confirm e antes da atualização local permite republicação. Publisher confirm comprova aceite pelo broker, não aplicação pelo Inventory.

#### Processamento no Inventory

```text
validar envelope e contrato
    -> verificar Inbox
        -> transaction inventory_db
            validar todos os produtos
            baixar todos ou nenhum
            criar movimentos, se concluído
            registrar Inbox
            criar Outbox de resultado
        -> commit
        -> ack
```

Conclusão produz `StockDeductionCompleted`. Produto ausente, saldo insuficiente ou solicitação semanticamente inválida produzem `StockDeductionRejected`. Falha técnica não é convertida prematuramente em rejeição funcional.

#### Publicação e aplicação do resultado

Inventory publica sua Outbox com mensagem persistente e publisher confirm. Se Billing estiver indisponível, o resultado permanece na fila.

Billing valida mensagem e vínculo, verifica Inbox e confirma Inbox, Invoice e InvoiceIssuanceProcess na mesma transação antes do ack.

- Completed fecha e desbloqueia;
- Rejected mantém aberta e desbloqueia;
- ProcessingFailed mantém aberta e bloqueada.

#### Janelas de falha

| Falha | Consequência |
|---|---|
| Broker indisponível após aceite | Outbox preserva a solicitação |
| Inventory parado | Solicitação aguarda na fila |
| Billing parado | Resultado aguarda na fila |
| Crash após publish e antes de marcar Outbox | Republicação possível |
| Crash após commit e antes do ack | Redelivery possível |
| Mesma mensagem duplicada | Inbox impede repetição |
| Outra mensagem para mesma invoice | Constraints impedem nova baixa lógica |
| Falha técnica persistente | Retry e posterior DLQ |
| Falha que impede resultado técnico | Processo pode permanecer aguardando |

#### Garantias e limites

O sistema garante intenção persistida antes do `202`, baixa local atômica, fechamento somente após resultado, redelivery sem repetição de efeitos, retomada após recuperação e evidências persistentes e observáveis.

Não garante exactly-once, ordem global, transação entre bancos, disponibilidade contínua, resolução automática de toda DLQ, conclusão automática de toda falha ou tempo máximo de emissão.

### 7.2 Bloco 2 - Topologia RabbitMQ e propriedades das mensagens

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Exchanges

| Exchange | Tipo | Produtor |
|---|---|---|
| `korp.billing.v1` | `direct` | Billing |
| `korp.inventory.v1` | `direct` | Inventory |
| `korp.retry.v1` | `direct` | Consumers em falha técnica |
| `korp.dead-letter.v1` | `direct` | Descarte controlado |

Todas são duráveis, não exclusivas, sem auto-delete e não internas.

#### Fluxos principais

```text
stock.deduction.requested.v1
stock.deduction.result.v1
```

`stock.deduction.result.v1` transporta Completed, Rejected e ProcessingFailed. O discriminador exato permanece em `messageType`; os três resultados compartilham consumidor e política.

| Fila | Exchange | Routing key | Consumer |
|---|---|---|---|
| `korp.inventory.stock-deduction.v1` | `korp.billing.v1` | `stock.deduction.requested.v1` | Inventory |
| `korp.billing.stock-deduction-results.v1` | `korp.inventory.v1` | `stock.deduction.result.v1` | Billing |

Filas principais são clássicas, duráveis, não exclusivas e sem auto-delete. Em ambiente local de nó único, quorum queue não produziria quorum real; adoção em cluster é evolução operacional, não garantia simulada nesta entrega.

#### Retry de Inventory

```text
korp.inventory.stock-deduction.retry-5s.v1
korp.inventory.stock-deduction.retry-30s.v1
korp.inventory.stock-deduction.retry-120s.v1
```

Bindings em `korp.retry.v1`:

```text
inventory.stock-deduction.retry.5s.v1
inventory.stock-deduction.retry.30s.v1
inventory.stock-deduction.retry.120s.v1
```

Cada fila usa TTL correspondente e dead-letter para `korp.billing.v1` com routing key `stock.deduction.requested.v1`.

#### Retry de Billing

```text
korp.billing.stock-deduction-results.retry-5s.v1
korp.billing.stock-deduction-results.retry-30s.v1
korp.billing.stock-deduction-results.retry-120s.v1
```

Bindings em `korp.retry.v1`:

```text
billing.stock-deduction-result.retry.5s.v1
billing.stock-deduction-result.retry.30s.v1
billing.stock-deduction-result.retry.120s.v1
```

Cada fila usa TTL correspondente e dead-letter para `korp.inventory.v1` com routing key `stock.deduction.result.v1`.

#### Dead-letter queues

```text
korp.inventory.stock-deduction.dlq.v1
korp.billing.stock-deduction-results.dlq.v1
```

Bindings em `korp.dead-letter.v1`:

```text
inventory.stock-deduction.dead.v1
billing.stock-deduction-result.dead.v1
```

DLQs são duráveis, não possuem TTL, dead-letter de retorno ou consumer automático. Redrive exige inspeção e não faz parte da automação desta entrega.

#### Propriedades de publicação

```text
deliveryMode = persistent
contentType = application/json
contentEncoding = utf-8
messageId = envelope.messageId
type = envelope.messageType
correlationId = envelope.correlationId
mandatory = true
```

Headers:

```text
x-message-version
x-causation-id, quando existente
x-producer
x-retry-count
```

O corpo preserva o envelope completo. Propriedades duplicam metadados essenciais para diagnóstico sem substituir a validação do body.

#### Mensagem não roteável

Com `mandatory = true`, retorno por ausência de binding impede sucesso da publicação mesmo que exista confirm do broker. Outbox não recebe PublishedAtUtc, tentativa é registrada e permanece elegível.

#### Declaração e isolamento

Topologia é declarada idempotentemente antes de consumers e dispatchers. Recurso existente com argumentos incompatíveis impede o início do processamento; a aplicação não apaga nem recria automaticamente.

Testes usam vhost ou nomes isolados. Vhost, usuários e secrets do ambiente serão finalizados no SDD-11.

### 7.3 Bloco 3 - Dispatchers de Outbox e publisher confirms

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Responsabilidade e isolamento

Cada microsserviço possui um `BackgroundService` dedicado à sua própria Outbox:

- Billing publica `StockDeductionRequested`;
- Inventory publica `StockDeductionCompleted`, `StockDeductionRejected` ou `StockDeductionProcessingFailed`;
- nenhum dispatcher acessa o banco, a Outbox ou entidades internas do outro serviço;
- somente contratos de integração e convenções do envelope podem ser compartilhados.

#### Reserva concorrente

O dispatcher abre uma transação curta e seleciona até 50 registros que atendam simultaneamente a:

```text
PublishedAtUtc IS NULL
NextAttemptAtUtc <= utcNow
LockId IS NULL OR LockedUntilUtc <= utcNow
```

A seleção usa `FOR UPDATE SKIP LOCKED`, ordena por `NextAttemptAtUtc` e `OccurredAtUtc` e atribui o mesmo `LockId` ao lote e `LockedUntilUtc = utcNow + 60 segundos`. A reserva é confirmada antes de qualquer acesso ao broker.

O relógio vem de `TimeProvider`. O lease permite execução concorrente por múltiplas instâncias sem coordenação externa. Se o processo cair ou o lease expirar durante uma publicação lenta, outra instância poderá republicar; essa duplicidade é prevista pelo modelo at-least-once.

#### Montagem da mensagem

O dispatcher identifica o contrato por `MessageType` e `SchemaVersion`, desserializa o payload persistido e monta o envelope completo definido no SDD-03. `MessageId`, `CorrelationId`, `CausationId`, `OccurredAtUtc` e payload derivam exclusivamente da Outbox; `producer` deriva do serviço proprietário.

A serialização utiliza as opções fixadas de `System.Text.Json` e deve ser determinística para que uma republicação preserve identificador e conteúdo. Falha de serialização é falha de publicação e nunca marca o registro como publicado.

#### Publicação e confirmação

Cada dispatcher mantém conexão de longa duração e possui um canal de publicação exclusivo, sem compartilhamento concorrente. O canal opera em confirm mode.

O lote é publicado sequencialmente pelo canal, mantendo a correlação entre cada mensagem e sua confirmação. O dispatcher aguarda as confirmações por até 5 segundos. Uma publicação somente é bem-sucedida quando recebe `ACK` e não recebe `BasicReturn` de `mandatory = true`.

São tratadas como falha ou resultado desconhecido:

- `NACK`;
- `BasicReturn`;
- timeout de confirmação;
- fechamento da conexão ou do canal;
- exceção de serialização ou publicação.

Mensagens sem confirmação inequívoca permanecem recuperáveis. Publisher confirm prova que o broker aceitou a mensagem; não prova que o consumer a processou.

#### Confirmação local do sucesso

Depois do confirm, o dispatcher abre uma nova transação local, recarrega o registro por `Id` e pelo `LockId` reservado e define:

```text
PublishedAtUtc = utcNow
LockId = null
LockedUntilUtc = null
LastError = null
```

No Billing, a mesma transação altera `InvoiceIssuanceProcess` de `Pending` para `AwaitingStock`, se esse ainda for seu estado. Um estado terminal ou uma transição concorrente não pode ser sobrescrito. No Inventory, a transação limita-se à confirmação da Outbox.

Se o broker confirmar e a atualização local falhar, o registro permanece pendente e pode ser republicado depois do lease. Esse comportamento é intencional e exige idempotência no consumidor.

#### Tratamento da falha

Quando ainda possui o lease, o dispatcher confirma em transação local:

```text
AttemptCount = AttemptCount + 1
NextAttemptAtUtc = utcNow + backoff
LastError = erro sanitizado e limitado a 1000 caracteres
LockId = null
LockedUntilUtc = null
```

O backoff da Outbox é:

```text
1s -> 2s -> 4s -> 8s -> 16s -> 30s -> 30s nas tentativas seguintes
```

Esse backoff trata indisponibilidade de publicação e é independente do retry de consumers de 5, 30 e 120 segundos. Registros de Outbox não são descartados, enviados para DLQ ou encerrados por quantidade de tentativas ou tempo decorrido.

Caso o dispatcher já não possua o lease, ele não sobrescreve a atualização de outra instância. Em crash antes do registro da falha, a expiração do lease recupera a mensagem.

#### Ritmo de execução

Após processar um lote, o dispatcher consulta imediatamente o próximo. Quando não encontra trabalho elegível, aguarda de forma cancelável por até 1 segundo. Cancelamento impede novas reservas; registros ainda reservados permanecem recuperáveis pela expiração do lease.

#### Garantia resultante

O mecanismo garante publicação eventual enquanto banco, serviço e broker voltarem a estar disponíveis, sem perder a intenção confirmada no commit de negócio. Ele não oferece exactly-once e admite republicação nas janelas entre broker e banco.

### 7.4 Bloco 4 - Processamento da solicitação no Inventory

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Recepção e validação técnica

O consumer recebe cada entrega em handler isolado e utiliza acknowledgment manual. Antes de invocar o caso de uso, valida:

- corpo JSON legível;
- envelope completo e campos obrigatórios;
- `messageType = stock_deduction_requested`;
- `messageVersion = 1`;
- `producer = billing`;
- propriedades RabbitMQ coerentes com o envelope;
- identificadores válidos e não vazios;
- `occurredAtUtc` representando um instante UTC;
- payload correspondente ao contrato reconhecido.

JSON ou envelope ilegível, tipo ou versão incompatível e divergência entre propriedades e corpo são falhas técnicas determinísticas. Não produzem efeito funcional nem retry e seguem para a DLQ do Inventory.

Propriedades adicionais desconhecidas no JSON são ignoradas conforme a regra de compatibilidade do SDD-03. O consumer não utiliza `requestedByUserId` como credencial ou autorização.

#### Inbox e identidade do conteúdo

Antes da desserialização funcional, o consumer calcula SHA-256 sobre os bytes UTF-8 exatos do corpo recebido. O valor hexadecimal normalizado é comparado com `InboxMessage.PayloadHash`:

- `MessageId` novo: continua para o processamento;
- mesmo `MessageId` e mesmo hash: envia `ACK` sem repetir efeitos ou criar outra resposta;
- mesmo `MessageId` e hash diferente: registra violação crítica, não produz efeito funcional e encaminha para DLQ;
- falha de acesso à Inbox: falha técnica transitória.

A consulta antecipada otimiza a duplicidade comum. A chave primária de `InboxMessage` continua sendo a garantia definitiva quando consumers concorrentes observam a mensagem como nova.

#### Transação funcional

Uma mensagem nova é tratada em uma transação PostgreSQL `ReadCommitted`:

```text
validar invariantes do payload
    -> verificar movimentos anteriores da invoice
        -> carregar todos os produtos em lote
            -> validar existência de todos
                -> validar todos os saldos
                    -> baixar todos ou nenhum
                        -> criar movimentos, quando concluído
                            -> registrar Inbox
                                -> criar Outbox do resultado
                                    -> commit
                                        -> ACK
```

Nenhum saldo é alterado antes da validação completa. Products, StockMovements, Inbox e Outbox são confirmados ou revertidos em conjunto. O `ACK` nunca antecede o commit.

#### Resultados funcionais

| Decisão | Evento persistido na Outbox | Código |
|---|---|---|
| Solicitação válida e saldo suficiente | `StockDeductionCompleted` | Não aplicável |
| Payload reconhecido contrário às invariantes | `StockDeductionRejected` | `invalid_stock_deduction_request` |
| Qualquer produto ausente | `StockDeductionRejected` | `product_not_found` |
| Qualquer saldo insuficiente | `StockDeductionRejected` | `insufficient_stock` |

Rejeição funcional é resultado definitivo: nenhum saldo ou movimento é alterado e a solicitação original não recebe retry.

Cada resposta recebe novo `MessageId`, preserva `CorrelationId`, utiliza o `MessageId` da solicitação como `CausationId`, define `producer = inventory` e preserva `invoiceId` e `issuanceProcessId`.

#### Duplicidade lógica

Uma nova mensagem, com outro `MessageId`, pode representar uma invoice já movimentada:

- mesmos produtos e quantidades: registra a nova Inbox e cria novo `StockDeductionCompleted`, sem alterar saldos ou criar movimentos;
- conjunto ou quantidades divergentes: inconsistência técnica, sem efeito funcional;
- conjunto parcial de movimentos preexistente: inconsistência técnica, sem continuação automática.

Conteúdo divergente ou estado parcial são falhas determinísticas encaminhadas para DLQ, pois uma nova tentativa com o mesmo conteúdo não pode corrigir o estado. As constraints de movimento continuam como defesa persistente contra disputa e erro de implementação.

#### Concorrência otimista

Conflito de `xmin`, inclusive na confirmação concorrente da Inbox, reverte toda a tentativa. Inventory descarta contexto e transação, cria um novo escopo, relê o estado completo e reavalia as regras.

São permitidas até três tentativas locais, sem espera artificial. Depois do limite, o conflito permanece falha técnica transitória e segue ao mecanismo externo de retry. Ele não é convertido em saldo insuficiente sem nova leitura que sustente essa decisão.

#### Acknowledgment e preservação da entrega

| Resultado do consumer | Ação sobre a entrega original |
|---|---|
| Commit de conclusão ou rejeição realizado | `ACK` |
| Duplicata com mesmo identificador e hash | `ACK` |
| Falha técnica transitória | Publicar no retry correspondente e somente depois enviar `ACK` |
| Falha determinística | Publicar na DLQ e somente depois enviar `ACK` |
| Falha ao publicar em retry ou DLQ | `NACK` com requeue |

O encaminhamento para retry ou DLQ deve ser persistente, usar publisher confirm e respeitar `mandatory = true`. Assim, a entrega original somente é removida quando o efeito local foi confirmado ou quando existe outro destino durável aceito pelo broker.

### 7.5 Bloco 5 - Processamento dos resultados no Billing

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Recepção e validação

O consumer utiliza acknowledgment manual e aceita exclusivamente:

- `stock_deduction_completed` versão 1;
- `stock_deduction_rejected` versão 1;
- `stock_deduction_processing_failed` versão 1;
- `producer = inventory`;
- routing key principal `stock.deduction.result.v1`.

Envelope, propriedades RabbitMQ, payload, correlação e causalidade devem ser coerentes com o SDD-03. JSON ilegível, contrato desconhecido ou divergência entre propriedades e corpo são falhas determinísticas encaminhadas para a DLQ do Billing, sem alteração funcional.

#### Inbox e integridade

Billing calcula SHA-256 sobre os bytes UTF-8 exatos recebidos:

- `MessageId` novo: continua para processamento;
- mesmo identificador e mesmo hash: envia `ACK` sem repetir efeitos;
- mesmo identificador e hash diferente: violação de integridade e DLQ;
- falha de acesso à Inbox: falha técnica transitória.

Inbox, Invoice e InvoiceIssuanceProcess são confirmados na mesma transação. A chave primária da Inbox resolve a disputa entre consumers que observem simultaneamente uma mensagem como nova.

#### Identificação do destino

O evento deve corresponder simultaneamente a `IssuanceProcessId` e `InvoiceId`. Billing não infere o destino por número da invoice, correlação, processo mais recente ou estado corrente.

São inconsistências técnicas determinísticas:

- processo ou invoice inexistente;
- processo associado a outra invoice;
- identificadores incompatíveis no payload;
- resultado atribuído a processo diferente daquele explicitamente declarado.

Esses casos não alteram qualquer invoice e seguem para DLQ.

#### Aplicação de StockDeductionCompleted

A partir de `Pending` ou `AwaitingStock`, Billing confirma:

```text
registrar Inbox
    -> fechar Invoice
        -> definir ClosedAtUtc
            -> remover IsIssuanceInProgress
                -> mover processo para Completed
                    -> definir FinishedAtUtc
                        -> manter OutcomeCode e OutcomeDescription nulos
                            -> commit
                                -> ACK
```

O instante persistido vem do `TimeProvider` local do Billing. O horário do evento permanece informação de integração e não substitui o instante local da transição.

#### Aplicação de StockDeductionRejected

A partir de `Pending` ou `AwaitingStock`, Billing confirma:

```text
registrar Inbox
    -> manter Invoice Open
        -> remover IsIssuanceInProgress
            -> mover processo para Rejected
                -> persistir OutcomeCode e OutcomeDescription
                    -> definir FinishedAtUtc
                        -> commit
                            -> ACK
```

São aceitos os códigos funcionais definidos no contrato:

- `invalid_stock_deduction_request`;
- `product_not_found`;
- `insufficient_stock`.

Descrição sanitizada deve respeitar o limite de 500 caracteres. Código desconhecido, excesso ou estrutura incompatível não são truncados nem convertidos silenciosamente; constituem falha técnica determinística.

Itens permanecem inalterados. A invoice aberta e desbloqueada pode ser corrigida e submetida novamente com nova chave de idempotência.

#### Aplicação de StockDeductionProcessingFailed

A partir de `Pending` ou `AwaitingStock`, Billing confirma:

```text
registrar Inbox
    -> manter Invoice Open
        -> manter IsIssuanceInProgress
            -> mover processo para ManualIntervention
                -> persistir resultado técnico sanitizado
                    -> definir FinishedAtUtc
                        -> commit
                            -> ACK
```

O evento informa que Inventory encerrou o processamento automático sem conclusão funcional segura. Não equivale a rejeição, não afirma baixa concluída e não autoriza nova emissão pública.

O contrato aceita exclusivamente `reasonCode = stock_processing_failed`. `reasonDescription` é mapeado para a descrição sanitizada do processo e deve respeitar o limite de 500 caracteres. Outro código ou estrutura incompatível é falha técnica determinística.

#### Processos terminais e eventos tardios

Para uma nova mensagem referente a processo terminal:

- resultado semanticamente equivalente: registrar a Inbox e enviar `ACK`, sem repetir efeitos;
- resultado contraditório: preservar processo e invoice e encaminhar para DLQ;
- nenhum estado terminal pode regredir;
- evento de processo anterior nunca altera uma tentativa posterior da mesma invoice.

Exemplos de equivalência incluem outro Completed para processo `Completed` e outro Rejected com o mesmo resultado para processo `Rejected`. Completed para processo `Rejected`, Rejected para `Completed` ou resultado funcional para `ManualIntervention` são contraditórios.

#### Concorrência

Invoice e processo usam seus tokens de concorrência. Em conflito, toda a transação é revertida, contexto e entidades são descartados e o estado é recarregado antes da reavaliação.

Se outro processamento já aplicou resultado equivalente, a entrega torna-se duplicidade lógica. Estado incompatível torna-se inconsistência técnica. Conflito que não possa ser resolvido com segurança permanece falha transitória para o mecanismo de retry, sem sobrescrita cega.

#### Acknowledgment

| Resultado do consumer | Ação sobre a entrega original |
|---|---|
| Transação confirmada | `ACK` |
| Duplicata íntegra | `ACK` |
| Resultado terminal equivalente registrado | `ACK` |
| Falha técnica transitória | Publicar no retry correspondente e somente depois enviar `ACK` |
| Falha determinística | Publicar na DLQ e somente depois enviar `ACK` |
| Falha ao encaminhar para retry ou DLQ | `NACK` com requeue |

O encaminhamento usa mensagem persistente, publisher confirm e `mandatory = true`. Billing somente remove a entrega original depois do commit local ou da confirmação de outro destino durável.

### 7.6 Bloco 6 - Retry, DLQ e intervenção manual

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Classificação

| Categoria | Exemplo | Tratamento |
|---|---|---|
| Rejeição funcional | Produto inexistente ou saldo insuficiente | Resultado `StockDeductionRejected`, sem retry |
| Falha técnica transitória | Banco indisponível, timeout ou conflito persistente | Retry limitado |
| Falha técnica determinística | JSON ilegível, versão desconhecida ou inconsistência de integridade | DLQ direta |
| Falha terminal verificável no Inventory | Tentativas esgotadas e ausência de efeitos comprovada | `StockDeductionProcessingFailed`, quando possível |
| Resultado inconclusivo | Impossibilidade de provar se houve efeito | DLQ e processo permanece aguardando |

Falha funcional, falha técnica e resultado inconclusivo não são intercambiáveis. O sistema não produz `StockDeductionProcessingFailed` sem conseguir provar que a baixa não foi confirmada.

#### Sequência de retry

A entrega principal inicia com `x-retry-count = 0`. Falhas transitórias seguem a sequência:

```text
entrega inicial
    -> retry de 5 segundos  (x-retry-count = 1)
        -> retry de 30 segundos (x-retry-count = 2)
            -> retry de 120 segundos (x-retry-count = 3)
                -> tratamento terminal
```

Corpo, `MessageId`, tipo, versão, correlação e causalidade permanecem inalterados. Somente headers técnicos de entrega podem mudar.

Header ausente equivale a zero. Valor malformado, negativo ou superior ao permitido é falha determinística e segue para DLQ. O consumer usa o valor validado para escolher a próxima fila, sem depender da descrição textual da exceção.

#### Encaminhamento confirmado

Retry e DLQ obedecem à mesma sequência segura:

1. preservar os bytes do corpo recebido;
2. publicar mensagem persistente na exchange e routing key correspondentes;
3. utilizar `mandatory = true`;
4. aguardar publisher confirm e ausência de `BasicReturn`;
5. enviar `ACK` na entrega original somente depois da confirmação;
6. se o encaminhamento falhar, enviar `NACK` com requeue.

O corpo não é desserializado e serializado novamente durante o encaminhamento. Isso preserva os bytes utilizados pelo hash da Inbox.

#### Esgotamento no Inventory

Depois da falha da entrega com `x-retry-count = 3`, Inventory tenta determinar, em nova transação, se nenhum efeito funcional foi confirmado.

Quando consegue provar a ausência da baixa e persistir o encerramento com segurança, confirma:

```text
Inbox da solicitação
    + Outbox StockDeductionProcessingFailed
    + nenhuma alteração de Product
    + nenhum StockMovement
        -> commit
            -> ACK
```

O evento possui código técnico estável, descrição sanitizada, mesmo `CorrelationId` e `CausationId` igual ao identificador da solicitação. Billing pode então mover o processo para `ManualIntervention`.

Inventory não produz esse evento quando:

- o resultado de commit anterior for desconhecido;
- existirem movimentos parciais ou divergentes;
- o banco continuar inacessível;
- a transação terminal não puder ser confirmada;
- não for possível provar ausência de efeitos.

Nesses casos, a solicitação segue para a DLQ do Inventory e o processo em Billing pode permanecer `Pending` ou `AwaitingStock`. Essa limitação é deliberada: estado atrasado ou inconclusivo não é convertido em decisão falsa.

#### Esgotamento no Billing

Depois da falha da entrega de resultado com `x-retry-count = 3`, a mensagem segue para a DLQ do Billing. Nenhuma atualização artificial é aplicada ao processo ou à invoice; o evento preservado continua sendo a evidência pendente.

#### Metadados de DLQ

A publicação preserva corpo e propriedades originais e acrescenta apenas:

```text
x-original-exchange
x-original-routing-key
x-retry-count
x-error-code
x-failed-at-utc
x-failed-consumer
```

`x-error-code` usa categoria estável. Headers não incluem exception message, SQL, stack trace, host, credencial ou payload. Diagnósticos internos permanecem em logs correlacionados por `MessageId` e `CorrelationId`.

#### Operação das DLQs

As DLQs:

- não possuem consumer automático;
- não possuem TTL;
- não retornam automaticamente à fila principal;
- não alteram bancos;
- não encerram processos por sua simples presença;
- permanecem disponíveis para inspeção pelo RabbitMQ Management, logs e métricas.

Redrive não faz parte desta entrega. Uma evolução manual deve verificar causa, estado dos dois serviços e segurança da repetição antes de republicar.

#### Proteção contra requeue agressivo

Se o broker não confirmar o encaminhamento para retry ou DLQ, a entrega original permanece disponível. O consumer interrompe ou reduz temporariamente o consumo antes da recuperação, evitando ciclo agressivo de `NACK` e redelivery imediata. A retomada e o ciclo da conexão são definidos no bloco seguinte.

### 7.7 Bloco 7 - Ciclo de vida, configuração, observabilidade e health

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Inicialização

Billing e Inventory iniciam seus componentes de mensageria na ordem:

```text
iniciar aplicação
    -> validar configuração local
        -> verificar banco próprio
            -> conectar ao RabbitMQ
                -> declarar e validar topologia
                    -> iniciar dispatcher de Outbox
                        -> iniciar consumer
```

Falha transitória de conexão não encerra a API. O componente de mensageria permanece indisponível e tenta se recuperar. Topologia existente com argumentos incompatíveis gera log crítico, impede consumers e dispatchers, deixa o health de mensageria `Unhealthy` e exige correção operacional; a aplicação não remove nem recria o recurso.

#### Conexão e recuperação

A conexão RabbitMQ é de longa duração. Canais de consumer e publisher são separados e não são compartilhados concorrentemente.

Após interrupção:

- consumers deixam de aceitar novas entregas;
- mensagens sem `ACK` voltam a ficar disponíveis pelo broker;
- dispatchers preservam Outbox pendente;
- leases locais expiram normalmente;
- conexão e canais são recriados;
- topologia é novamente declarada e validada;
- consumo e publicação são retomados.

Tentativas de restabelecimento do componente usam `1s, 2s, 4s, 8s, 16s` e depois 30 segundos enquanto a indisponibilidade continuar. Esse ciclo operacional não incrementa `x-retry-count`, pois nenhuma entrega funcional está sendo reprocessada.

#### Configuração

```text
Messaging:RabbitMq:Host
Messaging:RabbitMq:Port
Messaging:RabbitMq:VirtualHost
Messaging:RabbitMq:Username
Messaging:RabbitMq:Password
Messaging:RabbitMq:RequestedHeartbeatSeconds = 30
Messaging:RabbitMq:NetworkRecoveryIntervalSeconds = 5

Messaging:Publisher:ConfirmTimeoutSeconds = 5
Messaging:Outbox:BatchSize = 50
Messaging:Outbox:PollingIntervalMilliseconds = 1000
Messaging:Outbox:LeaseSeconds = 60

Messaging:Consumer:PrefetchCount = 1
Messaging:Retry:FirstDelaySeconds = 5
Messaging:Retry:SecondDelaySeconds = 30
Messaging:Retry:ThirdDelaySeconds = 120
```

Nomes de exchanges, filas, routing keys, tipos e versões são constantes do contrato definido neste SDD e não variam livremente por ambiente.

Os três tempos de retry fazem parte dos argumentos das filas e devem permanecer exatamente em 5, 30 e 120 segundos nesta versão. As respectivas opções existem para validação e ligação explícita da configuração; um valor diferente impede o componente de mensageria de iniciar e exige nova decisão documental, evitando topologias semanticamente diferentes entre ambientes.

Credenciais não ficam no repositório. Docker Compose recebe valores por variáveis de ambiente e exemplos não secretos serão definidos no SDD-11. Configuração ausente ou inválida não usa fallback secreto, registra somente o nome da opção e impede o componente afetado de iniciar.

#### Concorrência do consumer

Cada instância inicia um consumer por fluxo com:

```text
prefetchCount = 1
consumer dispatch concurrency = 1
manual acknowledgment = true
```

A escolha prioriza previsibilidade e demonstração correta das garantias para o volume do desafio. Escala horizontal permanece possível com instâncias concorrendo pela mesma fila. Aumento de paralelismo interno exige medição e decisão posterior.

#### Encerramento controlado

Ao receber cancelamento, cada serviço:

1. interrompe o recebimento de novas entregas;
2. permite que o handler atual finalize dentro do prazo;
3. deixa de reservar registros da Outbox;
4. aguarda publicações em andamento;
5. fecha canais;
6. fecha a conexão.

O prazo é de 30 segundos. Depois dele, transação incompleta sofre rollback, entrega sem `ACK` retorna ao broker e Outbox reservada torna-se elegível após o lease. Nenhuma mensagem é marcada artificialmente como publicada.

#### Health checks

```text
GET /health/live
GET /health/ready
GET /health/dependencies
```

`/health/live` verifica somente se o processo está ativo e não consulta banco ou RabbitMQ.

`/health/ready` verifica configuração local, acesso ao banco próprio e schema esperado. RabbitMQ não invalida automaticamente o readiness HTTP do Billing, pois `PrintInvoice` consegue persistir a intenção na Outbox; o mesmo isolamento preserva as responsabilidades HTTP do Inventory.

`/health/dependencies` apresenta separadamente banco próprio, RabbitMQ, topologia, dispatcher e consumer. O endpoint fica não saudável quando qualquer dependência operacional estiver indisponível, permitindo identificar degradação sem rejeitar indevidamente trabalho que pode ser persistido.

Respostas não expõem host, usuário, senha, vhost, tabelas ou mensagens internas de exceção.

#### Logs estruturados

Eventos mínimos:

```text
rabbitmq_connection_changed
rabbitmq_topology_declared
rabbitmq_topology_incompatible
outbox_batch_claimed
outbox_message_published
outbox_publish_failed
message_received
message_processed
message_retried
message_dead_lettered
duplicate_message_ignored
message_integrity_violation
```

Campos permitidos incluem serviço, `MessageId`, `CorrelationId`, `CausationId`, tipo, versão, tentativa, fila lógica, duração e código estável do resultado.

Corpo, itens, descrições de produto, tokens, credenciais, connection strings e stack trace não aparecem em logs informativos. Exceção completa permanece restrita ao diagnóstico interno configurado.

#### Métricas

```text
outbox_pending_messages
outbox_oldest_pending_age_seconds
outbox_publications_total{outcome}
outbox_publish_duration_seconds
messages_consumed_total{message_type,outcome}
message_processing_duration_seconds{message_type}
message_retries_total{consumer,retry_stage}
messages_dead_lettered_total{consumer,reason}
message_duplicates_total{consumer}
rabbitmq_connection_state{service}
```

Mensagem, invoice, produto, processo, correlação e usuário não são labels de métricas.

#### Estado atrasado

Processo em `Pending` ou `AwaitingStock` por mais de cinco segundos é apresentado como atrasado conforme SDD-03 e SDD-06. Isso pode produzir informação visual, log ou métrica, mas não altera estado persistido, não desbloqueia invoice, não cria retry adicional ou `StockDeductionProcessingFailed` e não representa falha de negócio.

### 7.8 Bloco 8 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-19

As decisões dos blocos anteriores são verificadas pelas seções 8 a 11. Cada critério possui ao menos uma evidência planejada, e as provas distribuídas relevantes utilizam infraestrutura real em containers.

---

## 8. Critérios de aceite

### CA-DST-01 - Aceite independente do broker

**Dado** que o RabbitMQ está indisponível,  
**quando** Billing confirma invoice bloqueada, processo e Outbox,  
**então** retorna `202`, preserva a intenção e publica quando o broker se recuperar.

### CA-DST-02 - Topologia correta

**Dado** um ambiente vazio,  
**quando** Billing e Inventory inicializam a mensageria,  
**então** exchanges, filas, bindings, retries e DLQs são declarados com os nomes e argumentos aprovados.

### CA-DST-03 - Topologia incompatível

**Dado** um recurso RabbitMQ existente com configuração incompatível,  
**quando** a aplicação valida a topologia,  
**então** o processamento não inicia, o recurso não é recriado e a falha fica observável.

### CA-DST-04 - Publicação confirmada

**Dado** uma Outbox elegível,  
**quando** o broker envia `ACK` sem `BasicReturn`,  
**então** `PublishedAtUtc` é confirmado e o processo pode passar para `AwaitingStock`.

### CA-DST-05 - Resultado incerto da publicação

**Dado** confirm do broker seguido de falha ao atualizar o banco,  
**quando** o lease expira,  
**então** a mensagem pode ser republicada com o mesmo identificador e corpo.

### CA-DST-06 - Baixa atômica

**Dado** uma solicitação válida com vários produtos,  
**quando** Inventory a processa,  
**então** saldos, movimentos, Inbox e Outbox de conclusão são confirmados juntos.

### CA-DST-07 - Rejeição sem efeito parcial

**Dado** produto ausente, saldo insuficiente ou payload semanticamente inválido,  
**quando** Inventory processa a solicitação,  
**então** nenhum saldo é alterado e uma rejeição funcional é persistida na Outbox.

### CA-DST-08 - Duplicidade técnica

**Dado** o mesmo `MessageId` e o mesmo hash,  
**quando** a mensagem é entregue novamente,  
**então** o consumer envia `ACK` sem repetir efeitos.

### CA-DST-09 - Violação de integridade

**Dado** o mesmo `MessageId` com corpo diferente,  
**quando** o consumer compara a Inbox,  
**então** nenhum efeito é aplicado e a mensagem segue para DLQ.

### CA-DST-10 - Duplicidade lógica

**Dado** outro `MessageId` para uma invoice já baixada com conteúdo equivalente,  
**quando** Inventory processa a mensagem,  
**então** não reduz novamente os saldos e pode produzir nova conclusão idempotente.

### CA-DST-11 - Conteúdo lógico divergente

**Dado** uma invoice já movimentada,  
**quando** chega solicitação com produtos ou quantidades diferentes,  
**então** nenhuma nova baixa ocorre e a mensagem segue para diagnóstico em DLQ.

### CA-DST-12 - Conclusão no Billing

**Dado** `StockDeductionCompleted` compatível,  
**quando** Billing confirma a transação,  
**então** Inbox, fechamento, desbloqueio e conclusão do processo são persistidos juntos.

### CA-DST-13 - Rejeição no Billing

**Dado** `StockDeductionRejected` compatível,  
**quando** Billing confirma a transação,  
**então** Inbox, processo rejeitado e desbloqueio da invoice são persistidos juntos.

### CA-DST-14 - Evento tardio

**Dado** um processo terminal,  
**quando** chega resultado equivalente ou contraditório,  
**então** o equivalente é consumido sem novo efeito e o contraditório não altera o terminal.

### CA-DST-15 - Retry progressivo

**Dado** uma falha transitória,  
**quando** o processamento falha repetidamente,  
**então** a mensagem percorre 5, 30 e 120 segundos preservando corpo e identidade.

### CA-DST-16 - DLQ determinística

**Dado** contrato incompatível ou falha determinística,  
**quando** o consumer classifica a entrega,  
**então** encaminha diretamente à DLQ com publicação confirmada antes do `ACK`.

### CA-DST-17 - Intervenção manual segura

**Dado** retries esgotados no Inventory e ausência de efeitos comprovada,  
**quando** o encerramento técnico é confirmado,  
**então** Inbox e `StockDeductionProcessingFailed` são persistidos juntos e Billing pode entrar em `ManualIntervention`.

### CA-DST-18 - Resultado inconclusivo

**Dado** que Inventory não consegue provar se houve efeito,  
**quando** as tentativas se esgotam,  
**então** não produz resultado falso, preserva a mensagem na DLQ e o processo pode permanecer aguardando.

### CA-DST-19 - Recuperação após indisponibilidade

**Dado** banco, serviço ou RabbitMQ temporariamente indisponível,  
**quando** a dependência retorna,  
**então** o fluxo retoma a partir de Outbox, fila, retry ou entrega não confirmada.

### CA-DST-20 - Encerramento seguro

**Dado** cancelamento da aplicação,  
**quando** o prazo de shutdown termina,  
**então** nenhuma entrega ou Outbox é marcada como concluída sem confirmação.

### CA-DST-21 - Health coerente

**Dado** RabbitMQ indisponível e banco disponível,  
**quando** os health checks são consultados,  
**então** liveness e readiness HTTP permanecem coerentes, enquanto dependências mostram degradação da mensageria.

### CA-DST-22 - Observabilidade segura

**Dado** publicação, consumo, retry, duplicidade ou DLQ,  
**quando** logs e métricas são inspecionados,  
**então** existe correlação suficiente sem payloads, credenciais ou labels de alta cardinalidade.

---

## 9. Plano de testes

| ID | Evidência | Tipo | Critérios |
|---|---|---|---|
| TST-DST-001 | Declarar toda a topologia em RabbitMQ real | Integração | CA-DST-02 |
| TST-DST-002 | Detectar recurso incompatível sem recriação | Integração | CA-DST-03 |
| TST-DST-003 | Aceitar impressão com broker parado e publicar após retorno | E2E | CA-DST-01, CA-DST-19 |
| TST-DST-004 | Confirmar ACK, NACK, return e timeout do publisher | Integração | CA-DST-04, CA-DST-05 |
| TST-DST-005 | Disputar lote de Outbox entre duas instâncias | Integração | CA-DST-05 |
| TST-DST-006 | Simular crash após publish e antes de `PublishedAtUtc` | Integração | CA-DST-05, CA-DST-08 |
| TST-DST-007 | Executar baixa completa com PostgreSQL e RabbitMQ reais | E2E | CA-DST-06, CA-DST-12 |
| TST-DST-008 | Rejeitar produto ausente e saldo insuficiente | Integração | CA-DST-07, CA-DST-13 |
| TST-DST-009 | Reentregar mesma mensagem após commit e antes do ACK | Integração | CA-DST-08 |
| TST-DST-010 | Repetir identificador com corpo divergente | Integração | CA-DST-09 |
| TST-DST-011 | Repetir intenção equivalente com novo identificador | Integração | CA-DST-10 |
| TST-DST-012 | Enviar intenção divergente para invoice movimentada | Integração | CA-DST-11 |
| TST-DST-013 | Entregar resultado antes de `AwaitingStock` | Integração | CA-DST-12, CA-DST-13 |
| TST-DST-014 | Entregar resultados equivalentes e contraditórios após terminal | Integração | CA-DST-14 |
| TST-DST-015 | Percorrer retries com relógio e TTL controlados | Integração | CA-DST-15 |
| TST-DST-016 | Encaminhar contrato inválido diretamente para DLQ | Integração | CA-DST-16 |
| TST-DST-017 | Falhar publicação para retry ou DLQ e preservar original | Integração | CA-DST-16, CA-DST-19 |
| TST-DST-018 | Produzir `ProcessingFailed` somente com ausência comprovada | Integração | CA-DST-17 |
| TST-DST-019 | Simular commit inconclusivo e impedir resultado falso | Integração | CA-DST-18 |
| TST-DST-020 | Derrubar e recuperar cada dependência durante o fluxo | E2E/Resiliência | CA-DST-19 |
| TST-DST-021 | Encerrar consumer e dispatcher durante processamento | Integração | CA-DST-20 |
| TST-DST-022 | Verificar os três health endpoints por dependência | Integração | CA-DST-21 |
| TST-DST-023 | Inspecionar logs e métricas com valores sentinela | Segurança | CA-DST-22 |
| TST-DST-024 | Validar dependências e limites entre camadas e serviços | Arquitetura | CA-DST-01 a CA-DST-22 |
| TST-DST-025 | Executar fluxo completo pelo Docker Compose | E2E | CA-DST-01, CA-DST-06, CA-DST-12, CA-DST-19 |

Testes unitários cobrem classificação de falhas, backoff, transições, validações, equivalência e construção de metadados. Testes de integração utilizam PostgreSQL e RabbitMQ reais em containers nos cenários relevantes.

Conforme ADR-014, cada assembly de produção relevante deve atingir ao menos 80% de line coverage. Branch coverage é coletada e publicada, inicialmente sem gate percentual, mas os ramos críticos destes critérios devem possuir testes. Provas distribuídas não são substituídas por mocks.

---

## 10. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Confundir at-least-once com exactly-once | Garantia falsa | Janelas de duplicidade documentadas e testadas |
| ACK prematuro | Perda de mensagem | ACK somente após commit ou encaminhamento confirmado |
| Outbox presa por crash | Fluxo interrompido | Lease expirável e recuperação por outra instância |
| Consumers concorrentes repetirem baixa | Saldo incorreto | Inbox, constraints e concorrência otimista |
| Retry em loop | Sobrecarga | Três estágios limitados e DLQ |
| Informar falha sem saber se houve baixa | Estado falso no Billing | `ProcessingFailed` somente com ausência comprovada |
| Processo permanecer bloqueado por DLQ | Intervenção operacional | Estado atrasado, métricas e limitação explícita |
| Topologia divergir entre ambientes | Falha de integração | Nomes fixos e validação no startup |
| Logs vazarem dados | Exposição de informação | Allowlist, hashes e testes com sentinelas |
| Mocks ocultarem falhas reais | Falsa confiança | PostgreSQL, RabbitMQ e Docker nas provas relevantes |

---

## 11. Marcadores de qualidade

| Marcador | Condição |
|---|---|
| `ARC` | Limites entre Gateway, serviços, broker e bancos verificados |
| `DOM` | Rejeição funcional separada de falha técnica |
| `DAT` | Inbox, Outbox, movimentos e estados confirmados atomicamente |
| `IDM` | Duplicidade técnica e lógica cobertas |
| `CON` | Concorrência e leases testados |
| `MSG` | Confirms, ACK, retries e DLQs comprovados |
| `TST` | Cobertura mínima de 80% e critérios rastreados |
| `INT` | PostgreSQL e RabbitMQ reais nas provas relevantes |
| `OBS` | Logs, métricas e health checks verificados |
| `SEC` | Ausência de segredos e payloads em telemetria |
| `DOC` | Garantias e limitações descritas sem promessas falsas |
| `QA` | Fluxo Docker completo e cenários de falha demonstrados |

---

## 12. Limites para implementação futura

Uma implementação deste SDD poderá criar:

- topologia RabbitMQ especificada;
- dispatchers locais de Outbox;
- consumers de solicitação e resultado;
- adapters de Inbox, retry, DLQ e confirmação;
- integração dos casos internos definidos nos SDDs 05 e 06;
- configuração, health checks, logs e métricas aprovados;
- testes unitários, de integração, arquitetura, resiliência e ponta a ponta descritos.

Não autoriza:

- acesso entre bancos;
- comunicação do Gateway ou frontend com RabbitMQ;
- exactly-once, transação distribuída ou reconciliação automática;
- redrive automático ou painel administrativo de DLQ;
- mudança dos contratos aprovados sem atualizar o SDD-03;
- funcionalidade fiscal, compra, fornecedor ou ajuste de estoque adicional;
- implementação antes da baseline documental conjunta.

---

## 13. Condição para Gate A

O SDD pode atingir o Gate A quando:

- todos os oito blocos estiverem aprovados;
- topologia e propriedades forem implementáveis sem decisão implícita;
- janelas de falha e garantias estiverem explicitadas;
- cada critério possuir evidência planejada;
- compatibilidade com SDD-02, SDD-03, SDD-05 e SDD-06 estiver confirmada;
- matriz de rastreabilidade e índice estiverem atualizados;
- nenhuma promessa de exactly-once ou conclusão automática indevida permanecer.

A aprovação estabiliza a integração distribuída, mas não autoriza implementação antes da aprovação da baseline documental conjunta.
