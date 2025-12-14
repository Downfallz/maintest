using DA.Game.Domain2.Matches.Entities.Conditions;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Conditions;

public sealed class ConditionCollection : IEnumerable<ConditionInstance>
{
    private readonly List<ConditionInstance> _items = new();
    public IReadOnlyList<ConditionInstance> Items => _items;

    public IEnumerator<ConditionInstance> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(ConditionInstance incoming)
    {
        if (incoming.StackPolicy == StackPolicy.Stack)
        {
            _items.Add(incoming);
            return;
        }
        var existing = _items.FirstOrDefault(x => x.Kind == incoming.Kind && !x.IsExpired);

        if (existing is null)
        {
            _items.Add(incoming);
            return;
        }

        switch (incoming.StackPolicy)
        {
            case StackPolicy.RefreshDuration:
                _items.Remove(existing);
                _items.Add(incoming);
                break;

            case StackPolicy.NoStack:
                // Ignore if already present (ex: stun)
                break;
        }
    }

    public void TickAll()
    {
        for (var i = 0; i < _items.Count; i++)
            _items[i] = _items[i].Tick();
    }
    public IEnumerable<ConditionInstance> Active()
    => _items.Where(x => !x.IsExpired);
    public void RemoveExpired()
        => _items.RemoveAll(x => x.IsExpired);
}