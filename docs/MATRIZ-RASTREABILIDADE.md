# Matriz Inicial de Rastreabilidade

> Status: Baseline documental aprovada
> Versão: 1.0
> Última atualização: 2026-08-20
> Aprovação: 2026-08-20
> Data: 2026-08-17
> Fonte primária: `../teste tecnico KORP ERP.pdf`

---

## 1. Finalidade

Esta matriz liga cada requisito à decisão que o interpreta e ao SDD responsável por transformá-lo em regras, critérios de aceite, implementação, testes e evidências.

O SDD-01 já possui implementação e evidências. Os demais requisitos possuem destino, critérios e provas planejadas nos SDDs aprovados; arquivos, resultados e evidências reais permanecem pendentes até a implementação correspondente.

Classificações:

- `OBR`: requisito obrigatório do desafio;
- `APR`: requisito de apresentação ou detalhamento técnico;
- `OPA`: requisito originalmente opcional e adotado;
- `OPE`: requisito originalmente opcional e excluído;
- `DIF`: diferencial acrescentado pelo projeto;
- `QLT`: requisito interno de qualidade.

---

## 2. Requisitos obrigatórios do desafio

| ID | Origem | Requisito normalizado | Decisão ou regra | SDD responsável | Aceite planejado | Implementação / teste / evidência |
|---|---|---|---|---|---|---|
| OBR-001 | Objetivo | Aplicação web desenvolvida em Angular | Angular SPA acessa somente o Gateway | SDD-01, SDD-08, SDD-09 | Aplicação carrega e permite executar o fluxo principal | Pendente de implementação |
| OBR-002 | Produtos | Cadastrar produto | Inventory é proprietário do cadastro | SDD-03, SDD-05 | Produto válido é persistido e pode ser consultado | Pendente de implementação |
| OBR-003 | Produtos | Código obrigatório | `Product.Code`, máximo 50, normalizado e único | SDD-02, SDD-05 | Ausência ou duplicidade é rejeitada | Pendente de implementação |
| OBR-004 | Produtos | Descrição obrigatória | `Product.Description`, trim e máximo 200 | SDD-02, SDD-05 | Ausência e tamanho inválido são rejeitados | Pendente de implementação |
| OBR-005 | Produtos | Saldo obrigatório | `Balance` inteiro, aceita zero e nunca negativo | SDD-02, SDD-05 | Saldo inicial é persistido e invariantes são protegidas | Pendente de implementação |
| OBR-006 | Produtos | Produto previamente cadastrado pode ser usado na nota | Billing valida referência por HTTP interno e guarda snapshot | SDD-03, SDD-05, SDD-06 | Produto existente é incluído; inexistente não é incluído | Pendente de implementação |
| OBR-007 | Notas | Criar nota com numeração sequencial | Sequence PostgreSQL, positiva, única, crescente e com lacunas permitidas | SDD-02, SDD-06 | Notas recebem números distintos e ordenáveis | Pendente de implementação |
| OBR-008 | Notas | Status `Aberta` ou `Fechada` | `InvoiceStatus.Open` e `Closed` | SDD-02, SDD-06 | Nova nota nasce aberta e somente confirmação a fecha | Pendente de implementação |
| OBR-009 | Notas | Incluir múltiplos produtos e quantidades | `InvoiceItem`; produto único por nota; quantidade inteira positiva | SDD-02, SDD-03, SDD-06 | Nota aceita vários produtos válidos e respectivas quantidades | Pendente de implementação |
| OBR-010 | Impressão | Botão visível e intuitivo | Ação `PrintInvoice` no detalhe da nota | SDD-09 | Botão é identificável e habilitado somente quando elegível | Pendente de implementação |
| OBR-011 | Impressão | Exibir indicador de processamento | Polling de `InvoiceIssuanceProcess` | SDD-03, SDD-07, SDD-09 | Interface informa processamento e atraso sem presumir falha | Pendente de implementação |
| OBR-012 | Impressão | Após finalizar, fechar a nota | Billing fecha após `StockDeductionCompleted` | SDD-06, SDD-07 | `Completed` e `ClosedAtUtc` são persistidos atomicamente | Pendente de implementação |
| OBR-013 | Impressão | Não imprimir nota diferente de aberta | Frontend oculta ação e backend rejeita novo comando | SDD-03, SDD-06, SDD-09 | Nota fechada retorna conflito sem novo efeito | Pendente de implementação |
| OBR-014 | Impressão | Atualizar saldo pelas quantidades utilizadas | Inventory baixa todos os itens atomicamente | SDD-05, SDD-07 | Exemplo 10 menos 2 resulta 8; nenhuma baixa parcial | Pendente de implementação |
| OBR-015 | Impressão | Disponibilizar ação real de impressão | HTML e `window.print()`, sem PDF | SDD-09 | Diálogo é solicitado somente após conclusão | Pendente de implementação |
| OBR-016 | Arquitetura | No mínimo dois microsserviços | Inventory e Billing são serviços de domínio independentes | SDD-01, SDD-05, SDD-06 | Processos, APIs e bancos separados são executáveis | Pendente de implementação |
| OBR-017 | Arquitetura | Serviço de Estoque controla produtos e saldos | Inventory é dono exclusivo de Product, Balance e StockMovement | SDD-02, SDD-05 | Nenhum outro serviço altera saldo diretamente | Pendente de implementação |
| OBR-018 | Arquitetura | Serviço de Faturamento gerencia notas | Billing é dono exclusivo de Invoice e emissão | SDD-02, SDD-06 | Nenhum outro serviço altera nota diretamente | Pendente de implementação |
| OBR-019 | Falhas | Demonstrar falha de um microsserviço | Inventory parado com mensagem preservada na fila | SDD-07, SDD-11 | Cenário reproduzível mantém a solicitação pendente | Pendente de implementação |
| OBR-020 | Falhas | Recuperar-se da falha | Consumer retoma automaticamente quando Inventory volta | SDD-07, SDD-10, SDD-11 | Reinício conclui ou rejeita sem nova solicitação do usuário | Pendente de implementação |
| OBR-021 | Falhas | Fornecer feedback apropriado | Estados, `isDelayed`, rejeição e erros compreensíveis | SDD-03, SDD-07, SDD-09 | Usuário distingue processamento, demora, rejeição e falha | Pendente de implementação |
| OBR-022 | Persistência | Conexão real com banco | PostgreSQL, EF Core, migrations e bancos isolados | SDD-01, SDD-02, SDD-11 | Dados sobrevivem ao reinício da aplicação | Pendente de implementação |

---

## 3. Requisitos de apresentação e detalhamento

