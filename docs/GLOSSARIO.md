# Glossário Técnico e de Domínio

> Status: Aprovado
> Aprovação: 2026-08-17
> Data: 2026-08-17
> Escopo: termos canônicos para documentação, código, APIs, banco, mensagens e interface

---

## 1. Regra de uso

Identificadores técnicos utilizam o termo inglês canônico. Documentação e interface utilizam o equivalente em português. Sinônimos não devem ser alternados no código para o mesmo conceito.

Quando um termo do desafio possuir significado mais amplo no mercado, este glossário registra o recorte adotado pelo projeto.

---

## 2. Domínio de estoque

| Português | Termo canônico | Definição |
|---|---|---|
| Produto | `Product` | Item previamente cadastrado e controlado exclusivamente por Inventory |
| Código do produto | `ProductCode` / `Code` | Identificador de negócio obrigatório, normalizado, imutável e único sem distinção de caixa |
| Descrição do produto | `ProductDescription` / `Description` | Nome textual obrigatório do produto |
| Saldo | `Balance` | Quantidade inteira atualmente disponível; pode ser zero e nunca negativa |
| Estoque inicial | `InitialBalance` | Saldo informado no cadastro; sua origem comercial não é modelada |
| Baixa de estoque | `StockDeduction` | Redução atômica dos saldos causada por uma emissão confirmada |
| Movimentação de estoque | `StockMovement` | Registro auditável de uma alteração confirmada de saldo |
| Baixa por nota | `InvoiceDeduction` | Único tipo de movimentação implementado nesta feature |
| Saldo anterior | `BalanceBefore` | Valor observado imediatamente antes da baixa confirmada |
| Saldo posterior | `BalanceAfter` | Valor resultante imediatamente depois da baixa confirmada |

---

## 3. Domínio de faturamento

| Português | Termo canônico | Definição |
|---|---|---|
| Nota fiscal simplificada | `Invoice` | Documento interno de saída de estoque, sem validade fiscal externa |
| Item da nota | `InvoiceItem` | Produto referenciado, snapshot descritivo e quantidade utilizada |
| Número da nota | `InvoiceNumber` / `Number` | Número positivo, único e crescente gerado por sequence; lacunas são permitidas |
| Nota aberta | `InvoiceStatus.Open` | Nota editável e elegível para iniciar impressão quando não há emissão ativa |
| Nota fechada | `InvoiceStatus.Closed` | Nota concluída e imutável, sem nova impressão nesta feature |
| Snapshot do produto | `ProductSnapshot` | Código e descrição copiados para o item no momento da inclusão; não contém saldo |
| Emissão | `InvoiceIssuance` | Processo distribuído que solicita baixa e fecha a nota após confirmação |
| Imprimir nota | `PrintInvoice` | Único comando público que inicia emissão e, após sucesso, solicita o diálogo do navegador |
| Processo de emissão | `InvoiceIssuanceProcess` | Process Manager persistido pelo Billing para acompanhar a operação distribuída |
| Data de fechamento | `ClosedAtUtc` | Instante UTC preenchido somente quando a baixa é confirmada |

---

## 4. Estados do processo de emissão

| Estado | Definição | Efeito sobre a invoice |
|---|---|---|
| `Pending` | Processo e Outbox persistidos; publicação ainda não confirmada | Continua `Open`, mas bloqueada |
| `AwaitingStock` | Solicitação publicada; Billing aguarda Inventory | Continua `Open`, mas bloqueada |
| `Completed` | Baixa confirmada e resultado aplicado | Torna-se `Closed` e dispara a continuação de impressão |
| `Rejected` | Regra de negócio recusou toda a baixa | Continua `Open` e volta a permitir correção |
| `ManualIntervention` | Automação esgotada com efeito técnico incerto | Continua `Open` e bloqueada para diagnóstico |
| Atrasado | `isDelayed` | Informação derivada por tempo; não é estado nem transição |

---

## 5. Mensagens de integração

| Termo canônico | Natureza | Definição |
|---|---|---|
| `StockDeductionRequested` | Solicitação de integração | Billing informa que uma baixa idempotente precisa ser processada por Inventory |
| `StockDeductionCompleted` | Evento de integração | Inventory informa que todos os itens foram baixados atomicamente |
| `StockDeductionRejected` | Evento de integração | Inventory informa que nenhuma baixa foi aplicada por falha de negócio |
| `StockDeductionProcessingFailed` | Evento técnico | Informa, quando for possível publicá-lo com segurança, esgotamento do processamento automático |
| Message ID | `messageId` | Identificador único da mensagem usado no Inbox e diagnóstico |
| Correlation ID | `correlationId` | Identificador comum a todo o fluxo distribuído |
| Causation ID | `causationId` | Identificador da requisição ou mensagem que causou a mensagem atual |

Os nomes físicos de exchange, queue e routing key serão definidos no SDD de emissão e não fazem parte do vocabulário de domínio.

---

## 6. Confiabilidade distribuída

| Termo | Definição |
|---|---|
| Idempotência | Repetir a mesma intenção não duplica processo, movimentação ou baixa |
| `Idempotency-Key` | Chave HTTP fornecida pelo cliente para reconhecer a mesma intenção de `PrintInvoice` |
| At-least-once | Mensagem pode ser entregue mais de uma vez; consumers precisam tolerar duplicidade |
| Outbox | Mensagem persistida na mesma transação do estado local e publicada posteriormente |
| Inbox | Registro transacional de mensagem já processada que impede repetir seus efeitos |
| Retry | Nova tentativa automática de uma falha técnica potencialmente transitória |
| Backoff | Aumento controlado do intervalo entre tentativas |
| Dead-letter queue / DLQ | Fila para mensagens incompatíveis ou que esgotaram tentativas automáticas |
| Redelivery | Nova entrega de uma mensagem não confirmada ou encaminhada por retry |
| Publisher confirm | Confirmação do RabbitMQ de que recebeu a publicação segundo a configuração adotada |
| Manual acknowledgement / ack | Confirmação do consumer somente depois do commit local necessário |
| Process Manager | Componente que persiste e coordena o progresso conhecido de um processo distribuído |

