# SDD-04 - Identity Service

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-18
> Gate A aprovado em: 2026-08-18
> Dependências: SDD-01, SDD-02, SDD-03, ADR-005, ADR-006, ADR-009, ADR-010, ADR-013 e ADR-014

---

## 1. Objetivo

Especificar o serviço responsável por autenticar o usuário administrativo, emitir JWTs verificáveis pelos demais componentes e preparar o ambiente de identidade com segurança, sem transformar o desafio em uma plataforma de gestão de usuários.

O documento detalhará responsabilidades por camada, login, seed, emissão e validação de tokens, políticas, configuração, falhas, observabilidade e testes necessários para implementar o diferencial de segurança já aprovado.

---

## 2. Requisitos rastreados

- `DIF-002` - autenticação;
- `DIF-003` - autorização;
- `DIF-008` - OpenAPI;
- `DIF-009` - observabilidade;
- `QLT-001` a `QLT-008`, conforme aplicáveis;
- `APR-008` e `APR-009` para futura apresentação técnica.

Identity não é requisito obrigatório do enunciado. Sua implementação não pode comprometer a entrega dos fluxos funcionais definidos para Inventory, Billing e frontend.

---

## 3. Escopo previsto

- fronteira e responsabilidades do Identity;
- organização pelas camadas existentes;
- `ApplicationUser`, papel `Admin` e ASP.NET Core Identity;
- inicializador administrativo idempotente;
- caso de uso de login;
- emissão JWT e claims;
- parâmetros de validação compartilhados por configuração;
- políticas `AuthenticatedUser` e `AdminOnly`;
- configuração e proteção de segredos;
- tratamento de falhas e logs sanitizados;
- OpenAPI bearer;
- critérios de aceite e testes.

---

## 4. Fora do escopo

- cadastro público ou administração de usuários;
- troca, recuperação ou redefinição de senha;
- confirmação de e-mail;
- autenticação social ou multifator;
- refresh token, sessão persistente ou revogação distribuída;
- permissões configuráveis;
- provedor externo de identidade;
- armazenamento de access tokens;
- mensageria, Inbox ou Outbox;
- comportamento de sessão do Angular, pertencente ao SDD-09;
- roteamento e políticas de borda do Gateway, pertencentes ao SDD-08.

---

## 5. Blocos de decisão

1. responsabilidade, modelo e limites arquiteturais;
2. inicializador administrativo e configuração segura;
3. fluxo de login e proteção contra enumeração;
4. formato, claims, assinatura e duração do JWT;
5. validação distribuída e políticas de autorização;
6. falhas, observabilidade e OpenAPI;
7. critérios de aceite, testes, riscos e marcadores.

Nenhuma implementação funcional será iniciada durante esta macroetapa documental.

---

## 6. Decisões herdadas

- backend em C# com .NET 10 LTS;
- Minimal APIs organizadas por feature;
- ASP.NET Core Identity com `ApplicationUser : IdentityUser<Guid>` e `IdentityRole<Guid>`;
- PostgreSQL exclusivo `identity_db` e migrations próprias;
- login público exclusivamente por e-mail e senha;
- único papel funcional inicial `Admin`;
- access token JWT curto, sem refresh token;
- Gateway, Inventory e Billing validam tokens localmente;
- Identity não participa do RabbitMQ nem do fluxo de emissão;
- segredo de assinatura, e-mail e senha administrativos vêm do ambiente;
- respostas HTTP obedecem ao contrato estabilizado no SDD-03;
- código relevante sujeito a testes unitários, integração real e gate mínimo de 80%.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Responsabilidade, modelo e limites arquiteturais

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Responsabilidade do serviço

`Identity.Service` é o proprietário exclusivo de:

- usuários, credenciais e hashes de senha;
- papéis e associação entre usuário e papel;
- autenticação por e-mail e senha;
- emissão de access tokens JWT;
- banco `identity_db` e migrations correspondentes;
- inicialização controlada do administrador.