| ID | Origem | Requisito normalizado | Destino | Aceite planejado | Evidência |
|---|---|---|---|---|---|
| APR-001 | Objetivo | Vídeo demonstra telas | SDD-12 | Todas as telas relevantes são exibidas | Pendente de implementação |
| APR-002 | Objetivo | Vídeo demonstra funcionalidades | SDD-12 | Fluxo principal, falha e diferenciais são executados | Pendente de implementação |
| APR-003 | Objetivo | Apresentar detalhamento técnico | SDD-12 | Documento explica decisões, limites e arquitetura | Pendente de implementação |
| APR-004 | Angular | Explicar lifecycle hooks utilizados | SDD-09, SDD-12 | Somente hooks realmente usados são apresentados com exemplos | Pendente de implementação |
| APR-005 | Angular | Explicar RxJS, se utilizado | SDD-09, SDD-12 | Operadores e fluxo reais são demonstrados | Pendente de implementação |
| APR-006 | Bibliotecas | Informar bibliotecas e finalidades | SDD-01, SDD-12 | Inventário corresponde aos manifests e ao código | Pendente de implementação |
| APR-007 | Visual | Informar biblioteca de componentes | SDD-09, SDD-12 | Angular Material e customizações são explicados | Pendente de implementação |
| APR-008 | Backend | Informar frameworks C# | SDD-01, SDD-12 | Stack realmente utilizada é documentada | Pendente de implementação |
| APR-009 | Backend | Explicar erros e exceções | SDD-03, SDD-12 | ProblemDetails, exceptions e mensagens são demonstrados | Pendente de implementação |
| APR-010 | C# | Explicar uso de LINQ | SDD-05, SDD-06, SDD-12 | Exemplos reais, não artificiais, são apresentados | Pendente de implementação |
| APR-011 | Golang | Informar gerenciamento se aplicável | SDD-12 | Marcado como não aplicável porque backend é C# | Pendente de implementação |

---

## 4. Requisitos originalmente opcionais

| ID | Classificação | Requisito | Decisão | SDD responsável | Aceite planejado |
|---|---|---|---|---|---|
| OPA-001 | Adotado | Concorrência sobre produto com saldo 1 | Concorrência otimista via `xmin`; somente uma nota vence | SDD-02, SDD-05, SDD-07, SDD-10 | Duas baixas simultâneas resultam em uma conclusão e uma rejeição |
| OPA-002 | Adotado | Idempotência | Idempotency-Key, Inbox, Outbox e efeitos at-least-once seguros | SDD-02, SDD-03, SDD-07, SDD-10 | Repetição HTTP ou de mensagem não duplica processo nem saldo |
| OPE-001 | Excluído | Inteligência artificial | Excluída para priorizar completude e consistência | Nenhum | Ausência registrada na documentação final |

---

## 5. Diferenciais acrescentados

| ID | Diferencial | Justificativa | Decisão | SDD responsável | Evidência planejada |
|---|---|---|---|---|---|
| DIF-001 | API Gateway | Entrada única e políticas de borda | YARP stateless, sem banco ou RabbitMQ | SDD-01, SDD-08 | Rotas e limites verificados por integração |
| DIF-002 | Autenticação | Operações empresariais não ficam anônimas | Identity.Service, ASP.NET Core Identity e JWT | SDD-02, SDD-03, SDD-04, SDD-09 | Login e defesa em profundidade demonstrados |
| DIF-003 | Autorização | Separar identidade válida de permissão | Políticas `AuthenticatedUser` e `AdminOnly` | SDD-03, SDD-04, SDD-08 | Casos `401` e `403` testados |
| DIF-004 | Mensageria confiável | Recuperar fluxo distribuído e evitar perda | RabbitMQ, Outbox, Inbox, retry e DLQ | SDD-02, SDD-03, SDD-07, SDD-11 | Indisponibilidade e redelivery demonstrados |
| DIF-005 | Process Manager | Expor progresso sem alterar status fiscal | `InvoiceIssuanceProcess` no Billing | SDD-02, SDD-06, SDD-07 | Estados e transições testados |
| DIF-006 | Clean Architecture | Isolar domínio de frameworks | Quatro projetos por serviço persistente | SDD-01, SDD-10 | Testes de arquitetura e referências válidas |
| DIF-007 | Docker-first | Ambiente reproduzível | Build, execução, migrations e testes via Compose | SDD-01, SDD-11 | Ambiente sobe sem SDK local |
| DIF-008 | OpenAPI | Tornar contratos inspecionáveis | Documento separado por serviço | SDD-03, SDD-08 | Três documentos acessíveis pelo Gateway |
| DIF-009 | Observabilidade | Diagnosticar operação distribuída | Logs estruturados e IDs correlacionados | SDD-03, SDD-07, SDD-11 | Um fluxo pode ser seguido entre serviços |
| DIF-010 | UI consistente | Acelerar e profissionalizar as telas | Angular Material com tema próprio | SDD-09 | Telas responsivas e acessíveis |

---

## 6. Requisitos internos de qualidade

| ID | Requisito | Fonte | SDD responsável | Gate planejado |
|---|---|---|---|---|
| QLT-001 | Testes unitários obrigatórios | ADR-009 | SDD-10 | Regras e casos de uso possuem testes isolados |
| QLT-002 | Testes de integração com infraestrutura real | ADR-009 | SDD-10 | PostgreSQL e RabbitMQ reais em Docker |
| QLT-003 | Mínimo de 80% de linhas por assembly relevante | ADR-014 | SDD-10 | Coverlet falha abaixo do limite |
| QLT-004 | Branch coverage publicada | ADR-014 | SDD-10 | Métrica aparece na evidência, sem gate inicial |
| QLT-005 | Testes de arquitetura | ADR-006 | SDD-10 | Dependências proibidas falham a suíte |
| QLT-006 | Critérios de aceite rastreáveis | AGENTS.md | Todos | Cada critério aponta para teste e evidência |
| QLT-007 | Segredos fora do repositório e logs | ADR-013 | SDD-04, SDD-11 | Varredura e testes não encontram credenciais |
| QLT-008 | Build sem erros e avisos injustificados | AGENTS.md | SDD-01, SDD-10 | Gate de build aprovado |

---

## 7. Evidências consolidadas do SDD-01

| IDs atendidos nesta fase | Implementação | Teste ou inspeção | Evidência |
|---|---|---|---|
| OBR-001 (estrutura), DIF-010 (base visual) | `frontend/korp-erp-web`, Angular 21 standalone e Angular Material | Build Release e 2 testes Vitest | `RELATORIO-IMPLEMENTACAO-SDD-01.md`, seção 6 |
| OBR-016 (estrutura), DIF-006 | Projetos Identity, Inventory e Billing isolados em quatro camadas | `ProjectReferenceRulesTests`: 4/4 | `RELATORIO-IMPLEMENTACAO-SDD-01.md`, seções 2, 4 e 6 |
| APR-006, APR-007, APR-008 | Manifests npm, Central Package Management e inventário de versões | Inspeção de manifests e restores reproduzíveis | `RELATORIO-IMPLEMENTACAO-SDD-01.md`, seção 3 |
| DIF-001 (bootstrap) | `Korp.Gateway.Api` com YARP, sem referência a serviço, banco ou broker | Build e teste arquitetural | `RELATORIO-IMPLEMENTACAO-SDD-01.md`, seções 4 e 6 |
| DIF-007, QLT-008 | Dockerfiles multi-stage e perfis de tooling/aplicações no Compose | Build da solution, frontend e cinco imagens | `RELATORIO-IMPLEMENTACAO-SDD-01.md`, seções 5 e 6 |
| DIF-008 (bootstrap) | OpenAPI básico em Identity, Inventory e Billing | Build sem warnings e auditoria de dependência | `RELATORIO-IMPLEMENTACAO-SDD-01.md`, seções 3 e 7.1 |
| QLT-005 | Teste automatizado das referências permitidas | 4 testes aprovados | `tests/Architecture/Korp.ArchitectureTests/ProjectReferenceRulesTests.cs` |

