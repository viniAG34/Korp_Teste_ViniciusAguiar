# SDD-13 - QA e Validação Final

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-20
> Gate A: aprovado em 2026-08-20
> Dependências para elaboração: SDD-01 a SDD-11, ADRs, convenções, glossário e matriz de rastreabilidade
> Dependência para execução final: SDD-12 e todas as implementações concluídas

---

## 1. Objetivo

Especificar a auditoria final do projeto, os critérios de regressão, as validações manuais e automatizadas, o tratamento de desvios e as evidências necessárias para declarar a entrega completa.

O documento define como provar que o sistema implementado corresponde ao desafio, aos diferenciais aprovados e à baseline documental. Ele não substitui os testes dos SDDs funcionais nem antecipa resultados ainda inexistentes.

---

## 2. Requisitos rastreados

- todos os requisitos `OBR`, `OPA`, `DIF`, `QLT` e `APR` da matriz;
- todos os critérios de aceite dos SDDs 01 a 11;
- critérios futuros de documentação e entrega do SDD-12;
- gates A, B e C definidos no AGENTS.md.

---

## 3. Escopo previsto

- auditoria cruzada de documentos e implementação;
- validação integral da matriz de rastreabilidade;
- build, testes, cobertura e análise das evidências;
- regressão funcional e distribuída;
- QA visual, responsivo e de acessibilidade;
- segurança, configuração e ausência de segredos;
- recuperação diante de falhas;
- execução limpa pelo Docker Compose;
- classificação, correção e nova validação de defeitos;
- checklist de aceite e relatório final.

---

## 4. Fora do escopo

- acrescentar funcionalidade durante a validação;
- alterar teste para acompanhar comportamento divergente do SDD;
- aceitar falha conhecida sem registrar risco e decisão;
- teste de invasão profissional;
- certificação fiscal da nota simplificada;
- benchmark ou teste de carga sem requisito aprovado;
- validação de impressão física;
- produção do README e do vídeo, pertencente ao SDD-12.

---

## 5. Blocos de decisão

1. estratégia, entradas, ambientes e ordem da validação;
2. auditoria documental, arquitetural e de rastreabilidade;
3. build, testes, cobertura e qualidade estática;
4. regressão funcional do usuário;
5. concorrência, idempotência, mensageria e recuperação;
6. segurança, configuração, logs e observabilidade;
7. frontend, responsividade, acessibilidade e impressão;
8. defeitos, severidade, revalidação e critérios de bloqueio;
9. evidências, checklist final, Gate C e critérios de aceite.

---

## 6. Decisões herdadas

- a especificação aprovada prevalece sobre a implementação;
- cada critério exige prova objetiva;
- toda execução oficial usa Docker e Docker Compose;
- testes obrigatórios não podem estar ignorados ou instáveis;
- backend e frontend manual exigem ao menos 80% de line coverage;
- branch coverage é publicada;
- PostgreSQL e RabbitMQ reais são obrigatórios nas integrações aplicáveis;
- falha de infraestrutura não é aprovação;
- impressão física não é automatizável, mas seu acionamento é;
- segredo ou dado sensível em repositório, imagem, log ou evidência bloqueia a entrega;
- resultado parcial nunca será apresentado como conclusão integral.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Estratégia, entradas, ambiente e ordem

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Pergunta de validação

O QA final determina se o candidato implementado corresponde ao desafio, à baseline aprovada e aos diferenciais declarados. QA não cria regra: divergência retorna ao SDD ou implementação proprietária.

#### Entradas

São obrigatórios PDF original, AGENTS.md, visão geral, ADRs, convenções, glossário, SDDs aprovados, matriz sem requisito sem destino, implementação, relatórios, Compose final, SDD-12 e candidato identificável.

#### Ambiente

```text
korp-erp-qa-<testRunId>
```