O serviço não possui produtos, notas, saldos ou regras de autorização pertencentes aos domínios funcionais. Também não armazena JWTs, não mantém sessão do frontend, não participa do RabbitMQ e não é consultado por Inventory ou Billing a cada requisição.

Inventory e Billing confiam na assinatura e nas claims validadas localmente. Essa independência evita acoplamento temporal ao Identity durante as operações funcionais.

#### Modelo mínimo

```csharp
ApplicationUser : IdentityUser<Guid>
IdentityRole<Guid>
```

Não existe uma segunda entidade `User` no Domain. Ela duplicaria estado e comportamento já protegidos pelo ASP.NET Core Identity sem acrescentar regra própria.

O usuário utiliza somente:

- UUID;
- e-mail obrigatório;
- nome de usuário igual ao e-mail;
- senha armazenada exclusivamente pelo mecanismo do Identity;
- associação ao papel `Admin`;
- campos internos necessários à segurança e ao funcionamento do framework.

Nome, telefone, endereço, empresa, preferências e perfil detalhado não serão adicionados.

#### Responsabilidade por camada

```text
Identity.Api
  -> endpoint de login
  -> contrato HTTP e Problem Details
  -> OpenAPI
  -> composição de dependências

Identity.Application
  -> caso de uso Login
  -> coordenação da autenticação
  -> resultado independente de HTTP
  -> abstração de emissão do token

Identity.Infrastructure
  -> ApplicationUser e IdentityDbContext
  -> UserManager e RoleManager
  -> verificação segura de senha
  -> geração e assinatura JWT
  -> configuração EF Core e migrations
  -> inicializador administrativo

Identity.Domain
  -> sem modelo próprio enquanto não existir regra de domínio real
```

O projeto Domain permanece na solução para preservar o limite arquitetural, mas não receberá classes artificiais. Regra própria futura exige decisão e especificação antes de ampliar esse projeto.

#### Superfície funcional

```text
POST /api/v1/auth/login
```

Esse é o único endpoint funcional do Identity nesta entrega. Não existem endpoints para criar ou listar usuários, alterar perfil ou senha, administrar papéis, renovar ou revogar token.

Health checks e documentos OpenAPI são superfícies técnicas tratadas pelos SDDs responsáveis e não ampliam o domínio funcional do serviço.

#### Limites de dependência

- Identity acessa somente `identity_db`;
- Gateway não acessa banco de identidade;
- Inventory e Billing não referenciam `ApplicationUser`, `IdentityDbContext` ou migrations;
- não existem foreign keys ou navegações entre bancos;
- outros serviços persistem somente o UUID obtido da claim `sub`;
- `Korp.Shared.Contracts` não recebe usuários, claims internas ou abstrações do ASP.NET Core Identity;
- Identity não publica nem consome eventos.

### 7.2 Bloco 2 - Inicializador administrativo e configuração segura

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Preparação controlada

O administrador não é criado pelo startup da API. A preparação do ambiente segue:

```text
PostgreSQL disponível
    -> processo controlado aplica migrations do Identity
        -> inicializador cria papel e administrador
            -> processo termina com sucesso
                -> Identity API pode iniciar
```

No Docker Compose, a API dependerá da conclusão bem-sucedida dessa preparação. A forma de hospedagem e a ordem física desse processo serão definidas pelo SDD-11, sem executar migrations no startup da API e sem exigir antecipadamente um novo projeto na solution. A separação impede que múltiplas instâncias da API disputem migrations ou modifiquem credenciais durante reinicializações comuns.

#### Configurações obrigatórias

```text
ConnectionStrings__IdentityDatabase
IdentitySeed__Email
IdentitySeed__Password
Jwt__SigningKey
Jwt__Issuer
Jwt__Audience
```

O `.env.example` documenta somente nomes e valores inequivocamente ilustrativos. Valores reais permanecem no `.env` ignorado ou em secrets do ambiente.

Não existem defaults funcionais para senha administrativa, chave JWT, connection string, issuer ou audience. Configuração ausente ou inválida encerra o processo dependente com mensagem sanitizada e exit code diferente de zero.

