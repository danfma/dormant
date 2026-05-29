using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// Feature 012 (Slice 2): member-level constraints lower to named table-level DDL constraints.
// `unique` → UNIQUE; numeric bounds + length → CHECK. Names come from `as` or a deterministic default.
public sealed class ConstraintEmitTests
{
    private const string Schema = """
        module shop;

        entity Product {
          id: Uuid { constraint primary; }
          sku: String {
            constraint unique as products_sku_unique;
            constraint max_length(32);
          }
          price: Int { constraint range(min = 0, max = 1000000); }
          name: String { constraint min_length(1); }
        }
        """;

    private const string Schema2 = """
        module shop;

        entity Account {
          id: Uuid { constraint primary; }
          status: String { constraint one_of("Open", "Closed", "Merged"); }
          email: String { constraint regex("^[^@]+@[^@]+$"); }
          first: String;
          last: String;

          constraint unique on (first, last) as accounts_name;
        }

        entity Booking {
          id: Uuid { constraint primary; }
          start_at: DateTime;
          end_at: DateTime;
          qty: Int { constraint check (qty > 0); }

          constraint check (start_at <= end_at);
        }
        """;

    private const string ScalarSchema = """
        module shop;

        scalar Username extending String {
          constraint min_length(3);
          constraint max_length(30);
        }

        entity Account2 {
          id: Uuid { constraint primary; }
          handle: Username;
        }
        """;

    private const string InheritanceSchema = """
        module shop;

        abstract entity Timestamped {
          created_at: DateTime;
          updated_at: DateTime;
          constraint check (created_at <= updated_at);
        }

        entity Article2 extending Timestamped {
          id: Uuid { constraint primary; }
          title: String { constraint max_length(200); }
        }
        """;

    private static string Generate() => GenerateFrom("schema/shop.dqls", Schema);

    private static string GenerateFrom(string path, string schema)
    {
        var driver = GeneratorTestHarness.CreateDriver(new TestAdditionalText(path, schema));
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var sources = driver.GetRunResult().Results[0].GeneratedSources;
        return string.Join("\n", sources.Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Member_unique_emits_named_unique_constraint()
    {
        var generated = Generate();
        // Explicit `as` name is honored.
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"products_sku_unique\" UNIQUE (\"sku\")");
    }

    [Test]
    public async Task Length_and_range_emit_check_constraints_with_default_names()
    {
        var generated = Generate();
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"product_sku_maxlen\" CHECK (length(\"sku\") <= 32)");
        await Assert
            .That(generated)
            .Contains(
                "CONSTRAINT \"product_price_range\" CHECK (\"price\" >= 0 AND \"price\" <= 1000000)"
            );
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"product_name_minlen\" CHECK (length(\"name\") >= 1)");
    }

    [Test]
    public async Task Primary_stays_inline_on_the_column()
    {
        var generated = Generate();
        // Single-column primary remains an inline column constraint, not a table constraint.
        await Assert.That(generated).Contains("\"id\" uuid NOT NULL PRIMARY KEY");
        await Assert.That(generated).DoesNotContain("PRIMARY KEY (\"id\")");
    }

    [Test]
    public async Task OneOf_emits_in_list_check_with_quoted_strings()
    {
        var generated = GenerateFrom("schema/shop2.dqls", Schema2);
        await Assert
            .That(generated)
            .Contains(
                "CONSTRAINT \"account_status_oneof\" CHECK (\"status\" IN ('Open', 'Closed', 'Merged'))"
            );
    }

    [Test]
    public async Task Regex_emits_postgres_tilde_check()
    {
        var generated = GenerateFrom("schema/shop2.dqls", Schema2);
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"account_email_regex\" CHECK (\"email\" ~ '^[^@]+@[^@]+$')");
    }

    [Test]
    public async Task Entity_level_unique_emits_composite_constraint()
    {
        var generated = GenerateFrom("schema/shop2.dqls", Schema2);
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"accounts_name\" UNIQUE (\"first\", \"last\")");
    }

    [Test]
    public async Task Inheritance_flattens_base_members_and_constraints()
    {
        var generated = GenerateFrom("schema/inherit.dqls", InheritanceSchema);
        // Concrete entity gets the inherited columns + the base's check + its own constraint.
        await Assert.That(generated).Contains("CREATE TABLE IF NOT EXISTS \"shop\".\"article2\"");
        await Assert.That(generated).Contains("\"created_at\"");
        await Assert.That(generated).Contains("\"updated_at\"");
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"article2_check\" CHECK (\"created_at\" <= \"updated_at\")");
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"article2_title_maxlen\" CHECK (length(\"title\") <= 200)");
        // The abstract base emits no table.
        await Assert.That(generated).DoesNotContain("\"timestamped\"");
    }

    [Test]
    public async Task Scalar_typed_member_inherits_scalar_constraints()
    {
        var generated = GenerateFrom("schema/scalars.dqls", ScalarSchema);
        // handle: Username → base String column with the scalar's length constraints.
        await Assert.That(generated).Contains("\"handle\" text NOT NULL");
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"account2_handle_minlen\" CHECK (length(\"handle\") >= 3)");
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"account2_handle_maxlen\" CHECK (length(\"handle\") <= 30)");
    }

    [Test]
    public async Task Member_and_entity_check_expressions_lower_to_sql()
    {
        var generated = GenerateFrom("schema/shop2.dqls", Schema2);
        // Member-level check: identifier → quoted column, operator carried through.
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"booking_qty_check\" CHECK (\"qty\" > 0)");
        // Entity-level cross-field check.
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"booking_check\" CHECK (\"start_at\" <= \"end_at\")");
    }
}
