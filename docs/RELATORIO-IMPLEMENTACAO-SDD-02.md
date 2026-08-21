# Relatório de Implementação do SDD-02

> Gate: C - Conclusão
> Status: Em revisão pelo engenheiro
> Data: 2026-08-20
> Especificação: `SDD-02-MODELAGEM-DE-DADOS.md`
> Plano aprovado: `PLANO-IMPLEMENTACAO-SDD-02.md`

---

## 1. Resultado

A modelagem de domínio e a persistência dos bancos Identity, Inventory e Billing foram implementadas conforme o SDD-02. Cada serviço mantém ownership exclusivo dos próprios dados; referências externas são armazenadas apenas como identificadores, sem foreign keys entre bancos.

O escopo inclui invariantes dos agregados, EF Core com PostgreSQL, migrations independentes, concorrência otimista por `xmin`, sequence fiscal simplificada, Inbox, Outbox e inicialização idempotente do usuário administrativo. Endpoints, autenticação operacional, consumers e fluxo distribuído permanecem para os SDDs posteriores.

## 2. Estrutura entregue

- modelos `Product`, `StockMovement`, `Invoice`, `InvoiceItem` e `InvoiceIssuanceProcess`;
- tipos, estados, transições e erros de domínio explícitos;
- `IdentityDbContext`, `InventoryDbContext` e `BillingDbContext`;
- configurações Fluent API, constraints, índices e conversões;
- migrations iniciais independentes para os três bancos;
- sequence PostgreSQL `invoice_number_seq`;
- concorrência otimista por coluna sistêmica `xmin`;
- Inbox e Outbox locais em Inventory e Billing;
- seed administrativo idempotente e sem sobrescrever senha existente;
- perfil Docker `persistence-tests` com três PostgreSQL 16 isolados.

## 3. Evidências executadas

| Evidência | Resultado |
|---|---|
| Build Release da solução | Aprovado, sem erros ou novos warnings |
| Testes de arquitetura | 4 aprovados |
| Testes unitários Inventory | 13 aprovados |
| Testes unitários Billing | 13 aprovados |
| Integração Identity/PostgreSQL | 2 aprovados |
| Integração Inventory/PostgreSQL | 9 aprovados |
| Integração Billing/PostgreSQL | 9 aprovados |
| Regressão Docker final | 50 testes aprovados, 0 falhas |
| Migrations pendentes | Nenhuma alteração de modelo pendente nos três contextos |

Os projetos Identity UnitTests e Gateway IntegrationTests continuam sem casos porque o SDD-02 não introduz comportamento correspondente nesses componentes. O runner informa a ausência, mas não oculta nem ignora falhas.

## 4. Cobertura consolidada

Relatórios Cobertura XML foram coletados em `artifacts/coverage/sdd-02`. A consolidação usa a união de unitários e integração por arquivo e linha. Migrations geradas foram excluídas conforme ADR-014.

| Assembly manual aplicável | Line coverage |
|---|---:|
| `Korp.Billing.Domain` | 96,34% |
| `Korp.Billing.Infrastructure` | 88,36% |
| `Korp.Identity.Infrastructure` | 87,23% |
| `Korp.Inventory.Domain` | 93,55% |
| `Korp.Inventory.Infrastructure` | 91,07% |

Assemblies Application vazios possuem 100% trivial e não são usados para compensar resultados. APIs e Gateway ainda contêm somente bootstrap de SDD-01 e ficam fora do gate desta atividade.

## 5. Garantias verificadas

- códigos de produto são normalizados e únicos sem distinção de caixa;
- saldo nunca fica negativo e duas baixas concorrentes sobre a última unidade permitem somente um commit;
- movimentos não podem duplicar o mesmo efeito lógico de nota e produto;
- números de nota são positivos, únicos, crescentes e admitem lacunas;
- itens pertencem à raiz Invoice e alterações concorrentes são detectadas;
- somente um processo de emissão ativo pode existir por nota;
- Inbox rejeita redelivery alterada com o mesmo `MessageId`;
- Outbox preserva intenção, lease, tentativa, erro e confirmação de publicação;
- referências a produto e usuário não criam acoplamento físico entre serviços;
- tabelas fiscais especulativas não foram antecipadas;
- seed administrativo pode ser repetido sem duplicação ou troca de senha.

## 6. Ocorrências tratadas

O runner inicialmente tentou reutilizar assets NuGet produzidos no Windows dentro do Linux. O comando do perfil foi corrigido para restaurar dependências no próprio contêiner.

O teste de chave da Inbox foi isolado em dois `DbContext`, garantindo que a rejeição seja comprovada pelo PostgreSQL, e não antecipada pelo change tracker do EF Core.

Uma asserção temporal foi alinhada à precisão de microssegundos do PostgreSQL. A tolerância máxima é de 1 μs e não altera a regra funcional.

## 7. Limites preservados

- não existem endpoints ou casos de uso funcionais novos;
- RabbitMQ não foi introduzido nesta fase;
- Inbox e Outbox ainda não possuem dispatcher ou consumer;
- Identity ainda não emite JWT;
- a sequence existe, mas sua reserva será coordenada pelo caso de uso do Billing;
- o perfil criado serve às provas de persistência; a topologia completa permanece no SDD-11.

## 8. Recomendação para o Gate C

O SDD-02 atende ao plano aprovado, às invariantes, às provas PostgreSQL e ao gate de cobertura. A implementação está pronta para revisão do engenheiro. Após aprovação explícita, o SDD-02 e este relatório podem ser marcados como `Validado`, e o próximo trabalho recomendado é o Gate B do SDD-03.