#### Política de senha

A senha configurada para o administrador exige:

- mínimo de 12 e máximo de 128 caracteres;
- ao menos uma letra maiúscula;
- ao menos uma letra minúscula;
- ao menos um número;
- ao menos um caractere não alfanumérico.

Esses valores serão configurados explicitamente no ASP.NET Core Identity. A senha não sofre trim ou normalização, não aparece em logs e passa exclusivamente pelos validadores e pelo password hasher do framework.

#### Algoritmo idempotente

O inicializador:

1. valida toda configuração necessária antes de alterar estado;
2. garante a existência do papel `Admin`;
3. normaliza e procura o administrador pelo e-mail;
4. cria o usuário com `EmailConfirmed = true` somente se não existir;
5. garante sua associação ao papel `Admin`;
6. encerra sem modificar senha ou dados de segurança de usuário existente.

Papel, usuário e associação existentes não são duplicados. Se o e-mail configurado já pertencer a um usuário, sua senha atual é preservada e somente a associação ausente ao papel pode ser acrescentada. Qualquer resultado malsucedido de `RoleManager` ou `UserManager` impede que a preparação informe sucesso.

O inicializador pode ser executado novamente após falha parcial: operações já confirmadas são reconhecidas e as etapas restantes são concluídas de modo idempotente.

#### Logs e limitação operacional

Logs podem informar migrations concluídas, existência ou criação do papel, existência ou criação do administrador e associação concluída. Nunca incluem senha, hash, chave JWT, token ou connection string.

Alterar `IdentitySeed__Password` depois da criação não redefine a senha. Isso evita reset acidental. Como troca e recuperação não pertencem à entrega, uma redefinição necessária será uma operação administrativa controlada e documentada fora do fluxo normal da aplicação.

### 7.3 Bloco 3 - Fluxo de login e proteção contra enumeração

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Entrada e validação

```text
POST /api/v1/auth/login
Política: Anonymous
```

O request e o response obedecem integralmente ao SDD-03. Na fronteira:

- e-mail é obrigatório, válido e possui no máximo 254 caracteres;
- somente espaços externos do e-mail são removidos;
- senha é obrigatória e possui no máximo 128 caracteres;
- senha não sofre trim ou outra normalização;
- contrato inválido retorna `400` com `validation_failed`.

#### Coordenação do caso de uso

```text
LoginEndpoint
    -> LoginHandler
        -> localizar usuário pelo e-mail normalizado
        -> verificar resultado de autenticação e lockout
        -> obter papéis
        -> emitir JWT
        -> retornar identidade e expiração
```

Application trabalha com request e resultado próprios, sem depender de `HttpContext`, status HTTP ou `ProblemDetails`. Infrastructure encapsula a integração necessária com ASP.NET Core Identity e o emissor JWT sem recriar um repositório genérico de usuários.

#### Resposta indistinguível

Usuário inexistente, senha incorreta e usuário bloqueado retornam a mesma resposta:

```text
401 Unauthorized
code: invalid_credentials
detail: E-mail ou senha inválidos.
```

Quando o e-mail não existir, será executado trabalho de hash equivalente ao caminho de senha incorreta. A medida reduz diferenças temporais observáveis, sem alegar resistência absoluta a análises estatísticas de timing.

Logs operacionais não distinguem publicamente qual parte da credencial falhou e nunca registram senha. Métricas podem contabilizar tentativas malsucedidas sem usar e-mail como label de alta cardinalidade.

#### Lockout

```text
MaxFailedAccessAttempts = 5
DefaultLockoutTimeSpan = 5 minutos
AllowedForNewUsers = true
```

- falha de senha incrementa o contador pelo mecanismo do Identity;
- autenticação bem-sucedida limpa o contador;
- usuário bloqueado recebe a resposta genérica;
- o período expirado permite nova tentativa;
- não existe endpoint público de desbloqueio.

Rate limiting de borda pertence ao SDD-08 e complementa, sem substituir, o lockout por usuário.

#### Condições para emissão

