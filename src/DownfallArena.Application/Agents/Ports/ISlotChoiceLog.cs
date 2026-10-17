namespace DownfallArena.Application.Agents.Ports;

/// <summary>
/// Where a searching agent writes what it read at a slot: every spell it could cast there and what the best
/// cast of each was worth, beside the one it chose. Owned by Application, implemented by Infrastructure; an
/// agent built without one writes nothing. It is how a spell's worth is read decision by decision rather than
/// off the result of a whole match, where one cast rarely decides anything.
/// </summary>
public interface ISlotChoiceLog
{
    void Record(SlotChoice choice);
}
