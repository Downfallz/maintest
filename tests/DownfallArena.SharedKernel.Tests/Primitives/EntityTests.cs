using DownfallArena.Domain.Common;

namespace DownfallArena.Domain.Tests.Common;

public sealed class EntityTests
{
    [Fact]
    public void Entities_with_the_same_id_and_type_are_equal()
    {
        var id = Guid.CreateVersion7();

        var left = new Creature(id);
        var right = new Creature(id);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void An_entity_equals_itself()
    {
        var creature = new Creature(Guid.CreateVersion7());

        creature.Equals(creature).ShouldBeTrue();
    }

    [Fact]
    public void An_entity_does_not_equal_null_or_an_unrelated_object()
    {
        var creature = new Creature(Guid.CreateVersion7());

        creature.Equals(null).ShouldBeFalse();
        creature.Equals("not an entity").ShouldBeFalse();
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var left = new Creature(Guid.CreateVersion7());
        var right = new Creature(Guid.CreateVersion7());

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Entities_of_different_types_are_not_equal_even_with_the_same_id()
    {
        var id = Guid.CreateVersion7();

        var creature = new Creature(id);
        var team = new Team(id);

        creature.Equals(team).ShouldBeFalse();
    }

    private sealed class Creature(Guid id) : Entity<Guid>(id)
    {
    }

    private sealed class Team(Guid id) : Entity<Guid>(id)
    {
    }
}