O JWT só é criado depois de usuário encontrado, resultado de senha bem-sucedido, ausência de lockout e obtenção dos papéis. Os papéis retornados são ordenados deterministicamente.

Falha de banco ou de consulta aos papéis não é convertida em credencial inválida:

```text
503 Service Unavailable
code: identity_unavailable
```

O response nunca contém senha, hash, stamps ou campos internos. Token emitido também não é registrado em log.

#### Cancelamento e falhas

- cancellation token da requisição é propagado sempre que a API utilizada permitir;
- cancelamento esperado não é traduzido para `500` nem registrado como falha inesperada;
- falhas técnicas utilizam correlação e mensagens sanitizadas;
- falhas inesperadas seguem o tratamento central definido pelos ADRs e pelo SDD-03;
- nenhuma resposta ou log expõe diferença interna capaz de confirmar a existência do e-mail.

### 7.4 Bloco 4 - Formato, claims, assinatura e duração do JWT

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Parâmetros criptográficos

```text
Algoritmo: HS256
Formato da chave configurada: Base64
Tamanho mínimo após decodificação: 256 bits (32 bytes)
Duração do access token: 15 minutos
Clock skew: 30 segundos
```

`Jwt__SigningKey` contém bytes aleatórios codificados em Base64. O processo falha durante validação de configuração se a chave não for Base64 válida, possuir menos de 32 bytes ou se issuer ou audience estiverem ausentes.

Somente `HS256` é permitido. Tokens sem assinatura, com `alg: none` ou outro algoritmo são rejeitados independentemente da validade das demais claims.

#### Claims canônicas

```json
{
  "sub": "6dab8c4c-2bb8-46cc-a865-0e992aaeb443",
  "email": "admin@example.com",
  "role": ["Admin"],
  "jti": "85a5ce2f-e660-4620-92dd-4b04ac625389",
  "iat": 1787072400,
  "nbf": 1787072400,
  "exp": 1787073300,
  "iss": "korp-identity",
  "aud": "korp-erp-api"
}
```

- `sub`: UUID canônico e não vazio do usuário;
- `email`: e-mail normalizado para apresentação no contrato;
- `role`: uma ou mais claims de papel, ordenadas deterministicamente;
- `jti`: UUID único por token;
- `iat`, `nbf` e `exp`: NumericDate em UTC;
- `iss` e `aud`: valores exatos da configuração validada.

Senha, hash, security stamp, connection string, dados de perfil inexistentes e permissões especulativas nunca aparecem no token.

#### Mapeamento

```text
MapInboundClaims = false
NameClaimType = email
RoleClaimType = role
```

As APIs trabalham diretamente com os nomes canônicos, sem conversão automática para URIs de claims do .NET. Inventory e Billing obtêm autoria exclusivamente de `sub`; e-mail não é identificador persistente de usuário.

#### Relógio e expiração

A emissão usa `TimeProvider` para permitir testes determinísticos:

```text
issuedAt = UTC atual
notBefore = issuedAt
expiresAt = issuedAt + 15 minutos
expiresInSeconds = 900
```

O frontend pode usar os campos de expiração para experiência de sessão, mas somente a validação do backend decide validade. Não existem refresh token, renovação silenciosa, armazenamento do JWT, revogação individual ou extensão automática.

#### Limite da chave simétrica

Identity, Gateway, Inventory e Billing recebem o mesmo segredo para permitir validação local. Isso é aceitável no ambiente autocontido do desafio, mas concede material de assinatura também aos validadores.

A evolução recomendada para um ambiente distribuído real é assinatura assimétrica, chave privada exclusiva do Identity, chave pública nos validadores e rotação identificada por chave. Essa infraestrutura não será simulada parcialmente nesta entrega.

### 7.5 Bloco 5 - Validação distribuída e políticas de autorização

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Parâmetros obrigatórios

Gateway, Inventory e Billing configuram:

```text
ValidateIssuerSigningKey = true
ValidateIssuer = true
ValidateAudience = true
ValidateLifetime = true
RequireSignedTokens = true
RequireExpirationTime = true
ClockSkew = 30 segundos
ValidAlgorithms = HS256
MapInboundClaims = false
```

