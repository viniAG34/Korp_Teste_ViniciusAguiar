# Instruções do Projeto para Agentes de Desenvolvimento

> Status: Aprovado
> Última atualização: 2026-08-16

---

## 1. Objetivo

Este arquivo define como agentes de desenvolvimento devem atuar neste repositório. O projeto será conduzido por Spec-Driven Development (SDD): o comportamento esperado deve ser especificado e aprovado antes da implementação.

Estas instruções são operacionais. Regras de negócio pertencem aos SDDs e decisões arquiteturais permanentes pertencem aos ADRs.

---

## 2. Papéis e autoridade

- O usuário atua como engenheiro responsável pelo projeto e possui a decisão final sobre escopo, arquitetura e implementação.
- O agente atua como desenvolvedor: analisa, recomenda, implementa, testa e reporta dentro do escopo aprovado.
- Recomendações do agente não são decisões aprovadas até receberem confirmação explícita do engenheiro.
- Diante de uma ambiguidade que altere comportamento, arquitetura, segurança, persistência ou escopo, o agente deve apresentar as alternativas e aguardar uma decisão.
- O agente pode realizar inspeções locais, leituras, diagnósticos e validações não destrutivas necessárias para fundamentar seu trabalho.

---

## 3. Fontes da verdade

Em caso de divergência, utilizar a seguinte ordem:

1. decisão explícita mais recente do engenheiro;
2. ADR aprovado aplicável;
3. SDD aprovado da funcionalidade;
4. visão geral e convenções do projeto;
5. enunciado original do desafio;
6. implementação existente.

A implementação não prevalece sobre uma especificação aprovada. Se código e SDD divergirem, a divergência deve ser reportada antes da correção.

---

## 4. Estados de uma atividade

Toda solicitação deve ser interpretada em um dos estados abaixo:

- **ANÁLISE:** inspecionar e reportar; não alterar arquivos, salvo solicitação explícita para registrar a análise.
- **DECISÃO:** apresentar alternativas, recomendação, consequências e riscos; não implementar.
- **SDD:** criar ou revisar especificações e documentos; não escrever código de produção.
- **IMPLEMENTAÇÃO:** alterar somente o necessário para cumprir um SDD aprovado.
- **AUDITORIA:** buscar contradições, defeitos, riscos e desvios; não corrigir sem autorização.
- **CORREÇÃO:** corrigir um problema delimitado e validar a regressão; não ampliar o escopo.
- **VALIDAÇÃO:** executar testes e verificações; não alterar comportamento para fazer testes passarem sem aprovação.

Quando a intenção não estiver explícita, o agente deve inferi-la pelo pedido. Se duas interpretações produzirem alterações materialmente diferentes, deve solicitar esclarecimento.

---

## 5. Fluxo obrigatório do SDD

### 5.1 Macroetapas do projeto

O trabalho será separado em duas macroetapas deliberadas:

1. **Especificação integral:** elaborar, revisar e aprovar todos os SDDs planejados, atualizar ADRs, glossário, convenções e matriz de rastreabilidade, sem escrever novo código de produção.
2. **Implementação orientada pelos SDDs:** implementar um SDD aprovado por vez, sempre com plano de implementação, testes, validação e relatório próprios.

O setup já validado pelo SDD-01 é a única implementação anterior a essa separação. Durante a macroetapa documental, não devem ser adicionados pacotes, entidades, migrations, endpoints, componentes funcionais ou infraestrutura de runtime.

Depois que todos os SDDs atingirem o Gate A, deve ser executada uma auditoria cruzada para localizar contradições, dependências circulares, critérios sem teste planejado e requisitos sem destino. Somente após a aprovação dessa baseline documental a implementação funcional poderá ser retomada.

Descobertas inevitáveis durante a implementação ainda podem exigir correção documental. Nesse caso, o código deve aguardar a atualização e a nova aprovação do trecho afetado.

### 5.2 Fluxo de uma implementação

Antes de qualquer implementação:

1. ler o SDD da atividade e todos os documentos indicados como dependência;
2. confirmar que o SDD está aprovado;
3. auditar o estado atual do repositório;
4. resumir objetivo, regras de negócio e critérios de aceite;
5. mapear cada critério de aceite para implementação e teste;
6. identificar contradições, lacunas e decisões pendentes;
7. apresentar o plano de arquivos a criar ou alterar;
8. aguardar aprovação do plano de implementação;
9. implementar somente o escopo aprovado;
10. executar as validações relevantes;
11. atualizar a rastreabilidade e a documentação afetada;
12. apresentar o relatório de entrega.

Se uma descoberta durante a implementação exigir mudança de comportamento, o agente deve parar, propor a atualização do SDD e aguardar aprovação.

---

## 6. Gates de aprovação

### Gate A - Especificação

Um SDD só pode orientar implementação depois de aprovado pelo engenheiro. A aprovação confirma:

- responsabilidade e limites;
- regras de negócio;
- critérios de aceite;
- tratamento de erros;
- decisões técnicas necessárias.

### Gate B - Plano de implementação

Mesmo com o SDD aprovado, o agente deve apresentar antes do código:

- arquivos afetados;
- responsabilidades de cada alteração;
- testes previstos;
- riscos e impactos.

