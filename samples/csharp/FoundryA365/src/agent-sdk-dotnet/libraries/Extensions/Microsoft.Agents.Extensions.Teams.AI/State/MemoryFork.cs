using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.State
{

    /// <summary>
    /// Forks an existing memory.
    /// A memory fork is a memory that is a copy of another memory, but can be modified without affecting the original memory.
    /// </summary>
    public class MemoryFork : TurnState
    {
        private readonly Dictionary<string, Record> _fork = new();
        private readonly TurnState? _memory;

        /// <summary>
        /// Creates a new `MemoryFork` instance.
        /// </summary>
        /// <param name="memory">Memory to fork.</param>
        public MemoryFork(TurnState? memory = null)
        {
            _memory = memory;
        }

        /// <summary>
        /// Deletes a value from the memory. Only forked values will be deleted.
        /// </summary>
        /// <param name="path">Path to the value to delete in the form of `[scope].property`.
        /// If scope is omitted, the value is deleted from the temporary scope.</param>
        public override void DeleteValue(string path)
        {
            (string scope, string name) = GetScopeAndName(path);
            if (_fork.TryGetValue(scope, out Record? scopeValue) && scopeValue.ContainsKey(name))
            {
                scopeValue.Remove(name);
            }
        }

        /// <summary>
        /// Retrieves a value from the memory. The forked memory is checked first, then the original memory.
        /// </summary>
        /// <param name="path">Path to the value to retrieve in the form of `[scope].property`.
        /// If scope is omitted, the value is retrieved from the temporary scope.</param>
        /// <returns>The value or undefined if not found.</returns>
        public object? GetValue(string path)
        {
            (string scope, string name) = GetScopeAndName(path);
            if (_fork.ContainsKey(scope))
            {
                if (_fork[scope].TryGetValue(name, out object? scopeValue))
                {
                    return scopeValue;
                }
            }

            return _memory?.GetValue<object?>(path);
        }

        /// <summary>
        /// Checks if a value exists in the memory. The forked memory is checked first, then the original memory.
        /// </summary> 
        /// <param name="path">Path to the value to check in the form of `[scope].property`.
        /// If scope is omitted, the value is checked in the temporary scope.</param>
        /// <returns>True if the value exists, false otherwise.</returns>
        public override bool HasValue(string path)
        {
            (string scope, string name) = GetScopeAndName(path);
            if (_fork.TryGetValue(scope, out Record? scopeValue))
            {
                return scopeValue.ContainsKey(name);
            }

            if (_memory != null)
            {
                return _memory.HasValue(path);
            }

            return false;
        }

        /// <summary>
        /// Assigns a value to the memory. The value is assigned to the forked memory.
        /// </summary>
        /// <param name="path">Path to the value to assign in the form of `[scope].property`.
        /// If scope is omitted, the value is assigned to the temporary scope.</param>
        /// <param name="value">Value to assign.</param>
        public override void SetValue(string path, object value)
        {
            (string scope, string name) = GetScopeAndName(path);
            if (!_fork.TryGetValue(scope, out Record? scopeValue))
            {
                scopeValue = new();
                _fork[scope] = scopeValue;
            }

            scopeValue[name] = scopeValue;
        }

#pragma warning disable CA1822 // Mark members as static
        private (string, string) GetScopeAndName(string path)
#pragma warning restore CA1822 // Mark members as static
        {
            List<string> parts = path.Split('.').ToList();

            if (parts.Count > 2)
            {
                throw new InvalidOperationException($"Invalid state path: {path}");
            }
            if (parts.Count == 1)
            {
                parts.Insert(0, "temp");
            }
            return (parts[0], parts[1]);
        }
    }
}