Cada processo valida chave, issuer e audience antes de aceitar tráfego. Configuração ausente ou inválida impede startup. Nenhum componente consulta o banco do Identity, chama o login para validar token, confia apenas na passagem pelo Gateway ou aceita o algoritmo escolhido livremente pelo token.

#### Claims mínimas

Depois da validação criptográfica, o principal precisa conter:

- `sub` como UUID válido e não vazio;
- `email` preenchido;
- `jti` como UUID válido e não vazio;
- ao menos uma claim `role` preenchida;
- claims temporais, issuer e audience aceitos pelo bearer handler.

Um token criptograficamente válido, mas sem as claims mínimas, não constitui a identidade esperada pela aplicação e retorna `401`.

#### Políticas

```text
AuthenticatedUser
  -> identidade autenticada
  -> sub válido
  -> email presente

AdminOnly
  -> todos os requisitos de AuthenticatedUser
  -> role contém exatamente Admin
```

A comparação do papel é ordinal e sensível a maiúsculas, usando `Admin` como valor canônico. A extração de autoria ocorre somente depois do sucesso da política.

#### Defesa em profundidade

```text
Angular
    -> Gateway valida token e política
        -> encaminha Authorization original
            -> Inventory ou Billing valida novamente
                -> endpoint aplica sua própria política
```

O Gateway protege a borda, mas a API proprietária permanece responsável por proteger seu endpoint. Na consulta HTTP interna, Billing propaga o bearer original e Inventory aplica novamente `AdminOnly`.

Como o único endpoint funcional do Identity é o login anônimo, sua API não possui nesta entrega operação funcional que exija bearer. Uma futura superfície administrativa exigirá validação e políticas locais antes de ser publicada.

#### Respostas de autenticação e autorização

- token ausente, expirado, adulterado, malformado ou incompleto: `401 Unauthorized`;
- identidade válida sem o papel necessário: `403 Forbidden`;
- ambas as respostas usam Problem Details sanitizado;
- o `401` produzido pelo desafio bearer de uma rota protegida inclui `WWW-Authenticate: Bearer`;
- o `401 invalid_credentials` do login não simula um desafio bearer, pois nenhuma credencial bearer foi apresentada nesse fluxo;
- nenhuma resposta informa claim ausente, algoritmo recebido, diferença de assinatura ou configuração esperada.

#### Janela de alteração de papel

O papel contido no JWT permanece efetivo até a expiração do token. Alterar associações no banco não modifica tokens já emitidos porque não existe revogação distribuída. A duração de 15 minutos limita essa janela, que será documentada sem alegação de invalidação imediata.

### 7.6 Bloco 6 - Falhas, observabilidade e OpenAPI

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Classificação de respostas

| Situação | Status | `code` |
|---|---:|---|
| Request inválido | 400 | `validation_failed` |
| Credencial inválida ou usuário bloqueado | 401 | `invalid_credentials` |
| Token ausente ou inválido em API protegida | 401 | `authentication_required` |
| Token válido sem permissão | 403 | `access_denied` |
| Banco indisponível durante login | 503 | `identity_unavailable` |
| Falha inesperada | 500 | `unexpected_error` |

Todas as respostas obedecem ao Problem Details do SDD-03. Resultados do `UserManager` e `RoleManager` durante preparação não são expostos por HTTP; falha impede conclusão do processo preparador com mensagem sanitizada.

#### Exceções e cancelamento

```text
validação
    -> regra da aplicação
        -> infraestrutura conhecida
            -> falha inesperada
```

- credencial inválida e lockout são resultados esperados, não exceções;
- indisponibilidade conhecida é traduzida para `503`;
- cancelamento esperado não é convertido em `500`;
- detalhes de Npgsql, EF Core, Identity ou JWT não aparecem na resposta;
- exceção inesperada retorna `500` com `traceId` e mantém diagnóstico interno.

#### Logs de autenticação

Sucesso pode registrar:

