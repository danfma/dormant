# Feature Specification: DQL Syntax Highlighting

**Feature Branch**: `[011-dql-syntax-highlighting]`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "vamos criar tooling suporte para sintaxe highlight para nossos arquivos .dql e .dqls."

## Clarifications

### Session 2026-05-27

- Q: A visualização de arquivos DormantQL com syntax highlighting em plataformas de repositório (GitHub, GitLab, etc.) continua dentro do escopo desta feature, ou estamos removendo esse objetivo por enquanto e focando exclusivamente na experiência local nos editores Zed e VS Code? → A: Manter User Story 3 e SC-003 no escopo. Vamos entregar syntax highlighting também em repositórios (mesmo que exija trabalho adicional de grammar para o formato que o GitHub/etc. consiga consumir).
- Q: A gramática de syntax highlighting deve ser projetada desde o início para ser facilmente extensível para outros editores no futuro (além de Zed, VS Code e web), ou podemos focar primeiro em fazer um excelente trabalho só para esses três alvos e tratar expansões futuras como um possível rework posterior? → A: Sim, projetar para extensibilidade desde o início (escolher formato portável), pois pretendemos suportar JetBrains Rider e outros editores no futuro.
- Q: Entre Zed e VS Code, qual editor deve receber o foco inicial de implementação da syntax highlighting? → A: Começar por VS Code (mesmo preferindo pessoalmente Zed), pois é o mais maduro para validar a gramática e o ecossistema primeiro.
- Q: Como deve ser o suporte inicial em VS Code? → B: Entregar uma extensão VS Code mínima mas completa (package.json + ativação automática para .dql/.dqls + instruções claras), em vez de apenas o arquivo de gramática cru.
- Q: Como vamos entregar o syntax highlighting para visualização em repositórios (principalmente GitHub)? → B: Manter a gramática em um repositório nosso (formato TextMate ou similar) e documentar como o GitHub (e outros) podem consumir.
- Q: Devemos atualizar o FR-003 para definir explicitamente essas categorias semânticas como requisitos obrigatórios de distinguishability? → A: Sim, tornar explícito. Listar keywords, parameters, types/entity names, aliases e comments como categorias que devem ter destaque distinto.
- Q: As distinções semânticas exigidas (keywords, parameters, types/entities, aliases, comments) devem ser implementadas com boa qualidade tanto na gramática TextMate (VS Code) quanto nas queries Tree-sitter (Zed), ou podemos aceitar níveis diferentes de fidelidade entre os dois? → A: Sim, exigir boa qualidade em ambos os backends para manter consistência de experiência entre VS Code e Zed.
- Q: Esperamos entregar as distinções semânticas completas (keywords, parameters, types/entities, aliases e comments com boa qualidade) já na primeira versão do suporte ao VS Code e Zed, ou algumas dessas distinções mais finas (especialmente parameters e aliases) podem vir em iterações posteriores? → B: Entregar distinções fortes em keywords, types/entities e comments desde o início, aceitando que parameters e aliases possam ter qualidade mais básica inicialmente.
- Q: No lado de repositórios (US3 / SC-003), esperamos que as distinções semânticas (keywords, parâmetros, aliases etc.) fiquem visíveis de forma razoável no GitHub, ou aceitamos que o suporte web continue sendo apenas um mapeamento genérico para outra linguagem (ex: TypeScript)? → B: Aceitar que o suporte web permaneça como "melhor esforço" via mapeamento genérico. As distinções finas ficam restritas aos editores locais.
- Q: Dado o escopo mais realista (foco inicial forte em keywords + types/entities + comments), como devemos priorizar o trabalho entre VS Code e Zed? → B: Trabalhar os dois editores de forma mais paralela, investindo primeiro na gramática compartilhada (Tree-sitter + TextMate) e depois avançando nas extensões de forma coordenada.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Readable DormantQL Files in Primary Editor (Priority: P1)

A developer authoring DormantQL files (both schema in `.dqls` and operations in `.dql`) opens them in their main code editor and immediately sees proper syntax coloring that makes the structure easy to scan and understand.

**Why this priority**: This is the baseline expectation for any language or DSL used in daily development. Without it, developers experience the files as plain text, leading to higher cognitive load, more syntax mistakes, and slower onboarding for new contributors or team members.

**Independent Test**: Install the syntax highlighting support in the primary editor, open any existing `.dqls` and `.dql` file from the repository, and verify that keywords, entity names, field definitions, query/mutation blocks, strings, and comments are visually distinct.

**Acceptance Scenarios**:

1. **Given** a developer opens a `.dqls` file containing multiple entity definitions, **When** the syntax highlighting support is active, **Then** `entity`, field names, types (`uuid`, `string`, `int`), and relationship declarations are colored differently from each other.
2. **Given** a developer opens a `.dql` file containing queries and mutations, **When** the syntax highlighting support is active, **Then** the `query` and `mutation` keywords, parameter lists, `from`/`where`/`select` clauses, and string literals are clearly distinguishable.
3. **Given** a new team member who has never seen DormantQL before, **When** they open a module file with syntax highlighting enabled, **Then** they can quickly identify the overall structure (entities vs operations) without reading every line.

### User Story 2 - Consistent Experience Across Primary Editors (Priority: P2)

A developer who switches between their primary editor and a secondary editor environment (initial focus on VS Code, followed by Zed, with Rider and others planned later) gets comparable syntax highlighting quality for DormantQL files in both places.

**Why this priority**: Many developers in the target audience regularly use more than one editor or IDE. Inconsistent highlighting between environments creates friction and reduces trust in the overall DormantQL tooling experience. The grammar must support future expansion without breaking consistency.

**Independent Test**: Open the same `.dqls` file side-by-side in two different supported editor environments and verify that the visual distinction between language constructs is similar in intent and quality (exact colors may differ).