O host usa somente Docker e Compose; bancos e RabbitMQ começam vazios; secrets são sintéticos e exclusivos; imagens são fixadas; portas usam loopback; cultura, timezone e navegadores são registrados; volumes de desenvolvimento e serviços externos não participam.

#### Candidato imutável

A execução referencia uma versão identificável. Alteração posterior em código, configuração, migration, contrato ou documentação invalida as evidências afetadas e exige revalidação proporcional. Evidência anterior não pode ser atribuída à versão corrigida.

#### Ordem fail-fast

1. integridade documental e rastreabilidade;
2. segredos e dependências proibidas;
3. build limpo;
4. infraestrutura vazia;
5. migrations e seed;
6. unitários, componentes e arquitetura;
7. integrações;
8. testes sistêmicos;
9. Playwright;
10. QA visual, responsivo e acessível;
11. falhas e recuperação;
12. README, comandos e roteiro;
13. consolidação de evidências;
14. decisão do Gate C final.

Falha estrutural interrompe etapas dependentes. Verificação independente pode continuar para ampliar diagnóstico, mas a execução permanece não aprovada.

#### Incremental e final

Durante desenvolvimento, validação incremental cobre o SDD alterado e regressão afetada. A entrega exige ambiente novo, build limpo, migrations desde bancos vazios, todas as suítes, regressão manual e artefatos regenerados.

#### Condições de entrada

QA final não começa com SDD sem Gate A, implementação sem relatório, critério sem teste, requisito sem destino, migration não revisada, decisão funcional pendente, segredo conhecido ou SDD-12 incompleto. Auditoria parcial durante desenvolvimento continua permitida sem declarar entrega final.

### 7.2 Bloco 2 - Auditoria documental, arquitetural e de rastreabilidade

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Auditorias

A auditoria de baseline ocorre depois da especificação dos SDDs 01 a 13 e antes da implementação funcional. A auditoria final ocorre depois da implementação e do SDD-12. A primeira autoriza planejar o código; a segunda compara o candidato real com a baseline.

#### Documentos

São verificados cobertura do PDF, separação entre obrigatório, opcional e diferencial, reflexo das decisões recentes, hierarquia das fontes, glossário, contratos, estados, ownership, fronteiras do Gateway e RabbitMQ, unicidade de IDs, dependências não circulares, links, status, datas e ausência de promessa fora do escopo.

A baseline produzirá `docs/AUDITORIA-DOCUMENTAL-002.md`.

#### Implementação

Projetos, referências, entidades, migrations, endpoints, OpenAPI, DTOs, erros, eventos, topologia, configuração, policies, JWT, Angular, Compose, health, logs, métricas, testes e cobertura serão comparados às responsabilidades aprovadas.

Código sem requisito é possível ampliação de escopo; requisito sem código é lacuna. Build aprovado não normaliza nenhuma das situações.

#### Arquitetura

Provas automatizadas e inspeção verificam camadas, ausência de referência entre microsserviços, ownership de bancos, Gateway sem persistência ou broker, Domain independente, Application sem Infrastructure, contratos externos fora do domínio, endpoints sem regra, DI sem Service Locator, frontend sem endereço interno, Nginx limitado e dependências autorizadas.

#### Matriz final

```text
requisito -> decisão -> SDD -> critério -> implementação -> teste -> evidência
```

Antes do Gate C não permanece `A definir`, teste apenas planejado, implementação ausente, evidência de outra versão, requisito sem critério, critério sem prova ou diferencial anunciado sem demonstração.

Bootstrap e configuração trivial podem compartilhar responsabilidade técnica de setup. Código funcional órfão não recebe essa exceção.

#### Resultado

A auditoria final integra `docs/RELATORIO-QA-FINAL.md`. Cada desvio registra documento, requisito, localização, impacto e ação. Correção segue o fluxo do SDD afetado e não ocorre silenciosamente durante auditoria.

### 7.3 Bloco 3 - Build, testes, cobertura e qualidade estática

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Build