Os termos “estrutura” e “bootstrap” indicam atendimento parcial deliberado: comportamento, persistência, rotas e fluxos permanecem vinculados aos SDDs funcionais indicados nas linhas originais.

---

## 8. Planejamento consolidado do SDD-02

| IDs de requisito | Decisões de modelagem | Critérios | Testes planejados |
|---|---|---|---|
| OBR-003, OBR-004, OBR-005, OBR-017 | `Product`, normalização, constraints, saldo e `xmin` | CA-DATA-03, CA-DATA-04 | TST-DATA-004 a TST-DATA-006 |
| OBR-007, OBR-008, OBR-018 | `Invoice`, sequence, estados e bloqueio operacional | CA-DATA-06, CA-DATA-08 | TST-DATA-009, TST-DATA-010, TST-DATA-012 |
| OBR-006, OBR-009 | `InvoiceItem`, snapshot e unicidade por produto | CA-DATA-07 | TST-DATA-010, TST-DATA-011 |
| OBR-012, OBR-014 | Processo de emissão, fechamento e movimento auditável | CA-DATA-05, CA-DATA-09 | TST-DATA-007, TST-DATA-008, TST-DATA-013 |
| OBR-022, DIF-007 | Três bancos, migrations independentes e processo controlado | CA-DATA-01, CA-DATA-12 | TST-DATA-001, TST-DATA-002, TST-DATA-017 |
| OPA-001 | Concorrência otimista PostgreSQL por `xmin` | CA-DATA-04, CA-DATA-07, CA-DATA-08 | TST-DATA-006, TST-DATA-011, TST-DATA-012 |
| OPA-002, DIF-004 | Idempotency Key, Inbox, Outbox e unicidade de efeitos | CA-DATA-05, CA-DATA-08, CA-DATA-10, CA-DATA-11 | TST-DATA-008, TST-DATA-012, TST-DATA-014 a TST-DATA-016 |
| DIF-002, DIF-003 | Modelo mínimo do Identity e autoria externa | CA-DATA-02, CA-DATA-13 | TST-DATA-003, TST-DATA-018 |
| DIF-005 | `InvoiceIssuanceProcess` separado da invoice | CA-DATA-08, CA-DATA-09 | TST-DATA-012, TST-DATA-013 |
| DIF-006, QLT-005 | Agregados e persistência isolados por serviço | CA-DATA-01 | TST-DATA-002 |
| QLT-001, QLT-002, QLT-003 | Testes unitários, PostgreSQL real e cobertura da lógica manual | Todos | TST-DATA-001 a TST-DATA-019 |
| QLT-006, QLT-007, QLT-008 | Rastreabilidade, segredos isolados e build verificável | CA-DATA-02, CA-DATA-12, CA-DATA-13, CA-DATA-14 | TST-DATA-003, TST-DATA-017 a TST-DATA-019 |

### Evidência de implementação do SDD-02

| Escopo | Implementação | Prova executada | Resultado |
|---|---|---|---|
| Identity | `ApplicationUser`, contexto, migration e seed idempotente | TST-DATA-001 a TST-DATA-003 | 2 testes PostgreSQL aprovados |
| Inventory | `Product`, `StockMovement`, Inbox, Outbox, constraints e `xmin` | TST-DATA-004 a TST-DATA-008 e TST-DATA-014 a TST-DATA-019 aplicáveis | 13 unitários e 9 integrações aprovados |
| Billing | `Invoice`, itens, processo, sequence, Inbox, Outbox, constraints e `xmin` | TST-DATA-009 a TST-DATA-019 aplicáveis | 13 unitários e 9 integrações aprovados |
| Arquitetura | Bancos isolados e ausência de referências físicas externas | TST-DATA-002 e TST-DATA-018 | 4 testes arquiteturais e inspeções PostgreSQL aprovados |
| Qualidade | Build, regressão Docker e cobertura por assembly | QLT-001 a QLT-003 | 50 testes; cobertura aplicável entre 87,23% e 96,34% |

O SDD-02 está implementado e aguarda aprovação do Gate C. Evidências detalhadas, limitações e ocorrências estão em `RELATORIO-IMPLEMENTACAO-SDD-02.md`.

---

## 9. Planejamento consolidado do SDD-03

| IDs de requisito | Decisões de contrato | Critérios | Testes planejados |
|---|---|---|---|
| OBR-002, OBR-003, OBR-004, OBR-005 | Rotas e DTOs públicos de Product, normalização observável e erros | CA-CON-02, CA-CON-04 | TST-CON-003, TST-CON-004, TST-CON-006 |
| OBR-006, OBR-009 | Consulta interna autenticada, snapshots controlados por Billing e contratos de itens | CA-CON-01, CA-CON-04, CA-CON-05 | TST-CON-001, TST-CON-002, TST-CON-008, TST-CON-009 |
| OBR-007, OBR-008 | Contratos de criação e consulta preservam número e estados da invoice | CA-CON-02, CA-CON-05 | TST-CON-007 a TST-CON-009 |
| OBR-011, OBR-021, DIF-005 | Processo consultável, cinco estados, atraso derivado e polling orientado por header | CA-CON-09 | TST-CON-013 |
| OBR-012, OBR-013, OBR-014 | `PrintInvoice`, bloqueio, confirmação assíncrona e baixa atômica | CA-CON-05, CA-CON-06, CA-CON-10, CA-CON-13 | TST-CON-008 a TST-CON-010, TST-CON-014, TST-CON-019, TST-CON-022 |
| OBR-017, OBR-018 | Ownership explícito de Product/Balance e Invoice/Issuance | CA-CON-01, CA-CON-04 | TST-CON-001, TST-CON-006, TST-CON-007 |
| OBR-019, OBR-020, DIF-004 | Contratos duráveis, redelivery, Inbox, Outbox e falha técnica explícita | CA-CON-06, CA-CON-10 a CA-CON-13 | TST-CON-010, TST-CON-014, TST-CON-016 a TST-CON-019 |
| OPA-002 | Chave HTTP global, Inbox, hash e duplicidade lógica segura | CA-CON-07, CA-CON-08, CA-CON-11, CA-CON-12 | TST-CON-011, TST-CON-012, TST-CON-017 a TST-CON-019 |
| DIF-001, DIF-003 | Superfície do Gateway e políticas por operação | CA-CON-01, CA-CON-02 | TST-CON-001, TST-CON-002 |
| DIF-002 | Login JWT e resposta sem enumeração de usuário | CA-CON-02, CA-CON-03 | TST-CON-002, TST-CON-004, TST-CON-005 |
| DIF-008 | OpenAPI separado por serviço e fiel aos endpoints | CA-CON-16 | TST-CON-021 |
| DIF-009 | Correlação HTTP, causalidade de eventos e diagnóstico sanitizado | CA-CON-02, CA-CON-12, CA-CON-14 | TST-CON-004, TST-CON-018, TST-CON-020 |
| QLT-001, QLT-002, QLT-003 | Testes unitários, contratos e integração com infraestrutura real | Todos | TST-CON-001 a TST-CON-022 |
| QLT-006, QLT-007, QLT-008 | Rastreabilidade, proteção de dados e contratos verificáveis | CA-CON-02, CA-CON-03, CA-CON-14, CA-CON-16 | TST-CON-002 a TST-CON-005, TST-CON-020, TST-CON-021 |