```text
event = authentication_succeeded
userId
correlationId
duration
roleCount
```

Falha de credencial registra somente evento genérico `authentication_failed`, `correlationId` e duração. Não registra e-mail informado, existência do usuário, senha, lockout, token, hash ou security stamp.

Falhas técnicas usam `authentication_technical_failure`, operação e identificadores técnicos sanitizados. Nenhum log inclui credenciais ou material criptográfico.

#### Proteção de tráfego e configuração

O corpo de `/api/v1/auth/login` não é registrado em nenhum ambiente. `Authorization`, `Cookie` e `Set-Cookie` são removidos ou mascarados de qualquer logging HTTP. Valores de `IdentitySeed__Password`, `Jwt__SigningKey`, connection strings e access tokens nunca são registrados.

Configuração insegura impede startup. O diagnóstico informa somente a categoria inválida, por exemplo configuração de assinatura, seed ou banco ausente, sem reproduzir o valor recebido.

#### Métricas e correlação

Pode existir contador de baixa cardinalidade:

```text
authentication_attempts_total{outcome="success|failure|technical_failure"}
```

E-mail, usuário, IP e `jti` não são labels. Métricas não substituem as decisões do Identity nem são utilizadas como fonte do lockout.

`X-Correlation-ID`, `correlationId` e `traceId` obedecem ao SDD-03. Credenciais nunca são utilizadas como identificadores de correlação.

#### OpenAPI

```text
/openapi/v1.json
```

O login é explicitamente anônimo e documenta request, response `200`, Problem Details para `400`, `401`, `503` e `500`, limites dos campos e exemplos sem credenciais ou tokens reais.

Como Identity não possui operação funcional protegida nesta entrega, o documento não declara bearer como requisito do login. Gateway, Inventory e Billing descrevem bearer em seus próprios documentos para as operações protegidas.

### 7.7 Bloco 7 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-18

---

## 8. Critérios de aceite

### CA-ID-01 - Propriedade da identidade

**Dado** o conjunto de serviços,  
**quando** modelos, bancos e dependências forem inspecionados,  
**então** Identity será o único proprietário de usuários, credenciais, papéis e emissão de tokens.

### CA-ID-02 - Preparação idempotente

**Dado** o ambiente após migrations,  
**quando** o inicializador for executado mais de uma vez,  
**então** papel, usuário e associação não serão duplicados e a senha existente não será redefinida.

### CA-ID-03 - Configuração segura

**Dado** configuração obrigatória ausente ou insegura,  
**quando** o processo dependente iniciar,  
**então** ele falhará antes de aceitar tráfego, sem revelar os valores recebidos.

### CA-ID-04 - Login válido

**Dado** o administrador configurado,  
**quando** credenciais válidas forem autenticadas,  
**então** o contrato aprovado retornará um JWT utilizável e a expiração correspondente.

### CA-ID-05 - Falha indistinguível

**Dado** usuário inexistente, senha incorreta ou usuário bloqueado,  
**quando** o login for solicitado,  
**então** status, código e mensagem pública serão indistinguíveis e o caminho inexistente executará trabalho de hash equivalente.

### CA-ID-06 - Lockout

**Dado** um usuário habilitado para lockout,  
**quando** ocorrerem cinco falhas consecutivas,  
**então** novas tentativas serão bloqueadas por cinco minutos; autenticação bem-sucedida limpará o contador.

### CA-ID-07 - Emissão JWT

**Dado** uma autenticação válida,  
**quando** o token for emitido,  
**então** utilizará HS256, chave mínima de 256 bits, somente as claims aprovadas e duração de 15 minutos calculada por relógio injetável.

### CA-ID-08 - Validação estrita

**Dado** token expirado, adulterado, incompleto ou com algoritmo, issuer ou audience incorretos,  
**quando** alcançar componente protegido,  
**então** será rejeitado com `401` sem revelar a causa interna.

### CA-ID-09 - Políticas explícitas