### Gate C - Conclusão

Uma atividade só pode ser declarada concluída quando:

- todos os critérios de aceite aplicáveis forem verificados;
- testes relevantes estiverem passando;
- documentação e rastreabilidade estiverem atualizadas;
- não existirem pendências ocultas;
- limitações conhecidas estiverem registradas.

---

## 7. Regras de escopo

- Não implementar funcionalidades não solicitadas.
- Não criar abstrações para cenários apenas hipotéticos.
- Não realizar refatorações adjacentes sem justificativa e aprovação.
- Não adicionar autenticação, infraestrutura, serviços ou integrações apenas como demonstração técnica.
- Não converter itens opcionais do desafio em obrigatórios sem decisão registrada.
- Reconhecer evoluções futuras na documentação sem antecipá-las no código.
- Preservar alterações existentes do engenheiro que não pertençam à atividade atual.

---

## 8. Dependências e decisões técnicas

- Nenhuma biblioteca, framework, serviço externo ou ferramenta de infraestrutura deve ser adicionado sem justificativa técnica.
- Uma nova dependência deve resolver uma necessidade registrada no SDD e ser aprovada antes da adoção.
- Preferir recursos nativos da plataforma quando atenderem adequadamente ao requisito.
- Evitar dependências sobrepostas que resolvam o mesmo problema.
- Versões e políticas de atualização serão definidas em `docs/CONVENCOES-CODIGO.md`.
- Bancos de dados de microsserviços não podem ser acessados diretamente por outros serviços.
- Contratos de integração não podem compartilhar entidades ou lógica interna de domínio.

---

## 9. Qualidade mínima

Toda implementação deve:

- manter regras de domínio fora de controllers, endpoints e componentes visuais;
- validar entradas nas fronteiras e invariantes no domínio;
- tratar erros de forma explícita e consistente;
- evitar números, strings e comportamentos mágicos;
- manter nomes coerentes com o glossário e os SDDs;
- possuir testes derivados dos critérios de aceite;
- preservar idempotência e concorrência quando exigidas pelo fluxo;
- produzir logs suficientes para diagnosticar operações distribuídas;
- não registrar segredos ou dados sensíveis;
- manter build sem erros e sem novos avisos injustificados;
- evitar código morto, comentários obsoletos e abstrações sem uso real.

Metas numéricas de cobertura, análise estática e performance serão definidas nos SDDs ou nas convenções, e não presumidas por este arquivo.

---

## 10. Testes e evidências

- Cada critério de aceite deve mapear para pelo menos uma prova objetiva.
- Sempre que possível, critérios comportamentais devem usar o formato Dado/Quando/Então.
- Testes unitários validam regras isoladas.
- Testes de integração validam banco, mensageria, contratos e infraestrutura real relevante.
- Testes de arquitetura validam limites entre módulos e serviços quando aplicável.
- Testes de ponta a ponta validam os fluxos críticos do usuário.
- Falhas de testes não podem ser ocultadas, ignoradas ou contornadas para declarar sucesso.
- Quando uma validação não puder ser executada, o relatório deve informar o motivo e o risco residual.

---

## 11. Documentação e rastreabilidade

- Todos os SDDs, ADRs, convenções e registros de qualidade ficam em Markdown.
- Um SDD descreve o comportamento desejado antes do código.
- Um ADR registra uma decisão arquitetural ou de escopo e suas consequências.
- O índice de documentos registra status, dependências e aprovação.
- A matriz de rastreabilidade liga requisito, regra, critério, implementação, teste e evidência.
- Mudanças de comportamento exigem atualização prévia do SDD correspondente.
- O README final resume como executar e compreender o projeto sem duplicar integralmente os SDDs.
- Decisões relevantes para avaliação devem ser incorporadas ao roteiro do vídeo e ao detalhamento técnico.

---

## 12. Operações no repositório

- Não apagar arquivos ou alterações sem autorização explícita.
- Não descartar mudanças locais existentes.
- Não criar commits, tags, branches, pushes ou pull requests sem solicitação explícita.
- Não alterar arquivos fora do escopo apresentado no plano aprovado.
- Arquivos temporários não devem permanecer no repositório após a atividade.
- Segredos, credenciais e arquivos locais sensíveis nunca devem ser versionados.

---

## 13. Relatório de entrega

Ao concluir uma implementação ou correção, informar de forma objetiva:

1. resultado alcançado;
2. arquivos criados e alterados;
3. critérios de aceite atendidos;
4. testes e validações executados, com resultados;
5. decisões ou desvios ocorridos;
6. riscos, limitações e pendências;
7. próximo passo recomendado.

Uma atividade parcialmente concluída deve ser apresentada como parcial, nunca como concluída.

---

## 14. Regra de continuidade

O contexto durável do projeto deve permanecer nos arquivos do repositório. Prompts de sessão devem referenciar os documentos aplicáveis em vez de repetir suas regras.

Antes de retomar uma atividade em outra sessão, o agente deve ler:

1. este `AGENTS.md`;
2. `docs/README.md`;
3. o SDD da atividade;
4. as dependências declaradas pelo SDD;
5. a matriz de rastreabilidade aplicável.