### Evidência contratual do SDD-03

| Escopo | Implementação | Prova | Estado |
|---|---|---|---|
| Eventos V1 | Envelope, quatro payloads, constantes e fixtures em `Korp.Shared.Contracts` | TST-CON-014 e TST-CON-015 | Atendido no nível contratual |
| Identity HTTP | DTOs de login e política JSON | TST-CON-003 e inspeção de campos sensíveis | Baseline atendida; comportamento no SDD-04 |
| Product HTTP | DTOs público/interno, criação e paginação | TST-CON-003 e inspeção de ownership | Baseline atendida; endpoints no SDD-05 |
| Invoice HTTP | DTOs, estados, itens, processo, ETag e Idempotency Key | TST-CON-003, TST-CON-009 e TST-CON-013 no nível puro | Baseline atendida; comportamento no SDD-06 |
| Fronteiras | Shared Contracts sem domínio, EF Core ou RabbitMQ | Teste arquitetural automatizado | Atendido |
| Cobertura | Assemblies manuais aplicáveis | Cobertura entre 84,38% e 95,95% | Atendido |

CA-CON-04, CA-CON-09, CA-CON-10, CA-CON-14 e CA-CON-15 possuem evidência contratual direta. Os demais critérios conservam destino explícito nos SDDs 04 a 08 e não são declarados funcionalmente atendidos antes dos endpoints, políticas, Gateway, PostgreSQL transacional ou RabbitMQ correspondentes.

O SDD-03 está implementado como baseline contratual e aguarda revisão do relatório. A validação integral será cumulativa, após as provas dos SDDs proprietários.

---

## 10. Planejamento consolidado do SDD-04

| IDs de requisito | Decisões do Identity | Critérios | Testes planejados |
|---|---|---|---|
| DIF-002 | Identity proprietário, login seguro, seed idempotente e JWT curto | CA-ID-01 a CA-ID-08, CA-ID-14 | TST-ID-001 a TST-ID-016, TST-ID-022 |
| DIF-003 | Claims mínimas, `AuthenticatedUser`, `AdminOnly` e validação independente | CA-ID-08 a CA-ID-10 | TST-ID-014 a TST-ID-018 |
| DIF-008 | OpenAPI fiel ao login anônimo | CA-ID-13 | TST-ID-021 |
| DIF-009 | Correlação, logs e métricas sem credenciais | CA-ID-11, CA-ID-12 | TST-ID-019, TST-ID-020, TST-ID-023 |
| APR-008, APR-009 | Stack C#/.NET, Identity, JWT e tratamento de falhas documentados | CA-ID-11, CA-ID-13 | TST-ID-019, TST-ID-021 |
| QLT-001, QLT-002, QLT-003 | Testes unitários, integração PostgreSQL e cobertura mínima | Todos | TST-ID-001 a TST-ID-023 |
| QLT-005 | Fronteira e ausência de dependências ou features proibidas | CA-ID-01, CA-ID-10, CA-ID-14 | TST-ID-001, TST-ID-018, TST-ID-022 |
| QLT-006, QLT-007, QLT-008 | Rastreabilidade, segredos protegidos e startup verificável | CA-ID-03, CA-ID-11, CA-ID-12 | TST-ID-005, TST-ID-006, TST-ID-019, TST-ID-020 |

### Evidências implementadas do SDD-04

| IDs | Implementação e prova | Estado |
|---|---|---|
| DIF-002, CA-ID-01 a CA-ID-07, CA-ID-11, CA-ID-13 e CA-ID-14 | Login anônimo, Identity isolado, seed idempotente, lockout, JWT HS256, Problem Details e OpenAPI; 3 testes unitários e 28 de integração | Implementado e verificado |
| CA-ID-12, QLT-007 | Respostas de erro sanitizadas e segredo sentinela ausente; configuração real permanece fora do repositório | Implementado no limite atual; observabilidade operacional acumula no SDD-11 |
| CA-ID-08 a CA-ID-10, DIF-003 | Emissão e validação estrita do token comprovadas no Identity; validação local e policies nas APIs consumidoras | Parcial cumulativo; conclusão nos SDDs 05, 06 e 08 |
| QLT-001 a QLT-004, QLT-008 | Build Release 0/0; regressão 102/102; Identity 31/31; cobertura manual aplicável de linhas: API 98,47%, Application 100%, Infrastructure 96,05% | Verificado; branches publicadas no relatório |

O relatório `RELATORIO-IMPLEMENTACAO-SDD-04.md` contém os comandos, percentuais brutos, exclusões de código gerado, riscos residuais e avaliação do Gate C.

---

## 11. Planejamento consolidado do SDD-05

| IDs de requisito | Decisões do Inventory | Critérios | Testes planejados |
|---|---|---|---|
| OBR-002 a OBR-005 | `CreateProduct`, normalização, saldo inicial e unicidade | CA-INV-02 a CA-INV-05 | TST-INV-002 a TST-INV-010 |
| OBR-006 | Snapshot interno autenticado sem exposição de saldo | CA-INV-06, CA-INV-16 | TST-INV-011, TST-INV-012, TST-INV-024 |
| OBR-014, OBR-017 | Ownership, baixa atômica e movimentos consistentes | CA-INV-01, CA-INV-07 a CA-INV-10 | TST-INV-001, TST-INV-013 a TST-INV-016, TST-INV-022, TST-INV-028 |
| OBR-019, OBR-020 | Falhas técnicas recuperáveis sem baixa parcial | CA-INV-14, CA-INV-15 | TST-INV-017, TST-INV-018, TST-INV-027 |
| OPA-001 | Concorrência `xmin` e disputa da última unidade | CA-INV-11, CA-INV-14 | TST-INV-017, TST-INV-018 |
| OPA-002, DIF-004 | Repetição técnica e lógica sem novo efeito | CA-INV-12, CA-INV-13 | TST-INV-019 a TST-INV-021 |
| DIF-002, DIF-003 | Validação JWT, políticas e autoria por `sub` | CA-INV-06, CA-INV-16 | TST-INV-006, TST-INV-011, TST-INV-012, TST-INV-024 |
| DIF-006, QLT-005 | Clean Architecture e portas específicas | CA-INV-01, CA-INV-19 | TST-INV-001, TST-INV-023 |
| DIF-008 | OpenAPI público e rota interna identificada | CA-INV-18 | TST-INV-026 |
| DIF-009 | Logs, métricas e correlação da baixa | CA-INV-17 | TST-INV-025 |
| APR-009, APR-010 | Erros e usos reais de LINQ documentados | CA-INV-05, CA-INV-15, CA-INV-19 | TST-INV-010, TST-INV-023, TST-INV-024 |
| QLT-001 a QLT-004 | Unitários, integração PostgreSQL e cobertura | Todos | TST-INV-001 a TST-INV-028 |
| QLT-006 a QLT-008 | Rastreabilidade, segurança e comportamento verificável | CA-INV-15 a CA-INV-18 | TST-INV-024 a TST-INV-028 |

