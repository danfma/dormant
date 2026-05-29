# Research: DQL Syntax Highlighting (Feature 011)

**Feature**: 011-dql-syntax-highlighting
**Status**: Phase 0 – Research Questions

## NEEDS CLARIFICATION extracted from Technical Context

After the latest clarification session (May 2026), the following areas require research before detailed design and task breakdown:

### R-01: Achieving Semantic Quality Parity Between TextMate and Tree-sitter

**Question**: How do we practically achieve comparable quality in the required semantic distinctions (keywords, types/entities, comments as priority; parameters and aliases as secondary) in **both** the TextMate grammar and the Tree-sitter queries?

**Areas to investigate**:
- Recommended structure and capture names for Tree-sitter `highlights.scm` for a language like DormantQL.
- How to keep the TextMate grammar reasonably close in fidelity to the Tree-sitter one without excessive duplication.
- Tools or processes to help maintain consistency between the two formats.

### R-02: Parallel Development Model After Grammar Foundation

**Question**: What is the best way to structure the work so that, after the shared grammar foundation is solid, the VS Code extension and Zed extension can be advanced in a coordinated/parallel fashion?

**Areas to investigate**:
- Recommended sequencing inside the grammar work itself (which categories to tackle first).
- How much of the extension work (package.json, activation, queries, etc.) can be done in parallel vs sequentially.
- Risk of divergence if the two editor tracks move too independently.

### R-03: Validation Strategy for "Comparable Quality"

**Question**: How will we objectively or practically validate that the semantic distinctions have "good and comparable" quality between VS Code and Zed?

**Areas to investigate**:
- Snapshot testing of highlighting results.
- Use of real DormantQL example files from the repository.
- Manual review checklists.
- Any existing tools or community practices for cross-editor grammar validation.

### R-04: Phased Delivery of Semantic Distinctions

**Question**: What is a realistic and maintainable way to implement "strong" distinction for the priority categories (keywords, types/entities, comments) in v1 while leaving "basic" distinction for parameters and aliases, with a clear path to improve them later?

**Areas to investigate**:
- How to structure the grammars/queries so that weaker distinctions can be upgraded without major rewrites.
- Documentation and communication strategy (so users know what to expect in v1 vs later).

### R-05: Long-term Grammar Maintenance Model

**Question**: What is a sustainable ownership and contribution model for the dual grammar (Tree-sitter + TextMate) so that future DormantQL language changes can be supported efficiently in both formats?

**Areas to investigate**:
- Single source of truth strategies (e.g., generating one from the other where possible).
- Contribution guidelines and review process for grammar changes.
- Versioning and compatibility expectations for the grammar artifacts themselves.

---

## Next Steps

Once these research items are investigated and decisions are recorded, we can move to Phase 1 (detailed design of the grammar components, extension architecture, and contracts).