Containers novos executam backend Release com `CI=true`, gerenciamento central, warnings como erros, analyzers recomendados e build determinístico. Frontend usa `npm ci`, configuração production, templates estritos e budgets Angular.

Erro, warning backend, erro de template, budget máximo excedido, divergência de lockfile, dependência ausente ou restore não reproduzível reprovam o gate.

`dotnet format --verify-no-changes` e `prettier --check` verificam formatação sem corrigir arquivos durante auditoria.

#### Suítes

São obrigatórios unitários de Identity, Inventory e Billing; frontend; arquitetura; integrações de Identity, Inventory, Billing e Gateway; contratos; testes sistêmicos; Playwright e acessibilidade.

O candidato construído é reutilizado quando aplicável. O gate exige zero falha, teste obrigatório ignorado, desabilitado, retry necessário, execução abortada ou queda inexplicada na descoberta. Resultados e duração são exportados.

Teste instável é defeito mesmo que passe em nova tentativa.

#### Cobertura

Cada assembly backend aplicável exige 80% de linhas; frontend manual exige 80%. Média global é informativa e branch coverage é publicada. Exclusões são comparadas à allowlist e não podem retirar regra ou critério.

Independentemente do percentual, autenticação, autorização, saldo, atomicidade, concorrência, ETag, Idempotency-Key, Inbox, Outbox, redelivery, retry, DLQ, estados, ManualIntervention, recuperação e impressão após Completed possuem prova explícita.

#### Qualidade estática

São verificados nulabilidade, analyzers, TypeScript e templates estritos, ausência de `any` evasivo, código morto, comentário obsoleto, dependência sobreposta ou não autorizada, referência arquitetural, gerado versionado indevidamente e source map público não aprovado.

#### Dependências

Manifests e lockfiles são inventariados. Auditoria externa de advisories, quando executada, é datada. Vulnerabilidade relevante exige avaliação; indisponibilidade da fonte não é declarada como ausência de vulnerabilidade.

SonarQube, mutation testing, benchmark, carga, cobertura de 100% e rejeição cega de advisory transitório não aplicável ficam fora do gate inicial.

### 7.4 Bloco 4 - Regressão funcional do usuário

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Smoke

```text
login -> Product -> Invoice -> item -> emissão -> processo -> fechamento -> impressão
```

Falha no fluxo essencial reprova o smoke e impede aprovação da regressão completa.

#### Autenticação

São verificados login válido e inválido sem enumeração, restauração na mesma aba, expiração, logout, retorno seguro, ausência de open redirect, `401`, `403` e bloqueio Admin. Identidade sem Admin é fixture exclusiva da suíte, não funcionalidade de gestão de usuários.

#### Product

São cobertos saldo inicial zero e positivo, normalização, limites, formato, inteiro, duplicidade, paginação, detalhe, saldo atualizado, not found, permissão e ausência de edição, exclusão ou ajuste manual.

#### Invoice

São cobertos criação vazia explícita, número automático, paginação, detalhe, adição, produto duplicado, alteração, remoção, quantidade válida, snapshot, catálogo indisponível, papel, bloqueio durante emissão e somente leitura depois de Closed.

#### Concorrência HTTP

Mutações usam ETag atual. Ausência de If-Match, ETag antigo, recarga após `412`, ausência de retry ou merge, intenções concorrentes e estado final do servidor possuem prova.

#### Emissão concluída

São verificados elegibilidade, confirmação, uma Idempotency-Key por intenção, `202`, Pending, AwaitingStock, atraso informativo, Completed, baixa correta, Closed, conteúdo, uma chamada de `window.print()`, fallback sem baixa e ausência de reimpressão posterior.

#### Rejeição

Saldo insuficiente não altera nenhum saldo, produz Rejected, preserva Open, remove bloqueio, apresenta justificativa, permite correção e usa nova chave na nova tentativa.

#### Estados e falhas