### Evidências implementadas do SDD-05

| IDs | Implementação e prova | Estado |
|---|---|---|
| OBR-002 a OBR-005, OBR-017 | cadastro, consultas, normalização, saldo inicial e ownership do Inventory | Implementado e verificado |
| OBR-006 | snapshot interno sem saldo, autoria ou versão | Implementado; bloqueio externo da rota acumula no SDD-08 |
| OBR-014, OPA-001 | baixa transacional, movimentos, `xmin` e reavaliação limitada | Implementado e verificado em PostgreSQL |
| OPA-002 | repetição lógica equivalente sem nova baixa e divergência como falha técnica | Implementado no caso de uso; Inbox/Outbox e redelivery acumulam no SDD-07 |
| DIF-002, DIF-003 | JWT validado localmente, `AuthenticatedUser`, `AdminOnly` e autoria por `sub` | Implementado e verificado |
| DIF-008 | quatro rotas aprovadas presentes no OpenAPI e rotas excluídas ausentes | Implementado; enriquecimento documental final acumula no SDD-08/12 |
| DIF-009 | log estruturado de criação e métricas de criação/baixa/conflito/duração | Implementado; logs distribuídos da baixa acumulam no adapter do SDD-07 |
| QLT-001 a QLT-004, QLT-008 | 19 testes unitários, 23 integrações, build 0/0 e cobertura por assembly acima de 80% | Verificado; branches publicadas no relatório |

O relatório `RELATORIO-IMPLEMENTACAO-SDD-05.md` registra comandos, percentuais, deferimentos cumulativos e riscos residuais.

---

## 12. Planejamento consolidado do SDD-06

| IDs de requisito | Decisões do Billing | Critérios | Testes planejados |
|---|---|---|---|
| OBR-006, OBR-009 | Snapshot interno, produto único e mutações protegidas por ETag | CA-BIL-05 a CA-BIL-10 | TST-BIL-007 a TST-BIL-018 |
| OBR-007, OBR-008, OBR-018 | Criação, sequence, estados e ownership de Invoice | CA-BIL-01 a CA-BIL-04 | TST-BIL-001 a TST-BIL-006, TST-BIL-039 |
| OBR-010, OBR-011 | `PrintInvoice` durável e processo consultável | CA-BIL-11, CA-BIL-12, CA-BIL-21, CA-BIL-22 | TST-BIL-019 a TST-BIL-021, TST-BIL-032, TST-BIL-033 |
| OBR-012, OBR-013, OBR-015 | Fechamento após confirmação, bloqueio e impressão sem nova baixa | CA-BIL-09, CA-BIL-16, CA-BIL-19, CA-BIL-28 | TST-BIL-017, TST-BIL-026, TST-BIL-029, TST-BIL-038 |
| OBR-019 a OBR-021 | Estados de processamento, rejeição, intervenção e ausência de decisão por timeout | CA-BIL-15 a CA-BIL-22, CA-BIL-24 | TST-BIL-025 a TST-BIL-035 |
| OPA-002 | Chave global, replay e exclusão mútua | CA-BIL-11 a CA-BIL-14, CA-BIL-19, CA-BIL-20 | TST-BIL-019 a TST-BIL-024, TST-BIL-029 a TST-BIL-031 |
| DIF-002, DIF-003 | JWT local, políticas, autoria e bearer propagado | CA-BIL-23 | TST-BIL-010, TST-BIL-034 |
| DIF-004, DIF-005 | Outbox inicial, Process Manager e transições idempotentes | CA-BIL-11, CA-BIL-15 a CA-BIL-22 | TST-BIL-019, TST-BIL-025 a TST-BIL-033 |
| DIF-006, QLT-005 | Clean Architecture, portas específicas e ausência de dependências proibidas | CA-BIL-01, CA-BIL-27, CA-BIL-28 | TST-BIL-001, TST-BIL-038 |
| DIF-008 | OpenAPI com headers e respostas de Billing | CA-BIL-26 | TST-BIL-037 |
| DIF-009 | Correlação, logs e métricas do processo | CA-BIL-25 | TST-BIL-036 |
| APR-009, APR-010 | Tratamento de falhas e LINQ real documentados | CA-BIL-04, CA-BIL-24, CA-BIL-27 | TST-BIL-005, TST-BIL-006, TST-BIL-035 |
| QLT-001 a QLT-004 | Testes unitários, integração real e cobertura | Todos | TST-BIL-001 a TST-BIL-039 |
| QLT-006 a QLT-008 | Rastreabilidade, segurança e comportamento verificável | CA-BIL-23 a CA-BIL-28 | TST-BIL-034 a TST-BIL-039 |

### Evidência de implementação do SDD-06

| Requisitos | Evidência implementada | Estado |
|---|---|---|
| OBR-006, OBR-009 | Cliente HTTP de Inventory, snapshot validado, itens, ETag e mutações HTTP | Verificado por testes unitários, de contrato e API/PostgreSQL |
| OBR-007, OBR-008, OBR-018 | Sequence real, criação, estados, projeções e ownership exclusivo | Verificado por migration, arquitetura, domínio e API |
| OBR-011 | Processo consultável, `isDelayed` e `Retry-After` | Backend verificado; polling visual permanece no SDD-09 |
| OBR-012, OBR-013 | Transições internas, bloqueio e fechamento testáveis | Domínio/Application verificados; transporte real permanece no SDD-07 |
| OPA-002 | Replay, chave global e exclusão mútua com disputas HTTP reais | Verificado com PostgreSQL e requests concorrentes |
| DIF-002, DIF-003 | JWT local, policies, autoria e propagação de bearer | Verificado por API e handler HTTP controlado |
| DIF-004, DIF-005 | Outbox atômica, Process Manager e terminais idempotentes | Aceite durável verificado; publisher, Inbox e consumers permanecem no SDD-07 |
| DIF-006, QLT-005 | Camadas, portas específicas e ausência de escopo fiscal | Verificado por build, arquitetura e inspeção de rotas/esquema |
| DIF-008, DIF-009 | OpenAPI, correlação, logs estruturados e métricas | Verificado na API; fluxo distribuído completo acumula no SDD-07 |
| QLT-001 a QLT-004, QLT-008 | 25 testes unitários, 42 integrações, regressão e cobertura por assembly acima de 80% | Verificado; branches publicadas no relatório |