**Acceptance Scenarios**:

1. **Given** the same DormantQL file is opened in two different supported editors, **When** syntax highlighting is applied in both, **Then** keywords are highlighted distinctly from identifiers and literals in each editor.
2. **Given** a developer switches between editors during a workday, **When** they edit a complex mutation with relationships and `with` blocks, **Then** they do not need to mentally re-parse the file structure due to missing or inconsistent coloring.

### User Story 3 - Improved Contribution and Code Review Experience (Priority: P3)

A contributor or reviewer looks at DormantQL files in GitHub (or another repository browser) and can quickly understand the schema and operations without cloning the repository or opening a full editor.

**Why this priority**: Even while initial implementation focuses on VS Code first (followed by Zed), with Rider and others planned later, repository browsing and code review remain important surfaces. Reasonable syntax highlighting on the web (via best-effort mapping) still lowers the barrier for reviewers and contributors, even if semantic distinctions are not available.

**Independent Test**: View a `.dqls` or `.dql` file in a supported repository hosting platform and confirm that syntax coloring is applied in the file viewer.

**Acceptance Scenarios**:

1. **Given** a pull request changes a DormantQL module, **When** a reviewer views the diff in the browser after the recommended `.gitattributes` mapping is applied, **Then** the changed DormantQL file receives reasonable syntax coloring via language mapping.
2. **Given** a new developer is evaluating whether to adopt Dormant, **When** they browse the example schemas in the repository on GitHub, **Then** the files are presented with usable syntax coloring (via generic mapping) that does not look like plain text.

### Edge Cases

- What happens when a DormantQL file contains syntax that is invalid or uses newer language constructs not yet covered by the highlighting rules?
- How does highlighting behave on very large files (hundreds of entities or complex nested queries)?
- Are there any constructs that are intentionally ambiguous or context-sensitive that make reliable highlighting difficult?
- How are escaped strings, raw strings, or special characters inside DormantQL string literals handled?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Syntax highlighting support MUST be available for files with the `.dqls` extension (schema definitions).
- **FR-002**: Syntax highlighting support MUST be available for files with the `.dql` extension (query and mutation units).
- **FR-003**: The highlighting rules MUST provide distinct visual treatment for the following semantic categories, with the understanding that in the first release the highest priority will be given to keywords, types/entity names, and comments (parameters and aliases may receive more basic distinction initially, with improvements in follow-up iterations):
  - Keywords (e.g. `entity`, `query`, `mutation`, `from`, `where`, `select`, `insert`, `update`, `delete`, `with`, `returning`, `set`, etc.)
  - Parameters (in query/mutation signatures and their usage)
  - Types and Entity names (including references to entities)
  - Aliases (table/relation aliases, e.g. `u` in `from User u`)
  - Comments
  - In addition to the baseline categories: string literals, numeric literals, and punctuation.
- **FR-004**: A proper VS Code extension (with package.json and automatic activation for .dql and .dqls files) MUST be delivered as part of the initial support, in addition to making the grammar consumable by Zed and major repository hosting platforms.
- **FR-005**: Highlighting behavior MUST remain stable and not degrade when the DormantQL language evolves (new keywords, constructs, or syntax forms introduced in future features).
- **FR-006**: The syntax highlighting support MUST be available for local editing in both Zed and VS Code with comparable quality in the semantic distinctions defined in FR-003, and for web-based viewing of DormantQL files on major repository hosting platforms (best-effort).

### Key Entities *(include if feature involves data)*

N/A — This feature is about developer tooling experience rather than runtime data entities.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A survey of active DormantQL authors shows that at least 75% rate the readability of module files as "noticeably improved" or better after syntax highlighting support is installed.
- **SC-002**: In a controlled onboarding exercise, new contributors can correctly identify the main entities and operations in a DormantQL module file within 45 seconds when syntax highlighting is enabled (versus a control group without highlighting).
- **SC-003**: DormantQL source files viewed through the primary repository browsing interface display syntax coloring using a reasonable generic language mapping (e.g. TypeScript), providing basic readability even without semantic distinctions.
- **SC-004**: In the first release, keywords, types/entity names, and comments must be clearly distinguishable with good quality in both VS Code and Zed. Parameters and aliases should receive at least basic visual distinction, with the expectation of improvement in subsequent iterations.
- **SC-005**: When new DormantQL syntax is introduced, the highlighting support is updated in the same release cycle so that new constructs receive appropriate coloring without requiring manual workarounds.

## Assumptions

- The work should proceed with the shared grammar (Tree-sitter + TextMate) as the primary foundation, developed first. After that, the VS Code and Zed extensions should be advanced in a more parallel and coordinated way, rather than completing VS Code fully before starting Zed. In the first release, strong distinction is expected for keywords, types/entities, and comments, while parameters and aliases may start with more basic treatment (to be improved later). Long-term local editor support is planned for Zed, VS Code, JetBrains Rider, and others. The grammar must be designed for extensibility from the start.
- Repository browsing and code review interfaces on popular hosting platforms are important, but semantic highlighting distinctions on the web are accepted as future work. Best-effort generic language mapping is sufficient for this feature.
- Syntax highlighting is a foundational layer of language support and delivers value even before more advanced tooling (diagnostics, auto-completion, navigation) exists.
- The DormantQL language will continue to evolve; therefore the highlighting rules must be maintainable and extensible over time.
- A portable grammar (TextMate or equivalent) will be maintained in our own repository so it can be consumed by the VS Code extension, Zed, and repository hosting platforms (GitHub etc.), rather than depending on external projects to host the canonical grammar.
- This feature focuses exclusively on syntax highlighting. Full language server features (diagnostics, go-to-definition, refactoring) are considered separate concerns and out of scope for this specification.