**Dado** um principal autenticado,  
**quando** uma política for avaliada,  
**então** `AuthenticatedUser` exigirá claims mínimas e `AdminOnly` exigirá adicionalmente o papel canônico `Admin`.

### CA-ID-10 - Validação independente

**Dado** um request que atravessa o Gateway,  
**quando** alcançar Inventory ou Billing,  
**então** a API validará novamente token e política sem consultar Identity.

### CA-ID-11 - Erros padronizados

**Dado** falha de contrato, autenticação, autorização, infraestrutura ou execução inesperada,  
**quando** a resposta HTTP for produzida,  
**então** status e Problem Details seguirão este SDD e distinguirão falha técnica de credencial inválida.

### CA-ID-12 - Ausência de vazamento

**Dado** requests válidos, inválidos e falhos,  
**quando** respostas, logs, métricas e OpenAPI forem inspecionados,  
**então** senha, hash, token, chave e connection string não estarão presentes.

### CA-ID-13 - OpenAPI fiel

**Dado** o documento OpenAPI do Identity,  
**quando** comparado à API,  
**então** o login estará anônimo e seus contratos, limites e respostas estarão documentados sem exemplos secretos.

### CA-ID-14 - Escopo mínimo

**Dado** o serviço implementado e seu banco,  
**quando** superfícies e estruturas próprias forem inspecionadas,  
**então** não existirão fluxos adicionais de gestão de usuário, refresh token, sessão ou revogação.

---

## 9. Estratégia de testes planejada

| ID | Teste planejado | Nível | Critérios |
|---|---|---|---|
| TST-ID-001 | Validar referências e propriedade do Identity | Arquitetura | CA-ID-01 |
| TST-ID-002 | Aplicar migration do zero em PostgreSQL | Integração | CA-ID-01, CA-ID-02 |
| TST-ID-003 | Executar inicializador duas vezes | Integração | CA-ID-02 |
| TST-ID-004 | Preservar senha de usuário existente | Integração | CA-ID-02 |
| TST-ID-005 | Rejeitar senha inicial fora da política | Integração | CA-ID-03 |
| TST-ID-006 | Rejeitar configurações ausentes ou inseguras | Unitário/startup | CA-ID-03 |
| TST-ID-007 | Autenticar credenciais válidas | Integração | CA-ID-04 |
| TST-ID-008 | Comparar respostas para usuário inexistente, senha incorreta e lockout | Integração | CA-ID-05 |
| TST-ID-009 | Confirmar execução de hash no caminho de usuário inexistente | Unitário | CA-ID-05 |
| TST-ID-010 | Bloquear após cinco falhas e liberar após o período | Integração | CA-ID-06 |
| TST-ID-011 | Limpar contador após login válido | Integração | CA-ID-06 |
| TST-ID-012 | Validar claims e duração usando relógio controlado | Unitário | CA-ID-07 |
| TST-ID-013 | Rejeitar chave Base64 inválida ou menor que 256 bits | Unitário | CA-ID-03, CA-ID-07 |
| TST-ID-014 | Rejeitar assinatura, algoritmo, issuer e audience inválidos | Integração | CA-ID-08 |
| TST-ID-015 | Rejeitar token expirado ou ainda não válido | Integração | CA-ID-08 |
| TST-ID-016 | Rejeitar token sem claims mínimas | Integração | CA-ID-08, CA-ID-09 |
| TST-ID-017 | Diferenciar `401` de `403` nas políticas | Integração | CA-ID-09, CA-ID-11 |
| TST-ID-018 | Validar token novamente na API de domínio sem chamar Identity | Integração/arquitetura | CA-ID-10 |
| TST-ID-019 | Simular banco indisponível durante login | Integração | CA-ID-11 |
| TST-ID-020 | Inspecionar logs e respostas com credenciais sentinela | Segurança/integração | CA-ID-12 |
| TST-ID-021 | Comparar OpenAPI ao endpoint implementado | Snapshot/arquitetura | CA-ID-13 |
| TST-ID-022 | Inspecionar ausência de funcionalidades excluídas | Arquitetura | CA-ID-14 |
| TST-ID-023 | Propagar cancelamento sem produzir erro inesperado | Unitário/integração | CA-ID-11 |