Loading, empty, retry seguro, `400`, `401`, `403`, `404`, `409`, `412`, `428`, `429`, `500` sanitizado, `503` e código de suporte são exercitados onde aplicáveis.

#### Evidência

UI, APIs públicas, Invoice, processo e saldo são fontes principais. Logs e métricas complementam. Banco direto fica restrito a invariante técnica sem representação pública e nunca substitui o comportamento percebido.

### 7.5 Bloco 5 - Concorrência, idempotência, mensageria e recuperação

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Fluxo

O QA correlaciona PrintInvoice, persistência de Invoice, Process e Outbox, `202`, publisher confirm, consumo no Inventory, baixa com Movement, Inbox e Outbox, consumo no Billing e fechamento.

#### Idempotência HTTP

Mesma chave e intenção recuperam o mesmo processo sem nova Outbox ou baixa; chave reutilizada para intenção diferente é rejeitada; nova chave durante emissão ativa é rejeitada; replay depois de Completed recupera resultado; resposta perdida permite repetição consciente. A prova confirma efeito único, não apenas response igual.

#### Inbox

Redelivery do mesmo messageId, crash entre commit e ACK, duplicidade equivalente e conflito lógico não criam segundo Movement, nova transição ou regressão. Métrica e log sanitizado registram duplicidade.

#### Concorrência e atomicidade

Duas Invoices disputando a última unidade produzem somente uma baixa, saldo não negativo e resolução aprovada do outro processo. Conflitos de xmin respeitam limite.

Em múltiplos itens, insuficiência de um impede todas as baixas e movimentos, rejeita processo, preserva Invoice Open e permite correção posterior.

#### Outbox

São verificados broker indisponível após `202`, intenção persistida, retomada, dispatchers concorrentes, lease expirado, confirm, perda ou timeout de confirm, publicação mandatory não roteável e ausência de PublishedAtUtc sem confirmação válida.

#### Retry e DLQ

Falha funcional não recebe retry técnico. Falha transitória percorre 5, 30 e 120 segundos, com contador, ACK pós-commit e preservação da entrega se encaminhamento falhar. Versão incompatível, JSON inválido e esgotamento chegam à DLQ, sem redrive automático.

Topologia e TTLs são inspecionados e ao menos um cenário integral percorre os atrasos reais aprovados.

#### Ordem e intervenção

Resultado em Pending é aceito; AwaitingStock posterior não sobrescreve terminal; duplicidade não regride estado; ProcessingFailed produz ManualIntervention; timeout não decide terminal; Invoice permanece bloqueada em intervenção.

#### Recuperação

Reinícios controlados de RabbitMQ, Inventory, Billing e consumer comprovam recriação de conexão, channels e topologia, retomada da Outbox, redelivery sem ACK, proteção da Inbox e avanço posterior. Topologia incompatível permanece falha explícita.

#### Garantia

O relatório declara entrega at-least-once com efeitos idempotentes e não utiliza “exactly-once”.

### 7.6 Bloco 6 - Segurança, configuração, logs e observabilidade

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Segredos

Valores sentinela são pesquisados em arquivos e histórico disponível, bundle, layers, configuração pública, saída Compose, logs, resultados, relatórios, screenshots e README. Evidência não contém senha, JWT, chave, connection string, Idempotency-Key, credencial RabbitMQ ou hash.

#### Autenticação e autorização

A matriz cobre token ausente, expirado, assinatura, algoritmo, issuer, audience, claims e papel inválidos; acesso direto às APIs; rota interna; login e health anônimos; OpenAPI em Development; `401` e `403`. Gateway não é defesa única.

Login usa resposta genérica, trabalho de hash equivalente, lockout, headers no-store, bearer removido no encaminhamento e seed que não redefine senha.

#### Gateway e HTTP

São verificados allowlist, bloqueio de `/api/v1/internal/*`, CORS explícito, limites, correlação inválida, rate limiting, Retry-After, hop-by-hop, Problem Details, cancelamento, timeout e ausência de bearer em destino externo.

