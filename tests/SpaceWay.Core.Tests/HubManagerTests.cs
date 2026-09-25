using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class HubManagerTests
{
    private LauncherDatabase _db = null!;
    private HubManager _hubs = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        _hubs = new HubManager(new HubStore(_db));
        _hubs.Reload();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void DisabledHub_IsLeftOutOfThePoll()
    {
        var hub = Add("Первый", 0);
        Add("Второй", 1);

        _hubs.SetEnabled(hub.Id, false);

        Assert.That(_hubs.Enabled.Select(h => h.DisplayName), Is.EqualTo(new[] { "Второй" }));
    }

    [Test]
    public void MovingUp_SwapsPriorities()
    {
        Add("Первый", 0);
        var second = Add("Второй", 1);

        _hubs.Move(second.Id, -1);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.DisplayName),
            Is.EqualTo(new[] { "Второй", "Первый" }));
    }

    [Test]
    public void MovingPastTheEnd_ChangesNothing()
    {
        var first = Add("Первый", 0);
        Add("Второй", 1);

        _hubs.Move(first.Id, -1);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.DisplayName),
            Is.EqualTo(new[] { "Первый", "Второй" }));
    }

    [Test]
    public void AfterMoves_PrioritiesStayContiguous()
    {
        Add("Первый", 5);
        Add("Второй", 10);
        var third = Add("Третий", 42);

        _hubs.Move(third.Id, -1);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.Priority),
            Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void RemovingHub_LeavesOthersAlone()
    {
        var first = Add("Первый", 0);
        Add("Второй", 1);

        _hubs.Remove(first.Id);

        Assert.That(_hubs.Hubs.Select(h => h.DisplayName), Is.EqualTo(new[] { "Второй" }));
    }

    [Test]
    public void ChangingList_RaisesEvent()
    {
        var raised = 0;
        _hubs.Changed += () => raised++;

        var hub = Add("Первый", 0);
        _hubs.SetEnabled(hub.Id, false);

        Assert.That(raised, Is.EqualTo(2));
    }

    [Test]
    public void DraggingDown_TakesTargetPlace()
    {
        var first = Add("Первый", 0);
        Add("Второй", 1);
        Add("Третий", 2);

        _hubs.Reorder(first.Id, 2);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.DisplayName),
            Is.EqualTo(new[] { "Второй", "Третий", "Первый" }));
    }

    [Test]
    public void DraggingUp_TakesTargetPlace()
    {
        Add("Первый", 0);
        Add("Второй", 1);
        var third = Add("Третий", 2);

        _hubs.Reorder(third.Id, 0);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.DisplayName),
            Is.EqualTo(new[] { "Третий", "Первый", "Второй" }));
    }

    [Test]
    public void DraggingOntoItself_ChangesNothing()
    {
        var first = Add("Первый", 0);
        Add("Второй", 1);

        _hubs.Reorder(first.Id, 0);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.DisplayName),
            Is.EqualTo(new[] { "Первый", "Второй" }));
    }

    [Test]
    public void DraggingPastTheList_ClampsToEdge()
    {
        var first = Add("Первый", 0);
        Add("Второй", 1);

        _hubs.Reorder(first.Id, 99);

        Assert.That(_hubs.Hubs.OrderBy(h => h.Priority).Select(h => h.DisplayName),
            Is.EqualTo(new[] { "Второй", "Первый" }));
    }

    private HubEntry Add(string name, int priority)
    {
        var hub = new HubEntry(Guid.NewGuid(), name, new Uri($"https://{name}.example/"), priority);
        _hubs.Save(hub);
        return hub;
    }
}
