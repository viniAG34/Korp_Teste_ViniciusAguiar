# Relatório de Implementação - SDD-07

> Gate: C - Conclusão
> Status: Aprovado pelo engenheiro
> Data: 2026-08-22
> Gate C aprovado em: 2026-08-22
> SDD: `SDD-07-EMISSAO-E-CONSISTENCIA-DISTRIBUIDA.md`
> Plano: `PLANO-IMPLEMENTACAO-SDD-07.md`

---

## 1. Resultado

O fluxo assíncrono de emissão foi implementado entre Billing, RabbitMQ e Inventory com Transactional Outbox, publisher confirms, Inbox idempotente, acknowledgment manual, retry limitado, DLQ, estados operacionais e recuperação após indisponibilidade. Billing fecha a invoice somente após o resultado confirmado do Inventory; Inventory baixa o saldo, registra o movimento e produz o resultado na mesma transação local.

## 2. Funcionalidades entregues

- topologia RabbitMQ durável e versionada, validada antes do consumo;
- dispatchers independentes para as Outboxes de Billing e Inventory;
- reserva concorrente por lote, lease, publisher confirms, `mandatory` e backoff persistido;
- consumers idempotentes com Inbox, hash de payload e acknowledgment após commit;
- baixa atômica de estoque e conclusão ou rejeição atômicas da emissão;
- retry em 5, 30 e 120 segundos, DLQs e falha técnica terminal segura;
- health checks de processo, prontidão e dependências com resposta sanitizada;
- logs estruturados e métricas de baixa cardinalidade;
- shutdown de 30 segundos com rollback, redelivery e leases recuperáveis;
- recuperação do fluxo após indisponibilidade de Billing, Inventory e interrupção das conexões pelo RabbitMQ.

## 3. Critérios e evidências

Os 22 critérios CA-DST possuem destino verificável em testes unitários, integrações com PostgreSQL e RabbitMQ reais, testes de arquitetura e sete cenários distribuídos. TST-DST-004 e TST-DST-005 têm suas garantias comprovadas por provas específicas separadas, sem teste monolítico adicional. TST-DST-020 cobre a recuperação dirigida de Inventory, Billing e broker.

## 4. Testes e validações

- regressão serial da solução: 180 aprovados, 0 falhas e 0 ignorados;
- suíte distribuída: 7 aprovados, 0 falhas e 0 ignorados;
- Gateway: nenhum teste descoberto, condição preexistente destinada ao SDD-08;
- `git diff --check`: sem erro, apenas avisos locais de conversão LF/CRLF.

## 5. Cobertura

| Assembly | Line coverage aplicável |
|---|---:|
| `Korp.Billing.Api` | 87,86% |
| `Korp.Billing.Application` | 90,88% |
| `Korp.Billing.Domain` | 96,86% |
| `Korp.Billing.Infrastructure` | 80,09% |
| `Korp.Inventory.Api` | 86,02% |
| `Korp.Inventory.Application` | 97,55% |
| `Korp.Inventory.Domain` | 96,64% |
| `Korp.Inventory.Infrastructure` | 80,48% |

Código gerado, migrations, factories de design time e `Program.cs` foram excluídos apenas do cálculo manual aplicável, conforme ADR-014. Branch coverage foi publicada nos relatórios Cobertura sem gate percentual.

## 6. Ocorrências técnicas

1. O teste de interrupção pelo RabbitMQ revelou que consumers permaneciam aguardando indefinidamente após o canal fechar. Billing e Inventory agora detectam o canal fechado e recriam o ciclo de consumo após a reconexão.
2. A falha de publicação da Outbox recebeu prova direta de liberação do lease, incremento de tentativa, erro estável e backoff persistido.
3. A janela entre publicação e confirmação local foi reproduzida por republicação do mesmo `MessageId`, sem segunda baixa ou segundo efeito.

## 7. Limitações

- não existe garantia exactly-once; a garantia implementada é at-least-once com efeitos idempotentes;
- DLQs exigem intervenção operacional, conforme o SDD;
- a composição executável final e a observabilidade agregada pertencem ao SDD-11;
- o Gateway e o fluxo pelo navegador serão comprovados nos SDD-08 a SDD-10.

## 8. Avaliação do Gate C

Gate C **aprovado pelo engenheiro em 2026-08-22**. O SDD-07 está validado, sem pendências ocultas dentro de seu escopo, e suas limitações operacionais estão registradas.

## 9. Próximo passo

Elaborar e submeter à aprovação o Gate B do SDD-08 - API Gateway antes de alterar código de produção.