#### Configuração

JWT, banco, seed, RabbitMQ, CORS, destinos, apiBaseUrl, topologia e tempos fixos são testados ausentes ou inválidos. O processo falha fechado, sem default funcional, destino alternativo ou segredo embutido.

#### Containers e frontend

Usuário, capabilities, filesystem, mounts, secrets, portas, redes, restart, health, Docker socket, SDK e conectividade são inspecionados.

Frontend mantém token em sessionStorage, limpa sessão, limita bearer, não usa innerHTML funcional, bypass de segurança, CDN, URL interna ou source map público e trata conteúdo da API como não confiável.

#### Logs e tracing

Sentinelas percorrem HTTP, login, domínio e mensagens. Logs são então inspecionados. Correlação, causalidade, trace separado, replay histórico, eventos suficientes, health sem ruído e ausência de stack trace ao cliente são comprovados.

#### Métricas

São verificados incremento, allowlist de labels, ausência de identificadores, endpoint interno, sanitização e transições de dependência.

#### Limite

O relatório descreve QA de segurança e defesa em profundidade, nunca pentest, certificação ou garantia absoluta de ausência de vulnerabilidade.

### 7.7 Bloco 7 - Frontend, responsividade, acessibilidade e impressão

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Navegadores e viewports

A suíte completa usa Chromium; smoke principal também usa Firefox e WebKit, com versões Playwright registradas.

Viewports mínimas são 375 × 667, 768 × 1024 e 1440 × 900. Zoom de 200% é aplicado às páginas críticas. Não pode haver corte essencial, rolagem horizontal, ação inacessível, dialog fora da tela, perda de dados, sobreposição ou foco oculto.

#### Visual

São verificados tema verde e branco, tokens, contraste, tipografia, espaçamento, hierarquia de ação, estados, shell, navegação e equivalência entre tabela e lista compacta. Dashboard, função fictícia, CDN, excesso decorativo e animação desnecessária permanecem ausentes.

Screenshots documentam a revisão e não formam gate pixel a pixel.

#### Estados

Páginas exercitam initial, loading, loaded, empty, submitting, updating, error, forbidden, not found, conflito, indisponibilidade, processamento, atraso, rejeição e intervenção quando aplicáveis. Submissão concorrente é impedida.

#### Acessibilidade

Axe roda nas páginas e estados críticos. Violação critical ou serious bloqueia; moderate exige análise. Automação não substitui inspeção de landmarks, skip link, h1, títulos, nomes, labels, erros, tabulação, foco, dialogs, teclado, live regions, alertas, polling, cor, alvos e reduced motion.

Não se declara certificação assistiva completa, mas a semântica deve permitir uso por tecnologia de apoio.

#### Idioma

Documento usa pt-BR; mensagens são claras; datas e horário seguem decisão local; quantidades usam singular e plural; não há dado fiscal inventado ou diagnóstico técnico como mensagem principal.

#### Impressão

Antes de Completed, ação e print-view são negados. Depois, autorização transitória, render anterior ao window.print, CSS, conteúdo permitido, fallback local, descarte após reload e ausência de backend, baixa ou reimpressão são comprovados por emulação print.

O QA não afirma produção de papel.

#### Console e rede

Fluxo crítico não apresenta erro inesperado, CDN, acesso direto ao serviço, mutação duplicada, polling sobreposto, bearer fora do Gateway ou recurso obrigatório ausente.

### 7.8 Bloco 8 - Defeitos, severidade, revalidação e bloqueios

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Registro

Durante a validação, `docs/REGISTRO-DE-DEFEITOS-QA.md` registra ID, título, candidato, requisito, ambiente, reprodução, esperado, obtido, evidência, impacto, severidade, status, correção e validações afetadas. O arquivo não é criado antecipadamente vazio.

#### Severidade

