using System.Text.Json;
using System.Text.Json.Nodes;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Services;

/// <summary>
/// Older versions had a NOT ("negate") checkbox on every condition. It no longer exists: when a saved rule uses it,
/// the condition is rewritten into an equivalent one without NOT (De Morgan for groups, the opposite comparison for
/// size/date, the other categories for file type...). When no exact equivalent exists the rule is disabled and its
/// name marked, so it can never act on the wrong files, and the user is told to review it.
/// </summary>
public static class LegacyNotMigration
{
    /// <summary>Migrates a saved library ({"profiles": [...]}) or a single exported profile ({"rules": [...]}) in place.
    /// Returns true when something changed; <paramref name="notes"/> gets one line per migrated rule.</summary>
    public static bool Migrate(JsonNode root, List<MigrationNote> notes)
    {
        if (root is not JsonObject obj) return false;
        bool changed = false;
        if (obj["profiles"] is JsonArray profiles)
        {
            foreach (var p in profiles.OfType<JsonObject>()) changed |= MigrateProfile(p, notes);
        }
        else changed |= MigrateProfile(obj, notes);
        return changed;
    }

    private static bool MigrateProfile(JsonObject profile, List<MigrationNote> notes)
    {
        if (profile["rules"] is not JsonArray rules) return false;
        var profileName = (profile["name"] as JsonValue)?.ToString() ?? "";
        bool changed = false;
        foreach (var rule in rules.OfType<JsonObject>())
        {
            if (rule["condition"] is not JsonObject condition) continue;
            bool negated = false, unconvertible = false;
            var fixedNode = Fix(condition, ref negated, ref unconvertible);
            if (!negated) continue;
            changed = true;
            if (!ReferenceEquals(fixedNode, condition))
            {
                // the root of a rule is always a group
                if ((fixedNode["type"] as JsonValue)?.ToString() != "group")
                    fixedNode = new JsonObject { ["type"] = "group", ["operator"] = "all", ["children"] = new JsonArray(fixedNode) };
                rule["condition"] = fixedNode;
            }
            var ruleName = (rule["name"] as JsonValue)?.ToString() ?? "";
            if (unconvertible)
            {
                rule["enabled"] = false;
                rule["name"] = ruleName + " " + Loc.T("Migration.RuleSuffix");
            }
            notes.Add(new MigrationNote(profileName, ruleName, NeedsReview: unconvertible));
        }
        return changed;
    }

    /// <summary>Removes "negate" from the node and its children. Returns the node to keep in place of <paramref name="node"/>.</summary>
    private static JsonObject Fix(JsonObject node, ref bool negated, ref bool unconvertible)
    {
        if (node["children"] is JsonArray children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is not JsonObject child) continue;
                var replacement = Fix(child, ref negated, ref unconvertible);
                if (!ReferenceEquals(replacement, child)) children[i] = replacement;
            }
        }
        bool negate = node["negate"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        node.Remove("negate");
        if (!negate) return node;
        negated = true;

        Condition? inverse;
        try
        {
            var condition = node.Deserialize<Condition>(Json.Options);
            inverse = condition == null ? null : Invert(condition);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException) { inverse = null; }
        if (inverse == null)
        {
            unconvertible = true; // keep the condition as it is (without NOT); the rule gets disabled
            return node;
        }
        return JsonSerializer.SerializeToNode<Condition>(inverse, Json.Options)!.AsObject();
    }

    /// <summary>Exact equivalent of NOT <paramref name="c"/>, or null when it cannot be expressed without NOT.</summary>
    public static Condition? Invert(Condition c)
    {
        switch (c)
        {
            case ConditionGroup g:
                {
                    if (g.Children.Count == 0) return new AllFilesCondition(); // empty group = nothing, so NOT = everything
                    var inverted = new ConditionGroup { Operator = g.Operator == GroupOperator.All ? GroupOperator.Any : GroupOperator.All };
                    foreach (var child in g.Children)
                    {
                        var x = Invert(child);
                        if (x == null) return null;
                        inverted.Children.Add(x);
                    }
                    return inverted;
                }
            case AllFilesCondition:
                return new ConditionGroup(); // matches nothing
            case FileTypeCondition t:
                {
                    var any = new ConditionGroup { Operator = GroupOperator.Any };
                    foreach (var cat in Enum.GetValues<FileCategory>().Where(x => x != t.Category))
                        any.Children.Add(new FileTypeCondition { Category = cat });
                    return any;
                }
            case ItemTypeCondition i:
                return new ItemTypeCondition { Kind = i.Kind == ItemKind.File ? ItemKind.Folder : ItemKind.File };
            case SizeCondition s:
                {
                    long a = ConditionEvaluator.ToBytes(s.Value, s.Unit), b = ConditionEvaluator.ToBytes(s.Value2, s.Unit);
                    SizeCondition Bytes(SizeComparison cmp, long value) => new() { Comparison = cmp, Value = value, Unit = SizeUnit.Bytes };
                    return s.Comparison switch
                    {
                        SizeComparison.GreaterThan => Bytes(SizeComparison.LessThan, a + 1),      // NOT size > a  ⇔ size < a+1
                        SizeComparison.LessThan => Bytes(SizeComparison.GreaterThan, a - 1),      // NOT size < a  ⇔ size > a-1
                        SizeComparison.EqualTo => new ConditionGroup
                        {
                            Operator = GroupOperator.Any,
                            Children = { Bytes(SizeComparison.LessThan, a), Bytes(SizeComparison.GreaterThan, a) },
                        },
                        _ => new ConditionGroup
                        {
                            Operator = GroupOperator.Any,
                            Children = { Bytes(SizeComparison.LessThan, Math.Min(a, b)), Bytes(SizeComparison.GreaterThan, Math.Max(a, b)) },
                        },
                    };
                }
            case DateCondition d when d.Field != DateField.Taken: // "taken" may be missing: NOT would also match files without it
                {
                    var x = new DateCondition { Field = d.Field, Amount = d.Amount, Unit = d.Unit, Date = d.Date };
                    switch (d.Mode)
                    {
                        case DateMode.OlderThan: x.Mode = DateMode.NewerThan; break;  // v < t  ⇔ NOT v >= t
                        case DateMode.NewerThan: x.Mode = DateMode.OlderThan; break;
                        case DateMode.Before: x.Mode = DateMode.After; x.Date = d.Date.AddTicks(-1); break; // NOT v < d ⇔ v > d-1tick
                        default: x.Mode = DateMode.Before; x.Date = d.Date.AddTicks(1); break;            // NOT v > d ⇔ v < d+1tick
                    }
                    return x;
                }
            default:
                return null;
        }
    }
}

/// <summary>A rule whose NOT was removed. <see cref="NeedsReview"/>: no exact equivalent, the rule was disabled.</summary>
public sealed record MigrationNote(string Profile, string Rule, bool NeedsReview);