PostgreSQL real será utilizado quando o comportamento depender do ASP.NET Core Identity ou da persistência. Tokens, usuários, senhas sentinela e chaves da suíte são exclusivos do ambiente de testes. Código relevante permanece sujeito ao gate mínimo de 80% definido pelo ADR-014.

---

## 10. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Chave simétrica disponível em vários serviços | Um validador comprometido também possui material de assinatura | Secrets isolados, escopo autocontido e evolução assimétrica documentada |
| Configurações de validação divergirem | Token aceito por um componente e rejeitado por outro | Mesmos nomes canônicos e matriz comum de tokens inválidos |
| Lockout negar acesso ao único administrador | Indisponibilidade temporária por abuso | Período curto, rate limiting de borda e limitação operacional documentada |
| Token roubado permanecer válido | Uso indevido até expiração | Duração de 15 minutos, HTTPS fora do ambiente local e ausência em logs |
| Seed alterar credenciais existentes | Perda de acesso ou mudança silenciosa | Senha preservada e teste idempotente |
| Login revelar existência por tempo | Enumeração de usuários | Trabalho de hash equivalente, resposta e logs uniformes |
| Segurança adicional atrasar o núcleo do desafio | Entrega funcional incompleta | Escopo limitado a login, papel Admin e access token |
| Segredo aparecer em diagnóstico | Comprometimento de credenciais | Sanitização e testes com valores sentinela |

---

## 11. Marcadores de qualidade

| Marcador | Exigência neste SDD |
|---|---|
| ESP | Sete blocos aprovados antes da implementação funcional |
| RAS | Decisões ligadas a critérios e testes identificáveis |
| ARC | Identity isolado dos domínios, do Gateway e da mensageria |
| DOM | Nenhuma entidade artificial ou regra especulativa criada |
| ERR | Credenciais inválidas, autorização e falhas técnicas tratadas separadamente |
| SEG | Senha, token, configuração, lockout, assinatura e validação protegidos |
| TST | Testes unitários e de integração sujeitos ao mínimo de 80% |
| INT | ASP.NET Core Identity e PostgreSQL verificados em infraestrutura real |
| OBS | Logs e métricas úteis sem dados sensíveis ou alta cardinalidade |
| DOC | OpenAPI, configuração e limitações de segurança documentados |
| QA | Matriz de tokens inválidos, repetição do seed e varredura de segredos executadas |

---

## 12. Limites para a futura implementação

Uma implementação deste SDD poderá criar:

- caso de uso e endpoint de login;
- integrações mínimas com `UserManager`, `SignInManager` e `RoleManager`;
- emissor JWT e validações de configuração;
- `IdentityDbContext`, configuração do Identity e migrations;
- inicializador administrativo no processo controlado de preparação;
- políticas e requisitos de claims reutilizados conceitualmente pelos hosts;
- testes descritos neste documento.

Não poderá criar:

- cadastro ou administração pública de usuários;
- recuperação ou alteração de senha;
- refresh token, sessão ou revogação;
- autorização dinâmica ou papéis adicionais;
- dependência de Identity em Inventory ou Billing;
- migrations ou seed executados pela API no startup;
- material secreto versionado;
- implementação antecipada de Gateway ou sessão Angular.

---

## 13. Condição para Gate A

O SDD-04 estará apto ao Gate A quando:

- os sete blocos estiverem aprovados;
- não houver contradição material com SDD-01, SDD-02, SDD-03 e ADR-013;
- cada critério possuir ao menos um teste planejado;
- a matriz de rastreabilidade estiver atualizada;
- parâmetros criptográficos e limites operacionais estiverem explícitos;
- nenhuma resposta, exemplo ou estratégia exigir segredo versionado;
- dependências dos SDDs de Inventory, Billing, Gateway e frontend estiverem delimitadas.

A aprovação estabiliza o desenho de identidade, mas não autoriza implementação antes da baseline documental conjunta.