| Nível | Definição | Exemplo |
|---|---|---|
| S0 - Crítico | Segurança, corrupção ou efeito empresarial grave | Segredo, autorização contornada, saldo negativo, baixa dupla |
| S1 - Alto | Fluxo principal ou requisito obrigatório indisponível | Emissão, migration, idempotência ou Docker falhos |
| S2 - Médio | Impacto limitado ou workaround real | Feedback ou estado secundário incorreto |
| S3 - Baixo | Cosmético ou documental sem alterar fluxo | Alinhamento ou redação secundária |

#### Bloqueios

Bloqueiam Gate C: S0/S1, obrigatório ausente, opcionais A/C aprovados ausentes, diferencial anunciado inexistente, build/teste falho, cobertura abaixo de 80%, segredo, autorização contornável, estoque inconsistente, duplicidade, migration falha em vazio, Docker irreproduzível, matriz incompleta, comando essencial incorreto e acessibilidade critical/serious no fluxo principal.

S2 ligado a requisito bloqueia até correção ou decisão explícita. S3 pode ser risco aceito. S0 e S1 não são convertidos em limitação conhecida.

#### Status e autoridade

```text
Open
Triaged
Correction approved
Fixed pending retest
Closed
Accepted risk
```

Somente o engenheiro aceita risco, registrando justificativa e impacto. Não é elegível quando viola obrigatório, segurança ou integridade.

#### Correção

Auditoria não corrige automaticamente. Divergência gera plano; ambiguidade atualiza SDD antes do código; arquitetura, segurança ou escopo podem exigir ADR; refatoração adjacente requer aprovação.

#### Revalidação

Correção executa reprodução, suíte proprietária, critérios dependentes, integrações afetadas, build, estática e cobertura. Contrato, migration, autenticação, mensageria, idempotência, concorrência ou Compose exigem regressão sistêmica correspondente.

Depois da última correção ocorre nova execução integral do candidato.

#### Instabilidade e limitações

Teste instável abre defeito, não recebe retry oculto, ignore ou remoção sem redundância provada. Limitação é decisão fora do escopo previamente documentada; requisito que falha é defeito e não vira limitação por redação no README.

### 7.9 Bloco 9 - Evidências, checklist, Gate C e critérios de aceite

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Critérios e provas planejadas

| Critério | Resultado verificável | Prova |
|---|---|---|
| `CA-QA-01` | Candidato validado em ambiente novo, isolado e identificável | `TST-QA-001` |
| `CA-QA-02` | Auditoria não encontra contradição ou requisito sem destino | `TST-QA-002` |
| `CA-QA-03` | Matriz liga requisito, decisão, SDD, critério, código, teste e evidência | `TST-QA-003` |
| `CA-QA-04` | Limites arquiteturais são comprovados por automação e inspeção | `TST-QA-004` |
| `CA-QA-05` | Backend e frontend possuem build limpo | `TST-QA-005` |
| `CA-QA-06` | Suítes obrigatórias passam sem ignore ou retry | `TST-QA-006` |
| `CA-QA-07` | Backend e frontend atingem gates de cobertura | `TST-QA-007` |
| `CA-QA-08` | Smoke funcional completo é aprovado | `TST-QA-008` |
| `CA-QA-09` | Cadastro, consulta e saldo de Product são aprovados | `TST-QA-009` |
| `CA-QA-10` | Criação, itens, concorrência e estados de Invoice são aprovados | `TST-QA-010` |
| `CA-QA-11` | Completed reduz estoque, fecha Invoice e aciona impressão | `TST-QA-011` |
| `CA-QA-12` | Saldo insuficiente não altera estoque e permite correção | `TST-QA-012` |
| `CA-QA-13` | ETag e Idempotency-Key impedem conflito ou efeito duplicado | `TST-QA-013` |
| `CA-QA-14` | Concorrência e múltiplos itens preservam saldo e atomicidade | `TST-QA-014` |
| `CA-QA-15` | Inbox e Outbox preservam efeito único diante de redelivery | `TST-QA-015` |
| `CA-QA-16` | Retry, DLQ, intervenção e recuperação seguem estados aprovados | `TST-QA-016` |
| `CA-QA-17` | Autenticação, autorização, configuração e secrets passam sentinelas | `TST-QA-017` |
| `CA-QA-18` | Health, logs, tracing e métricas são corretos e sanitizados | `TST-QA-018` |
| `CA-QA-19` | Frontend passa responsividade, acessibilidade e impressão | `TST-QA-019` |
| `CA-QA-20` | Ambiente Docker completo é reproduzível pelos comandos documentados | `TST-QA-020` |
| `CA-QA-21` | Não existe bloqueador ou teste instável aberto | `TST-QA-021` |
| `CA-QA-22` | README e vídeo descrevem somente comportamento demonstrado | `TST-QA-022` |
| `CA-QA-23` | Toda evidência pertence ao candidato final | `TST-QA-023` |
| `CA-QA-24` | Limitações e riscos aceitos são explícitos e não ocultam requisito | `TST-QA-024` |
| `CA-QA-25` | Relatório permite decisão objetiva de aprovação ou reprovação | `TST-QA-025` |

