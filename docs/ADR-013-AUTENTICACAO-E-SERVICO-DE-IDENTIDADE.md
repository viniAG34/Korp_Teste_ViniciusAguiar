# ADR-013 - Autenticação e Serviço de Identidade

> Status: Aprovada
> Data: 2026-08-17
> Última atualização: 2026-08-20
> Dependências: ADR-002, ADR-004, ADR-005, ADR-006, ADR-009 e ADR-010

---

## 1. Contexto

O enunciado do desafio não exige autenticação. Entretanto, a aplicação permite cadastrar saldo, alterar documentos em aberto, iniciar baixa de estoque e fechar notas. Em uma aplicação empresarial real, essas operações não devem ficar disponíveis anonimamente.

A inclusão foi deliberadamente aprovada como diferencial técnico. O objetivo não é construir uma plataforma completa de identidade, mas demonstrar um caminho coerente entre protótipo e aplicação real: credenciais protegidas, emissão de token, autorização nas APIs, configuração segura e experiência de sessão no frontend.

Implementar JWT apenas no Gateway seria insuficiente. Qualquer acesso direto a uma API interna poderia ignorar essa barreira. Colocar usuários no Estoque ou no Faturamento também atribuiria identidade a um domínio que não é seu proprietário. Por isso, a autenticação terá uma fronteira própria.

---

## 2. Decisão

Será criado `Identity.Service`, um serviço de apoio desenvolvido em C# com .NET 10. Ele será o único proprietário de usuários, credenciais e emissão de tokens e possuirá banco PostgreSQL próprio.

```text
Angular SPA
    -> HTTP -> API Gateway
                   |-- HTTP -> Identity API -> Identity Database
                   |-- HTTP -> Inventory API -> Inventory Database
                   `-- HTTP -> Billing API  -> Billing Database