O relatório `RELATORIO-IMPLEMENTACAO-SDD-06.md` registra resultados, percentuais, ocorrências, deferimentos cumulativos e riscos residuais.

---

## 13. Planejamento consolidado do SDD-07

| IDs de requisito | Decisões da consistência distribuída | Critérios | Testes planejados |
|---|---|---|---|
| OBR-011, OBR-021, DIF-005 | Processo assíncrono observável, atraso derivado e estados recuperáveis | CA-DST-01, CA-DST-14, CA-DST-17 a CA-DST-22 | TST-DST-003, TST-DST-014, TST-DST-018 a TST-DST-023 |
| OBR-012, OBR-014 | Baixa atômica seguida de fechamento transacional no Billing | CA-DST-06, CA-DST-07, CA-DST-12, CA-DST-13 | TST-DST-007, TST-DST-008, TST-DST-013 |
| OBR-019, OBR-020 | Preservação por Outbox, filas duráveis, retry, redelivery e retomada | CA-DST-01 a CA-DST-06, CA-DST-15 a CA-DST-20 | TST-DST-001 a TST-DST-007, TST-DST-015 a TST-DST-021, TST-DST-025 |
| OPA-001 | Concorrência otimista reavaliada sem baixa parcial | CA-DST-06, CA-DST-07, CA-DST-10, CA-DST-11 | TST-DST-007, TST-DST-008, TST-DST-011, TST-DST-012 |
| OPA-002 | Inbox, hash, Outbox, leases e duplicidade lógica | CA-DST-04, CA-DST-05, CA-DST-08 a CA-DST-11, CA-DST-14 | TST-DST-004 a TST-DST-006, TST-DST-009 a TST-DST-014 |
| DIF-004 | RabbitMQ, publisher confirms, ack manual, retry e DLQ | CA-DST-01 a CA-DST-20 | TST-DST-001 a TST-DST-021, TST-DST-025 |
| DIF-007 | Fluxo distribuído reproduzível por Docker Compose | CA-DST-02, CA-DST-19 a CA-DST-21 | TST-DST-001, TST-DST-020 a TST-DST-022, TST-DST-025 |
| DIF-009 | Correlação, logs, métricas e health checks sanitizados | CA-DST-21, CA-DST-22 | TST-DST-022, TST-DST-023 |
| APR-003, APR-009 | Fluxo, resiliência, falhas e limites demonstráveis | CA-DST-01, CA-DST-15 a CA-DST-22 | TST-DST-003, TST-DST-015 a TST-DST-025 |
| QLT-001 a QLT-004 | Unitários, integração real, E2E e cobertura mínima | Todos | TST-DST-001 a TST-DST-025 |
| QLT-005 a QLT-008 | Limites arquiteturais, rastreabilidade, dados protegidos e execução reproduzível | CA-DST-02, CA-DST-03, CA-DST-20 a CA-DST-22 | TST-DST-001, TST-DST-002, TST-DST-021 a TST-DST-025 |

O SDD-07 possui Gate C aprovado em 2026-08-22. Os Marcos 1 a 6 implementaram topologia, Outbox confirmada, consumers idempotentes, retry limitado, DLQs, estados operacionais, health sanitizado, logs, métricas e prazo de shutdown. O Marco 7 comprovou o fluxo distribuído completo, a recuperação da janela entre publish e confirmação local, TTL real, preservação da entrega quando o encaminhamento não é confirmado, shutdown durante transação bloqueada com rollback e redelivery e recuperação após indisponibilidade de Inventory, Billing e interrupção das conexões pelo RabbitMQ. TST-DST-001, TST-DST-002, TST-DST-006 a TST-DST-017, a regra central de TST-DST-018 e TST-DST-020 a TST-DST-025 possuem evidências reais; TST-DST-004 e TST-DST-005 possuem evidência parcial. A regressão final aprovou 180 testes sem falhas, a suíte distribuída possui 7 testes aprovados e todos os assemblies aplicáveis superam 80% de line coverage. Evidências e limitações estão consolidadas em `RELATORIO-IMPLEMENTACAO-SDD-07.md`.

---

## 14. Planejamento consolidado do SDD-08

| IDs de requisito | Decisões do Gateway | Critérios | Testes planejados |
|---|---|---|---|
| OBR-001 | Entrada HTTP exclusiva do Angular com rotas funcionais preservadas | CA-GTW-02, CA-GTW-11 | TST-GTW-002, TST-GTW-012, TST-GTW-025 |
| OBR-016 | Gateway adicional sem assumir domínio dos microsserviços | CA-GTW-01 a CA-GTW-03 | TST-GTW-001 a TST-GTW-003, TST-GTW-026 |
| DIF-001 | YARP stateless, clusters explícitos, allowlist e ausência de banco ou broker | CA-GTW-01 a CA-GTW-04, CA-GTW-15, CA-GTW-18 a CA-GTW-20 | TST-GTW-001 a TST-GTW-004, TST-GTW-017, TST-GTW-018, TST-GTW-021 a TST-GTW-023, TST-GTW-026 |
| DIF-003 | JWT estrito, `AuthenticatedUser`, `AdminOnly` e defesa em profundidade | CA-GTW-05 a CA-GTW-08 | TST-GTW-005 a TST-GTW-009 |
| DIF-008 | Documentos OpenAPI separados e somente em Development | CA-GTW-17 | TST-GTW-020 |
| DIF-009 | Correlação, tracing, logs, métricas e health sanitizados | CA-GTW-09, CA-GTW-18, CA-GTW-21 | TST-GTW-010, TST-GTW-021, TST-GTW-024 |
| APR-003, APR-008, APR-009 | Fluxo HTTP, autenticação, falhas e limites demonstráveis | CA-GTW-02, CA-GTW-05 a CA-GTW-21 | TST-GTW-002, TST-GTW-005 a TST-GTW-025 |
| QLT-001 a QLT-004 | Testes unitários, integração, E2E, line coverage e branches publicadas | Todos | TST-GTW-001 a TST-GTW-026 |
| QLT-005 | Referências, dependências e superfície proibida verificadas | CA-GTW-01, CA-GTW-03, CA-GTW-04 | TST-GTW-001, TST-GTW-003, TST-GTW-004, TST-GTW-026 |
| QLT-006 a QLT-008 | Rastreabilidade, segredos protegidos e comportamento reproduzível | CA-GTW-09 a CA-GTW-21 | TST-GTW-010 a TST-GTW-025 |

