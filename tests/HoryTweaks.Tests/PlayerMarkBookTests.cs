using BetterAmongUs.Modules;
using Xunit;

namespace HoryTweaks.Tests;

public sealed class PlayerMarkBookTests
{
    private const byte PlayerA = 3;
    private const byte PlayerB = 7;

    [Fact]
    public void Get_UnmarkedPlayer_ReturnsEmpty()
    {
        var book = new PlayerMarkBook();

        var mark = book.Get(PlayerA);

        Assert.True(mark.IsEmpty);
        Assert.Null(mark.RoleId);
        Assert.Null(mark.Status);
        Assert.Equal(0, book.MarkedCount);
    }

    [Fact]
    public void ToggleRole_AppliesRoleGuess()
    {
        var book = new PlayerMarkBook();

        book.ToggleRole(PlayerA, 12); // Detective

        var mark = book.Get(PlayerA);
        Assert.Equal(12, mark.RoleId);
        Assert.Equal(1, book.MarkedCount);
    }

    [Fact]
    public void ToggleRole_SameRoleTwice_ClearsIt()
    {
        var book = new PlayerMarkBook();
        book.ToggleRole(PlayerA, 12);

        book.ToggleRole(PlayerA, 12);

        Assert.True(book.Get(PlayerA).IsEmpty);
        Assert.Equal(0, book.MarkedCount);
    }

    [Fact]
    public void ToggleRole_DifferentRole_ReplacesGuess()
    {
        var book = new PlayerMarkBook();
        book.ToggleRole(PlayerA, 12); // Detective

        book.ToggleRole(PlayerA, 1); // Impostor

        Assert.Equal(1, book.Get(PlayerA).RoleId);
    }

    [Fact]
    public void ToggleStatus_SameStatusTwice_ClearsIt()
    {
        var book = new PlayerMarkBook();
        book.ToggleStatus(PlayerA, PlayerMarkStatus.Suspect);

        book.ToggleStatus(PlayerA, PlayerMarkStatus.Suspect);

        Assert.True(book.Get(PlayerA).IsEmpty);
    }

    [Fact]
    public void ToggleStatus_DifferentStatus_ReplacesIt()
    {
        var book = new PlayerMarkBook();
        book.ToggleStatus(PlayerA, PlayerMarkStatus.Suspect);

        book.ToggleStatus(PlayerA, PlayerMarkStatus.Cleared);

        Assert.Equal(PlayerMarkStatus.Cleared, book.Get(PlayerA).Status);
    }

    [Fact]
    public void RoleAndStatus_CoexistAsTwoMarkers()
    {
        var book = new PlayerMarkBook();

        book.ToggleRole(PlayerA, 12);
        book.ToggleStatus(PlayerA, PlayerMarkStatus.Investigate);

        var mark = book.Get(PlayerA);
        Assert.Equal(12, mark.RoleId);
        Assert.Equal(PlayerMarkStatus.Investigate, mark.Status);
        Assert.Equal(1, book.MarkedCount); // one player, two markers
    }

    [Fact]
    public void ClearingOneSlot_KeepsTheOther()
    {
        var book = new PlayerMarkBook();
        book.ToggleRole(PlayerA, 12);
        book.ToggleStatus(PlayerA, PlayerMarkStatus.Suspect);

        book.ToggleRole(PlayerA, 12);

        var mark = book.Get(PlayerA);
        Assert.Null(mark.RoleId);
        Assert.Equal(PlayerMarkStatus.Suspect, mark.Status);
    }

    [Fact]
    public void ClearPlayer_RemovesAllMarks()
    {
        var book = new PlayerMarkBook();
        book.ToggleRole(PlayerA, 12);
        book.ToggleStatus(PlayerA, PlayerMarkStatus.Suspect);

        book.ClearPlayer(PlayerA);

        Assert.True(book.Get(PlayerA).IsEmpty);
        Assert.Equal(0, book.MarkedCount);
    }

    [Fact]
    public void Reset_WipesEveryPlayer()
    {
        var book = new PlayerMarkBook();
        book.ToggleRole(PlayerA, 12);
        book.ToggleStatus(PlayerB, PlayerMarkStatus.Suspect);

        book.Reset();

        Assert.True(book.Get(PlayerA).IsEmpty);
        Assert.True(book.Get(PlayerB).IsEmpty);
        Assert.Equal(0, book.MarkedCount);
    }

    [Fact]
    public void Marks_AreScopedPerPlayer()
    {
        var book = new PlayerMarkBook();
        book.ToggleRole(PlayerA, 12);

        Assert.True(book.Get(PlayerB).IsEmpty);
        Assert.Equal(12, book.Get(PlayerA).RoleId);
    }
}
