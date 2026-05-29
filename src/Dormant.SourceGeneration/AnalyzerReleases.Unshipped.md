; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------
ORM001  | DormantQL | Error    | DormantQL syntax error
ORM002  | DormantQL | Error    | Link targets an undefined entity
ORM003  | DormantQL | Error    | Unknown property type
ORM010  | DormantQL | Error    | Query targets an undefined entity
ORM011  | DormantQL | Error    | Query references an unknown column
ORM012  | DormantQL | Error    | Query references an undeclared parameter
ORM013  | DormantQL | Error    | Database name collision
ORM020  | DormantQL | Error    | Removed DormantQL syntax
ORM021  | DormantQL | Error    | Subject is missing an explicit alias
ORM022  | DormantQL | Error    | Reference to an undeclared alias
ORM023  | DormantQL | Error    | Duplicate alias
ORM024  | DormantQL | Error    | Member reference is not alias-qualified
ORM025  | DormantQL | Error    | Clauses are out of canonical order
ORM026  | DormantQL | Error    | Insert is missing a required member
ORM027  | DormantQL | Error    | Result member is not in the projected shape
ORM028  | DormantQL | Error    | Duplicate composition member
ORM029  | DormantQL | Error    | Unknown constraint
ORM030  | DormantQL | Error    | Constraint not applicable to member type
ORM031  | DormantQL | Error    | Constraint references an unknown member
ORM032  | DormantQL | Error    | Duplicate constraint name
ORM033  | DormantQL | Error    | Unknown scalar base type
ORM034  | DormantQL | Error    | Inheritance conflict
ORM035  | DormantQL | Error    | Removed member modifier syntax
ORM036  | DormantQL | Error    | Invalid annotation or constraint target