---

## 7. Componentes e propriedade

| Nome canônico | Nome na interface/documentação | Responsabilidade |
|---|---|---|
| `Identity.Service` | Serviço de Identidade | Usuários, credenciais, hash de senha e emissão de JWT |
| `Inventory.Service` | Serviço de Estoque | Produtos, saldos, movimentações e baixa atômica |
| `Billing.Service` | Serviço de Faturamento | Notas, itens, processo de emissão e fechamento |
| `Korp.Gateway.Api` | API Gateway | Entrada HTTP, roteamento e políticas de borda; sem banco ou RabbitMQ |
| Angular SPA | Frontend | Interface, sessão, acompanhamento e diálogo de impressão |
| RabbitMQ | Broker | Transporte de mensagens entre Billing e Inventory; não acessa bancos |
| PostgreSQL | Banco relacional | Instância local compartilhável, com banco lógico e credencial por serviço |

---

## 8. Identidade e segurança

| Termo | Definição |
|---|---|
| Usuário administrativo | `Admin` | Único perfil funcional inicial da aplicação |
| Autenticação | Verificação de identidade por e-mail e senha |
| Autorização | Verificação da política necessária para executar uma operação |
| Access token | Token de curta duração usado para acessar APIs protegidas |
| JWT | Formato assinado do access token |
| Bearer token | Forma de envio do JWT no header `Authorization` |
| `AuthenticatedUser` | Política que exige identidade autenticada válida |
| `AdminOnly` | Política que exige o perfil administrativo |
| `401 Unauthorized` | Credencial ausente, inválida ou expirada |
| `403 Forbidden` | Identidade válida sem a permissão exigida |
| Seed administrativo | Criação inicial e idempotente do usuário de demonstração por configuração segura |
| Serviço-a-serviço | Chamada interna entre aplicações, como Billing para Inventory; autenticação ainda será definida |

---

## 9. Persistência e concorrência

| Termo | Definição |
|---|---|
| Banco lógico por serviço | Database e credencial pertencentes exclusivamente a um serviço |
| `DbContext` | Unidade de persistência local e implementação de Unit of Work do EF Core |
| Migration | Alteração de schema versionada e aplicada por processo controlado |
| Migrator | Container/processo dedicado a aplicar migrations, separado da API |
| Concorrência otimista | Operações avançam sem lock longo e detectam alteração concorrente no commit |
| `xmin` | Coluna de sistema PostgreSQL usada como token de concorrência |
| Sequence | Recurso PostgreSQL que gera números únicos e crescentes, permitindo lacunas |
| Transação local | Atomicidade restrita ao banco pertencente ao serviço |
| Constraint | Garantia do banco que complementa, mas não substitui, invariantes do domínio |

---

## 10. APIs, erros e observabilidade

| Termo | Definição |
|---|---|
| Minimal API | Modelo ASP.NET Core usado para definir endpoints com pouca infraestrutura acidental |
| `ProblemDetails` | Formato padronizado das respostas de erro HTTP |
| Código de erro | `code` estável em inglês usado por clientes e testes |
| `traceId` | Identificador técnico da execução HTTP usado para diagnóstico |
| OpenAPI | Documento gerado por cada serviço para descrever seu contrato HTTP |
| Health check | Endpoint técnico de saúde, sem executar regra de negócio |
| Log estruturado | Evento de log com template e propriedades pesquisáveis, sem segredo ou payload sensível |
| Snapshot | Cópia histórica mínima de dados pertencentes a outro agregado ou serviço |

---

## 11. Testes e qualidade

| Termo | Definição |
|---|---|
| Teste unitário | Prova isolada sem PostgreSQL, RabbitMQ ou rede |
| Teste de integração | Prova com infraestrutura real relevante no Docker |
| Teste de arquitetura | Regra automatizada que rejeita dependências proibidas entre projetos e serviços |
| Teste ponta a ponta | Prova do fluxo do usuário atravessando frontend e backend |
| Line coverage | Percentual de linhas executadas pela suíte |
| Branch coverage | Percentual de ramificações condicionais executadas |
| Gate de cobertura | Falha automática quando assembly relevante fica abaixo de 80% de linhas |
| Evidência | Resultado reproduzível que comprova um critério de aceite |
| Critério de aceite | Condição verificável que define quando um requisito foi atendido |

---

## 12. Termos proibidos ou evitados

| Evitar | Usar | Motivo |
|---|---|---|
| `EstoqueService` no código | `Inventory.Service` | Identificadores técnicos são ingleses |
| `FaturamentoService` no código | `Billing.Service` | Identificadores técnicos são ingleses |
| `IssuanceStatus` para a invoice | `InvoiceStatus` | Invoice possui apenas `Open` e `Closed` |
| “NF-e” | “nota fiscal simplificada” | Não existe integração ou validade fiscal externa |
| “exactly-once” | “at-least-once com consumers idempotentes” | Broker e Outbox podem produzir duplicidade |
| “RabbitMQ atualiza o banco” | “consumer do serviço atualiza seu banco” | Broker não acessa persistência |
| “Gateway processa emissão” | “Billing processa emissão” | Gateway apenas roteia HTTP |
| “reimpressão” | “continuação da impressão aceita” | Nota fechada não inicia novo comando nesta feature |
