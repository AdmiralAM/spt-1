using System.Runtime.CompilerServices;

namespace SPTBeltArmbandInventory
{
    internal sealed class RuntimeListOwnership
    {
        sealed class State
        {
            internal readonly object List;
            internal readonly object Entry;

            internal State(object list, object entry)
            {
                List = list;
                Entry = entry;
            }
        }

        ConditionalWeakTable<object, State> states = new ConditionalWeakTable<object, State>();

        internal bool Owns(object owner, object list, object entry)
        {
            if (owner == null || list == null || entry == null) return false;
            State state;
            return states.TryGetValue(owner, out state)
                && ReferenceEquals(state.List, list)
                && ReferenceEquals(state.Entry, entry);
        }

        internal void Mark(object owner, object list, object entry)
        {
            if (owner == null || list == null || entry == null) return;
            states.Remove(owner);
            states.Add(owner, new State(list, entry));
        }

        internal void Forget(object owner)
        {
            if (owner != null) states.Remove(owner);
        }

        internal void Reset()
        {
            states = new ConditionalWeakTable<object, State>();
        }
    }
}
