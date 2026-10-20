namespace Loja.SharedKernel.Tests;

public class EntityTests
{
    private readonly record struct TestId(Guid Value);

    private sealed record TestHappened : DomainEvent;

    private sealed class TestEntity(TestId id) : Entity<TestId>(id)
    {
        public string Name { get; set; } = string.Empty;

        public void DoSomething() => Raise(new TestHappened());
    }

    private sealed class OtherEntity(TestId id) : Entity<TestId>(id);

    [Fact]
    public void Entities_WithSameIdAndType_AreEqual()
    {
        var id = new TestId(Guid.NewGuid());

        var first = new TestEntity(id) { Name = "A" };
        var second = new TestEntity(id) { Name = "B" };

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
    }

    [Fact]
    public void Entities_WithSameIdButDifferentType_AreNotEqual()
    {
        var id = new TestId(Guid.NewGuid());

        new TestEntity(id).Equals(new OtherEntity(id)).ShouldBeFalse();
    }

    [Fact]
    public void Raise_AddsDomainEvent_AndClearRemovesIt()
    {
        var entity = new TestEntity(new TestId(Guid.NewGuid()));

        entity.DoSomething();
        entity.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TestHappened>();

        entity.ClearDomainEvents();
        entity.DomainEvents.ShouldBeEmpty();
    }
}