#### Evidências

```text
docs/
|- AUDITORIA-DOCUMENTAL-002.md
|- REGISTRO-DE-DEFEITOS-QA.md, quando houver
|- RELATORIO-QA-FINAL.md
`- relatórios de implementação por SDD

artifacts/
|- test-results/
|- coverage/
|- diagnostics/
|- accessibility/
|- playwright/
`- qa/
   |- screenshots/
   |- checklist-manual.md
   |- environment.md
   `- summary.md
```

Artefatos gerados permanecem fora do Git, salvo evidência pequena incorporada deliberadamente à documentação.

#### Relatório final

`RELATORIO-QA-FINAL.md` registra candidato, data, ambiente, escopo, requisitos, comandos, builds, testes, duração, cobertura, regressão, concorrência, mensageria, segurança, acessibilidade, observabilidade, defeitos, riscos, limitações, evidências e decisão recomendada.

#### Checklist do Gate C

- [ ] PDF integralmente rastreado;
- [ ] SDDs e ADRs coerentes;
- [ ] código correspondente à baseline;
- [ ] build limpo;
- [ ] testes passando;
- [ ] cobertura atendida;
- [ ] infraestrutura real utilizada;
- [ ] Docker reproduzível;
- [ ] fluxo principal completo;
- [ ] concorrência e idempotência comprovadas;
- [ ] recuperação comprovada;
- [ ] nenhum segredo;
- [ ] acessibilidade aprovada;
- [ ] documentação e vídeo fiéis;
- [ ] matriz completa;
- [ ] nenhum bloqueador;
- [ ] evidência vinculada ao candidato final.

#### Decisão

O relatório recomenda somente `APROVADO` ou `REPROVADO`. Execução parcial é incompleta, nunca aprovação condicional. A recomendação técnica não substitui a decisão final do engenheiro.

---

## 8. Condição para Gate A

O SDD poderá atingir o Gate A quando:

- os nove blocos estiverem aprovados;
- cada classe de requisito possuir validação objetiva;
- critérios de bloqueio e severidade estiverem definidos;
- execução limpa, regressão e recuperação tiverem procedimentos reproduzíveis;
- evidências exigidas e responsabilidades de correção estiverem explícitas;
- nenhuma validação depender de resultado inventado antes da implementação;
- índice e matriz de rastreabilidade estiverem atualizados.

A condição foi atendida em 2026-08-20: os nove blocos foram aprovados, os 25 critérios possuem provas planejadas e a auditoria do plano não encontrou decisão de QA pendente.

A aprovação deste documento estabiliza o plano de QA. Sua execução e Gate C final dependem da implementação integral e do SDD-12 concluído.