```

O serviço de Identidade não participa da mensageria nem do fluxo de emissão. RabbitMQ continua integrando exclusivamente Faturamento e Estoque.

---

## 3. Escopo funcional

### Incluído

- login por e-mail e senha;
- usuário administrativo inicial criado por seed idempotente;
- armazenamento e verificação segura de senha com ASP.NET Core Identity;
- emissão de JWT bearer assinado;
- expiração curta do access token;
- validação do token no Gateway, Inventory API e Billing API;
- autorização das operações funcionais;
- interceptor Angular para envio do bearer token;
- tratamento de sessão expirada;
- logout local;
- suporte bearer nos documentos OpenAPI;
- testes unitários e de integração de autenticação e autorização.

### Excluído

- cadastro público de usuários;
- recuperação ou alteração de senha;
- confirmação de e-mail;
- autenticação social;
- autenticação multifator;
- refresh token;
- revogação distribuída de access tokens;
- painel administrativo de usuários;
- permissões configuráveis;
- integração com provedor externo de identidade.

Essas capacidades são evoluções reconhecidas, mas não são necessárias para demonstrar a fronteira e o fluxo seguro propostos.

---

## 4. Autenticação e autorização

O perfil inicial será:

```text
Admin
```

Não serão inventados perfis adicionais sem regras que realmente diferenciem suas permissões. A aplicação utilizará políticas explícitas, inicialmente:

```text
AuthenticatedUser
AdminOnly
```

Os endpoints de login e de saúde são anônimos. As operações de Produtos, Notas e processos de emissão exigem usuário autenticado e a política administrativa definida pelo SDD. Rotas OpenAPI poderão ser expostas no ambiente de desenvolvimento sem conceder acesso às operações protegidas.

Uma resposta sem credencial válida utiliza `401 Unauthorized`. Uma identidade válida sem a permissão exigida utiliza `403 Forbidden`. O frontend não substitui essa verificação e apenas melhora a experiência do usuário.

---

## 5. Defesa em profundidade

O API Gateway valida o token antes de encaminhar rotas protegidas, mas essa validação não é a única barreira. Identity, Inventory e Billing configuram autenticação conforme sua exposição e cada API de domínio valida localmente:

- assinatura;
- algoritmo permitido;
- emissor;
- audiência;
- expiração;
- período de validade;
- claims exigidas pela política.

O Gateway não consulta o banco de identidade e não decide credenciais. Ele aplica políticas de borda e encaminha o token recebido. Inventory e Billing não chamam Identity a cada requisição; a assinatura permite validação local e evita acoplamento temporal.

Somente o frontend/Nginx terá porta publicada para o host no ambiente padrão. Ele encaminha a superfície funcional ao Gateway interno, que permanece a única entrada das APIs. Portas internas existem nas redes Docker para roteamento e integração, mas isso não elimina a validação de token pelo Gateway, Inventory e Billing.

---

## 6. Tokens e chaves

O SDD de Identidade definirá os valores quantitativos finais, mantendo estes princípios:

- access token de curta duração;
- horário sempre em UTC;
- ausência de refresh token nesta entrega;
- algoritmo e tamanho de chave explicitamente permitidos;
- validação estrita de issuer e audience;
- tolerância de relógio pequena e configurada;
- token sem dados sensíveis;
- claims reduzidas ao necessário, como identificador, e-mail e perfil;
- chave de assinatura nunca versionada.

Como o ambiente será local e autocontido, o mecanismo inicial poderá usar chave simétrica robusta fornecida por secret de ambiente. A evolução para assinatura assimétrica e rotação de chaves deverá ser reconhecida na documentação final, sem ser simulada parcialmente.

Logout remove o token do estado do frontend. Sem revogação ou refresh token, um access token já emitido continua válido até expirar; por isso sua vida útil deve ser curta e essa limitação será documentada.

---

## 7. Usuário inicial e segredos

O usuário de demonstração será criado de forma idempotente na preparação controlada do ambiente. Sua senha será processada pelo password hasher do ASP.NET Core Identity e nunca armazenada em texto simples no banco.

Configurações sensíveis serão fornecidas por variáveis ou secrets do ambiente, incluindo conceitualmente:

```text
AUTH_SEED_EMAIL
AUTH_SEED_PASSWORD
JWT_SIGNING_KEY
```

O repositório conterá `.env.example` apenas com nomes e valores ilustrativos não secretos. O `.env` real será ignorado pelo Git. Aplicação, migrations, testes e logs não devem revelar senha, hash ou chave de assinatura.

O seed não substituirá senha de usuário já existente a cada inicialização. Falha de configuração obrigatória deve interromper o startup com mensagem sanitizada, em vez de iniciar com credencial padrão conhecida.

---

## 8. Persistência e arquitetura interna

Identity possui banco e credencial próprios. Inventory e Billing não acessam suas tabelas. O Gateway permanece sem banco.

O serviço seguirá os mesmos limites arquiteturais gerais do projeto, mas não recriará abstrações internas do ASP.NET Core Identity sem benefício. Application coordena login e emissão; Infrastructure integra Identity, EF Core e assinatura; Api publica contratos; Domain conterá somente regras próprias que existirem de fato.

Migrations serão versionadas e executadas pelo migrator do serviço. `EnsureCreated` continua proibido.

---

## 9. Frontend

O Angular terá:

- tela de login;
- estado explícito de sessão;
- interceptor para anexar `Authorization: Bearer`;
- guardas de rota como recurso de experiência, não de segurança;
- tratamento central de `401` para encerrar a sessão e solicitar novo login;
- apresentação apropriada de `403` sem apagar automaticamente uma sessão válida;
- botão de logout.

O mecanismo de armazenamento do token será decidido no SDD do frontend considerando simplicidade do desafio e risco de XSS. Independentemente da escolha, nenhum dado sensível adicional será persistido no navegador e o projeto aplicará medidas básicas contra injeção de conteúdo.

---

## 10. Testes e evidências

No mínimo, deverão existir provas para:

- login válido retorna token utilizável;
- senha inválida não revela se credenciais parcialmente coincidem;
- endpoint protegido sem token retorna `401`;
- token expirado, adulterado, com issuer ou audience inválidos é rejeitado;
- identidade sem política exigida retorna `403`;
- token válido atravessa o Gateway e é novamente validado pelo serviço;
- segredo não aparece em resposta ou log;
- seed é idempotente;
- expiração encerra a sessão do frontend de forma compreensível.

Os testes de integração utilizarão PostgreSQL real no Docker. Chaves e usuários de teste serão exclusivos do ambiente de testes.

---

## 11. Consequências

### Positivas

- protege operações que alteram estoque e fecham documentos;
- demonstra uma preocupação típica de sistemas empresariais;
- mantém identidade fora dos domínios de Estoque e Faturamento;
- evita que o Gateway se torne proprietário de usuários;
- demonstra autenticação no frontend, Gateway e APIs;
- apresenta um caminho evolutivo tecnicamente defensável.

### Custos e limitações

- adiciona serviço, banco, migrations, tela e testes;
- aumenta a configuração do Docker Compose;
- JWT sem revogação permanece válido até expirar;
- um único perfil demonstra estrutura, mas não um modelo completo de permissões;
- gestão do ciclo de vida de usuários permanece fora desta entrega.

---

## 12. Alternativas não adotadas

### Aplicação anônima

Atenderia literalmente ao enunciado, mas foi rejeitada como decisão de produto porque exporia operações mutáveis sem identidade.

### Gateway emitindo tokens e armazenando usuários

Rejeitada porque mistura autenticação com roteamento e dá persistência a um componente que deve permanecer de borda.

### Usuários pertencentes ao Faturamento

Rejeitada porque identidade é transversal e não pertence ao domínio de notas.

### Validação somente no Gateway

Rejeitada porque cria uma única barreira e permite contorno caso uma API seja alcançada diretamente.

### Provedor externo de identidade

Não adotado para manter o backend do desafio em C#/.NET, a execução autocontida e a demonstração compreensível. Em produção, um provedor dedicado continuaria sendo uma alternativa válida.

---

## 13. Impacto no planejamento

- será criado um SDD específico para Identidade;
- contratos de login e segurança serão incluídos no SDD de contratos;
- o SDD do Gateway definirá rotas anônimas e protegidas;
- Inventory e Billing incluirão validação e políticas;
- o SDD do frontend incluirá sessão, interceptor e guardas;
- testes, Docker Compose, observabilidade, documentação e vídeo incluirão o fluxo autenticado;
- a matriz de rastreabilidade distinguirá requisitos originais e diferenciais aprovados.