O SDD-08 possui Gate A aprovado. Implementações e evidências reais serão adicionadas somente na macroetapa de implementação.

---

## 15. Planejamento consolidado do SDD-09

| IDs de requisito | Decisões do frontend | Critérios | Testes planejados |
|---|---|---|---|
| OBR-001 a OBR-009 | Angular acessa somente o Gateway; sessão, produtos, invoices e itens possuem fluxos completos | CA-FRT-01, CA-FRT-04 a CA-FRT-10, CA-FRT-20, CA-FRT-21 | TST-FRT-001, TST-FRT-004 a TST-FRT-013, TST-FRT-024, TST-FRT-025, TST-FRT-028 |
| OBR-010 a OBR-015 | Emissão confirmada, acompanhamento assíncrono e impressão transitória pelo navegador | CA-FRT-11 a CA-FRT-19 | TST-FRT-014 a TST-FRT-023, TST-FRT-028 |
| OBR-021 | Feedback distingue processamento, atraso, rejeição, intervenção e falha técnica | CA-FRT-12 a CA-FRT-17, CA-FRT-20, CA-FRT-24 | TST-FRT-016 a TST-FRT-021, TST-FRT-024 |
| OPA-001 | ETag opaco, `If-Match` e revisão manual de conflito | CA-FRT-10 | TST-FRT-013 |
| OPA-002, DIF-004, DIF-005 | Intenção idempotente e observação segura do processo distribuído | CA-FRT-11 a CA-FRT-17 | TST-FRT-014 a TST-FRT-021 |
| DIF-002, DIF-003 | Login, JWT, sessão curta, guards e tratamento distinto de `401` e `403` | CA-FRT-04, CA-FRT-05, CA-FRT-23 | TST-FRT-004 a TST-FRT-007, TST-FRT-027 |
| DIF-008, DIF-009 | Erros tipados, correlação e suporte sanitizado | CA-FRT-20, CA-FRT-23, CA-FRT-24 | TST-FRT-024, TST-FRT-027 |
| DIF-010 | Angular Material com tema minimalista verde e branco, responsivo e acessível | CA-FRT-02, CA-FRT-03, CA-FRT-22 | TST-FRT-002, TST-FRT-003, TST-FRT-026 |
| APR-001 a APR-007 | Fluxos Angular, componentes, lifecycle e RxJS reais demonstráveis | CA-FRT-02 a CA-FRT-22 | TST-FRT-002 a TST-FRT-026, TST-FRT-028 |
| APR-009 | Problem Details e feedback de falhas demonstráveis na experiência | CA-FRT-05, CA-FRT-10, CA-FRT-15 a CA-FRT-17, CA-FRT-20 | TST-FRT-007, TST-FRT-013, TST-FRT-019 a TST-FRT-021, TST-FRT-024 |
| QLT-001, QLT-003, QLT-004 | Testes unitários, integração, E2E, cobertura de linhas e branches publicadas | Todos | TST-FRT-001 a TST-FRT-028 |
| QLT-006 a QLT-008 | Rastreabilidade, proteção de dados e build verificável | CA-FRT-01, CA-FRT-21, CA-FRT-23, CA-FRT-24 | TST-FRT-001, TST-FRT-025, TST-FRT-027, TST-FRT-028 |

O SDD-09 possui Gate A aprovado. Implementações e evidências reais serão adicionadas somente na macroetapa de implementação.

---

## 16. Planejamento consolidado do SDD-10

| IDs de requisito | Decisões de teste | Critérios | Provas planejadas |
|---|---|---|---|
| Todos os critérios dos SDDs 01 a 09 | Uma prova principal por critério e complementação conforme risco | CA-TST-01 | TST-TST-001 |
| QLT-001 | Testes unitários backend e frontend isolados por responsabilidade | CA-TST-02, CA-TST-03 | TST-TST-002, TST-TST-003 |
| QLT-002 | ASP.NET Core, PostgreSQL e RabbitMQ reais nas integrações aplicáveis | CA-TST-04 a CA-TST-06 | TST-TST-004 a TST-TST-006 |
| OBR-016 a OBR-022, OPA-001, OPA-002, DIF-004 a DIF-006 | Contratos, arquitetura e fluxos sistêmicos distribuídos | CA-TST-07, CA-TST-08, CA-TST-11 | TST-TST-007, TST-TST-008, TST-TST-011 |
| DIF-002, DIF-003, DIF-009, QLT-007 | Sentinelas de autenticação, autorização, correlação, segredos e logs | CA-TST-09, CA-TST-20 | TST-TST-009, TST-TST-020 |
| OBR-001 a OBR-015, OBR-021, DIF-010 | Fluxos críticos pelo navegador real | CA-TST-10 | TST-TST-010 |
| QLT-006 | Isolamento, determinismo, paralelismo e vínculo explícito das provas | CA-TST-01, CA-TST-12 a CA-TST-14 | TST-TST-001, TST-TST-012 a TST-TST-014 |
| QLT-003 | Gate mínimo de 80% por assembly backend aplicável e frontend manual | CA-TST-15, CA-TST-16 | TST-TST-015, TST-TST-016 |
| QLT-004 | Branch coverage backend e frontend publicada | CA-TST-17 | TST-TST-017 |
| DIF-007, QLT-008 | Execução reproduzível por Docker e gate sem testes ocultos | CA-TST-18, CA-TST-19 | TST-TST-018, TST-TST-019 |
| APR-001 a APR-011 | Evidências objetivas para documentação e demonstração, incluindo não aplicabilidade de Golang | CA-TST-01, CA-TST-20 | TST-TST-001, TST-TST-020 |

O SDD-10 possui Gate A aprovado. Implementações, percentuais e evidências reais serão adicionados somente na macroetapa de implementação.

---

## 17. Planejamento consolidado do SDD-11

| IDs de requisito | Decisões operacionais | Critérios | Provas planejadas |
|---|---|---|---|
| OBR-001, DIF-001 | Nginx publica Angular e encaminha API somente ao Gateway interno | CA-OPS-01 a CA-OPS-03, CA-OPS-06 | TST-OPS-001 a TST-OPS-003, TST-OPS-006 |
| OBR-016, OBR-022 | Três serviços e três PostgreSQL isolados, com migrators próprios | CA-OPS-03, CA-OPS-07 a CA-OPS-09 | TST-OPS-003, TST-OPS-007 a TST-OPS-009 |
| OBR-019 a OBR-021, DIF-004, DIF-005 | RabbitMQ durável, identidades próprias, degradação e recuperação | CA-OPS-10 a CA-OPS-14, CA-OPS-25 | TST-OPS-010 a TST-OPS-014, TST-OPS-025 |
| DIF-007 | Profiles local, tooling, testes e coverage executáveis pelo Docker | CA-OPS-01, CA-OPS-04, CA-OPS-12, CA-OPS-22 a CA-OPS-24 | TST-OPS-001, TST-OPS-004, TST-OPS-012, TST-OPS-022 a TST-OPS-024 |
| DIF-009 | Health, logs, W3C tracing, correlação e métricas internas | CA-OPS-15 a CA-OPS-21 | TST-OPS-015 a TST-OPS-021 |
| QLT-002 a QLT-004 | Infraestrutura real, resultados e cobertura reproduzíveis | CA-OPS-07 a CA-OPS-12, CA-OPS-20, CA-OPS-22 a CA-OPS-24 | TST-OPS-007 a TST-OPS-012, TST-OPS-020, TST-OPS-022 a TST-OPS-024 |
| QLT-005, QLT-006 | Redes, ownership e critérios operacionais verificáveis | CA-OPS-01 a CA-OPS-03, CA-OPS-07, CA-OPS-10 | TST-OPS-001 a TST-OPS-003, TST-OPS-007, TST-OPS-010 |
| QLT-007 | Secrets por arquivo, imagens, health, logs e artefatos sanitizados | CA-OPS-04 a CA-OPS-06, CA-OPS-17, CA-OPS-18, CA-OPS-24 | TST-OPS-004 a TST-OPS-006, TST-OPS-017, TST-OPS-018, TST-OPS-024 |
| QLT-008 | Builds, startup, shutdown e recuperação verificáveis | CA-OPS-04, CA-OPS-08, CA-OPS-12 a CA-OPS-16, CA-OPS-22, CA-OPS-25 | TST-OPS-004, TST-OPS-008, TST-OPS-012 a TST-OPS-016, TST-OPS-022, TST-OPS-025 |

O SDD-11 possui Gate A aprovado. Compose final, imagens, comandos, resultados e evidências serão adicionados somente na macroetapa de implementação.

---

## 18. Planejamento consolidado do SDD-13

| IDs de requisito | Decisões de QA | Critérios | Provas planejadas |
|---|---|---|---|
| Todos | Ambiente limpo, candidato identificado, auditoria documental e matriz integral | CA-QA-01 a CA-QA-03, CA-QA-23 a CA-QA-25 | TST-QA-001 a TST-QA-003, TST-QA-023 a TST-QA-025 |
| OBR-001 a OBR-015, OBR-021 | Smoke, Products, Invoices, emissão, rejeição e impressão | CA-QA-08 a CA-QA-13, CA-QA-19 | TST-QA-008 a TST-QA-013, TST-QA-019 |
| OBR-016 a OBR-022 | Arquitetura, persistência, integração, recuperação e execução local | CA-QA-04, CA-QA-14 a CA-QA-16, CA-QA-18, CA-QA-20 | TST-QA-004, TST-QA-014 a TST-QA-016, TST-QA-018, TST-QA-020 |
| OPA-001, OPA-002 | ETag, concorrência, Idempotency-Key, Inbox e Outbox | CA-QA-13 a CA-QA-16 | TST-QA-013 a TST-QA-016 |
| DIF-001 a DIF-010 | Diferenciais implementados, demonstrados e sem ampliação órfã | CA-QA-03, CA-QA-04, CA-QA-08 a CA-QA-20, CA-QA-22 | TST-QA-003, TST-QA-004, TST-QA-008 a TST-QA-020, TST-QA-022 |
| QLT-001 a QLT-005, QLT-008 | Build, suítes, arquitetura, cobertura e Docker aprovados | CA-QA-04 a CA-QA-07, CA-QA-20, CA-QA-21 | TST-QA-004 a TST-QA-007, TST-QA-020, TST-QA-021 |
| QLT-006, QLT-007 | Rastreabilidade, segurança, secrets e evidências sanitizadas | CA-QA-02, CA-QA-03, CA-QA-17, CA-QA-18, CA-QA-23 | TST-QA-002, TST-QA-003, TST-QA-017, TST-QA-018, TST-QA-023 |
| APR-001 a APR-011 | README e vídeo fiéis ao candidato, às limitações e aos itens não aplicáveis | CA-QA-22, CA-QA-24, CA-QA-25 | TST-QA-022, TST-QA-024, TST-QA-025 |

O SDD-13 possui Gate A aprovado como plano. Resultados reais, defeitos e decisão do Gate C serão registrados somente depois da implementação e do SDD-12.

---

## 19. Planejamento consolidado do SDD-12

| IDs de requisito | Decisões de apresentação | Critérios | Provas planejadas |
|---|---|---|---|
| APR-001, APR-002 | Vídeo posterior demonstra telas e funcionalidades do candidato validado | CA-DOC-09, CA-DOC-11 | TST-DOC-009, TST-DOC-011 |
| APR-003 | Arquitetura e fluxo real possuem diagrama e explicação | CA-DOC-04 a CA-DOC-06 | TST-DOC-004 a TST-DOC-006 |
| APR-004 | Lifecycle Angular documentado somente com uso real | CA-DOC-06, CA-DOC-07 | TST-DOC-006, TST-DOC-007 |
| APR-005 | RxJS e Signals explicados por operadores e responsabilidades reais | CA-DOC-06, CA-DOC-07 | TST-DOC-006, TST-DOC-007 |
| APR-006, APR-007 | Bibliotecas gerais e visuais possuem versão, finalidade e local de uso | CA-DOC-06, CA-DOC-07 | TST-DOC-006, TST-DOC-007 |
| APR-008 | Frameworks C#/.NET e dependências são explicados; Golang é não aplicável | CA-DOC-06 | TST-DOC-006 |
| APR-009 | Erros HTTP, exceções, rejeições e falhas distribuídas são diferenciados | CA-DOC-06, CA-DOC-07 | TST-DOC-006, TST-DOC-007 |
| APR-010 | LINQ é documentado com consultas reais e fronteira de execução | CA-DOC-06, CA-DOC-07 | TST-DOC-006, TST-DOC-007 |
| APR-011 | Golang é declarado não aplicável porque todo o backend é C#/.NET | CA-DOC-06 | TST-DOC-006 |
| DIF-001 a DIF-010 | Diferenciais reais são resumidos sem confundir escopo e limitação | CA-DOC-04, CA-DOC-05, CA-DOC-12 | TST-DOC-004, TST-DOC-005, TST-DOC-012 |
| QLT-003, QLT-004, QLT-006 a QLT-008 | Comandos, testes, cobertura e evidências pertencem ao mesmo candidato | CA-DOC-01 a CA-DOC-03, CA-DOC-08, CA-DOC-10, CA-DOC-11 | TST-DOC-001 a TST-DOC-003, TST-DOC-008, TST-DOC-010, TST-DOC-011 |

O SDD-12 possui Gate A aprovado como estrutura. README, detalhamento e roteiro serão materializados ou completados depois da implementação e antes do QA final.

---

## 20. Regra de evolução

Cada SDD deverá:

1. listar os IDs desta matriz que atende;
2. criar critérios de aceite identificáveis;
3. acrescentar os IDs dos testes previstos;
4. após implementação, apontar arquivos e testes reais;
5. após validação, registrar comando, resultado e evidência;
6. criar nova linha quando surgir requisito aprovado, sem reutilizar ID existente.
